#!/usr/bin/env python3
from __future__ import annotations

import io
import json
import pathlib
import sys
import urllib.error
import urllib.request
from dataclasses import dataclass

from PIL import Image

CARDS_URL = "https://api.hearthstonejson.com/v1/latest/enUS/cards.json"
ART_URL = "https://art.hearthstonejson.com/v1/512x/{card_id}.jpg"
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

# Only used when Hearthstone itself spells a character differently than the
# Warbands content name. Keep this intentionally small: exact matches remain the
# default and the report records when an alias was needed.
ALIASES = {
    "arthas-menethil": ["Prince Arthas", "The Lich King"],
    "garona-halforcen": ["Garona Halforcen", "Garona"],
    "nekros-skullcrusher": ["Nekros Skullcrusher", "Nekros"],
}

TYPE_PRIORITY = {
    "HERO": 5,
    "MINION": 4,
    "SPELL": 2,
    "WEAPON": 1,
}


@dataclass(frozen=True)
class Match:
    card: dict
    matched_name: str
    exact: bool


def fetch_bytes(url: str) -> bytes:
    request = urllib.request.Request(
        url,
        headers={"User-Agent": "Battlegrounds-Warbands-Art-Fetcher/1.0"},
    )
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def load_cards() -> list[dict]:
    return json.loads(fetch_bytes(CARDS_URL).decode("utf-8"))


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
        source.load()
        image = source.convert("RGBA")
        target.parent.mkdir(parents=True, exist_ok=True)
        image.save(target, format="PNG", optimize=True)
        return image.size


def main() -> int:
    cards = load_cards()
    report: dict[str, dict] = {}
    found = 0
    missing = 0

    for slug, display_name in LEADERS.items():
        match = find_card(cards, slug, display_name)
        if match is None:
            print(f"MISS  {slug}: no Hearthstone card named {display_name!r}")
            report[slug] = {
                "displayName": display_name,
                "status": "missing",
                "source": "hearthstonejson",
            }
            missing += 1
            continue

        card = match.card
        card_id = str(card["id"])
        art_url = ART_URL.format(card_id=card_id)
        target = OUTPUT_ROOT / slug / "base.png"
        try:
            width, height = save_png(fetch_bytes(art_url), target)
        except (urllib.error.URLError, urllib.error.HTTPError, OSError) as exc:
            print(f"MISS  {slug}: {card_id} art download/decode failed: {exc}")
            report[slug] = {
                "displayName": display_name,
                "status": "missing-art",
                "source": "hearthstonejson",
                "cardId": card_id,
                "matchedName": match.matched_name,
            }
            missing += 1
            continue

        print(f"OK    {slug}: {match.matched_name} -> {card_id} ({width}x{height})")
        report[slug] = {
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
        found += 1

    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(
        json.dumps(
            {
                "generatedFrom": CARDS_URL,
                "artApi": "https://art.hearthstonejson.com/",
                "found": found,
                "missing": missing,
                "leaders": report,
            },
            indent=2,
            ensure_ascii=False,
        ) + "\n",
        encoding="utf-8",
    )

    print(f"\nHearthstone art: {found}/{len(LEADERS)}; missing: {missing}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
