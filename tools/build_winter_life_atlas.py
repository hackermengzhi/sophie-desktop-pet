#!/usr/bin/env python3
"""Assemble the three seven-row winter sheets into the runtime life atlas."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "art/winter-outfit"
CELL = (192, 208)
COLUMNS = 4
ROWS_PER_PART = 7
RESERVED_ROWS = {13}
# Image generation preserves row order, but not equally spaced source bands.
# These measured alpha bands keep complete poses together before normalization.
SOURCE_BANDS = (
    ((6, 242), (261, 467), (485, 696), (716, 886),
     (909, 1082), (1109, 1229), (1247, 1468)),
    ((59, 244), (258, 497), (508, 744), (764, 1004),
     (1009, 1256), (1265, 1511), None),
    ((6, 241), (255, 492), (508, 739), (760, 991),
     (1066, 1239), (1257, 1485), (1498, 1727)),
)


def main() -> None:
    atlas = Image.new("RGBA", (768, 4368))
    with Image.open(ROOT / "SophieDesktopPet/Assets/sophie-life.png") as opened:
        original = opened.convert("RGBA")
    for part in range(3):
        with Image.open(ART / f"winter-life-part-{part + 1}.png") as opened:
            sheet = opened.convert("RGBA")
        if sheet.getchannel("A").getextrema() != (0, 255):
            raise SystemExit(f"Part {part + 1} must have a transparent background")
        # Generated alpha can contain nearly invisible key-colour speckles.
        sheet.putalpha(sheet.getchannel("A").point(lambda alpha: 0 if alpha < 8 else alpha))
        for local_row in range(ROWS_PER_PART):
            row = part * ROWS_PER_PART + local_row
            if row in RESERVED_ROWS:
                continue
            band = SOURCE_BANDS[part][local_row]
            if band is None or not 0 <= band[0] < band[1] <= sheet.height:
                raise SystemExit(f"Invalid source band for row {row}")
            for column in range(COLUMNS):
                box = (
                    round(column * sheet.width / COLUMNS),
                    band[0],
                    round((column + 1) * sheet.width / COLUMNS),
                    band[1],
                )
                frame = sheet.crop(box)
                bounds = frame.getchannel("A").getbbox()
                if bounds is None:
                    raise SystemExit(f"Missing winter frame at row {row}, column {column}")
                target = original.crop((column * CELL[0], row * CELL[1],
                                        (column + 1) * CELL[0], (row + 1) * CELL[1]))
                target_bounds = target.getchannel("A").point(
                    lambda alpha: 255 if alpha >= 8 else 0).getbbox()
                if target_bounds is None:
                    raise SystemExit(f"Missing original frame at row {row}, column {column}")
                sprite = frame.crop(bounds)
                scale = min((target_bounds[3] - target_bounds[1]) / sprite.height,
                            (CELL[0] - 4) / sprite.width)
                sprite = sprite.resize((max(1, round(sprite.width * scale)),
                                        max(1, round(sprite.height * scale))),
                                       Image.Resampling.LANCZOS)
                center = (target_bounds[0] + target_bounds[2]) / 2
                left = max(0, min(CELL[0] - sprite.width, round(center - sprite.width / 2)))
                top = target_bounds[3] - sprite.height
                atlas.alpha_composite(sprite, (column * CELL[0] + left, row * CELL[1] + top))
    output = ROOT / "SophieDesktopPet/Assets/sophie-life-winter.png"
    atlas.save(output)
    print(f"Wrote {output.relative_to(ROOT)} ({atlas.width}x{atlas.height})")


if __name__ == "__main__":
    main()
