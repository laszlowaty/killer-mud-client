#!/usr/bin/env python3
"""Initialize, validate, and publish staged KillerMUD area workspaces."""

from __future__ import annotations

import argparse
import json
import re
import sys
import textwrap
from collections import deque
from pathlib import Path

STAGES = ("grid", "descriptions", "mobs", "items", "resets", "review")
OPPOSITE = {"north": "south", "south": "north", "east": "west", "west": "east", "up": "down", "down": "up"}
DIRECTION_NUMBER = {"north": 0, "east": 1, "south": 2, "west": 3, "up": 4, "down": 5}
SECTIONS = ("#AREADATA", "#MOBILES", "#OBJECTS", "#ROOMS", "#SHOPS", "#BANKS", "#SPECIALS", "#RESETS", "#MOBPROGSNEW", "#OBJPROGSNEW", "#ROOMPROGSNEW", "#TRAPS", "#RANDDESC", "#DESC", "#REPAIRS", "#BONUS_SET", "#$")
STANDARD_RESERVOIRS = ("rezerwua.are", "rezerwuc.are", "rezerwuf.are", "rezerwuj.are", "rezerwum.are")
ROOM_LIGHT_FLAGS = {"natural": "0", "lit": "0|H", "dark": "0|A"}
WEAR_SLOT_RULES = {
    "light": ("R", {0}),
    "finger": ("B", {1, 2}),
    "neck": ("C", {3, 4}),
    "body": ("D", {5}),
    "head": ("E", {6}),
    "legs": ("F", {7}),
    "feet": ("G", {8}),
    "hands": ("H", {9}),
    "arms": ("I", {10}),
    "shield": ("J", {11}),
    "about": ("K", {12}),
    "waist": ("L", {13}),
    "wrist": ("M", {14, 15}),
    "wield": ("N", {16}),
    "hold": ("O", {17}),
    "float": ("Q", {18}),
    "second": ("Z", {19}),
    "instrument": ("S", {20}),
    "ear": ("T", {21, 22}),
}
WEAPON_CLASSES = {"exotic": 0, "sword": 1, "dagger": 2, "spear": 3, "mace": 4, "axe": 5, "flail": 6, "whip": 7, "polearm": 8, "staff": 9, "shortsword": 11, "claws": 12}
MOB_PROFILES = {
    "dwarf-civilian": {"race": "krasnolud", "act": "0|AEG", "affected": "0 0 0 0", "affected_ext": "0", "align": 500, "group": 31, "lang": "AC", "speaks": 2, "hitroll": 1, "hp": "8d10+360", "damage": "2d4+0", "weaponbonus": 1, "attack": "uderzenie", "xac": "5 5 5 5", "stats": "126 126 126 126 132 78 96", "off": "0", "wealth": 20, "size": "medium", "form": "AFHMV", "parts": "ABCDEFGHIJKR", "exp": 100},
    "dwarf-guard": {"race": "krasnolud", "act": "0|AEGT", "affected": "512 4096 0 0", "affected_ext": "0|J/1|M", "align": 500, "group": 31, "lang": "AC", "speaks": 2, "hitroll": 3, "hp": "19d6+420", "damage": "3d3+0", "weaponbonus": 4, "attack": "uderzenie", "xac": "2 2 3 4", "stats": "138 126 108 138 138 60 102", "off": "FNT", "wealth": 35, "size": "medium", "form": "AFHMV", "parts": "ABCDEFGHIJKR", "exp": 100},
    "dwarf-chaplain": {"race": "krasnolud", "act": "0|AEGQ", "affected": "520 0 0 0", "affected_ext": "0|DJ", "align": 1000, "group": 31, "lang": "AC", "speaks": 0, "hitroll": 2, "hp": "8d10+500", "damage": "2d4+0", "weaponbonus": 3, "attack": "uderzenie", "xac": "0 0 0 0", "stats": "138 132 150 126 144 90 120", "off": "V", "wealth": 20, "size": "large", "form": "AFHMV", "parts": "ABCDEFGHIJKR", "exp": 100},
    "dwarf-commander": {"race": "krasnolud", "act": "0|AEGST", "affected": "520 0 0 0", "affected_ext": "0|DJ", "align": 750, "group": 31, "lang": "AC", "speaks": 2, "hitroll": 5, "hp": "8d10+680", "damage": "2d5+0", "weaponbonus": 7, "attack": "uderzenie", "xac": "0 -2 0 0", "stats": "150 120 144 132 132 78 78", "off": "CEIKL", "wealth": 100, "size": "large", "form": "AFHMV", "parts": "ABCDEFGHIJKR", "exp": 100},
    "ogre-scout": {"race": "ogr", "act": "0|ACFIMT", "affected": "512 0 0 0", "affected_ext": "0|J", "align": -1000, "group": 0, "lang": "E", "speaks": 4, "hitroll": 4, "hp": "10d10+520", "damage": "3d5+4", "weaponbonus": 5, "attack": "walenie", "xac": "0 0 0 0", "stats": "144 72 72 96 144 78 78", "off": "CDILR", "wealth": 20, "size": "large", "form": "AEFHMV", "parts": "ABCDEFGHIJKRUV", "exp": 100},
    "ogre-leader": {"race": "ogr", "act": "0|ABFT/2|C", "affected": "33554440 0 0 0", "affected_ext": "0|DZ", "align": -1000, "group": 0, "lang": "E", "speaks": 4, "hitroll": 5, "hp": "20d10+1000", "damage": "4d6+3", "weaponbonus": 7, "attack": "walenie", "xac": "-3 -3 -3 -3", "stats": "162 120 120 102 144 60 90", "off": "CDIORY", "wealth": 120, "size": "huge", "form": "AEFHMV", "parts": "ABCDEFGHIJKRUV", "exp": 150},
}
PROFILE_COMBAT = {
    "dwarf-civilian": {"actFlags": [], "offSkills": []},
    "dwarf-guard": {"actFlags": ["warrior"], "offSkills": ["dodge", "trip", "shield_block"]},
    "dwarf-chaplain": {"actFlags": ["cleric"], "offSkills": ["stun"]},
    "dwarf-commander": {"actFlags": ["warrior"], "offSkills": ["bash", "disarm", "kick", "parry", "rescue"]},
    "ogre-scout": {"actFlags": ["warrior"], "offSkills": ["bash", "berserk", "kick", "rescue", "assist_race"]},
    "ogre-leader": {"actFlags": ["warrior", "boss"], "offSkills": ["bash", "berserk", "kick", "crush", "assist_race", "two_attack"]},
}
CASTER_ACTS = {"mage", "cleric"}
AREA_ROOT = Path("tools/KillerMUD/area-lore")


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def active_ranges(area_root: Path) -> list[tuple[int, int, str]]:
    result = []
    area_list = area_root / "area.lst"
    if not area_list.exists():
        return result
    for raw_name in area_list.read_text(encoding="iso-8859-2", errors="strict").splitlines():
        name = raw_name.strip()
        if not name or name.startswith("-") or name == "$":
            continue
        path = area_root / name
        if not path.is_file():
            continue
        text = path.read_text(encoding="iso-8859-2", errors="strict")
        match = re.search(r"(?m)^VNUMs\s+(\d+)\s+(\d+)\s*$", text)
        if match:
            result.append((int(match.group(1)), int(match.group(2)), name))
    return result


