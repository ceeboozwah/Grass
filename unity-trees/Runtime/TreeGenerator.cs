using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grows a tree as geometry: a trunk and recursive branches as tapered tubes (with root flare), and leaf cards clustered at the twig tips,
/// each card carrying its place in the crown so the shader can colour it from dark inside to bright outside.
/// Submesh 0 is bark, submesh 1 is leaves. Same seed, same tree.
/// </summary>
public static class TreeGenerator
{
    public struct Result { public Mesh mesh; public float height, crownRadius, crownBottom, crownTop, trunkBase; public int cards; }

    class Anchor { public Vector3 pos, dir; public float radius; }
    class Ctx
    {
        public TreeSpecies s; public System.Random rng; public float density, cardScale;
        public List<Vector3> bv = new(), bn = new(); public List<Color> bc = new(); public List<Vector4> buv = new(); public List<int> bt = new();
        public List<Vector3> lv = new(), ln = new(); public List<Color> lc = new(); public List<Vector4> luv = new(); public List<int> lt = new();
        public List<Anchor> anchors = new(); public float tubeSeed;
        public float R() => (float)rng.NextDouble();
        public float R(float a, float b) => a + R() * (b - a);
    }

    static Vector3 RandPerp(Ctx c, Vector3 d)
    {
        Vector3 r = new(c.R(-1, 1), c.R(-1, 1), c.R(-1, 1)); r -= d * Vector3.Dot(r, d);
        return r.sqrMagnitude < 1e-6f ? Vector3.Cross(d, Vector3.right).normalized : r.normalized;
    }

