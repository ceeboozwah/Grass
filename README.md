# Grass

Stylized grass for the browser, with every control exposed as a slider. About 520,000 instanced blades are drawn in one call. Built on [three.js](https://threejs.org) r160 (vendored, no build step).

Drag to orbit, scroll to zoom, **H** hides the panel.

## Run it

Any static file server works. From this folder:

```sh
python3 -m http.server 8000
```

then open <http://localhost:8000/>. (Opening `index.html` straight from disk will not work: browsers block ES modules on `file://`.)

## The sliders

| Control | Range | Default | What it does |
|---|---|---|---|
| Height | 5-70 cm | 32 cm | Full height of a blade (each blade varies about 60-140% around it) |
| Glow | 0-200% | 80% | Light the blades give back (more at the tips), plus how strong the bloom is |
| Gradient | 0-100% | 85% | How far the root-to-tip colour ramp is used; 0 flattens it to the middle colour |
| Gradient shift | -100 to +100 | 0 | Slides where the ramp changes: negative keeps blades dark and rooted, positive lets the tip colour climb down |
| Wind | 0-200% | 70% | Strength of the gusts that sway the blades |
| Root / Middle / Tip | colour pickers | `#0b8a4d` `#3be06b` `#d6ff4f` | The three colours of the ramp |
| Presets | 6 buttons | Neon meadow | Neon meadow, Aqua, Golden hour, Candy, Deep forest, Ember tips |
| Time of day | 6:00 am to 7:00 pm | 4:24 pm (80) | Sun position, sky, light colour and haze |
| Grass density | 20-100% | 100% | Draws a fraction of the blades (they are shuffled, so any fraction is an even scatter) |

## What is in here

| File | |
|---|---|
| `src/grass.js` | `makeGrass` (the blade mesh and its shader, patched into a three.js Lambert material so it still gets lights and fog) and `makeGround` |
| `src/sky.js` | The sky and `applySun`, which turns a time of day into sun, sky and light colours |
| `src/post.js` | Half-float target, dual-filter bloom, ACES tone map, vibrance and vignette |
| `src/main.js` | The playground: sliders, lighting and an orbit camera |
| `vendor/three` | three.js r160 (MIT, see `vendor/three/LICENSE`) |

### Using it in your own scene

```js
import { makeGrass, makeGround } from './src/grass.js';

const uniforms = { /* see main.js: uTime, uHeight, uWind, uLawn, uRoot, uMid, uTip,
                      uSunDirW, uSunColor, uGlow, uGradAmt, uGradBias */ };
scene.add(makeGround(uniforms), makeGrass(uniforms));
```

Change a uniform's `.value` and the grass follows. Update `uTime` every frame for the wind.