def object_records(path: Path, encoding: str) -> dict[int, dict]:
    if not path.is_file():
        return {}
    text = path.read_text(encoding=encoding, errors="strict")
    if "#OBJECTS" not in text or "#ROOMS" not in text:
        return {}
    body = text[text.index("#OBJECTS") + len("#OBJECTS"):text.index("#ROOMS")]
    result: dict[int, dict] = {}
    matches = list(re.finditer(r"(?m)^#Vnum\s+(\d+)\s*$", body))
    for index, match in enumerate(matches):
        vnum = int(match.group(1))
        if vnum == 0:
            continue
        end = matches[index + 1].start() if index + 1 < len(matches) else len(body)
        block = body[match.end():end]
        field = lambda pattern, default="": (found.group(1).strip() if (found := re.search(pattern, block, re.MULTILINE)) else default)
        result[vnum] = {
            "type": field(r"^Type\s+(\S+)"),
            "wear": field(r"^Wear\s+(.+)$"),
            "wear2Ext": field(r"^Wear2Ext\s+(.+)$", "0"),
            "values": [int(value) for value in field(r"^Value\s+(.+)$").split() if re.fullmatch(r"-?\d+", value)],
        }
    return result


def standard_reservoir_object_records(area_root: Path) -> dict[str, dict[int, dict]]:
    result: dict[str, dict[int, dict]] = {}
    for name in STANDARD_RESERVOIRS:
        result[name] = object_records(area_root / name, "iso-8859-2")
    return result


def skill_catalog(area_root: Path) -> dict[int, tuple[str, list[int], str]]:
    path = area_root.parent / "src" / "const_skills.c"
    if not path.is_file():
        return {}
    text = path.read_text(encoding="iso-8859-2", errors="replace")
    table = text[text.find("skill_table"):]
    records = re.findall(r'(?ms)^\s*\{\s*\n\s*"([^"]+)"[^\n]*\n\s*\{([^}]+)\}\s*,\s*\n\s*([A-Za-z_][A-Za-z0-9_]*|0)\s*,', table)
    result: dict[int, tuple[str, list[int], str]] = {}
    # Entry zero ("reserved") has a commented function field and is intentionally
    # absent from the regex; real spell numbers therefore start at one.
    for spell_id, (name, raw_levels, function) in enumerate(records, start=1):
        levels = [int(value) for value in re.findall(r"\d+", raw_levels)]
        result[spell_id] = (name, levels, function)
    return result


