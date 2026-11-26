# Stylized hedges (Unity, URP)

Hedges built in code from thousands of small leaf cards, coloured with the same root / middle / tip gradient as the grass. Written for Unity 6.4 with the Universal Render Pipeline. This folder is C# and HLSL for Unity; it is not part of the browser grass demo.

| File | |
|---|---|
| `HedgeBuilder.cs` | Grows a hedge mesh along a centre line on any ground, with rounded ends and varying height and width |
| `HedgeDemo.cs` | Sets the lighting values the shader reads and builds one hedge, to try it out |
| `Shaders/TreeLeaves.shader` | The leaf-card shader (`ChillCut/TreeLeaves`): leaf shapes, gradient, sun and sky light, wind sway, back-light, shadows |
| `Shaders/TreeCommon.hlsl` | Leaf shapes, wind and lighting |
| `Shaders/Common.hlsl` | Shared globals, noise, cloud shade and highlight |

## Use

1. Copy this folder into your project's `Assets`.
2. Put `HedgeDemo` on an empty GameObject, give it your directional light, and press Play.

Or call it yourself:

```csharp
var style = new HedgeBuilder.Style { seed = 91 };               // size, leaf length, colours
var mat = HedgeBuilder.MakeMaterial(style);
HedgeBuilder.Build("Hedge", parent, centreLine /* x, z points */, (x, z) => groundHeight(x, z), style, mat);
```

- The shader takes its light from `_CC_` shader globals (see `HedgeDemo.Publish`). Without them the leaves render black.
- The material is made with `Shader.Find`, so for a player build add the shader to Project Settings, Graphics, Always Included Shaders (or reference a material that uses it).
- A default hedge is about 1.4-2 m tall with 9 cm leaves; a long run is tens of thousands of cards.