    // ================================================================ entry
    public static Result Generate(TreeSpecies s, int seed, float leafDensity = 1f, float cardScale = 1f)
    {
        var c = new Ctx { s = s, rng = new System.Random(seed), density = leafDensity, cardScale = cardScale };
        if (s.kind == TreeSpecies.Kind.Conifer) GrowConifer(c); else GrowSpreading(c);
        float crownR, bottom, top; var center = CrownBounds(c, out crownR, out bottom, out top);
        int cards = BuildLeaves(c, center, crownR);

        var mesh = new Mesh { name = s.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        var v = new List<Vector3>(c.bv); v.AddRange(c.lv);
        var n = new List<Vector3>(c.bn); n.AddRange(c.ln);
        var col = new List<Color>(c.bc); col.AddRange(c.lc);
        var uv = new List<Vector4>(c.buv); uv.AddRange(c.luv);
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetColors(col); mesh.SetUVs(0, uv);
        mesh.subMeshCount = 2; mesh.SetTriangles(c.bt, 0);
        int off = c.bv.Count; var lt = new List<int>(c.lt.Count); foreach (int i in c.lt) lt.Add(i + off); mesh.SetTriangles(lt, 1);
        mesh.RecalculateBounds();
        return new Result { mesh = mesh, height = mesh.bounds.max.y, crownRadius = crownR, crownBottom = bottom, crownTop = top, trunkBase = s.trunkRadius * (1 + s.flare), cards = cards };
    }

    // ================================================================ bark tubes
    /// <summary>A tube along 'pts', radius per point, flared at the foot when 'trunk' is set. Adds to the bark lists.</summary>
    static void Tube(Ctx c, List<Vector3> pts, float[] radius, int sides, bool trunk, int level)
    {
        var s = c.s; int n = pts.Count; if (n < 2) return;
        float r0 = radius[0]; int N = Mathf.Max(3, Mathf.RoundToInt(2f * Mathf.PI * Mathf.Max(r0, 0.03f) / s.plateWidth));
        float seed = c.R() * 10f; c.tubeSeed = seed;
        Vector3 T0 = (pts[1] - pts[0]).normalized, nrm = Vector3.Cross(T0, Mathf.Abs(T0.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        int baseIdx = c.bv.Count; float v = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector3 T = i == 0 ? (pts[1] - pts[0]).normalized : i == n - 1 ? (pts[i] - pts[i - 1]).normalized : (pts[i + 1] - pts[i - 1]).normalized;
            nrm = (nrm - T * Vector3.Dot(nrm, T)).normalized; Vector3 bin = Vector3.Cross(T, nrm);
            if (i > 0) v += (pts[i] - pts[i - 1]).magnitude;
            float y = pts[i].y;
            for (int j = 0; j <= sides; j++)
            {
                float th = (float)j / sides * Mathf.PI * 2f, ct = Mathf.Cos(th), st = Mathf.Sin(th);
                float r = radius[i];
                if (trunk)
                {
                    float fl = Mathf.Exp(-Mathf.Max(y, 0f) / s.flareHeight);
                    r *= 1f + s.flare * fl * (1f + s.lobeAmount * 4f * Mathf.Cos(s.lobes * th) * 0.5f) ;
                }
                Vector3 radial = nrm * ct + bin * st;
                c.bv.Add(pts[i] + radial * r); c.bn.Add(radial);
                float moss = trunk ? Mathf.Clamp01(1f - Mathf.Max(y, 0f) / 1.2f) : 0f;
                c.bc.Add(new Color(moss, Mathf.Clamp01(level * 0.25f + (trunk ? 0f : 0.15f)), (float)i / (n - 1), 1f));
                c.buv.Add(new Vector4((float)j / sides * N, v, N, seed));
            }
        }
        int ring = sides + 1;
        for (int i = 0; i < n - 1; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = baseIdx + i * ring + j, b = a + ring, d = a + 1, e = b + 1;
                AddTri(c, a, b, d); AddTri(c, d, b, e);
            }
        // close the tip with a point
        int apex = c.bv.Count; Vector3 tipDir = (pts[n - 1] - pts[n - 2]).normalized;
        c.bv.Add(pts[n - 1] + tipDir * radius[n - 1] * 1.5f); c.bn.Add(tipDir); c.bc.Add(new Color(0, 0.5f, 1f, 1f)); c.buv.Add(new Vector4(0, v, N, seed));
        int last = baseIdx + (n - 1) * ring;
        for (int j = 0; j < sides; j++) AddTri(c, last + j, apex, last + j + 1);
    }

    /// <summary>Adds a triangle wound so it faces the way its vertex normals point.</summary>
    static void AddTri(Ctx c, int a, int b, int d)
    {
        Vector3 cr = Vector3.Cross(c.bv[b] - c.bv[a], c.bv[d] - c.bv[a]), nn = c.bn[a] + c.bn[b] + c.bn[d];
        if (Vector3.Dot(cr, nn) >= 0f) { c.bt.Add(a); c.bt.Add(b); c.bt.Add(d); } else { c.bt.Add(a); c.bt.Add(d); c.bt.Add(b); }
    }

    // ================================================================ spreading trees (oak, birch, maple, cherry)
    static void GrowSpreading(Ctx c)
    {
        var s = c.s; float trunkLen = s.height * s.trunkLenFrac;
        Vector3 lean = new Vector3(c.R(-1, 1), 0, c.R(-1, 1)).normalized * s.trunkLean * 0.09f;
        Branch(c, new Vector3(0, -0.25f, 0), (Vector3.up + lean).normalized, trunkLen + 0.25f, s.trunkRadius, 0, true, 0.6f);
    }

    /// <summary>Grows one branch and, recursively, its children.</summary>
    static void Branch(Ctx c, Vector3 start, Vector3 dir, float len, float r0, int level, bool trunk, float tipFrac)
    {
        var s = c.s; int steps = Mathf.Max(3, Mathf.CeilToInt(len / (s.segLen * (level == 0 ? 1.1f : 1f))));
        var pts = new List<Vector3> { start }; float step = len / steps; Vector3 d = dir.normalized, p = start;
        var dirs = new List<Vector3> { d };
        float wob = trunk ? s.trunkWobble : s.bend, phase = c.R() * 6.28f; Vector3 wobDir = RandPerp(c, d);
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            d += wobDir * Mathf.Sin(phase + t * (trunk ? 4f : 7f)) * wob * (trunk ? 0.12f : 0.35f) * step * 2f;
            d += RandPerp(c, d) * wob * (trunk ? 0.07f : 0.22f) * step;
            if (!trunk) { d += Vector3.up * s.upward * 0.25f * step * (level == 1 ? 1f : 0.7f); d += Vector3.down * s.droop * 0.5f * step * t; }
            else d += Vector3.up * 0.06f * step;
            d.Normalize(); p += d * step; pts.Add(p); dirs.Add(d);
        }
        var rad = new float[pts.Count];
        for (int i = 0; i < rad.Length; i++) { float t = (float)i / (rad.Length - 1); rad[i] = Mathf.Max(0.006f, r0 * Mathf.Lerp(1f, trunk ? s.trunkTaper : tipFrac * 0.3f, Mathf.Pow(t, trunk ? 0.9f : 1f))); }
        Tube(c, pts, rad, trunk ? 14 : Mathf.Max(5, 9 - level * 2), trunk, level);

        float R(float t) { float f = t * (rad.Length - 1); int i = Mathf.Min((int)f, rad.Length - 2); return Mathf.Lerp(rad[i], rad[i + 1], f - i); }
        Vector3 At(float t, out Vector3 dd) { float f = t * (pts.Count - 1); int i = Mathf.Min((int)f, pts.Count - 2); dd = Vector3.Slerp(dirs[i], dirs[i + 1], f - i); return Vector3.Lerp(pts[i], pts[i + 1], f - i); }

        if (level >= s.leafLevel)           // leaf clumps along the outer half of every twig, and one at the tip
        {
            for (float t = 0.4f; t < 1f; t += s.clumpSpacing / Mathf.Max(len, 0.5f)) { var q = At(t, out var dd); c.anchors.Add(new Anchor { pos = q, dir = dd, radius = s.clumpRadius * Mathf.Lerp(0.7f, 1f, t) }); }
            c.anchors.Add(new Anchor { pos = pts[pts.Count - 1], dir = dirs[dirs.Count - 1], radius = s.clumpRadius * 1.1f });
        }
        if (level >= s.levels) return;
        int count = s.childCount[level]; Vector2 tr = s.childT[level]; float az = c.R() * 6.28f;
        for (int k = 0; k < count; k++)
        {
            float t = count == 1 ? tr.y : Mathf.Lerp(tr.x, tr.y, (k + c.R(-0.2f, 0.2f)) / Mathf.Max(count - 1, 1)); t = Mathf.Clamp(t, tr.x, tr.y);
            Vector3 pos = At(t, out var pd);
            az += 2.39996f + c.R(-0.3f, 0.3f);
            Vector3 side = Vector3.Cross(pd, Mathf.Abs(pd.y) > 0.95f ? Vector3.right : Vector3.up).normalized, side2 = Vector3.Cross(pd, side);
            Vector3 outDir = side * Mathf.Cos(az) + side2 * Mathf.Sin(az);
            float ang = (s.childAngle[level] + c.R(-8, 8)) * Mathf.Deg2Rad;
            Vector3 cd = (pd * Mathf.Cos(ang) + outDir * Mathf.Sin(ang)).normalized;
            float cl = len * s.childLen[level] * Mathf.Lerp(1.15f, 0.55f, t) * c.R(0.85f, 1.12f);
            Branch(c, pos - cd * R(t) * 0.5f, cd, Mathf.Max(cl, 0.5f), Mathf.Max(R(t) * s.childRad[level], 0.012f), level + 1, false, 0.5f);
        }
        // the leader keeps going on trees that fork (the trunk is cut short and the crown does the rest)
    }

    // ================================================================ conifer (pine)
    static void GrowConifer(Ctx c)
    {
        var s = c.s; float H = s.height;
        var pts = new List<Vector3>(); int steps = Mathf.CeilToInt((H + 0.25f) / 0.6f); Vector3 p = new(0, -0.25f, 0); Vector3 lean = new Vector3(c.R(-1, 1), 0, c.R(-1, 1)).normalized * s.trunkLean * 0.06f;
        for (int i = 0; i <= steps; i++) { pts.Add(p); float t = (float)i / steps; p += (Vector3.up + lean * Mathf.Sin(t * 3f + 1f)) * (H + 0.25f) / steps; }
        var rad = new float[pts.Count]; for (int i = 0; i < rad.Length; i++) rad[i] = Mathf.Max(0.012f, s.trunkRadius * Mathf.Pow(1f - (float)i / (rad.Length - 1), 1.25f));
        Tube(c, pts, rad, 14, true, 0);
        float y0 = H * s.whorlStart, y1 = H * 0.97f; float az0 = c.R() * 6.28f; int w = 0;
        for (float y = y0; y < y1; y += s.whorlSpacing * c.R(0.85f, 1.15f), w++)
        {
            float u = (y - y0) / (y1 - y0), L = Mathf.Lerp(s.coneLength, 0.35f, Mathf.Pow(u, 0.85f)) * c.R(0.9f, 1.1f);
            float trunkR = Mathf.Max(0.02f, s.trunkRadius * Mathf.Pow(1f - y / H, 1.25f));
            int nb = u > 0.85f ? 4 : s.whorlBranches;
            for (int k = 0; k < nb; k++)
            {
                float az = az0 + w * 0.9f + k * Mathf.PI * 2f / nb + c.R(-0.25f, 0.25f);
                Vector3 outd = new(Mathf.Cos(az), 0, Mathf.Sin(az));
                Vector3 bp = new Vector3(0, y + c.R(-0.08f, 0.08f), 0) + outd * trunkR * 0.7f;
                PineBranch(c, bp, outd, L * c.R(0.85f, 1.1f), Mathf.Clamp(L * 0.032f, 0.012f, 0.06f), u);
            }
        }
        // the leader: a little tuft at the very top
        c.anchors.Add(new Anchor { pos = new Vector3(0, H - 0.1f, 0), dir = Vector3.up, radius = 0.14f });
        c.anchors.Add(new Anchor { pos = new Vector3(0, H - 0.5f, 0), dir = Vector3.up, radius = 0.2f });
    }

    static void PineBranch(Ctx c, Vector3 start, Vector3 outd, float len, float r0, float u)
    {
        var s = c.s; int steps = Mathf.Max(3, Mathf.CeilToInt(len / 0.45f)); float step = len / steps;
        var pts = new List<Vector3> { start }; Vector3 p = start; var dirs = new List<Vector3>();
        float dropAmt = Mathf.Lerp(s.branchDrop, 0.05f, u);
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            Vector3 d = (outd + Vector3.down * (dropAmt * (1f - t * 1.6f))).normalized;     // droops near the trunk, the tips lift
            d += Vector3.up * Mathf.Max(0f, t - 0.5f) * 0.5f; d.Normalize(); dirs.Add(d);
            p += d * step; pts.Add(p);
        }
        dirs.Add(dirs[dirs.Count - 1]);
        var rad = new float[pts.Count]; for (int i = 0; i < rad.Length; i++) rad[i] = Mathf.Max(0.006f, r0 * (1f - 0.85f * (float)i / (rad.Length - 1)));
        Tube(c, pts, rad, 5, false, 1);
        for (float d0 = 0.12f; d0 < len; d0 += s.clumpSpacing)
        {
            float t = d0 / len; int i = Mathf.Min((int)(t * (pts.Count - 1)), pts.Count - 2);
            c.anchors.Add(new Anchor { pos = Vector3.Lerp(pts[i], pts[i + 1], t * (pts.Count - 1) - i), dir = dirs[i], radius = s.clumpRadius * Mathf.Lerp(1.4f, 0.8f, t) });
        }
        c.anchors.Add(new Anchor { pos = pts[pts.Count - 1], dir = dirs[dirs.Count - 1], radius = s.clumpRadius * 1.3f });
    }

