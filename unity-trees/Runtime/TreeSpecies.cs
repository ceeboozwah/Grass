using System;
using UnityEngine;

/// <summary>Everything that makes one kind of tree itself: its shape, its bark and its leaves. TreeGenerator grows meshes from these numbers.</summary>
[Serializable]
public class TreeSpecies
{
    public enum Kind { Spreading, Conifer }
    public string name; public Kind kind = Kind.Spreading;

    // ---- shape
    public float height = 7f, trunkRadius = 0.3f, trunkLenFrac = 0.7f, trunkTaper = 0.3f, trunkLean = 0.3f, trunkWobble = 0.25f;
    public float flare = 0.7f, flareHeight = 0.35f, lobes = 5f, lobeAmount = 0.18f;
    public int levels = 3;
    public int[] childCount = { 7, 4, 4 };
    public Vector2[] childT = { new(0.38f, 0.95f), new(0.3f, 1f), new(0.25f, 1f) };
    public float[] childAngle = { 48f, 40f, 38f };
    public float[] childLen = { 0.75f, 0.62f, 0.55f };
    public float[] childRad = { 0.55f, 0.6f, 0.6f };
    public float upward = 0.5f, droop = 0f, bend = 0.35f;
    public float segLen = 0.45f;

    // ---- conifer only
    public float whorlStart = 0.16f, whorlSpacing = 0.55f; public int whorlBranches = 6; public float coneLength = 2.2f, branchDrop = 0.25f;

    // ---- bark
    public string barkName = "Oak";
    public float plateWidth = 0.3f, plateHeight = 0.55f, barkMode = 0f, bump = 1.9f, warp = 1f, moss = 0.6f;
    public string barkBase = "#6e4529", barkHigh = "#d08a52", barkCrevice = "#1f120d", barkMoss = "#4f7a2b";

    // ---- leaves
    public int leafLevel = 2; public float clumpSpacing = 0.5f, clumpRadius = 0.55f; public int cardsPerClump = 7;
    public float cardSize = 0.6f, cardAspect = 1.6f, leafSpread = 1.6f, leafWidth = 0.3f, leafCount = 5f, needle = 0f, leafDroop = 0f, crownSquash = 1f, gradBias = 0f;
    public string leafRoot = "#0f5c35", leafMid = "#2fae52", leafTip = "#b9f060";

    public float CrownRadiusGuess => height * 0.5f;

    // ================================================================ the species
    public static TreeSpecies Oak() => new()
    {
        name = "Oak", height = 7.2f, trunkRadius = 0.32f, trunkLenFrac = 0.68f, trunkTaper = 0.35f, trunkLean = 0.35f, trunkWobble = 0.3f, flare = 0.9f, lobes = 6f, lobeAmount = 0.2f,
        levels = 3, childCount = new[] { 7, 4, 4 }, childT = new Vector2[] { new(0.42f, 0.96f), new(0.28f, 1f), new(0.2f, 1f) },
        childAngle = new[] { 52f, 42f, 40f }, childLen = new[] { 0.82f, 0.6f, 0.55f }, childRad = new[] { 0.5f, 0.6f, 0.6f }, upward = 0.55f, bend = 0.4f, segLen = 0.5f,
        barkName = "Oak", plateWidth = 0.055f, plateHeight = 0.36f, warp = 1.7f, barkBase = "#9a4a2a", barkHigh = "#ffa66a", barkCrevice = "#4a2412", barkMoss = "#557f2c",
        leafLevel = 2, clumpSpacing = 0.55f, clumpRadius = 0.6f, cardsPerClump = 12, cardSize = 0.46f, cardAspect = 1.6f, leafSpread = 1.7f, leafWidth = 0.3f, leafCount = 5f,
        leafRoot = "#0d5a33", leafMid = "#2fae4f", leafTip = "#c4f063", crownSquash = 0.85f,
    };

