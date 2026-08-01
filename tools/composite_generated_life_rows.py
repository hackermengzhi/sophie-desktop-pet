#!/usr/bin/env python3
"""Composite generated Sophie/prop layers into deterministic 4-frame rows."""

from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


CELL_WIDTH = 192
CELL_HEIGHT = 208
FRAME_COUNT = 4
MAGENTA = (255, 0, 255)

MECHANIC_PROP_BOXES = [
    (33, 141, 473, 712),
    (512, 141, 942, 712),
    (984, 125, 1415, 712),
    (1472, 82, 1919, 713),
]
MECHANIC_SOPHIE_BOXES = [
    (114, 217, 384, 729),
    (519, 234, 788, 731),
    (933, 250, 1206, 731),
    (1352, 204, 1609, 731),
]
SEATED_SOPHIE_BOXES = [
    (129, 81, 431, 639),
    (665, 81, 934, 639),
    (1199, 81, 1481, 639),
    (1757, 87, 2041, 639),
]
TOILET_REAR_BOX = (172, 109, 662, 795)
TOILET_FRONT_BOX = (1085, 396, 1527, 795)
URGENT_SOPHIE_BOXES = [
    (137, 116, 361, 630),
    (597, 112, 907, 630),
    (1094, 196, 1386, 615),
    (1557, 196, 1848, 615),
]
BUSH_BOXES = [
    (122, 160, 684, 730),
    (947, 161, 1621, 732),
]


def chroma_key(image: Image.Image) -> Image.Image:
    """Remove generated near-magenta by hue, including its soft halo."""
    source = image.convert("RGBA")
    output = []
    pixels = (source.get_flattened_data() if
              hasattr(source, "get_flattened_data") else source.getdata())
    for r, g, b, source_alpha in pixels:
        is_magenta = (
            r >= 150 and b >= 145 and g <= 145
            and r - g >= 55 and b - g >= 50
        )
        if is_magenta:
            alpha = 0
        else:
            alpha = source_alpha
        output.append((r, g, b, alpha))
    keyed = Image.new("RGBA", source.size)
    keyed.putdata(output)
    return keyed


def extract(sheet: Image.Image, box: tuple[int, int, int, int]) -> Image.Image:
    left, top, right, bottom = box
    if not (0 <= left < right <= sheet.width and
            0 <= top < bottom <= sheet.height):
        raise ValueError(f"Crop {box} outside sheet size {sheet.size}")
    sprite = chroma_key(sheet.crop(box))
    alpha_box = sprite.getchannel("A").point(
        lambda value: 255 if value >= 8 else 0
    ).getbbox()
    if alpha_box is None:
        raise ValueError(f"No sprite pixels found in {box}")
    return sprite.crop(alpha_box)


def resize(sprite: Image.Image, scale: float) -> Image.Image:
    return sprite.resize(
        (max(1, round(sprite.width * scale)),
         max(1, round(sprite.height * scale))),
        Image.Resampling.NEAREST,
    )


def composite_checked(cell: Image.Image, sprite: Image.Image,
                      position: tuple[int, int], label: str) -> None:
    x, y = position
    if x < 0 or y < 0 or x + sprite.width > cell.width \
            or y + sprite.height > cell.height:
        raise ValueError(
            f"{label} clips cell: pos={position} size={sprite.size}"
        )
    cell.alpha_composite(sprite, position)


