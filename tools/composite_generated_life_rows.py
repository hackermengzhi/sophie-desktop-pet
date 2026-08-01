#!/usr/bin/env python3
"""Composite generated Sophie/prop layers into deterministic 4-frame rows."""

from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


CELL_WIDTH = 192
CELL_HEIGHT = 208
FRAME_COUNT = 4
MAGENTA = (255, 0, 255)

ROBOT_CONTACT_BOXES = [
    (56, 108, 501, 652),
    (517, 117, 983, 658),
    (1029, 121, 1473, 658),
    (1527, 66, 1998, 660),
]
TOILET_CONTACT_BOXES = [
    (91, 75, 449, 601),
    (590, 76, 920, 601),
    (1121, 76, 1464, 601),
    (1667, 85, 2041, 601),
]
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
WINTER_COAT_BOXES = [
    (165, 107, 393, 725),
    (637, 107, 864, 725),
    (1109, 107, 1337, 725),
    (1581, 107, 1808, 725),
]
SINGING_BOXES = [
    (0, 0, 485, 810),
    (485, 0, 971, 810),
    (971, 0, 1456, 810),
    (1456, 0, 1942, 810),
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


def remove_tiny_components(
        sprite: Image.Image, minimum_pixels: int) -> Image.Image:
    """Remove chroma-edge crumbs without touching readable sprite effects."""
    cleaned = sprite.copy()
    alpha = cleaned.getchannel("A")
    pixels = alpha.load()
    visited: set[tuple[int, int]] = set()
    for y in range(alpha.height):
        for x in range(alpha.width):
            if pixels[x, y] == 0 or (x, y) in visited:
                continue
            component: list[tuple[int, int]] = []
            pending = [(x, y)]
            visited.add((x, y))
            while pending:
                current_x, current_y = pending.pop()
                component.append((current_x, current_y))
                for next_x, next_y in (
                    (current_x - 1, current_y),
                    (current_x + 1, current_y),
                    (current_x, current_y - 1),
                    (current_x, current_y + 1),
                ):
                    if not (0 <= next_x < alpha.width and
                            0 <= next_y < alpha.height):
                        continue
                    point = (next_x, next_y)
                    if pixels[next_x, next_y] == 0 or point in visited:
                        continue
                    visited.add(point)
                    pending.append(point)
            if len(component) < minimum_pixels:
                for component_x, component_y in component:
                    pixels[component_x, component_y] = 0
    cleaned.putalpha(alpha)
    return cleaned


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
    sheet = Image.open(root / "art/update-action/robot-contact.png")
    sprites = [extract(sheet, box) for box in ROBOT_CONTACT_BOXES]
    if len(sprites) != FRAME_COUNT:
        raise ValueError("Robot-contact source frame count is not four")
    row = Image.new("RGBA", (CELL_WIDTH * FRAME_COUNT, CELL_HEIGHT))
    for frame, sprite in enumerate(sprites):
        cell = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT))
        sprite = resize(sprite, 0.33)
        position = (
            round((CELL_WIDTH - sprite.width) / 2),
            CELL_HEIGHT - sprite.height - 6,
        )
        composite_checked(cell, sprite, position, "robot-contact scene")
        row.alpha_composite(cell, (frame * CELL_WIDTH, 0))
    return row


def build_winter_coat_row(root: Path) -> Image.Image:
    sheet = Image.open(root / "art/weather-actions/winter-coat.png")
    sprites = [extract(sheet, box) for box in WINTER_COAT_BOXES]
    if len(sprites) != FRAME_COUNT:
        raise ValueError("Winter-coat source frame count is not four")
    row = Image.new("RGBA", (CELL_WIDTH * FRAME_COUNT, CELL_HEIGHT))
    for frame, sprite in enumerate(sprites):
        cell = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT))
        alpha = sprite.getchannel("A")
        alpha.paste(
            0,
            (
                0,
                0,
                sprite.width,
                15,
            ),
        )
        sprite.putalpha(alpha)
        sprite = resize(sprite, 0.31)
        position = (
            round((CELL_WIDTH - sprite.width) / 2),
            CELL_HEIGHT - sprite.height - 6,
        )
        composite_checked(cell, sprite, position, "winter-coat Sophie")
        row.alpha_composite(cell, (frame * CELL_WIDTH, 0))
    return row