    public static TreeSpecies Birch() => new()
    {
        name = "Birch", height = 8.6f, trunkRadius = 0.15f, trunkLenFrac = 0.95f, trunkTaper = 0.25f, trunkLean = 0.45f, trunkWobble = 0.3f, flare = 0.45f, lobes = 3f, lobeAmount = 0.06f, flareHeight = 0.25f,
        levels = 3, childCount = new[] { 9, 3, 3 }, childT = new Vector2[] { new(0.34f, 0.97f), new(0.3f, 1f), new(0.3f, 1f) },
        childAngle = new[] { 38f, 34f, 34f }, childLen = new[] { 0.34f, 0.55f, 0.5f }, childRad = new[] { 0.42f, 0.55f, 0.55f }, upward = 0.25f, droop = 0.55f, bend = 0.3f, segLen = 0.4f,
        barkName = "Birch", barkMode = 1f, plateWidth = 0.22f, plateHeight = 0.34f, barkBase = "#efe8d8", barkHigh = "#ffffff", barkCrevice = "#2b2523", barkMoss = "#6c8a52", moss = 0.25f, bump = 0.5f,
        leafLevel = 2, clumpSpacing = 0.45f, clumpRadius = 0.5f, cardsPerClump = 14, cardSize = 0.4f, cardAspect = 1.5f, leafSpread = 1.5f, leafWidth = 0.22f, leafCount = 7f, leafDroop = 0.5f,
        leafRoot = "#1d7a46", leafMid = "#68cf55", leafTip = "#e6ff7c", crownSquash = 1.15f,
    };

    public static TreeSpecies Pine() => new()
    {
        name = "Pine", kind = Kind.Conifer, height = 9.5f, trunkRadius = 0.26f, trunkTaper = 0.04f, trunkLean = 0.12f, trunkWobble = 0.1f, flare = 0.7f, lobes = 5f, lobeAmount = 0.12f,
        whorlStart = 0.14f, whorlSpacing = 0.5f, whorlBranches = 6, coneLength = 2.7f, branchDrop = 0.3f, segLen = 0.5f,
        barkName = "Pine", plateWidth = 0.065f, plateHeight = 0.6f, barkBase = "#94482a", barkHigh = "#f7a068", barkCrevice = "#3e1d10", barkMoss = "#4a7a30", warp = 0.7f, moss = 0.4f, bump = 1.4f,
        cardsPerClump = 4, cardSize = 0.66f, cardAspect = 2.0f, leafSpread = 2.4f, leafWidth = 0.1f, leafCount = 9f, needle = 1f, clumpSpacing = 0.26f, clumpRadius = 0.08f, crownSquash = 0.4f,
        leafRoot = "#06352b", leafMid = "#14774f", leafTip = "#79d494", gradBias = -0.1f,
    };

    public static TreeSpecies Maple() => new()
    {
        name = "Maple", height = 6.8f, trunkRadius = 0.26f, trunkLenFrac = 0.55f, trunkTaper = 0.4f, trunkLean = 0.25f, trunkWobble = 0.25f, flare = 0.8f, lobes = 5f, lobeAmount = 0.15f,
        levels = 3, childCount = new[] { 8, 4, 4 }, childT = new Vector2[] { new(0.3f, 0.97f), new(0.3f, 1f), new(0.2f, 1f) },
        childAngle = new[] { 50f, 44f, 42f }, childLen = new[] { 0.9f, 0.62f, 0.56f }, childRad = new[] { 0.5f, 0.6f, 0.6f }, upward = 0.6f, bend = 0.35f, segLen = 0.45f,
        barkName = "Maple", plateWidth = 0.05f, plateHeight = 0.32f, barkBase = "#80584e", barkHigh = "#e6b49c", barkCrevice = "#3a2226", barkMoss = "#5c7f34", moss = 0.5f, bump = 1.1f,
        leafLevel = 2, clumpSpacing = 0.5f, clumpRadius = 0.55f, cardsPerClump = 12, cardSize = 0.44f, cardAspect = 1.5f, leafSpread = 1.9f, leafWidth = 0.33f, leafCount = 5f, crownSquash = 0.95f,
        leafRoot = "#8d2a14", leafMid = "#ec6a1d", leafTip = "#ffd541",
    };