def init_project(args: argparse.Namespace) -> int:
    if args.vnum_min > args.vnum_max or args.level_min > args.level_max:
        raise ValueError("invalid inclusive range")
    if args.vnum_min < 1 or args.vnum_max > 32767:
        raise ValueError("vnum range must stay within 1..32767")
    room_count = args.room_count if args.room_count is not None else args.vnum_max - args.vnum_min + 1
    if room_count < 1 or room_count > args.vnum_max - args.vnum_min + 1:
        raise ValueError("room count must fit within the inclusive vnum range")
    area_root = AREA_ROOT
    collisions = [name for low, high, name in active_ranges(area_root) if args.vnum_min <= high and args.vnum_max >= low]
    if collisions:
        raise ValueError("vnum range overlaps active areas: " + ", ".join(collisions))
    workspace = area_root / "drafts" / args.slug
    if workspace.exists():
        raise FileExistsError(f"workspace already exists: {workspace}")
    workspace.mkdir(parents=True)
    project = {
        "schemaVersion": 3,
        "slug": args.slug,
        "name": args.name,
        "approval": {"accepted": True, "note": args.approval_note, "brief": args.brief},
        "worldContextPack": args.context,
        "vnumRange": {"min": args.vnum_min, "max": args.vnum_max},
        "roomCount": room_count,
        "levelRange": {"min": args.level_min, "max": args.level_max},
        "entryRoomVnum": None,
        "externalDependencies": [],
        "rooms": [], "mobs": [], "items": [], "equipmentAssignments": [], "resets": [],
        "stages": {stage: "pending" for stage in STAGES},
        "reviewNotes": [],
    }
    (workspace / "area-project.json").write_text(json.dumps(project, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    skeleton = f"""#AREADATA\nName {args.name}~\nBuilders AI_draft~\nVNUMs {args.vnum_min} {args.vnum_max}\nLocked ~\nSecurity 1\nResetAge 20\nRegion 0\nEnd\n\n\n\n#MOBILES\n#Vnum 0\n\n\n\n#OBJECTS\n#Vnum 0\n\n\n\n#ROOMS\n#Vnum 0\n\n\n\n#SHOPS\n0\n\n\n\n#BANKS\n0\n\n\n\n#SPECIALS\nS\n\n\n\n#RESETS\nS\n\n\n\n#MOBPROGSNEW\n#0\n\n#OBJPROGSNEW\n#0\n\n#ROOMPROGSNEW\n#0\n\n#TRAPS\n#Vnum 0\n\n#RANDDESC\n#Vnum 0\n\n#DESC\n#DescEnd\n\n#REPAIRS\n0\n\n\n\n#BONUS_SET\n#Vnum 0\n\n#$\n"""
    (workspace / "draft.are.utf8").write_text(skeleton, encoding="utf-8", newline="\n")
    print(workspace / "area-project.json")
    return 0


def require_fields(record: dict, fields: tuple[str, ...], label: str, errors: list[str]) -> None:
    for field in fields:
        if field not in record or record[field] in (None, "", []):
            errors.append(f"{label}: missing {field}")


def validate_project(path: Path, stage: str) -> list[str]:
    project = load_json(path)
    errors: list[str] = []
    low, high = project["vnumRange"]["min"], project["vnumRange"]["max"]
    if not project.get("approval", {}).get("accepted"):
        errors.append("concept is not explicitly accepted")
    expected_prior = STAGES[: STAGES.index(stage)]
    for prior in expected_prior:
        if project.get("stages", {}).get(prior) != "complete":
            errors.append(f"prior stage is not complete: {prior}")

    def check_ids(records: list[dict], key: str, kind: str) -> set[int]:
        seen: set[int] = set()
        for record in records:
            value = record.get(key)
            if not isinstance(value, int) or not low <= value <= high:
                errors.append(f"{kind} vnum outside range: {value}")
            elif value in seen:
                errors.append(f"duplicate {kind} vnum: {value}")
            else:
                seen.add(value)
        return seen

    rooms = project.get("rooms", [])
    room_ids = check_ids(rooms, "vnum", "room")
    if stage in STAGES[STAGES.index("grid"):]:
        if not rooms:
            errors.append("room grid is empty")
        if len(rooms) != project.get("roomCount"):
            errors.append(f"room grid has {len(rooms)} rooms; expected {project.get('roomCount')}")
        if project.get("entryRoomVnum") not in room_ids:
            errors.append("entryRoomVnum does not identify a room")
        adjacency: dict[int, set[int]] = {v: set() for v in room_ids}
        directions: dict[tuple[int, str], int] = {}
        for room in rooms:
            require_fields(room, ("vnum", "name", "sector", "role", "zone", "x", "y", "z"), f"room {room.get('vnum')}", errors)
            if project.get("schemaVersion", 1) >= 3 and room.get("lightMode") not in ROOM_LIGHT_FLAGS:
                errors.append(f"room {room.get('vnum')}: lightMode must be natural, lit, or dark")
            for ex in room.get("exits", []):
                direction, target = ex.get("direction"), ex.get("to")
                if direction not in OPPOSITE:
                    errors.append(f"room {room.get('vnum')}: invalid direction {direction}")
                if target not in room_ids:
                    errors.append(f"room {room.get('vnum')}: external or missing exit target {target}")
                else:
                    adjacency[room["vnum"]].add(target)
                    directions[(room["vnum"], direction)] = target
        for (source, direction), target in directions.items():
            if directions.get((target, OPPOSITE[direction])) != source:
                errors.append(f"exit {source} {direction} -> {target} is not reciprocal")
        entry = project.get("entryRoomVnum")
        if entry in adjacency:
            reached, queue = {entry}, deque([entry])
            while queue:
                current = queue.popleft()
                for target in adjacency[current] - reached:
                    reached.add(target); queue.append(target)
            if reached != room_ids:
                errors.append("rooms unreachable from entry: " + ", ".join(map(str, sorted(room_ids - reached))))

    if stage in STAGES[STAGES.index("descriptions"):]:
        for room in rooms:
            require_fields(room, ("description",), f"room {room.get('vnum')}", errors)
            if project.get("schemaVersion", 1) >= 2:
                description = str(room.get("description", "")).strip()
                if len(description) < 260 and not room.get("descriptionLengthException"):
                    errors.append(f"room {room.get('vnum')}: description shorter than 260 characters")
                if len(re.findall(r"[.!?](?:\s|$)", description)) < 3 and not room.get("descriptionLengthException"):
                    errors.append(f"room {room.get('vnum')}: description should normally contain at least three sentences")

    mobs = project.get("mobs", [])
    area_root = path.parent.parent.parent
    skills = skill_catalog(area_root)
    mob_ids = check_ids(mobs, "vnum", "mob")
    if stage in STAGES[STAGES.index("mobs"):]:
        if not mobs:
            errors.append("mob set is empty")
        for mob in mobs:
            require_fields(mob, ("name", "role", "level", "rooms", "description"), f"mob {mob.get('vnum')}", errors)
            if not project["levelRange"]["min"] <= mob.get("level", -1) <= project["levelRange"]["max"]:
                errors.append(f"mob {mob.get('vnum')}: level outside intended range")
            for room in mob.get("rooms", []):
                if room not in room_ids:
                    errors.append(f"mob {mob.get('vnum')}: missing room {room}")
            if project.get("schemaVersion", 1) >= 2:
                plan = mob.get("combatPlan", {})
                require_fields(plan, ("archetype",), f"mob {mob.get('vnum')} combatPlan", errors)
                for field in ("actFlags", "offSkills", "spells", "equipmentNeeds"):
                    if field not in plan or not isinstance(plan[field], list):
                        errors.append(f"mob {mob.get('vnum')} combatPlan: missing list {field}")
                act_flags = set(plan.get("actFlags", []))
                off_skills = set(plan.get("offSkills", []))
                spells = plan.get("spells", [])
                if not isinstance(spells, list) or len(spells) > 16 or any(not isinstance(value, int) or value <= 0 for value in spells):
                    errors.append(f"mob {mob.get('vnum')}: spells must contain at most 16 positive numeric spell IDs")
                if act_flags & CASTER_ACTS and not spells:
                    errors.append(f"mob {mob.get('vnum')}: mage/cleric act requires a non-empty spell repertoire")
                caster_acts = act_flags & CASTER_ACTS
                if len(caster_acts) > 1:
                    errors.append(f"mob {mob.get('vnum')}: select one primary mage/cleric caster act")
                class_index = 0 if "mage" in caster_acts else 1 if "cleric" in caster_acts else None
                for spell_id in spells if isinstance(spells, list) else []:
                    record = skills.get(spell_id)
                    if record is None or not record[2].startswith("spell_"):
                        errors.append(f"mob {mob.get('vnum')}: spell ID {spell_id} is not an active spell")
                    elif class_index is not None and (len(record[1]) <= class_index or record[1][class_index] >= 32 or record[1][class_index] > mob.get("level", -1)):
                        errors.append(f"mob {mob.get('vnum')}: spell ID {spell_id} is unavailable to its caster class at level {mob.get('level')}")
                needs = plan.get("equipmentNeeds", [])
                if "backstab" in off_skills and not any(need.get("slot") == "wield" and need.get("itemType") == "weapon" and need.get("weaponClass") == "dagger" for need in needs):
                    errors.append(f"mob {mob.get('vnum')}: backstab requires a wielded dagger equipment need")
                shield_item_type = "shield" if project.get("schemaVersion", 1) >= 3 else "armor"
                if "shield_block" in off_skills and not any(need.get("slot") == "shield" and need.get("itemType") == shield_item_type for need in needs):
                    errors.append(f"mob {mob.get('vnum')}: shield_block requires a shield equipment need")
                expected = PROFILE_COMBAT.get(mob.get("profile"))
                if expected and (act_flags != set(expected["actFlags"]) or off_skills != set(expected["offSkills"])):
                    errors.append(f"mob {mob.get('vnum')}: combatPlan flags differ from selected profile mechanics")

    items = project.get("items", [])
    item_ids = check_ids(items, "vnum", "item")
    external_dependencies = project.get("externalDependencies", [])
    reservoir_records = standard_reservoir_object_records(area_root)
    verified_reservoir_ids: set[int] = set()
    for index, dependency in enumerate(external_dependencies, 1):
        if dependency.get("kind") != "object" or dependency.get("sourceType") != "standard-reservoir":
            continue
        require_fields(dependency, ("sourceFile", "vnum", "reason"), f"external dependency {index}", errors)
        source_file, vnum = dependency.get("sourceFile"), dependency.get("vnum")
        if source_file not in STANDARD_RESERVOIRS:
            errors.append(f"external dependency {index}: unsupported standard reservoir {source_file}")
        elif vnum not in reservoir_records.get(source_file, {}):
            errors.append(f"external dependency {index}: object {vnum} not found in {source_file} #OBJECTS")
        else:
            verified_reservoir_ids.add(vnum)
    if stage in STAGES[STAGES.index("items"):]:
        if not items and not verified_reservoir_ids:
            errors.append("item set and verified standard-reservoir dependency set are both empty")
        for item in items:
            require_fields(item, ("name", "type", "level", "source", "description"), f"item {item.get('vnum')}", errors)
            if project.get("schemaVersion", 1) >= 3:
                require_fields(item, ("wear", "wear2Ext", "values"), f"item {item.get('vnum')}", errors)
                if not isinstance(item.get("values"), list) or len(item["values"]) != 7 or any(not isinstance(value, int) for value in item["values"]):
                    errors.append(f"item {item.get('vnum')}: values must contain exactly seven integers")
        if project.get("schemaVersion", 1) >= 2:
            assignments = project.get("equipmentAssignments", [])
            available_objects = item_ids | verified_reservoir_ids
            local_records = {
                item["vnum"]: {
                    "type": item.get("type", ""),
                    "wear": item.get("wear", ""),
                    "wear2Ext": item.get("wear2Ext", "0"),
                    "values": item.get("values", []),
                }
                for item in items if isinstance(item.get("vnum"), int)
            }
            available_records = dict(local_records)
            for source_file, records in reservoir_records.items():
                for vnum, record in records.items():
                    if vnum in verified_reservoir_ids:
                        available_records[vnum] = record
            for index, assignment in enumerate(assignments, 1):
                require_fields(assignment, ("mobVnum", "slot", "objectVnum", "itemType", "reason"), f"equipment assignment {index}", errors)
                if assignment.get("mobVnum") not in mob_ids:
                    errors.append(f"equipment assignment {index}: missing mob {assignment.get('mobVnum')}")
                if assignment.get("objectVnum") not in available_objects:
                    errors.append(f"equipment assignment {index}: undeclared object {assignment.get('objectVnum')}")
                    continue
                slot = assignment.get("slot")
                rule = WEAR_SLOT_RULES.get(slot)
                if rule is None:
                    errors.append(f"equipment assignment {index}: unsupported semantic wear slot {slot}")
                    continue
                wear_flag, reset_slots = rule
                record = available_records.get(assignment.get("objectVnum"), {})
                if wear_flag not in record.get("wear", ""):
                    errors.append(f"equipment assignment {index}: object {assignment.get('objectVnum')} Wear lacks {wear_flag} required for {slot}")
                if slot in ("wield", "second") and record.get("type") != "weapon":
                    errors.append(f"equipment assignment {index}: {slot} requires Type weapon")
                if slot == "shield" and record.get("type") != "shield":
                    errors.append(f"equipment assignment {index}: shield requires Type shield")
                if project.get("schemaVersion", 1) >= 3 and assignment.get("itemType") != record.get("type"):
                    errors.append(f"equipment assignment {index}: itemType differs from actual object Type")
                weapon_class = assignment.get("weaponClass")
                if weapon_class:
                    expected_class = WEAPON_CLASSES.get(weapon_class)
                    values = record.get("values", [])
                    if expected_class is None or not values or values[0] != expected_class:
                        errors.append(f"equipment assignment {index}: object weapon class differs from {weapon_class}")
                if project.get("schemaVersion", 1) >= 3:
                    verification = assignment.get("equipVerification")
                    if not isinstance(verification, dict):
                        errors.append(f"equipment assignment {index}: missing equipVerification")
                    else:
                        if verification.get("wearFlag") != wear_flag or verification.get("resetSlot") not in reset_slots:
                            errors.append(f"equipment assignment {index}: equipVerification wear flag or reset slot is incorrect")
                        for check in (
                            "raceAllowed",
                            "classAllowed",
                            "anatomyAllowed",
                            "levelAllowed",
                            "handsAllowed",
                            "sizeAllowed",
                            "weightAllowed",
                        ):
                            if verification.get(check) is not True:
                                errors.append(f"equipment assignment {index}: equipVerification {check} is not confirmed")
            for mob in mobs:
                for need in mob.get("combatPlan", {}).get("equipmentNeeds", []):
                    matches = [assignment for assignment in assignments if assignment.get("mobVnum") == mob.get("vnum") and assignment.get("slot") == need.get("slot") and assignment.get("itemType") == need.get("itemType") and (not need.get("weaponClass") or assignment.get("weaponClass") == need.get("weaponClass"))]
                    if not matches:
                        errors.append(f"mob {mob.get('vnum')}: unresolved equipment need for {need.get('slot')}")

    if stage in STAGES[STAGES.index("resets"):]:
        if not project.get("resets"):
            errors.append("reset set is empty")
        external = {dep.get("vnum") for dep in external_dependencies if dep.get("kind") == "object" and dep.get("reason")}
        for index, reset in enumerate(project.get("resets", []), 1):
            require_fields(reset, ("command",), f"reset {index}", errors)
            if "roomVnum" in reset and reset["roomVnum"] not in room_ids:
                errors.append(f"reset {index}: missing room {reset['roomVnum']}")
            if "mobVnum" in reset and reset["mobVnum"] not in mob_ids:
                errors.append(f"reset {index}: missing mob {reset['mobVnum']}")
            for key in ("objectVnum", "containerVnum"):
                if key in reset and reset[key] not in item_ids | external:
                    errors.append(f"reset {index}: undeclared object {reset[key]}")
        if project.get("schemaVersion", 1) >= 2:
            assignments = project.get("equipmentAssignments", [])
            last_mob = None
            contextual_equipment: list[dict] = []
            for index, reset in enumerate(project.get("resets", []), 1):
                if reset.get("command") == "M":
                    last_mob = reset.get("mobVnum")
                elif reset.get("command") in ("E", "G"):
                    if reset.get("mobVnum") != last_mob:
                        errors.append(f"reset {index}: equipment/give reset does not follow its mob reset")
                    if reset.get("command") == "E":
                        contextual_equipment.append(reset)
                        rule = WEAR_SLOT_RULES.get(reset.get("slot"))
                        parts = str(reset.get("line", "")).split()
                        if rule and (len(parts) < 5 or not parts[-1].lstrip("-").isdigit() or int(parts[-1]) not in rule[1]):
                            errors.append(f"reset {index}: numeric wear location differs from semantic slot {reset.get('slot')}")
            for index, assignment in enumerate(assignments, 1):
                matching = [reset for reset in contextual_equipment if reset.get("mobVnum") == assignment.get("mobVnum") and reset.get("objectVnum") == assignment.get("objectVnum") and reset.get("slot") == assignment.get("slot")]
                if not matching:
                    errors.append(f"equipment assignment {index}: missing matching E reset and semantic slot")
                elif assignment.get("required", True):
                    for reset in matching:
                        parts = str(reset.get("line", "")).split()
                        if len(parts) < 2 or parts[1] != "100":
                            errors.append(f"equipment assignment {index}: required tactical equipment must use a 100% conditional E reset")

    draft = path.parent / "draft.are.utf8"
    if not draft.exists():
        errors.append("missing draft.are.utf8")
    else:
        text = draft.read_text(encoding="utf-8")
        positions = [text.find(section) for section in SECTIONS]
        if any(pos < 0 for pos in positions) or positions != sorted(positions):
            errors.append("area section set/order is invalid")
        match = re.search(r"(?m)^VNUMs\s+(\d+)\s+(\d+)\s*$", text)
        if not match or (int(match.group(1)), int(match.group(2))) != (low, high):
            errors.append("draft VNUMs header differs from project")
        try:
            text.encode("iso-8859-2", errors="strict")
        except UnicodeEncodeError as exc:
            errors.append(f"draft cannot be encoded as ISO-8859-2: {exc}")
        section_bounds = {
            "mob": ("#MOBILES", "#OBJECTS", mob_ids),
            "item": ("#OBJECTS", "#ROOMS", item_ids),
            "room": ("#ROOMS", "#SHOPS", room_ids),
        }
        required_from = {"mob": "mobs", "item": "items", "room": "grid"}
        for kind, (start, end, ledger_ids) in section_bounds.items():
            if stage not in STAGES[STAGES.index(required_from[kind]):]:
                continue
            body = text[text.find(start) + len(start):text.find(end)]
            source_ids = {int(value) for value in re.findall(r"(?m)^#Vnum\s+(\d+)\s*$", body) if int(value) != 0}
            if source_ids != ledger_ids:
                errors.append(f"{kind} vnums differ between project and draft")
            if kind == "mob" and project.get("schemaVersion", 1) >= 2:
                for mob in mobs:
                    block_match = re.search(rf"(?ms)^#Vnum\s+{mob['vnum']}\s*$\n(.*?)(?=^#Vnum\s|\Z)", body)
                    if not block_match:
                        continue
                    rendered_spells = [int(value) for line in re.findall(r"(?m)^Spells\s+(.+)$", block_match.group(1)) for value in line.split() if int(value) != 0]
                    if rendered_spells != mob.get("combatPlan", {}).get("spells", []):
                        errors.append(f"mob {mob.get('vnum')}: rendered Spells differ from combatPlan")
        if stage in STAGES[STAGES.index("grid"):]:
            room_body = text[text.find("#ROOMS") + len("#ROOMS"):text.find("#SHOPS")]
            for target in map(int, re.findall(r"(?m)^ExitExtToVnum\s+(\d+)\s*$", room_body)):
                if target not in room_ids:
                    errors.append(f"draft contains external or missing room exit target {target}")
    return errors


def command_validate(args: argparse.Namespace) -> int:
    errors = validate_project(Path(args.project), args.stage)
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    print(f"OK: {args.stage}")
    return 0


def area_text(value: object, field: str) -> str:
    text = str(value).strip()
    if not text or "~" in text or "\x00" in text:
        raise ValueError(f"invalid area text in {field}")
    return text


def command_render_grid(args: argparse.Namespace) -> int:
    project_path = Path(args.project)
    project = load_json(project_path)
    if any(project.get("stages", {}).get(stage) == "complete" for stage in STAGES[1:]):
        raise ValueError("refusing to render grid after a later stage is complete")
    rooms = project.get("rooms", [])
    if len(rooms) != project.get("roomCount"):
        raise ValueError("room ledger does not match roomCount")
    blocks: list[str] = []
    for room in sorted(rooms, key=lambda value: value["vnum"]):
        name = area_text(room.get("name"), f"room {room.get('vnum')} name")
        description = area_text(room.get("description"), f"room {room.get('vnum')} description")
        light_mode = room.get("lightMode", "natural")
        if light_mode not in ROOM_LIGHT_FLAGS:
            raise ValueError(f"room {room.get('vnum')}: invalid lightMode {light_mode}")
        lines = [f"#Vnum {room['vnum']}", f"Name {name}~", "Descr " + "\n".join(textwrap.wrap(description, width=76)) + "~", f"FlagsExt {ROOM_LIGHT_FLAGS[light_mode]}", f"Sector {room['sector']}"]
        for exit_data in room.get("exits", []):
            direction = exit_data["direction"]
            number = DIRECTION_NUMBER[direction]
            target = exit_data["to"]
            lines.extend([
                f"ExitExt {number}", "ExitExtDescription ~", "ExitExtLiczba 1",
                "ExitExtInfo 0", "ExitExtKey 0", f"ExitExtToVnum {target}", "ExitExtEnd",
                f"Exit {number}", "~", "~", f"0 0 {target}",
            ])
        lines.extend(["Resources 0 0 0 0 0 0 0 0 ", "Capacity 0", "Roomend"])
        blocks.append("\n".join(lines))
    room_section = "#ROOMS\n" + "\n".join(blocks) + "\n#Vnum 0"
    draft = project_path.parent / "draft.are.utf8"
    source = draft.read_text(encoding="utf-8")
    start, end = source.index("#ROOMS"), source.index("#SHOPS")
    rendered = source[:start] + room_section + "\n\n\n\n" + source[end:]
    rendered.encode("iso-8859-2", errors="strict")
    draft.write_text(rendered, encoding="utf-8", newline="\n")
    print(f"rendered {len(rooms)} rooms: {draft}")
    return 0


def command_render_mobs(args: argparse.Namespace) -> int:
    project_path = Path(args.project)
    project = load_json(project_path)
    if project.get("stages", {}).get("items") == "complete":
        raise ValueError("refusing to render mobs after items are complete")
    blocks = []
    for mob in sorted(project.get("mobs", []), key=lambda value: value["vnum"]):
        profile = dict(MOB_PROFILES.get(mob.get("profile"), {}))
        profile.update(mob.get("mechanics", {}))
        required_mechanics = ("race", "act", "affected", "affected_ext", "align", "group", "lang", "speaks", "hitroll", "hp", "damage", "weaponbonus", "attack", "xac", "stats", "off", "wealth", "size", "form", "parts", "exp")
        missing_mechanics = [key for key in required_mechanics if key not in profile]
        if missing_mechanics:
            raise ValueError(f"mob {mob.get('vnum')}: missing mechanics fields: {', '.join(missing_mechanics)}")
        fields = {key: area_text(mob.get(key), f"mob {mob.get('vnum')} {key}") for key in ("keywords", "short", "long", "description")}
        declension = str(mob.get("declension", "")).strip()
        if declension.count("~") != 4 or "\x00" in declension:
            raise ValueError(f"mob {mob.get('vnum')} declension must contain five forms")
        description = "\n".join(textwrap.wrap(fields["description"], width=76))
        off_ext = "0" if profile["off"] == "0" else f"0|{profile['off']}"
        spells = mob.get("combatPlan", {}).get("spells", [])
        spell_lines = []
        for offset in range(0, len(spells), 4):
            group = spells[offset:offset + 4] + [0] * (4 - len(spells[offset:offset + 4]))
            spell_lines.append("Spells " + " ".join(map(str, group)))
        spell_block = ("\n".join(spell_lines) + "\n") if spell_lines else ""
        blocks.append(f"""#Vnum {mob['vnum']}
Name {fields['keywords']}~
Odmiana {declension}~
Short {fields['short']}~
Long {fields['long']}~
Descr {description}
~
Race {profile['race']}~
ActExt {profile['act']}
Affected {profile['affected']}
AffectedExt {profile['affected_ext']}
Align {profile['align']}
Group {profile['group']}
Lang {profile['lang']}
Speaks {profile['speaks']}
Level {mob['level']}
Hitroll {profile['hitroll']}
HP {profile['hp']}
Damage {profile['damage']}
Dammagic 0
Weaponbonus {profile['weaponbonus']}
Damflags 0
Attack {profile['attack']}
XAC {profile['xac']}
Stats {profile['stats']}
Off {profile['off']}
OffExt {off_ext}
Pos_start stand
Pos_def stand
Sex {mob.get('sex', 'male')}
Wealth {profile['wealth']}
Form {profile['form']}
Parts {profile['parts']}
Size {profile['size']}
Material unknown
Skin 100
ExpMultiplier {profile['exp']}
{spell_block}Mobend""")
    section = "#MOBILES\n" + "\n".join(blocks) + "\n#Vnum 0"
    draft = project_path.parent / "draft.are.utf8"
    source = draft.read_text(encoding="utf-8")
    start, end = source.index("#MOBILES"), source.index("#OBJECTS")
    rendered = source[:start] + section + "\n\n\n\n" + source[end:]
    rendered.encode("iso-8859-2", errors="strict")
    draft.write_text(rendered, encoding="utf-8", newline="\n")
    print(f"rendered {len(blocks)} mobs: {draft}")
    return 0


def command_render_items(args: argparse.Namespace) -> int:
    project_path = Path(args.project)
    project = load_json(project_path)
    if project.get("stages", {}).get("resets") == "complete":
        raise ValueError("refusing to render items after resets are complete")
    blocks = []
    for item in sorted(project.get("items", []), key=lambda value: value["vnum"]):
        fields = {key: area_text(item.get(key), f"item {item.get('vnum')} {key}") for key in ("keywords", "short", "long", "description", "material", "type", "wear")}
        declension = str(item.get("declension", "")).strip()
        if declension.count("~") != 4 or "\x00" in declension:
            raise ValueError(f"item {item.get('vnum')} declension must contain five forms")
        description = "\n".join(textwrap.wrap(fields["description"], width=76))
        wear2_ext = area_text(item.get("wear2Ext", "0"), f"item {item.get('vnum')} wear2Ext")
        values = item.get("values", [0, 0, 0, 0, 0, 0, 0])
        if not isinstance(values, list) or len(values) != 7 or any(not isinstance(value, int) for value in values):
            raise ValueError(f"item {item.get('vnum')}: values must contain exactly seven integers")
        blocks.append(f"""#Vnum {item['vnum']}
Name {fields['keywords']}~
Odmiana {declension}~
Short {fields['short']}~
Descr {fields['long']}~
Material {fields['material']}~
Type {fields['type']}
ExtraExt 0
Wear {fields['wear']}
Wear2Ext {wear2_ext}
Bonus 0
LiczbaMnoga 0
Gender 0
Value {' '.join(map(str, values))}
Repair 0 0 0 0
Length 0
Weight {item.get('weight', 1)}
Cost {item.get('cost', 1)}
Cond 100
Itemdesc {description}
~
Identdesc ~
Comments ~
Objend""")
    section = "#OBJECTS\n" + "\n".join(blocks) + "\n#Vnum 0"
    draft = project_path.parent / "draft.are.utf8"
    source = draft.read_text(encoding="utf-8")
    start, end = source.index("#OBJECTS"), source.index("#ROOMS")
    rendered = source[:start] + section + "\n\n\n\n" + source[end:]
    rendered.encode("iso-8859-2", errors="strict")
    draft.write_text(rendered, encoding="utf-8", newline="\n")
    print(f"rendered {len(blocks)} local items: {draft}")
    return 0


def command_render_resets(args: argparse.Namespace) -> int:
    project_path = Path(args.project)
    project = load_json(project_path)
    lines = []
    for index, reset in enumerate(project.get("resets", []), 1):
        line = str(reset.get("line", "")).strip()
        if not line or line.split(maxsplit=1)[0] != reset.get("command") or "\n" in line:
            raise ValueError(f"invalid reset line {index}")
        lines.append(line)
    section = "#RESETS\n" + "\n".join(lines) + "\nS"
    draft = project_path.parent / "draft.are.utf8"
    source = draft.read_text(encoding="utf-8")
    start, end = source.index("#RESETS"), source.index("#MOBPROGSNEW")
    rendered = source[:start] + section + "\n\n\n\n" + source[end:]
    rendered.encode("iso-8859-2", errors="strict")
    draft.write_text(rendered, encoding="utf-8", newline="\n")
    print(f"rendered {len(lines)} resets: {draft}")
    return 0


def command_publish(args: argparse.Namespace) -> int:
    project_path = Path(args.project)
    errors = validate_project(project_path, "review")
    project = load_json(project_path)
    if project.get("stages", {}).get("review") != "complete":
        errors.append("review stage is not marked complete")
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    source = project_path.parent / "draft.are.utf8"
    target = project_path.parent / f"{project['slug']}.are"
    if target.exists() and not args.force:
        raise FileExistsError(f"refusing to overwrite {target}")
    target.write_bytes(source.read_text(encoding="utf-8").encode("iso-8859-2", errors="strict"))
    print(target)
    return 0


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser()
    sub = root.add_subparsers(dest="command", required=True)
    init = sub.add_parser("init")
    init.add_argument("--slug", required=True); init.add_argument("--name", required=True)
    init.add_argument("--vnum-min", type=int, required=True); init.add_argument("--vnum-max", type=int, required=True)
    init.add_argument("--room-count", type=int)
    init.add_argument("--level-min", type=int, required=True); init.add_argument("--level-max", type=int, required=True)
    init.add_argument("--approval-note", required=True); init.add_argument("--brief", required=True)
    init.add_argument("--context")
    init.set_defaults(func=init_project)
    validate = sub.add_parser("validate")
    validate.add_argument("--project", required=True); validate.add_argument("--stage", choices=STAGES, required=True)
    validate.set_defaults(func=command_validate)
    render_grid = sub.add_parser("render-grid")
    render_grid.add_argument("--project", required=True)
    render_grid.set_defaults(func=command_render_grid)
    render_mobs = sub.add_parser("render-mobs")
    render_mobs.add_argument("--project", required=True)
    render_mobs.set_defaults(func=command_render_mobs)
    render_items = sub.add_parser("render-items")
    render_items.add_argument("--project", required=True)
    render_items.set_defaults(func=command_render_items)
    render_resets = sub.add_parser("render-resets")
    render_resets.add_argument("--project", required=True)
    render_resets.set_defaults(func=command_render_resets)
    publish = sub.add_parser("publish")
    publish.add_argument("--project", required=True); publish.add_argument("--force", action="store_true")
    publish.set_defaults(func=command_publish)
    return root


def main() -> int:
    args = parser().parse_args()
    try:
        return args.func(args)
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
