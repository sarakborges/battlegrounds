#!/usr/bin/env python3
from __future__ import annotations

import json
import pathlib
import sys
import urllib.error
import urllib.request

CARDS_URL = "https://api.hearthstonejson.com/v1/latest/enUS/cards.json"
ART_URL = "https://art.hearthstonejson.com/v1/512x/{card_id}.webp"
MOD_ROOT = pathlib.Path("mods/warbands")
ASSET_ROOT = MOD_ROOT / "assets"
PRESENTATION_PATH = ASSET_ROOT / "presentation.json"
SOURCES_PATH = ASSET_ROOT / "art-sources.json"

UNITS = {
    "wolf-cub": "Timber Wolf",
    "razorboar": "Razorboar",
    "pack-hunter": "Scavenging Hyena",
    "moonfang-alpha": "Moonfang",
    "thornhide-bear": "Witchwood Grizzly",
    "bloodmane-raptor": "Bloodfen Raptor",
    "ancient-stag": "Gloom Stag",
    "elder-hydra": "Cave Hydra",
    "dire-packlord": "Dire Wolf Alpha",
    "bone-squire": "Skeletal Sidekick",
    "grave-ghoul": "Unstable Ghoul",
    "restless-knight": "Risen Rider",
    "crypt-banshee": "Wailing Banshee",
    "bonecaller": "Bonecaller",
    "dread-guard": "Deathlord",
    "death-herald": "Deathspeaker",
    "tomb-colossus": "Flesh Behemoth",
    "grave-champion": "Skeletal Knight",
    "scrapbot": "Junkbot",
    "goblin-tinkerer": "Tinkertown Technician",
    "shield-drone": "Shielded Minibot",
    "bomb-lobber": "Bomb Lobber",
    "clockwork-brute": "Clockwork Knight",
    "junkyard-foreman": "Scrap Scraper",
    "war-machine": "Foe Reaper 4000",
    "siege-colossus": "Siege Engine",
    "scrap-titan": "Scrap Golem",
}

ACTIONS = {
    "battle-plans": "Secret Plan",
    "battle-training": "Training Session",
    "heavy-armor": "Heavy Plate",
    "market-voucher": "The Coin",
    "pack-bond": "Pack Tactics",
    "sharpen-blades": "Deadly Poison",
    "tactical-retreat": "Vanish",
    "veteran-drill": "Into the Fray",
    "war-drums": "Drum Circle",
}

PREFERRED_IDS = {
    "Timber Wolf": "DS1_175",
    "Razorboar": "BAR_325",
    "Scavenging Hyena": "EX1_531",
    "Moonfang": "YOP_035",
    "Witchwood Grizzly": "GIL_623",
    "Bloodfen Raptor": "CS2_172",
    "Dire Wolf Alpha": "EX1_162",
    "Risen Rider": "BG25_001",
    "Deathspeaker": "ICC_467",
    "The Coin": "GAME_005",
}


