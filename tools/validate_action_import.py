#!/usr/bin/env python3
"""Validate and resolve a low-token Sophie life-action import config."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
ALLOWED_KEYS = {
    "schema", "state", "slug", "source", "scale", "row", "boxes",
    "loops", "probability", "islandX",
}
PROBABILITY_KEYS = {"clearDay", "other"}


def stop(message: str) -> None:
    raise SystemExit(f"action import config invalid: {message}")


def require_number(value: object, label: str, low: float, high: float) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        stop(f"{label} must be numeric")
    result = float(value)
    if not low <= result <= high:
        stop(f"{label} must be between {low} and {high}")
    return result


def main() -> None:
    if len(sys.argv) != 2:
        stop("usage: validate_action_import.py <import.json>")

    config_path = Path(sys.argv[1]).expanduser().resolve()
    if not config_path.is_file():
        stop(f"config does not exist: {config_path}")
    try:
        config = json.loads(config_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        stop(f"cannot read JSON: {error}")
    if not isinstance(config, dict):
        stop("root must be an object")

    unknown = sorted(set(config) - ALLOWED_KEYS)
    if unknown:
        stop(f"unknown fields: {', '.join(unknown)}")
    if config.get("schema") != 1:
        stop("schema must be 1")

    state = config.get("state")
    if not isinstance(state, str) or not re.fullmatch(r"[A-Z][A-Za-z0-9]*", state):
        stop("state must be PascalCase")
    slug = config.get("slug")
    if not isinstance(slug, str) or not re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", slug):
        stop("slug must be kebab-case")

    source_text = config.get("source")
    if not isinstance(source_text, str) or not source_text:
        stop("source is required")
    source = (ROOT / source_text).resolve()
    if not source.is_relative_to(ROOT):
        stop("source must remain inside the repository")
    if not source.is_file() or source.suffix.lower() != ".png":
        stop(f"source must be an existing PNG: {source_text}")
    with Image.open(source) as opened:
        width, height = opened.size

    scale = require_number(config.get("scale", 0.30), "scale", 0.05, 1.0)
    row = config.get("row", "append")
    if row != "append" and (isinstance(row, bool) or
                             not isinstance(row, int) or row < 0):
        stop("row must be a non-negative integer or 'append'")
    loops = config.get("loops", False)
    if not isinstance(loops, bool):
        stop("loops must be boolean")

    raw_boxes = config.get("boxes", [])
    if not isinstance(raw_boxes, list):
        stop("boxes must be an array")
    if raw_boxes:
        if len(raw_boxes) != 4:
            stop("boxes must contain exactly four crop boxes")
        boxes: list[list[int]] = []
        for index, raw_box in enumerate(raw_boxes):
            if (not isinstance(raw_box, list) or len(raw_box) != 4 or
                    any(isinstance(value, bool) or not isinstance(value, int)
                        for value in raw_box)):
                stop(f"boxes[{index}] must be four integers")
            left, top, right, bottom = raw_box
            if not (0 <= left < right <= width and 0 <= top < bottom <= height):
                stop(f"boxes[{index}] is outside the {width}x{height} source")
            boxes.append(raw_box)
    else:
        edges = [round(index * width / 4) for index in range(5)]
        boxes = [[edges[index], 0, edges[index + 1], height]
                 for index in range(4)]

    probability = config.get("probability", {})
    if not isinstance(probability, dict):
        stop("probability must be an object")
    unknown_probability = sorted(set(probability) - PROBABILITY_KEYS)
    if unknown_probability:
        stop("unknown probability fields: " + ", ".join(unknown_probability))
    clear_day = require_number(
        probability.get("clearDay", 7), "probability.clearDay", 0, 100)
    other = require_number(
        probability.get("other", 5), "probability.other", 0, 100)
    island_x = require_number(config.get("islandX", 240), "islandX", 0, 420)

    resolved = {
        "schema": 1,
        "state": state,
        "slug": slug,
        "source": source.relative_to(ROOT).as_posix(),
        "sourceSize": [width, height],
        "boxes": boxes,
        "scale": scale,
        "row": row,
        "loops": loops,
        "frameCount": 4,
        "frameMs": 500,
        "timerMs": 100,
        "activityTicks": 20,
        "idleTicks": 200,
        "probability": {"clearDay": clear_day, "other": other},
        "desktop": True,
        "island": True,
        "islandX": island_x,
        "menu": False,
        "versionBump": "patch",
        "publish": "explicit-approval-only",
    }
    print(json.dumps({"ok": True, "resolved": resolved}, indent=2))


if __name__ == "__main__":
    main()