def build_mechanic_row(root: Path) -> Image.Image:
    prop_sheet = Image.open(
        root / "art/update-action/mechanic-props-layer.png"
    )
    sophie_sheet = Image.open(
        root / "art/update-action/sophie-mechanic-layer.png"
    )
    props = [extract(prop_sheet, box) for box in MECHANIC_PROP_BOXES]
    sophies = [extract(sophie_sheet, box) for box in MECHANIC_SOPHIE_BOXES]
    if len(props) != FRAME_COUNT or len(sophies) != FRAME_COUNT:
        raise ValueError("Mechanic source frame count is not four")
    row = Image.new("RGBA", (CELL_WIDTH * FRAME_COUNT, CELL_HEIGHT))

    for frame, (prop, sophie) in enumerate(zip(props, sophies)):
        cell = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT))
        prop = resize(prop, 0.32)
        sophie = resize(sophie, 0.28)
        prop_position = (CELL_WIDTH - prop.width - 2,
                         CELL_HEIGHT - prop.height - 5)
        sophie_position = (3, CELL_HEIGHT - sophie.height - 7)
        composite_checked(cell, prop, prop_position, "mechanic props")
        composite_checked(cell, sophie, sophie_position, "mechanic Sophie")
        row.alpha_composite(cell, (frame * CELL_WIDTH, 0))
    return row


def upper_toilet_foreground(sprite: Image.Image) -> Image.Image:
    """Keep only the near-facing seat arc that should cover Sophie's hips."""
    top = round(sprite.height * 0.32)
    bottom = round(sprite.height * 0.46)
    result = sprite.crop((0, top, sprite.width, bottom))
    visible = result.getchannel("A").getbbox()
    if visible is None:
        raise ValueError("Toilet foreground mask is empty")
    return result.crop(visible)


def build_toilet_row(root: Path) -> Image.Image:
    toilet_sheet = Image.open(
        root / "art/bathroom-actions/toilet-props-layer.png"
    )
    sophie_sheet = Image.open(
        root / "art/bathroom-actions/sophie-toilet-pose-layer.png"
    )
    rear = resize(extract(toilet_sheet, TOILET_REAR_BOX), 0.165)
    front = resize(
        upper_toilet_foreground(extract(toilet_sheet, TOILET_FRONT_BOX)),
        0.165,
    )
    sophies = [extract(sophie_sheet, box) for box in SEATED_SOPHIE_BOXES]
    row = Image.new("RGBA", (CELL_WIDTH * FRAME_COUNT, CELL_HEIGHT))

    # All three layers share the same perceived seat center near (108, 136).
    rear_position = (57, CELL_HEIGHT - rear.height - 7)
    front_position = (69, 157)
    for frame, sophie in enumerate(sophies):
        cell = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT))
        sophie = resize(sophie, 0.22)
        sophie_position = (
            round(108 - sophie.width * 0.50),
            CELL_HEIGHT - sophie.height - 7,
        )
        composite_checked(cell, rear, rear_position, "toilet rear")
        composite_checked(cell, sophie, sophie_position, "seated Sophie")
        composite_checked(cell, front, front_position, "toilet foreground")
        row.alpha_composite(cell, (frame * CELL_WIDTH, 0))
    return row


def build_urgent_bush_row(root: Path) -> Image.Image:
    sophie_sheet = Image.open(
        root / "art/bathroom-actions/sophie-urgent-layer.png"
    )
    bush_sheet = Image.open(root / "art/bathroom-actions/bush-layer.png")
    sophies = [extract(sophie_sheet, box) for box in URGENT_SOPHIE_BOXES]
    bushes = [resize(extract(bush_sheet, box), 0.16) for box in BUSH_BOXES]
    if len(sophies) != FRAME_COUNT or len(bushes) != 2:
        raise ValueError("Urgent-life source frame count is incorrect")
    row = Image.new("RGBA", (CELL_WIDTH * FRAME_COUNT, CELL_HEIGHT))

    for frame, sophie in enumerate(sophies):
        cell = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT))
        sophie = resize(sophie, 0.27)
        sophie_position = (
            round((CELL_WIDTH - sophie.width) / 2),
            CELL_HEIGHT - sophie.height - 7,
        )
        composite_checked(cell, sophie, sophie_position, "urgent Sophie")
        if frame >= 2:
            bush = bushes[frame - 2]
            bush_position = (
                round((CELL_WIDTH - bush.width) / 2),
                CELL_HEIGHT - bush.height - 7,
            )
            composite_checked(cell, bush, bush_position, "privacy bush")
            paper_layer = sophie.copy()
            paper_mask = Image.new("L", sophie.size)
            mask_draw = ImageDraw.Draw(paper_mask)
            mask_draw.polygon(
                [
                    (round(sophie.width * 0.79),
                     round(sophie.height * 0.29)),
                    (sophie.width, round(sophie.height * 0.29)),
                    (sophie.width, round(sophie.height * 0.72)),
                    (round(sophie.width * 0.59),
                     round(sophie.height * 0.72)),
                    (round(sophie.width * 0.59),
                     round(sophie.height * 0.52)),
                    (round(sophie.width * 0.73),
                     round(sophie.height * 0.42)),
                ],
                fill=255,
            )
            paper_layer.putalpha(ImageChops.multiply(
                sophie.getchannel("A"), paper_mask
            ))
            composite_checked(
                cell, paper_layer, sophie_position, "toilet paper foreground"
            )
        row.alpha_composite(cell, (frame * CELL_WIDTH, 0))
    return row


