# Weather-action artwork

These four source strips are the generated, four-frame pixel-art actions used
by the application:

- `rain-umbrella.png` — looping umbrella idle for rain.
- `snowman.png` — one-way snowman-building sequence; the final pose is held.
- `shiver.png` — looping cold-weather shiver.
- `sweat.png` — looping hot-weather brow wipe.

`tools/build_weather_life_atlas.py` deterministically normalizes their
extracted 192x208 frames and appends them as rows 8–11 of
`SophieDesktopPet/Assets/sophie-life.png`.
