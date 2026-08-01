#!/usr/bin/env python3
"""Build deterministic update and private-life rows from Sophie's source art."""

from pathlib import Path

from PIL import Image, ImageDraw


CELL_WIDTH = 192
CELL_HEIGHT = 208
FRAME_COUNT = 4


def source_cell(atlas: Image.Image, row: int, frame: int) -> Image.Image:
    return atlas.crop((frame * CELL_WIDTH, row * CELL_HEIGHT,
                       (frame + 1) * CELL_WIDTH, (row + 1) * CELL_HEIGHT))


def pixel_rect(draw: ImageDraw.ImageDraw, box, fill) -> None:
    draw.rectangle(box, fill=fill)


def draw_table_and_robot(cell: Image.Image, frame: int) -> None:
    draw = ImageDraw.Draw(cell, "RGBA")
    dark = (48, 33, 42, 255)
    wood = (124, 75, 48, 255)
    wood_light = (187, 116, 64, 255)
    metal = (90, 113, 122, 255)
    metal_light = (151, 180, 181, 255)
    cyan = (78, 226, 211, 255)
    amber = (255, 181, 62, 255)

    # A compact workbench hides the original seated pose below the waist.
    pixel_rect(draw, (61, 142, 159, 152), dark)
    pixel_rect(draw, (64, 144, 156, 149), wood_light)
    pixel_rect(draw, (70, 150, 78, 192), dark)
    pixel_rect(draw, (73, 152, 76, 190), wood)
    pixel_rect(draw, (145, 150, 153, 192), dark)
    pixel_rect(draw, (147, 152, 150, 190), wood)

    # The robot stays on the table; its panel and eye lights change by frame.
    rx = 130 + (1 if frame == 2 else 0)
    pixel_rect(draw, (rx - 12, 113, rx + 12, 140), dark)
    pixel_rect(draw, (rx - 9, 116, rx + 9, 137), metal)
    pixel_rect(draw, (rx - 7, 107, rx + 7, 115), dark)
    pixel_rect(draw, (rx - 1, 101, rx + 1, 109), dark)
    pixel_rect(draw, (rx - 3, 99, rx + 3, 103), metal_light)
    pixel_rect(draw, (rx - 6, 120, rx - 3, 123), dark)
    pixel_rect(draw, (rx + 3, 120, rx + 6, 123), dark)
    eye = cyan if frame == 3 else amber
    pixel_rect(draw, (rx - 5, 120, rx - 4, 122), eye)
    pixel_rect(draw, (rx + 4, 120, rx + 5, 122), eye)
    pixel_rect(draw, (rx - 6, 127, rx + 6, 134), dark)
    if frame == 1:
        pixel_rect(draw, (rx - 4, 129, rx + 4, 132), (43, 58, 63, 255))
        pixel_rect(draw, (rx - 2, 128, rx, 134), amber)
    elif frame == 2:
        pixel_rect(draw, (rx - 4, 129, rx + 4, 132), metal_light)
        pixel_rect(draw, (rx + 1, 128, rx + 3, 134), cyan)
    else:
        pixel_rect(draw, (rx - 3, 130, rx + 3, 132), eye)
    pixel_rect(draw, (rx - 17, 122, rx - 12, 126), dark)
    pixel_rect(draw, (rx + 12, 122, rx + 17, 126), dark)
    pixel_rect(draw, (rx - 8, 139, rx - 4, 144), dark)
    pixel_rect(draw, (rx + 4, 139, rx + 8, 144), dark)

    # A screwdriver stays connected to Sophie's forward hand and robot panel.
    hand = (98 + frame % 2, 130 - (frame == 2))
    tip = (rx - 7 + frame, 130 + frame % 2)
    draw.line((hand, tip), fill=dark, width=5)
    draw.line((hand, tip), fill=(207, 137, 55, 255), width=2)
    pixel_rect(draw, (hand[0] - 3, hand[1] - 2,
                      hand[0] + 3, hand[1] + 2), (232, 153, 104, 255))


def build_update_row(atlas: Image.Image) -> Image.Image:
    row = Image.new("RGBA", (CELL_WIDTH * FRAME_COUNT, CELL_HEIGHT))
    for frame in range(FRAME_COUNT):
        cell = source_cell(atlas, 3, frame)
        draw_table_and_robot(cell, frame)
        row.alpha_composite(cell, (frame * CELL_WIDTH, 0))
    return row


def draw_toilet_background() -> Image.Image:
    layer = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT))
    draw = ImageDraw.Draw(layer)
    outline = (68, 70, 83, 255)
    ceramic = (231, 239, 239, 255)
    shade = (172, 204, 211, 255)
    pixel_rect(draw, (104, 108, 141, 151), outline)
    pixel_rect(draw, (108, 112, 137, 147), ceramic)
    pixel_rect(draw, (111, 116, 134, 120), shade)
    pixel_rect(draw, (133, 125, 138, 131), outline)
    return layer


def draw_toilet_front(cell: Image.Image, frame: int) -> None:
    draw = ImageDraw.Draw(cell, "RGBA")
    outline = (68, 70, 83, 255)
    ceramic = (235, 243, 242, 255)
    shade = (169, 202, 210, 255)
    paper = (250, 245, 224, 255)
    pixel_rect(draw, (72, 148, 142, 162), outline)
    pixel_rect(draw, (77, 151, 137, 157), ceramic)
    pixel_rect(draw, (87, 159, 138, 180), outline)
    pixel_rect(draw, (91, 162, 134, 176), ceramic)
    pixel_rect(draw, (101, 176, 133, 191), outline)
    pixel_rect(draw, (105, 177, 129, 187), ceramic)
    pixel_rect(draw, (110, 183, 129, 188), shade)

    px = 72 + (frame % 2) * 2
    py = 126 - (frame == 2) * 2
    pixel_rect(draw, (px - 8, py - 8, px + 8, py + 8), outline)
    pixel_rect(draw, (px - 6, py - 6, px + 6, py + 6), paper)
    pixel_rect(draw, (px - 2, py - 2, px + 2, py + 2), shade)
    tail = 10 + frame * 3
    pixel_rect(draw, (px - 5, py + 7, px + 2, py + tail), outline)
    pixel_rect(draw, (px - 3, py + 7, px, py + tail - 1), paper)
    pixel_rect(draw, (px + 6, py - 3, px + 10, py + 3),
               (232, 153, 104, 255))


