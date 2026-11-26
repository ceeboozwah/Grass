using UnityEngine;

/// <summary>The tree prefabs, in one place, so code can ask for a tree instead of knowing about files.</summary>
[CreateAssetMenu(menuName = "Chill Cut/Tree Library")]
public class TreeLibrary : ScriptableObject
{
    public GameObject oak, birch, pine, maple, cherry, grandOak, avenueOak, oldOak;
    /// <summary>The five everyday trees (the grand oak is placed on purpose, not scattered).</summary>
    public GameObject[] All => new[] { oak, birch, pine, maple, cherry };
    public GameObject Pick(System.Random rng) { var a = All; return a[rng.Next(a.Length)]; }
}
