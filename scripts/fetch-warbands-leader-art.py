#!/usr/bin/env python3
from __future__ import annotations

import io
import json
import pathlib
import sys
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from html.parser import HTMLParser

from PIL import Image

CARDS_URL = "https://api.hearthstonejson.com/v1/latest/enUS/cards.json"
ART_URL = "https://art.hearthstonejson.com/v1/512x/{card_id}.jpg"
WARCRAFT_PAGE = "https://warcraft.wiki.gg/wiki/{title}"
OUTPUT_ROOT = pathlib.Path("mods/warbands/assets/cosmetics/leaders")
REPORT_PATH = OUTPUT_ROOT / "sources.json"

LEADERS = {
    "alleria-windrunner": "Alleria Windrunner",
    "altruis-the-sufferer": "Altruis the Sufferer",
    "anduin-wrynn": "Anduin Wrynn",
    "arthas-menethil": "Arthas Menethil",
    "aysa-cloudsinger": "Aysa Cloudsinger",
    "calia-menethil": "Calia Menethil",
    "celestine-of-the-harvest": "Celestine of the Harvest",
    "chen-stormstout": "Chen Stormstout",
    "chogall": "Cho'gall",
    "darion-mograine": "Darion Mograine",
    "drekthar": "Drek'Thar",
    "farseer-nobundo": "Farseer Nobundo",
    "first-arcanist-thalyssra": "First Arcanist Thalyssra",
    "garona-halforcen": "Garona Halforcen",
    "garrosh-hellscream": "Garrosh Hellscream",
    "genn-greymane": "Genn Greymane",
    "geyarah": "Geyarah",
    "grand-magister-rommath": "Grand Magister Rommath",
    "guldan": "Gul'dan",
    "hamuul-runetotem": "Hamuul Runetotem",
    "hemet-nesingwary": "Hemet Nesingwary",
    "illidan-stormrage": "Illidan Stormrage",
    "jaina-proudmoore": "Jaina Proudmoore",
    "ji-firepaw": "Ji Firepaw",
    "kayn-sunfury": "Kayn Sunfury",
    "khadgar": "Khadgar",
    "koltira-deathweaver": "Koltira Deathweaver",
    "lady-liadrin": "Lady Liadrin",
    "leona-darkstrider": "Leona Darkstrider",
    "lilian-voss": "Lilian Voss",
    "magatha-grimtotem": "Magatha Grimtotem",
    "malfurion-stormrage": "Malfurion Stormrage",
    "mathias-shaw": "Mathias Shaw",
    "nekros-skullcrusher": "Nekros Skullcrusher",
    "prophet-velen": "Prophet Velen",
    "rexxar": "Rexxar",
    "scalecommander-azurathel": "Scalecommander Azurathel",
    "scalecommander-cindrethresh": "Scalecommander Cindrethresh",
    "scalecommander-emberthal": "Scalecommander Emberthal",
    "scalecommander-sarkareth": "Scalecommander Sarkareth",
    "shandris-feathermoon": "Shandris Feathermoon",
    "sunwalker-dezco": "Sunwalker Dezco",
    "taran-zhu": "Taran Zhu",
    "thassarian": "Thassarian",
    "thrall": "Thrall",
    "tyrande-whisperwind": "Tyrande Whisperwind",
    "uther-lightbringer": "Uther Lightbringer",
    "valeera-sanguinar": "Valeera Sanguinar",
    "varian-wrynn": "Varian Wrynn",
    "wilfred-fizzlebang": "Wilfred Fizzlebang",
    "yrel": "Yrel",
    "zentabra": "Zentabra",
}

ALIASES = {
    "altruis-the-sufferer": ["Altruis the Outcast"],
    "arthas-menethil": ["Prince Arthas", "The Lich King"],
    "chen-stormstout": ["Youthful Brewmaster"],
    "first-arcanist-thalyssra": ["Thalyssra"],
    "garona-halforcen": ["Garona"],
    "geyarah": ["Overlord Geyarah"],
    "nekros-skullcrusher": ["Nekros"],
    "scalecommander-azurathel": ["Azurathel"],
    "scalecommander-cindrethresh": ["Cindrethresh"],
    "scalecommander-emberthal": ["Emberthal"],
    "scalecommander-sarkareth": ["Sarkareth"],
    "sunwalker-dezco": ["Dezco"],
    "zentabra": ["Zen'tabra"],
}

TYPE_PRIORITY = {"HERO": 5, "MINION": 4, "SPELL": 2, "WEAPON": 1}


@dataclass(frozen=True)
class Match:
    card: dict
    matched_name: str
    exact: bool


class SocialImageParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.image_url: str | None = None

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag != "meta" or self.image_url is not None:
            return
        values = {key.lower(): value for key, value in attrs if value is not None}
        marker = values.get("property") or values.get("name")
        if marker in {"og:image", "twitter:image"} and values.get("content"):
            self.image_url = values["content"]