    public static TreeSpecies Cherry() => new()
    {
        name = "Cherry", height = 5.4f, trunkRadius = 0.22f, trunkLenFrac = 0.42f, trunkTaper = 0.45f, trunkLean = 0.8f, trunkWobble = 0.7f, flare = 0.8f, lobes = 4f, lobeAmount = 0.16f,
        levels = 3, childCount = new[] { 6, 4, 4 }, childT = new Vector2[] { new(0.3f, 0.98f), new(0.3f, 1f), new(0.2f, 1f) },
        childAngle = new[] { 56f, 46f, 44f }, childLen = new[] { 1.0f, 0.66f, 0.58f }, childRad = new[] { 0.55f, 0.6f, 0.6f }, upward = 0.4f, droop = 0.15f, bend = 0.55f, segLen = 0.4f,
        barkName = "Cherry", plateWidth = 0.05f, plateHeight = 0.44f, barkBase = "#79454f", barkHigh = "#e2a4b2", barkCrevice = "#33191f", barkMoss = "#5d7d3a", moss = 0.3f, warp = 1.3f, bump = 1.0f,
        leafLevel = 2, clumpSpacing = 0.45f, clumpRadius = 0.6f, cardsPerClump = 12, cardSize = 0.4f, cardAspect = 1.5f, leafSpread = 1.8f, leafWidth = 0.34f, leafCount = 5f, crownSquash = 0.8f,
        leafRoot = "#a82f66", leafMid = "#ff7db0", leafTip = "#ffe3ef",
    };

    /// <summary>The great oak: a short, thick trunk and huge limbs that reach out sideways and bloom into a wide, flat-topped crown. For avenues and churchyards.</summary>
    public static TreeSpecies GrandOak() => new()
    {
        name = "GrandOak", height = 12.5f, trunkRadius = 0.62f, trunkLenFrac = 0.30f, trunkTaper = 0.5f, trunkLean = 0.15f, trunkWobble = 0.22f, flare = 1.05f, flareHeight = 0.55f, lobes = 7f, lobeAmount = 0.22f,
        levels = 3, childCount = new[] { 6, 6, 5 }, childT = new Vector2[] { new(0.55f, 0.98f), new(0.2f, 1f), new(0.18f, 1f) },
        childAngle = new[] { 70f, 54f, 46f }, childLen = new[] { 1.5f, 0.62f, 0.52f }, childRad = new[] { 0.5f, 0.6f, 0.6f }, upward = 0.18f, bend = 0.5f, segLen = 0.6f,
        barkName = "GrandOak", plateWidth = 0.06f, plateHeight = 0.42f, warp = 1.7f, barkBase = "#8c4a2c", barkHigh = "#f4a46a", barkCrevice = "#43220f", barkMoss = "#557f2c", bump = 1.9f,
        leafLevel = 2, clumpSpacing = 0.55f, clumpRadius = 0.78f, cardsPerClump = 14, cardSize = 0.5f, cardAspect = 1.6f, leafSpread = 1.7f, leafWidth = 0.3f, leafCount = 5f, crownSquash = 0.6f,
        leafRoot = "#0b5a30", leafMid = "#2fae4f", leafTip = "#c8f26a",
    };

    // Pruned avenue oak: clear trunk, lifted crown, narrow spread so the drive stays open.
    public static TreeSpecies AvenueOak()
    {
        var t = GrandOak();
        t.name = "AvenueOak"; t.barkName = "GrandOak";
        t.height = 13.5f; t.trunkLenFrac = 0.46f;
        t.childAngle = new[] { 48f, 42f, 38f }; t.childLen = new[] { 1.05f, 0.55f, 0.5f };
        t.childT = new Vector2[] { new(0.6f, 0.98f), new(0.2f, 1f), new(0.18f, 1f) };
        t.clumpRadius = 0.7f; t.crownSquash = 0.72f;
        return t;
    }

    // The cottage's old oak: much older than the house looks. A thick, twisted, flared trunk and a wide, generous crown.
    public static TreeSpecies OldOak()
    {
        var t = GrandOak();
        t.name = "OldOak"; t.barkName = "GrandOak";
        t.height = 14.5f; t.trunkRadius = 0.92f; t.trunkLean = 0.32f; t.trunkWobble = 0.5f; t.flare = 1.45f; t.flareHeight = 0.9f; t.lobes = 6f; t.lobeAmount = 0.3f;
        t.trunkLenFrac = 0.27f; t.clumpRadius = 0.85f; t.crownSquash = 0.52f;
        t.childAngle = new[] { 78f, 58f, 48f }; t.childLen = new[] { 1.75f, 0.66f, 0.54f };
        return t;
    }

    public static TreeSpecies[] All() => new[] { Oak(), Birch(), Pine(), Maple(), Cherry(), GrandOak(), AvenueOak(), OldOak() };
}