def build_toilet_row(atlas: Image.Image) -> Image.Image:
    row = Image.new("RGBA", (CELL_WIDTH * FRAME_COUNT, CELL_HEIGHT))
    for frame in range(FRAME_COUNT):
        cell = draw_toilet_background()
        cell.alpha_composite(source_cell(atlas, 3, frame))
        draw_toilet_front(cell, frame)
        row.alpha_composite(cell, (frame * CELL_WIDTH, 0))
    return row


def add_blush(cell: Image.Image, stronger: bool) -> None:
    draw = ImageDraw.Draw(cell, "RGBA")
    color = (255, 104, 116, 225 if stronger else 170)
    pixel_rect(draw, (78, 74, 82, 76), color)
    pixel_rect(draw, (110, 74, 114, 76), color)
    if stronger:
        pixel_rect(draw, (79, 79, 81, 80), color)
        pixel_rect(draw, (111, 79, 113, 80), color)


def draw_bush_front(cell: Image.Image, frame: int) -> None:
    draw = ImageDraw.Draw(cell, "RGBA")
    outline = (35, 56, 39, 255)
    dark = (45, 104, 62, 255)
    leaf = (75, 148, 78, 255)
    light = (124, 181, 83, 255)
    top = 132 if frame == 2 else 124
    clusters = [(52, top + 12), (70, top + 2), (89, top + 10),
                (108, top), (128, top + 10), (145, top + 16)]
    for index, (x, y) in enumerate(clusters):
        pixel_rect(draw, (x - 14, y - 10, x + 14, y + 18), outline)
        pixel_rect(draw, (x - 11, y - 7, x + 11, y + 15),
                   leaf if index % 2 else dark)
        pixel_rect(draw, (x - 7, y - 7, x - 2, y - 2), light)
    lower_clusters = [(55, 174), (76, 181), (99, 172),
                      (121, 180), (143, 174)]
    for index, (x, y) in enumerate(lower_clusters):
        pixel_rect(draw, (x - 16, y - 17, x + 16, y + 15), outline)
        pixel_rect(draw, (x - 13, y - 14, x + 13, y + 12),
                   dark if index % 2 else leaf)
        pixel_rect(draw, (x - 7, y - 10, x - 2, y - 5), light)

    px, py = (132, 125) if frame == 2 else (128, 116)
    paper = (250, 245, 224, 255)
    shade = (169, 202, 210, 255)
    pixel_rect(draw, (px - 8, py - 8, px + 8, py + 8), outline)
    pixel_rect(draw, (px - 6, py - 6, px + 6, py + 6), paper)
    pixel_rect(draw, (px - 2, py - 2, px + 2, py + 2), shade)
    pixel_rect(draw, (px - 3, py + 7, px + 2, py + 18), paper)
    pixel_rect(draw, (px + 6, py - 3, px + 10, py + 3),
               (232, 153, 104, 255))


def build_urgent_bush_row(atlas: Image.Image) -> Image.Image:
    row = Image.new("RGBA", (CELL_WIDTH * FRAME_COUNT, CELL_HEIGHT))
    first = source_cell(atlas, 0, 0)
    add_blush(first, stronger=False)
    row.alpha_composite(first, (0, 0))

    second = source_cell(atlas, 11, 1)
    add_blush(second, stronger=True)
    row.alpha_composite(second, (CELL_WIDTH, 0))

    for frame in (2, 3):
        source_frame = 0 if frame == 2 else 3
        cell = source_cell(atlas, 3, source_frame)
        add_blush(cell, stronger=True)
        draw_bush_front(cell, frame)
        row.alpha_composite(cell, (frame * CELL_WIDTH, 0))
    return row


def save_preview(row: Image.Image, path: Path) -> None:
    preview = Image.new("RGBA", (row.width * 2, row.height * 2),
                        (28, 24, 35, 255))
    preview.alpha_composite(row.resize(preview.size, Image.Resampling.NEAREST))
    preview.save(path)


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    atlas_path = root / "SophieDesktopPet/Assets/sophie-life.png"
    atlas = Image.open(atlas_path).convert("RGBA")
    original = atlas.crop((0, 0, atlas.width, 16 * CELL_HEIGHT))

    rows = [
        (16, build_update_row(original),
         root / "art/update-action/update-research.png"),
        (17, build_toilet_row(original),
         root / "art/bathroom-actions/toilet-break.png"),
        (18, build_urgent_bush_row(original),
         root / "art/bathroom-actions/urgent-bush.png"),
    ]
    output = Image.new("RGBA", (atlas.width, 19 * CELL_HEIGHT))
    output.alpha_composite(original)
    for target_row, row, row_path in rows:
        row_path.parent.mkdir(parents=True, exist_ok=True)
        row.save(row_path)
        save_preview(row, row_path.with_name(row_path.stem + "-preview.png"))
        output.alpha_composite(row, (0, target_row * CELL_HEIGHT))
    output.save(atlas_path)


if __name__ == "__main__":
    main()
