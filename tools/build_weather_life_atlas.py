#!/usr/bin/env python3
"""Append Sophie weather-action frames to the four-column life atlas."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image

CELL_WIDTH = 192
CELL_HEIGHT = 208
COLUMNS = 4
WEATHER_ROWS = (
    ("rain", 8),
    ("snowman", 9),
    ("shiver", 10),
    ("sweat", 11),
)


def frame_path(frames_root: Path, action: str, column: int) -> Path:
    return frames_root / action / "waving" / f"{column:02d}.png"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-atlas", type=Path, required=True)
    parser.add_argument("--frames-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    with Image.open(args.base_atlas) as source:
        base = source.convert("RGBA")

    expected_base_size = (COLUMNS * CELL_WIDTH, 8 * CELL_HEIGHT)
    if base.size != expected_base_size:
        raise SystemExit(
            f"expected an 8-row {expected_base_size} base atlas, got {base.size}"
        )

    atlas = Image.new(
        "RGBA", (COLUMNS * CELL_WIDTH, 12 * CELL_HEIGHT), (0, 0, 0, 0)
    )
    atlas.alpha_composite(base)

    for action, row in WEATHER_ROWS:
        for column in range(COLUMNS):
            path = frame_path(args.frames_root, action, column)
            if not path.is_file():
                raise SystemExit(f"missing weather frame: {path}")
            with Image.open(path) as opened:
                frame = opened.convert("RGBA")
            if frame.size != (CELL_WIDTH, CELL_HEIGHT):
                raise SystemExit(
                    f"unexpected size for {path}: {frame.size}; "
                    f"expected {(CELL_WIDTH, CELL_HEIGHT)}"
                )
            atlas.alpha_composite(frame, (column * CELL_WIDTH, row * CELL_HEIGHT))

    args.output.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(args.output)
    print(f"wrote {args.output} ({atlas.width}x{atlas.height})")


if __name__ == "__main__":
    main()
