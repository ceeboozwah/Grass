# Stylized trees (Unity, URP)

Trees grown in code: a trunk and recursive branches with root flare, leaf cards clustered at the twig tips, procedural bark and leaf shaders, and two levels of detail. No textures or model files. Written for Unity 6.4 with the Universal Render Pipeline.

Species: Oak, Birch, Pine, Maple, Cherry, GrandOak, AvenueOak, OldOak.

| File | |
|---|---|
| `Runtime/TreeSpecies.cs` | The numbers that make each species: shape, bark and leaves |
| `Runtime/TreeGenerator.cs` | Grows a mesh from a species (submesh 0 bark, submesh 1 leaves). Same seed, same tree |
| `Runtime/TreeInfo.cs` | Sizes recorded on each tree prefab |
| `Runtime/TreeLibrary.cs` | One asset holding the tree prefabs |
| `Editor/TreeBuilder.cs` | Menu **Chill Cut > Trees > Build Tree Assets**: saves materials, meshes, prefabs and the library under `Assets/Trees` |
| `Shaders/TreeLeaves.shader` | Leaf and needle cards (`ChillCut/TreeLeaves`): gradient, wind, back-light, shadows |
| `Shaders/TreeBark.shader` | Bark plates and birch bark (`ChillCut/TreeBark`) |
| `Shaders/TreeCommon.hlsl`, `Shaders/Common.hlsl` | Leaf shapes, bark patterns, wind and lighting |

## Use

1. Copy this folder into your project's `Assets` (keep `TreeBuilder.cs` inside a folder named `Editor`).
2. Run **Chill Cut > Trees > Build Tree Assets**, then drag the prefabs from `Assets/Trees/Prefabs` into a scene.

## Lighting

The shaders take their light from global values. **Until something sets them, the trees render black.** Set them from your sun and sky, for example in `Update`:

```csharp
Vector3 toSun = -sun.transform.forward;
Shader.SetGlobalVector("_CC_SunDir", toSun);
Shader.SetGlobalVector("_CC_SunColor", sun.color.linear);
Shader.SetGlobalVector("_CC_SunRad", sun.color.linear * 2.4f);
Shader.SetGlobalVector("_CC_HemiSky", new Color(0.62f, 0.76f, 0.92f).linear * 1.4f);
Shader.SetGlobalVector("_CC_HemiGround", new Color(0.30f, 0.45f, 0.22f).linear * 1.4f);
Shader.SetGlobalVector("_CC_Grass", new Vector4(0.3f, 0.55f /* wind */, 0.6f /* glow */, 0f));
Shader.SetGlobalVector("_CC_Wind", new Vector4(0.87f, 0.5f, 3.6f, 0.5f /* cloud shade */));
Shader.SetGlobalVector("_CC_Gloss", new Vector4(0.3f, 0f, 0f, 0f));
```

`../unity-hedges` carries its own copy of the leaf shader under the same name. If you use both folders, keep only the `Shaders` folder from this one.
