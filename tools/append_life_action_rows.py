#!/usr/bin/env python3
"""Append approved four-frame life-action rows to Sophie's PNG atlas."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image

CELL_WIDTH = 192
CELL_HEIGHT = 208
COLUMNS = 4


def parse_action(value: str) -> tuple[str, int]:
    try:
        action, row_text = value.rsplit(":", 1)
        row = int(row_text)
    except (ValueError, TypeError) as error:
        raise argparse.ArgumentTypeError(
            "action must have the form name:row, for example backpack:12"
        ) from error
    if not action or row < 0:
        raise argparse.ArgumentTypeError("action name and row must be valid")
    return action, row


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-atlas", type=Path, required=True)
    parser.add_argument("--frames-root", type=Path, required=True)
    parser.add_argument("--rows", type=int, required=True)
    parser.add_argument("--action", type=parse_action, action="append", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    if args.rows < 1:
        raise SystemExit("--rows must be positive")
    if any(row >= args.rows for _, row in args.action):
        raise SystemExit("every action row must be inside --rows")
    if len({row for _, row in args.action}) != len(args.action):
        raise SystemExit("each action must target a different row")

    with Image.open(args.base_atlas) as opened:
        base = opened.convert("RGBA")
    if base.width != COLUMNS * CELL_WIDTH or base.height % CELL_HEIGHT:
        raise SystemExit(f"unexpected base atlas size: {base.size}")
    if base.height > args.rows * CELL_HEIGHT:
        raise SystemExit("--rows cannot truncate the base atlas")

    atlas = Image.new(
        "RGBA", (COLUMNS * CELL_WIDTH, args.rows * CELL_HEIGHT), (0, 0, 0, 0)
    )
    atlas.alpha_composite(base)

    for action, row in args.action:
        for column in range(COLUMNS):
            frame_path = args.frames_root / action / "waving" / f"{column:02d}.png"
            if not frame_path.is_file():
                raise SystemExit(f"missing frame: {frame_path}")
            with Image.open(frame_path) as opened:
                frame = opened.convert("RGBA")
            if frame.size != (CELL_WIDTH, CELL_HEIGHT):
                raise SystemExit(f"unexpected frame size: {frame_path}: {frame.size}")
            atlas.alpha_composite(frame, (column * CELL_WIDTH, row * CELL_HEIGHT))

    args.output.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(args.output)
    print(f"wrote {args.output} ({atlas.width}x{atlas.height})")


if __name__ == "__main__":
    main()
