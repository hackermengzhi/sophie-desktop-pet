# Private life animations

These two four-frame rows are intentionally cute and non-graphic. No waste is
shown.

- `toilet-break.png`: Sophie sits on a pixel toilet and holds a paper roll.
- `urgent-bush.png`: Sophie blushes, becomes visibly worried, finds a leafy
  bush, and crouches behind it while holding paper.

`toilet-contact.png` is the approved combined toilet source. It was composed
from the separately generated seated Sophie and toilet prototypes with the
tank behind her, the seat under her hips, and her legs and paper fully in front
of the bowl. `tools/build_special_life_rows.py` delegates to
`tools/composite_generated_life_rows.py`.

The urgent bush row still uses explicit occlusion order:

- urgent Sophie, privacy bush, then the hand-and-paper foreground detail.
