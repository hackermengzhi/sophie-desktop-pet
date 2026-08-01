# Update research animation

`update-research.png` is the four-frame production row used while Sophie
checks, downloads, or installs an update. She inspects and repairs a tiny
armoured service robot on a physically grounded workbench; the sequence moves
from a powered-off robot through repair and testing to a cheerful wave.

The Sophie and prop sheets are generated separately from the recorded prompts
and `reference-sophie.png`. `tools/build_special_life_rows.py` then delegates to
`tools/composite_generated_life_rows.py`, which removes the chroma key, applies
shared per-row scales and anchors, and assembles the production atlas without
changing rows 0-15.