def save_png_atomic(image: Image.Image, path: Path) -> None:
    temporary = path.with_name(path.name + ".tmp")
    temporary.unlink(missing_ok=True)
    image.save(temporary, format="PNG")
    temporary.replace(path)


def save_preview(row: Image.Image, path: Path, scale: int = 3) -> None:
    preview = Image.new(
        "RGBA", (row.width * scale, row.height * scale), (28, 24, 35, 255)
    )
    preview.alpha_composite(
        row.resize(preview.size, Image.Resampling.NEAREST)
    )
    save_png_atomic(preview.convert("RGB"), path)


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    atlas_path = root / "SophieDesktopPet/Assets/sophie-life.png"
    base_path = root / "art/base/sophie-life-base-16rows.png"
    preserved = Image.open(base_path).convert("RGBA")
    expected_base_size = (CELL_WIDTH * FRAME_COUNT, 16 * CELL_HEIGHT)
    if preserved.size != expected_base_size:
        raise ValueError(
            f"Base atlas has size {preserved.size}, expected {expected_base_size}"
        )
    mechanic_row = build_mechanic_row(root)
    toilet_row = build_toilet_row(root)
    urgent_row = build_urgent_bush_row(root)

    output = Image.new(
        "RGBA", (CELL_WIDTH * FRAME_COUNT, 19 * CELL_HEIGHT)
    )
    output.alpha_composite(preserved)
    output.alpha_composite(mechanic_row, (0, 16 * CELL_HEIGHT))
    output.alpha_composite(toilet_row, (0, 17 * CELL_HEIGHT))
    output.alpha_composite(urgent_row, (0, 18 * CELL_HEIGHT))

    update_path = root / "art/update-action/update-research.png"
    toilet_path = root / "art/bathroom-actions/toilet-break.png"
    urgent_path = root / "art/bathroom-actions/urgent-bush.png"
    save_png_atomic(mechanic_row, update_path)
    save_png_atomic(toilet_row, toilet_path)
    save_png_atomic(urgent_row, urgent_path)
    save_preview(mechanic_row, update_path.with_name(
        "update-research-preview.png"
    ))
    save_preview(toilet_row, toilet_path.with_name(
        "toilet-break-preview.png"
    ))
    save_preview(urgent_row, urgent_path.with_name(
        "urgent-bush-preview.png"
    ))
    atlas_temporary = atlas_path.with_name(atlas_path.name + ".tmp")
    atlas_temporary.unlink(missing_ok=True)
    output.save(atlas_temporary, format="PNG")
    reopened = Image.open(atlas_temporary).convert("RGBA")
    kept_after_save = reopened.crop((0, 0, preserved.width, preserved.height))
    if ImageChops.difference(preserved, kept_after_save).getbbox() is not None:
        raise ValueError("Atlas rows 0-15 changed during composition")
    atlas_temporary.replace(atlas_path)


if __name__ == "__main__":
    main()
