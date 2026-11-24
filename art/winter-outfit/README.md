# Winter outfit artwork

`sophie-winter-source.png` is the generated winter wardrobe source sheet:
ivory puffer jacket, dark scarf, charcoal trousers and warm boots, retaining
Sophie's black side-parted hair, low ponytail and camera. This revision omits
the hat and glasses to keep her face visible and closer to the original.
`sophie-winter-preview.png` shows the first idle frame.

The initial source uses an 8-column, 11-row Codex companion layout and is kept
as a design reference. The application instead loads
`SophieDesktopPet/Assets/sophie-life-winter.png` by default.

`winter-life-part-1.png` through `winter-life-part-3.png` adapt the winter outfit
to the application's complete life-action layout, including its interaction
props. Each part contains four columns and seven rows. Rebuild the runtime atlas
with `python3 tools/build_winter_life_atlas.py` (Pillow required).
The builder crops measured source row bands, scales each pose without stretching
to the original frame's height, aligns it to the original baseline and horizontal
center inside its 192 by 208 cell, removes negligible-alpha key-colour speckles,
and leaves reserved row 13 transparent. The resulting
768 by 4368 atlas preserves all existing animation row and frame mappings.
The original summer atlas remains available as `Assets/sophie-life.png`.

Generated with the built-in image tool from the original Sophie companion
sheet. The supplied personal photos are not included in this repository.
The character artwork is personal project material and requires the owner's
separate permission for reuse, as described in `NOTICE.md`.
