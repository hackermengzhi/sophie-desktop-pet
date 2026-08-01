# Update research animation

`update-research.png` is the four-frame production row used while Sophie
checks, downloads, or installs an update. She inspects and repairs a tiny
armoured service robot on a physically grounded workbench; the sequence moves
from a powered-off robot through repair and testing to a cheerful wave.

`robot-contact.png` is the approved combined interaction source. It was composed
from the separately generated Sophie and prop prototypes so the magnifier,
wrench, fingertip, and high-five palm make visible physical contact with the
robot. `tools/build_special_life_rows.py` delegates to
`tools/composite_generated_life_rows.py`, which removes the chroma key, applies
a shared row scale and assembles the production atlas.
