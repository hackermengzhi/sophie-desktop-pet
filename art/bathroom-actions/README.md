# Private life animations

These two four-frame rows are intentionally cute and non-graphic. No waste is
shown.

- `toilet-break.png`: Sophie sits on a pixel toilet and holds a paper roll.
- `urgent-bush.png`: Sophie blushes, becomes visibly worried, finds a leafy
  bush, and crouches behind it while holding paper.

Sophie, the toilet layers, and the bush layers are generated separately from
the prompts stored beside them. `tools/build_special_life_rows.py` delegates to
`tools/composite_generated_life_rows.py`, which uses fixed camera anchors and
explicit occlusion order:

- toilet rear layer, seated Sophie, then only the near seat-rim arc;
- urgent Sophie, privacy bush, then the hand-and-paper foreground detail.
