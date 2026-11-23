# Grass

Stylized, mowable grass for the browser, with every control exposed as a slider. About 520,000 instanced blades are drawn in one call, and a per-texel cut map lets you mow, trim and regrow the lawn live. Built on [three.js](https://threejs.org) r160 (vendored, no build step).

Drag over the lawn to mow it with a 1.35 m deck. Right-drag (or shift-drag) orbits, scroll zooms, **H** hides the panel.

## Run it

Any static file server works. From this folder:

```sh
python3 -m http.server 8000
```

then open <http://localhost:8000/>. (Opening `index.html` straight from disk will not work: browsers block ES modules on `file://`.)

## The sliders

| Control | Range | Default | What it does |
|---|---|---|---|
| Height | 5-70 cm | 32 cm | Full height of an uncut blade (each blade varies about 60-140% around it) |
| Glow | 0-200% | 80% | Light the blades give back (more at the tips), plus how strong the bloom is |
| Gradient | 0-100% | 85% | How far the root-to-tip colour ramp is used; 0 flattens it to the middle colour |
| Gradient shift | -100 to +100 | 0 | Slides where the ramp changes: negative keeps blades dark and rooted, positive lets the tip colour climb down |
| Wind | 0-200% | 70% | Strength of the gusts that sway the blades |
| Root / Middle / Tip | colour pickers | `#0b8a4d` `#3be06b` `#d6ff4f` | The three colours of the ramp |
| Presets | 6 buttons | Neon meadow | Neon meadow, Aqua, Golden hour, Candy, Deep forest, Ember tips |
| Cut height | 2-12 cm | 5 cm | How short the mower cuts (ignored when it is above the grass) |
| Regrow lawn | button | | Puts every blade back |
| Time of day | 6:00 am to 7:00 pm | 4:24 pm (80) | Sun position, sky, light colour and haze |
| Grass density | 20-100% | 100% | Draws a fraction of the blades (they are shuffled, so any fraction is an even scatter) |

## What is in here

| File | |
|---|---|
| `src/grass.js` | `Lawn` (the cut map and its `stamp` / `stampDisc` / `addClear` calls), `makeGrass` (the blade mesh and its shader, patched into a three.js Lambert material so it still gets lights and fog) and `makeGround` |
| `src/sky.js` | The sky and `applySun`, which turns a time of day into sun, sky and light colours |
| `src/post.js` | Half-float target, dual-filter bloom, ACES tone map, vibrance and vignette |
| `src/main.js` | The playground: sliders, lighting, orbit camera and the mowing pointer |
| `vendor/three` | three.js r160 (MIT, see `vendor/three/LICENSE`) |

### Using it in your own scene

```js
import { Lawn, makeGrass, makeGround } from './src/grass.js';

const lawn = new Lawn();                       // 64 m square, 5 cm cut map
const uniforms = { /* see main.js: uTime, uHeight, uWind, uLawn, uCut, uCamFwd, uRoot, uMid, uTip,
                      uSunDirW, uSunColor, uGlow, uGradAmt, uGradBias */ };
scene.add(makeGround(uniforms), makeGrass(lawn, uniforms));

lawn.stamp(x, z, yaw, 0.05, 0.675, 0.45);      // cut a 1.35 x 0.9 m deck to 5 cm, heading yaw
```

Change a uniform's `.value` and the grass follows. `uCamFwd` is the camera's forward direction on the ground plane (used for the light and dark mowing stripes). Update `uTime` every frame for the wind.