def build_toilet_row(root: Path) -> Image.Image:
    sheet = Image.open(root / "art/bathroom-actions/toilet-contact.png")
    sprites = [extract(sheet, box) for box in TOILET_CONTACT_BOXES]
    if len(sprites) != FRAME_COUNT:
        raise ValueError("Toilet-contact source frame count is not four")
    row = Image.new("RGBA", (CELL_WIDTH * FRAME_COUNT, CELL_HEIGHT))
    for frame, sprite in enumerate(sprites):
        cell = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT))
        sprite = resize(sprite, 0.36)
        position = (
            round((CELL_WIDTH - sprite.width) / 2),
            CELL_HEIGHT - sprite.height - 6,
        )
        composite_checked(cell, sprite, position, "toilet-contact scene")
        row.alpha_composite(cell, (frame * CELL_WIDTH, 0))
    return row


def build_singing_row(root: Path) -> Image.Image:
    sheet = Image.open(root / "art/singing-action/sophie-singing.png")
    sprites = [extract(sheet, box) for box in SINGING_BOXES]
    if len(sprites) != FRAME_COUNT:
        raise ValueError("Singing source frame count is not four")
    row = Image.new("RGBA", (CELL_WIDTH * FRAME_COUNT, CELL_HEIGHT))
    for frame, sprite in enumerate(sprites):
        cell = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT))
        sprite = resize(sprite, 0.30)
        sprite = remove_tiny_components(sprite, minimum_pixels=20)
        position = (
            round((CELL_WIDTH - sprite.width) / 2),
            CELL_HEIGHT - sprite.height - 6,
        )
        composite_checked(cell, sprite, position, "singing Sophie")
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
    base = Image.open(base_path).convert("RGBA")
    expected_base_size = (CELL_WIDTH * FRAME_COUNT, 16 * CELL_HEIGHT)
    if base.size != expected_base_size:
        raise ValueError(
            f"Base atlas has size {base.size}, expected {expected_base_size}"
        )
    preserved = base.copy()
    winter_coat_row = build_winter_coat_row(root)
    preserved.paste(winter_coat_row, (0, 10 * CELL_HEIGHT))
    if ImageChops.difference(
            base.crop((0, 0, base.width, 10 * CELL_HEIGHT)),
            preserved.crop((0, 0, base.width, 10 * CELL_HEIGHT)),
    ).getbbox() is not None:
        raise ValueError("Rows 0-9 changed while replacing winter coat")
    if ImageChops.difference(
            base.crop((0, 11 * CELL_HEIGHT, base.width, base.height)),
            preserved.crop((0, 11 * CELL_HEIGHT, base.width, base.height)),
    ).getbbox() is not None:
        raise ValueError("Rows 11-15 changed while replacing winter coat")
    mechanic_row = build_mechanic_row(root)
    toilet_row = build_toilet_row(root)
    urgent_row = build_urgent_bush_row(root)
    singing_row = build_singing_row(root)

    output = Image.new(
        "RGBA", (CELL_WIDTH * FRAME_COUNT, 20 * CELL_HEIGHT)
    )
    output.alpha_composite(preserved)
    output.alpha_composite(mechanic_row, (0, 16 * CELL_HEIGHT))
    output.alpha_composite(toilet_row, (0, 17 * CELL_HEIGHT))
    output.alpha_composite(urgent_row, (0, 18 * CELL_HEIGHT))
    output.alpha_composite(singing_row, (0, 19 * CELL_HEIGHT))

    update_path = root / "art/update-action/update-research.png"
    toilet_path = root / "art/bathroom-actions/toilet-break.png"
    urgent_path = root / "art/bathroom-actions/urgent-bush.png"
    winter_coat_path = root / "art/weather-actions/winter-coat-row.png"
    singing_path = root / "art/singing-action/singing.png"
    save_png_atomic(mechanic_row, update_path)
    save_png_atomic(toilet_row, toilet_path)
    save_png_atomic(urgent_row, urgent_path)
    save_png_atomic(winter_coat_row, winter_coat_path)
    save_png_atomic(singing_row, singing_path)
    save_preview(mechanic_row, update_path.with_name(
        "update-research-preview.png"
    ))
    save_preview(toilet_row, toilet_path.with_name(
        "toilet-break-preview.png"
    ))
    save_preview(urgent_row, urgent_path.with_name(
        "urgent-bush-preview.png"
    ))
    save_preview(winter_coat_row, winter_coat_path.with_name(
        "winter-coat-preview.png"
    ))
    save_preview(singing_row, singing_path.with_name(
        "singing-preview.png"
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
