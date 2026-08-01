# Sophie's cat companion

The identity reference comes from Sophie's real cat video. The generated source
sheets preserve its round green-eyed face, grey-golden shaded coat, cream chest
and paws, stocky body, and thick tail.

`tools/build_cat_companion_assets.py` removes the magenta key, splits the four
generated frames, normalizes them into the shared 192×208 cell geometry, builds
`Assets/sophie-cat-life.png`, and appends the approved Sophie-and-cat interaction
as row 20 of `Assets/sophie-life.png`.

Companion atlas rows are idle, walking right, walking left, and sleeping. The
left walk is a deterministic mirror of the approved right-facing cycle.
