using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Chill Cut > Trees > Build Tree Assets: grows every species and saves their materials, meshes (a detailed one and a lighter one for
/// distance), prefabs (with LOD) and the TreeLibrary under Assets/Trees.
/// </summary>
public static class TreeBuilder
{
    const string Root = "Assets/Trees", Mats = Root + "/Materials", Meshes = Root + "/Meshes", Prefabs = Root + "/Prefabs", LibPath = Root + "/TreeLibrary.asset";

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    [MenuItem("Chill Cut/Trees/Build Tree Assets")]
    public static void BuildAssets()
    {
        foreach (var d in new[] { Mats, Meshes, Prefabs }) Directory.CreateDirectory(d);
        var lib = AssetDatabase.LoadAssetAtPath<TreeLibrary>(LibPath);
        if (!lib) { lib = ScriptableObject.CreateInstance<TreeLibrary>(); AssetDatabase.CreateAsset(lib, LibPath); }
        foreach (var s in TreeSpecies.All())
        {
            var go = BuildPrefab(s);
            switch (s.name) { case "Oak": lib.oak = go; break; case "Birch": lib.birch = go; break; case "Pine": lib.pine = go; break; case "Maple": lib.maple = go; break; case "Cherry": lib.cherry = go; break; case "GrandOak": lib.grandOak = go; break; case "AvenueOak": lib.avenueOak = go; break; case "OldOak": lib.oldOak = go; break; }
        }
        EditorUtility.SetDirty(lib); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("Chill Cut: built the trees under " + Root);
    }

    static Material Replace(string path, Shader sh)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); } else m.shader = sh;
        return m;
    }

    static (Material bark, Material leaves) MakeMaterials(TreeSpecies s)
    {
        var bark = Replace($"{Mats}/{s.name}_Bark.mat", Shader.Find("ChillCut/TreeBark"));
        bark.SetColor("_Base", Hex(s.barkBase)); bark.SetColor("_High", Hex(s.barkHigh)); bark.SetColor("_Crevice", Hex(s.barkCrevice)); bark.SetColor("_Moss", Hex(s.barkMoss));
        bark.SetFloat("_Mode", s.barkMode); bark.SetFloat("_PlateH", s.plateHeight); bark.SetFloat("_Warp", s.warp); bark.SetFloat("_Bump", s.bump); bark.SetFloat("_MossAmt", s.moss);
        var leaf = Replace($"{Mats}/{s.name}_Leaves.mat", Shader.Find("ChillCut/TreeLeaves"));
        leaf.SetColor("_Root", Hex(s.leafRoot)); leaf.SetColor("_Mid", Hex(s.leafMid)); leaf.SetColor("_Tip", Hex(s.leafTip));
        leaf.SetFloat("_Aspect", s.cardAspect); leaf.SetFloat("_Leaves", s.leafCount); leaf.SetFloat("_Spread", s.leafSpread); leaf.SetFloat("_LeafW", s.leafWidth);
        leaf.SetFloat("_Needle", s.needle); leaf.SetFloat("_GradBias", s.gradBias); leaf.SetFloat("_Glow", 0.6f); leaf.SetFloat("_Back", 0.7f);
        EditorUtility.SetDirty(bark); EditorUtility.SetDirty(leaf);
        return (bark, leaf);
    }

    static Mesh SaveMesh(Mesh m, string path)
    {
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old) { EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); EditorUtility.SetDirty(old); return old; }
        AssetDatabase.CreateAsset(m, path); return m;
    }

    static GameObject BuildPrefab(TreeSpecies s)
    {
        var (bark, leaf) = MakeMaterials(s);
        int seed = 1000 + s.name.GetHashCode() % 997;
        var hi = TreeGenerator.Generate(s, seed, 1f, 1f);
        var lo = TreeGenerator.Generate(s, seed, 0.4f, 1.55f);
        var meshHi = SaveMesh(hi.mesh, $"{Meshes}/{s.name}_LOD0.asset"); var meshLo = SaveMesh(lo.mesh, $"{Meshes}/{s.name}_LOD1.asset");

        var root = new GameObject(s.name);
        var l0 = new GameObject("LOD0"); l0.transform.SetParent(root.transform, false);
        var l1 = new GameObject("LOD1"); l1.transform.SetParent(root.transform, false);
        Renderer R0 = Attach(l0, meshHi, bark, leaf), R1 = Attach(l1, meshLo, bark, leaf);
        var lod = root.AddComponent<LODGroup>();
        lod.SetLODs(new[] { new LOD(0.14f, new[] { R0 }), new LOD(0.012f, new[] { R1 }) });
        lod.fadeMode = LODFadeMode.None; lod.RecalculateBounds();
        var info = root.AddComponent<TreeInfo>(); info.trunkRadius = s.trunkRadius * (1f + s.flare * 0.5f); info.crownRadius = hi.crownRadius; info.height = hi.height; info.crownBottom = hi.crownBottom;
        string path = $"{Prefabs}/{s.name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
        return prefab;
    }

    static Renderer Attach(GameObject go, Mesh m, Material bark, Material leaf)
    {
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = new[] { bark, leaf };
        r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
        return r;
    }
}