def fetch_bytes(url: str, timeout: int = 25) -> bytes:
    request = urllib.request.Request(
        url,
        headers={"User-Agent": "Battlegrounds-Warbands-Art-Fetcher/1.2"},
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.read()


def fetch_json(url: str) -> dict | list:
    return json.loads(fetch_bytes(url).decode("utf-8"))


def load_cards() -> list[dict]:
    payload = fetch_json(CARDS_URL)
    if not isinstance(payload, list):
        raise ValueError("HearthstoneJSON cards endpoint did not return a list")
    return payload


def score(card: dict) -> tuple[int, int, int]:
    return (
        TYPE_PRIORITY.get(str(card.get("type", "")), 0),
        1 if card.get("collectible") else 0,
        int(card.get("dbfId", 0) or 0),
    )


def find_card(cards: list[dict], slug: str, display_name: str) -> Match | None:
    names = [display_name, *ALIASES.get(slug, [])]
    seen: set[str] = set()
    for candidate_name in names:
        key = candidate_name.casefold()
        if key in seen:
            continue
        seen.add(key)
        matches = [card for card in cards if str(card.get("name", "")).casefold() == key]
        if matches:
            return Match(
                card=max(matches, key=score),
                matched_name=candidate_name,
                exact=candidate_name.casefold() == display_name.casefold(),
            )
    return None


def save_png(data: bytes, target: pathlib.Path) -> tuple[int, int]:
    with Image.open(io.BytesIO(data)) as source:
        source.seek(0)
        source.load()
        image = source.convert("RGBA")
        target.parent.mkdir(parents=True, exist_ok=True)
        image.save(target, format="PNG", optimize=True)
        return image.size


def warcraft_page_image(display_name: str) -> tuple[str, str] | None:
    title = urllib.parse.quote(display_name.replace(" ", "_"), safe="'_-")
    page_url = WARCRAFT_PAGE.format(title=title)
    html = fetch_bytes(page_url, timeout=12).decode("utf-8", errors="replace")
    parser = SocialImageParser()
    parser.feed(html)
    if not parser.image_url:
        return None
    return urllib.parse.urljoin(page_url, parser.image_url), page_url


def import_hearthstone(cards: list[dict], slug: str, display_name: str, target: pathlib.Path) -> dict | None:
    match = find_card(cards, slug, display_name)
    if match is None:
        return None

    card = match.card
    card_id = str(card["id"])
    art_url = ART_URL.format(card_id=card_id)
    width, height = save_png(fetch_bytes(art_url), target)
    print(f"HS    {slug}: {match.matched_name} -> {card_id} ({width}x{height})")
    return {
        "displayName": display_name,
        "status": "hearthstone",
        "source": "HearthstoneJSON",
        "cardId": card_id,
        "dbfId": card.get("dbfId"),
        "matchedName": match.matched_name,
        "exactName": match.exact,
        "cardType": card.get("type"),
        "artist": card.get("artist"),
        "artUrl": art_url,
        "output": target.as_posix(),
    }


def import_warcraft(display_name: str, slug: str, target: pathlib.Path) -> dict | None:
    page_image = warcraft_page_image(display_name)
    if page_image is None:
        return None
    image_url, page_url = page_image
    width, height = save_png(fetch_bytes(image_url, timeout=20), target)
    print(f"WOW   {slug}: {page_url} ({width}x{height})")
    return {
        "displayName": display_name,
        "status": "warcraft-wiki",
        "source": "Warcraft Wiki",
        "pageUrl": page_url,
        "imageUrl": image_url,
        "output": target.as_posix(),
    }


def main() -> int:
    cards = load_cards()
    report: dict[str, dict] = {}
    hearthstone_count = 0
    warcraft_count = 0
    missing_count = 0

    for slug, display_name in LEADERS.items():
        target = OUTPUT_ROOT / slug / "base.png"
        try:
            entry = import_hearthstone(cards, slug, display_name, target)
        except (urllib.error.URLError, urllib.error.HTTPError, OSError, ValueError) as exc:
            print(f"WARN  {slug}: Hearthstone import failed: {exc}")
            entry = None

        if entry is not None:
            report[slug] = entry
            hearthstone_count += 1
            continue

        try:
            entry = import_warcraft(display_name, slug, target)
        except (urllib.error.URLError, urllib.error.HTTPError, OSError, ValueError) as exc:
            print(f"WARN  {slug}: Warcraft fallback failed: {exc}")
            entry = None

        if entry is not None:
            report[slug] = entry
            warcraft_count += 1
            continue

        print(f"MISS  {slug}: no usable art found for {display_name!r}")
        report[slug] = {"displayName": display_name, "status": "missing"}
        missing_count += 1

    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(
        json.dumps(
            {
                "generatedFrom": CARDS_URL,
                "artApi": "https://art.hearthstonejson.com/",
                "hearthstone": hearthstone_count,
                "warcraftWiki": warcraft_count,
                "missing": missing_count,
                "leaders": report,
            },
            indent=2,
            ensure_ascii=False,
        ) + "\n",
        encoding="utf-8",
    )

    total = len(LEADERS)
    print(
        f"\nImported {total - missing_count}/{total}: "
        f"Hearthstone={hearthstone_count}, WarcraftWiki={warcraft_count}, missing={missing_count}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
