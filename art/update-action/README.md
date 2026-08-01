# Update research animation

`update-research.png` is the four-frame production row used while Sophie
checks, downloads, or installs an update. Sophie sits behind a workbench and
repairs a tiny robot whose indicator changes from amber to cyan. It is built
with `tools/build_special_life_rows.py`.

`update-research.prompt.txt` and `reference-sophie.png` record the attempted
relay image-generation input. The relay returned HTTP 502 on every production
request, so no generated source was accepted. The deterministic fallback keeps
Sophie's original face, body, clothing, hands, book, baseline, and alpha edges
unchanged.
