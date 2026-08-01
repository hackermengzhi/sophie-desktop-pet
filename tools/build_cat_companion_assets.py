#!/usr/bin/env python3
"""Build the deterministic cat companion atlas and Sophie interaction row."""

from __future__ import annotations

from pathlib import Path

from PIL import Image


CELL_WIDTH = 192
CELL_HEIGHT = 208
FRAME_COUNT = 4
ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "art" / "cat-action"
ASSETS = ROOT / "SophieDesktopPet" / "Assets"


def remove_magenta(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            red, green, blue, _ = pixels[x, y]
            if red >= 190 and blue >= 170 and green <= 105:
                pixels[x, y] = (red, green, blue, 0)
                continue

            # Despill the narrow antialiased fringe without touching warm fur.
            magenta = min(red, blue) - green
            if magenta > 80 and red > 150 and blue > 130:
                alpha = max(0, 255 - (magenta - 80) * 4)
                pixels[x, y] = (red, green, blue, alpha)
    return rgba


def split_frames(source_path: Path) -> list[Image.Image]:
    source = remove_magenta(Image.open(source_path))
    frames: list[Image.Image] = []
    for index in range(FRAME_COUNT):
        left = round(index * source.width / FRAME_COUNT)
        right = round((index + 1) * source.width / FRAME_COUNT)
        lane = source.crop((left, 0, right, source.height))
        alpha_box = lane.getchannel("A").getbbox()
        if alpha_box is None:
            raise ValueError(f"No visible pixels in {source_path.name} frame {index}")
        frames.append(lane.crop(alpha_box))
    return frames


def place_frame(
    sprite: Image.Image,
    *,
    max_width: int,
    max_height: int,
    baseline: int,
) -> Image.Image:
    scale = min(max_width / sprite.width, max_height / sprite.height)
    size = (
        max(1, round(sprite.width * scale)),
        max(1, round(sprite.height * scale)),
    )
    sprite = sprite.resize(size, Image.Resampling.NEAREST)
    cell = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT), (0, 0, 0, 0))
    x = (CELL_WIDTH - sprite.width) // 2
    y = baseline - sprite.height
    if x < 0 or y < 0 or x + sprite.width > CELL_WIDTH:
        raise ValueError(f"Sprite does not fit cell: {sprite.size} at {(x, y)}")
    cell.alpha_composite(sprite, (x, y))
    return cell


def build_row(
    source_name: str,
    *,
    max_width: int,
    max_height: int,
    baseline: int,
) -> Image.Image:
    row = Image.new(
        "RGBA",
        (CELL_WIDTH * FRAME_COUNT, CELL_HEIGHT),
        (0, 0, 0, 0),
    )
    for index, sprite in enumerate(split_frames(ART / source_name)):
        cell = place_frame(
            sprite,
            max_width=max_width,
            max_height=max_height,
            baseline=baseline,
        )
        row.alpha_composite(cell, (index * CELL_WIDTH, 0))
    return row


def validate_cells(
    image: Image.Image,
    rows: int,
    label: str,
    *,
    allowed_empty_rows: frozenset[int] = frozenset(),
) -> None:
    expected = (CELL_WIDTH * FRAME_COUNT, CELL_HEIGHT * rows)
    if image.size != expected:
        raise ValueError(f"{label} size {image.size}, expected {expected}")
    for row in range(rows):
        for column in range(FRAME_COUNT):
            cell = image.crop(
                (
                    column * CELL_WIDTH,
                    row * CELL_HEIGHT,
                    (column + 1) * CELL_WIDTH,
                    (row + 1) * CELL_HEIGHT,
                )
            )
            if (
                cell.getchannel("A").getbbox() is None
                and row not in allowed_empty_rows
            ):
                raise ValueError(f"{label} has empty cell {row},{column}")


def main() -> None:
    idle = build_row(
        "sophie-cat-idle.png", max_width=124, max_height=158, baseline=196
    )
    walk_right = build_row(
        "sophie-cat-walk.png", max_width=148, max_height=130, baseline=196
    )
    walk_left = walk_right.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    sleep = build_row(
        "sophie-cat-sleep.png", max_width=150, max_height=122, baseline=196
    )

    cat_atlas = Image.new(
        "RGBA", (CELL_WIDTH * FRAME_COUNT, CELL_HEIGHT * 4), (0, 0, 0, 0)
    )
    for row_index, row in enumerate((idle, walk_right, walk_left, sleep)):
        cat_atlas.alpha_composite(row, (0, row_index * CELL_HEIGHT))
    validate_cells(cat_atlas, 4, "cat atlas")
    cat_atlas.save(ASSETS / "sophie-cat-life.png")

    interaction = build_row(
        "sophie-cat-nuzzle.png", max_width=184, max_height=196, baseline=204
    )
    validate_cells(interaction, 1, "cat interaction")
    interaction.save(ART / "cat-nuzzle-row.png")

    life_path = ASSETS / "sophie-life.png"
    life = Image.open(life_path).convert("RGBA")
    if life.width != CELL_WIDTH * FRAME_COUNT or life.height < CELL_HEIGHT * 20:
        raise ValueError(f"Unexpected Sophie life atlas size: {life.size}")
    original_rows = life.crop((0, 0, life.width, CELL_HEIGHT * 20))
    combined = Image.new(
        "RGBA", (life.width, CELL_HEIGHT * 21), (0, 0, 0, 0)
    )
    combined.alpha_composite(original_rows, (0, 0))
    combined.alpha_composite(interaction, (0, CELL_HEIGHT * 20))
    # Row 13 is intentionally reserved in the pre-existing atlas.
    validate_cells(
        combined,
        21,
        "Sophie life atlas",
        allowed_empty_rows=frozenset({13}),
    )
    combined.save(life_path)

    print(f"Wrote {ASSETS / 'sophie-cat-life.png'}")
    print(f"Wrote {ART / 'cat-nuzzle-row.png'}")
    print(f"Updated {life_path} with interaction row 20")


if __name__ == "__main__":
    main()