    // ================================================================ leaves
    static Vector3 CrownBounds(Ctx c, out float radius, out float bottom, out float top)
    {
        Vector3 mn = new(1e9f, 1e9f, 1e9f), mx = -mn; float rr = 0;
        foreach (var a in c.anchors) { mn = Vector3.Min(mn, a.pos); mx = Vector3.Max(mx, a.pos); rr = Mathf.Max(rr, new Vector2(a.pos.x, a.pos.z).magnitude); }
        if (c.anchors.Count == 0) { radius = 1; bottom = 0; top = 1; return Vector3.up; }
        radius = Mathf.Max(rr, 0.5f); bottom = mn.y; top = mx.y;
        return new Vector3(0, (mn.y + mx.y) * 0.5f, 0);
    }

    static int BuildLeaves(Ctx c, Vector3 center, float crownR)
    {
        var s = c.s; int cards = 0; float bottom = center.y - 1f, top = center.y + 1f;
        foreach (var a in c.anchors) { bottom = Mathf.Min(bottom, a.pos.y); top = Mathf.Max(top, a.pos.y); }
        float hSpan = Mathf.Max(top - bottom, 0.5f);
        int per = Mathf.Max(1, Mathf.RoundToInt(s.cardsPerClump * c.density));
        float size = s.cardSize * c.cardScale / Mathf.Sqrt(Mathf.Max(c.density, 0.2f)) * (s.kind == TreeSpecies.Kind.Conifer ? 1f : 1f);
        foreach (var a in c.anchors)
            for (int k = 0; k < per; k++)
            {
                Vector3 off;
                float u1 = c.R(), u2 = c.R(), u3 = Mathf.Pow(c.R(), 0.6f), th = u1 * 6.2832f, ph = Mathf.Acos(2f * u2 - 1f);
                off = new Vector3(Mathf.Sin(ph) * Mathf.Cos(th), Mathf.Cos(ph) * (s.kind == TreeSpecies.Kind.Conifer ? 0.6f : 0.75f), Mathf.Sin(ph) * Mathf.Sin(th)) * a.radius * u3;
                Vector3 ctr = a.pos + off;
                Vector3 fromCrown = ctr - center; Vector3 outward = new Vector3(fromCrown.x, fromCrown.y / Mathf.Max(s.crownSquash, 0.2f), fromCrown.z);
                outward = outward.sqrMagnitude < 1e-4f ? Vector3.up : outward.normalized;
                Vector3 rnd = new Vector3(c.R(-1, 1), c.R(-1, 1), c.R(-1, 1)).normalized;
                Vector3 up, right;
                if (s.kind == TreeSpecies.Kind.Conifer)
                {
                    up = (a.dir * 0.85f + RandPerp(c, a.dir) * 0.45f).normalized; right = Vector3.Cross(up, Vector3.up); if (right.sqrMagnitude < 1e-4f) right = Vector3.right; right.Normalize();
                    // tilt the tuft a little off the horizontal so some face up and some face the viewer
                    up = (up + Vector3.up * c.R(-0.15f, 0.35f)).normalized;
                }
                else
                {
                    up = (outward * 0.7f + rnd * 0.6f + Vector3.down * s.leafDroop * 0.6f).normalized;
                    right = Vector3.Cross(up, new Vector3(c.R(-1, 1), c.R(-1, 1), c.R(-1, 1))); if (right.sqrMagnitude < 1e-4f) right = Vector3.right; right.Normalize();
                }
                float sz = size * c.R(0.8f, 1.25f), w = sz * s.cardAspect * 0.5f;
                Vector3 tip = ctr + up * sz;
                // light and shade come from where the card sits in the crown: low and inside is dark, high and outside is bright
                float outer = Mathf.Clamp01(new Vector3(fromCrown.x, fromCrown.y * 0.8f, fromCrown.z).magnitude / Mathf.Max(crownR * 0.95f, 0.5f)), h01 = Mathf.Clamp01((ctr.y - bottom) / hSpan);
                float t = Mathf.Clamp01(0.3f * h01 + 0.62f * outer + 0.24f * (c.R() - 0.5f) - 0.02f);
                float rndv = c.R();
                Vector3 nrm = (Vector3.Lerp(off.sqrMagnitude < 1e-5f ? outward : off.normalized, outward, 0.6f) + Vector3.up * 0.25f).normalized;
                int b0 = c.lv.Count; var col = new Color(t, rndv, 0, 1);
                c.lv.Add(ctr - right * w); c.lv.Add(ctr + right * w); c.lv.Add(tip - right * w); c.lv.Add(tip + right * w);
                for (int q = 0; q < 4; q++) { c.ln.Add(nrm); c.lc.Add(col); }
                c.luv.Add(new Vector4(0, 0, 0, 0)); c.luv.Add(new Vector4(1, 0, 0, 0)); c.luv.Add(new Vector4(0, 1, 0, 0)); c.luv.Add(new Vector4(1, 1, 0, 0));
                c.lt.Add(b0); c.lt.Add(b0 + 2); c.lt.Add(b0 + 1); c.lt.Add(b0 + 1); c.lt.Add(b0 + 2); c.lt.Add(b0 + 3);
                cards++;
            }
        return cards;
    }
}