def fetch_bytes(url: str, timeout: int = 30) -> bytes:
    request = urllib.request.Request(
        url,
        headers={"User-Agent": "Battlegrounds-Warbands-Card-Art-Fetcher/1.1"},
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.read()


def load_cards() -> list[dict]:
    payload = json.loads(fetch_bytes(CARDS_URL).decode("utf-8"))
    if not isinstance(payload, list):
        raise ValueError("HearthstoneJSON cards endpoint did not return a list")
    return payload


def score(card: dict, expected_type: str) -> tuple[int, int, int, int]:
    card_id = str(card.get("id", ""))
    is_triple = (
        card_id.startswith("TB_BaconUps_")
        or card_id.endswith("_G")
        or "_GOLDEN" in card_id.upper()
    )
    return (
        1 if str(card.get("type", "")) == expected_type else 0,
        1 if card.get("collectible") else 0,
        0 if is_triple else 1,
        int(card.get("dbfId", 0) or 0),
    )


def find_cards(cards: list[dict], name: str, expected_type: str) -> list[dict]:
    matches = [
        card
        for card in cards
        if str(card.get("name", "")).casefold() == name.casefold()
    ]
    if not matches:
        raise LookupError(f"No Hearthstone card named {name!r}")

    matches.sort(key=lambda card: score(card, expected_type), reverse=True)
    preferred_id = PREFERRED_IDS.get(name)
    if preferred_id:
        preferred = next((card for card in matches if card.get("id") == preferred_id), None)
        if preferred is not None:
            matches.remove(preferred)
            matches.insert(0, preferred)
    return matches


def download_art(candidates: list[dict], target: pathlib.Path) -> tuple[dict, str]:
    failures: list[str] = []
    for card in candidates:
        card_id = str(card["id"])
        art_url = ART_URL.format(card_id=card_id)
        try:
            data = fetch_bytes(art_url)
        except urllib.error.HTTPError as exc:
            failures.append(f"{card_id}:{exc.code}")
            continue
        if len(data) < 12 or data[:4] != b"RIFF" or data[8:12] != b"WEBP":
            failures.append(f"{card_id}:not-webp")
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        return card, art_url
    raise ValueError(f"No downloadable WebP art among candidates: {', '.join(failures)}")


def load_manifest() -> dict:
    if not PRESENTATION_PATH.exists():
        return {}
    payload = json.loads(PRESENTATION_PATH.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise ValueError("assets/presentation.json must contain an object")
    return payload


def import_group(
    cards: list[dict],
    mapping: dict[str, str],
    group: str,
    expected_type: str,
) -> dict[str, dict]:
    imported: dict[str, dict] = {}
    for entity_id, source_name in mapping.items():
        candidates = find_cards(cards, source_name, expected_type)
        target = ASSET_ROOT / group / f"{entity_id}.webp"
        card, art_url = download_art(candidates, target)
        imported[entity_id] = {
            "sourceCardName": source_name,
            "cardId": card.get("id"),
            "dbfId": card.get("dbfId"),
            "artist": card.get("artist"),
            "artUrl": art_url,
            "output": target.as_posix(),
        }
        print(f"{group.upper():7} {entity_id}: {source_name} -> {card.get('id')}")
    return imported


def update_manifest() -> None:
    manifest = load_manifest()
    units = manifest.setdefault("units", {})
    actions = manifest.setdefault("actions", {})

    for entity_id in UNITS:
        entry = units.setdefault(entity_id, {})
        entry["art"] = f"assets/units/{entity_id}.webp"

    for entity_id in ACTIONS:
        entry = actions.setdefault(entity_id, {})
        entry["art"] = f"assets/actions/{entity_id}.webp"

    PRESENTATION_PATH.parent.mkdir(parents=True, exist_ok=True)
    PRESENTATION_PATH.write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def validate_authored_ids() -> None:
    unit_ids = {path.stem for path in (MOD_ROOT / "content/units").glob("*.json")}
    action_ids = {path.stem for path in (MOD_ROOT / "content/actions").glob("*.json")}
    missing_units = sorted(set(UNITS) - unit_ids)
    missing_actions = sorted(set(ACTIONS) - action_ids)
    uncovered_units = sorted(unit_ids - set(UNITS))
    uncovered_actions = sorted(action_ids - set(ACTIONS))
    if missing_units or missing_actions or uncovered_units or uncovered_actions:
        raise ValueError(
            "Card art mapping is out of sync with authored content: "
            f"missing_units={missing_units}, missing_actions={missing_actions}, "
            f"uncovered_units={uncovered_units}, uncovered_actions={uncovered_actions}"
        )


def main() -> int:
    validate_authored_ids()
    cards = load_cards()
    units = import_group(cards, UNITS, "units", "MINION")
    actions = import_group(cards, ACTIONS, "actions", "SPELL")
    update_manifest()

    SOURCES_PATH.write_text(
        json.dumps(
            {
                "generatedFrom": CARDS_URL,
                "artApi": "https://art.hearthstonejson.com/",
                "units": units,
                "actions": actions,
            },
            indent=2,
            ensure_ascii=False,
            sort_keys=True,
        ) + "\n",
        encoding="utf-8",
    )

    print(f"\nImported {len(units)} unit arts and {len(actions)} action arts.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (urllib.error.URLError, urllib.error.HTTPError, OSError, ValueError, LookupError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        sys.exit(1)
