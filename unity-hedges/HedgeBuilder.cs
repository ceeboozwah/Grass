using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Natural hedges made the same way as the trees' foliage: thousands of small leaf cards (the ChillCut/TreeLeaves shader, so they get the grass's root /
/// middle / tip colour gradient, the same sun and sky light, wind sway and back-lit glow), packed over a living shape that swells and narrows along its
/// length, rises and dips, and rounds off at its ends. Leaf size is real: a card is one little twig of about 9 cm leaves, so a 1.7 m hedge is
/// hundreds of leaves deep. Outer, higher leaves are bright; inner and lower ones are dark, which also makes the hedge read as solid.
/// </summary>
public static class HedgeBuilder
{
    public class Style
    {
        public float heightMin = 1.4f, heightMax = 2.0f;      // metres
        public float halfWidthMin = 0.5f, halfWidthMax = 0.8f;
        public float leaf = 0.09f;                            // leaf length in metres (card height)
        public float aspect = 1.5f;                           // card width / height
        public float layers = 1.9f;                           // how many leaves deep the shell is
        public float shell = 0.2f;                            // thickness of the bright outer shell (metres)
        public float superEllipse = 2.6f;                     // 2 = round, higher = squarer; hedges are a little squarer than round
        public int seed = 1;
        public string root = "#27482a", mid = "#4f8238", tip = "#a2c75e";
    }

    public static Material MakeMaterial(Style s)
    {
        var m = new Material(Shader.Find("ChillCut/TreeLeaves")) { name = "Hedge_Leaves" };
        Color C(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        m.SetColor("_Root", C(s.root)); m.SetColor("_Mid", C(s.mid)); m.SetColor("_Tip", C(s.tip));
        m.SetFloat("_Aspect", s.aspect); m.SetFloat("_Leaves", 5f); m.SetFloat("_Spread", 1.7f); m.SetFloat("_LeafW", 0.3f); m.SetFloat("_Needle", 0f);
        m.SetFloat("_GradBias", 0f); m.SetFloat("_Glow", 0.5f); m.SetFloat("_Back", 0.6f);
        return m;
    }

    /// <summary>Builds one hedge along a centre line (world x, z). ground(x, z) gives the terrain height. The ends are rounded off.</summary>
    public static GameObject Build(string name, Transform parent, IList<Vector2> centre, System.Func<float, float, float> ground, Style st, Material mat)
    {
        if (centre.Count < 2) return null;
        var rng = new System.Random(st.seed); float R() => (float)rng.NextDouble(); float R2(float a, float b) => a + R() * (b - a);

        // resample to even spacing
        const float step = 0.3f;
        var P = new List<Vector2>(); var S = new List<float>(); float acc = 0f, next = 0f;
        for (int i = 1; i < centre.Count; i++)
        {
            Vector2 a = centre[i - 1], b = centre[i]; float len = (b - a).magnitude; if (len < 1e-4f) continue;
            while (acc + len >= next) { P.Add(a + (b - a) * ((next - acc) / len)); S.Add(next); next += step; }
            acc += len;
        }
        int n = P.Count; if (n < 3) return null; float total = S[n - 1];
        float no = R() * 100f;

        var H = new float[n]; var W = new float[n]; var D = new Vector2[n]; var N = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float e = Mathf.Min(S[i], total - S[i]) / 1.3f; e = Mathf.Clamp01(e); e = e * e * (3f - 2f * e);         // rounded end
            float nh = Mathf.PerlinNoise(S[i] * 0.22f + no, 3.1f), nw = Mathf.PerlinNoise(S[i] * 0.31f + no, 9.7f), bump = Mathf.PerlinNoise(S[i] * 0.9f + no, 5.3f);
            H[i] = Mathf.Lerp(st.heightMin, st.heightMax, nh) * Mathf.Lerp(0.3f, 1f, e) * (0.94f + 0.12f * bump);
            W[i] = Mathf.Lerp(st.halfWidthMin, st.halfWidthMax, nw) * Mathf.Lerp(0.35f, 1f, e);
            Vector2 d = (P[Mathf.Min(i + 1, n - 1)] - P[Mathf.Max(i - 1, 0)]).normalized; D[i] = d; N[i] = new Vector2(-d.y, d.x);
        }

        float pw = st.superEllipse;
        // a point on the cross-section: angle a in (0, pi), returns offset (across, up) and the outward normal in the same two axes
        void Section(float h, float w, float a, out float x, out float y, out Vector2 nrm)
        {
            float cx = Mathf.Cos(a), sy = Mathf.Sin(a), ex = Mathf.Pow(Mathf.Abs(cx), 2f / pw), ey = Mathf.Pow(Mathf.Abs(sy), 2f / pw);
            x = Mathf.Sign(cx) * ex * w; y = ey * h;
            Vector2 g = new(Mathf.Sign(cx) * Mathf.Pow(ex, pw - 1f) / Mathf.Max(w, 0.05f), Mathf.Pow(ey, pw - 1f) / Mathf.Max(h, 0.05f));
            nrm = g.sqrMagnitude < 1e-6f ? Vector2.up : g.normalized;
        }

        var lv = new List<Vector3>(); var ln = new List<Vector3>(); var lc = new List<Color>(); var luv = new List<Vector2>(); var lt = new List<int>();
        float cardArea = st.leaf * st.leaf * st.aspect * 0.55f;
        for (int i = 0; i + 1 < n; i++)
        {
            float h = (H[i] + H[i + 1]) * 0.5f, w = (W[i] + W[i + 1]) * 0.5f;
            float arc = Mathf.PI * (3f * (w + h) - Mathf.Sqrt((3f * w + h) * (w + 3f * h))) * 0.5f * 1.08f;           // half-ellipse perimeter, a little more for the squarer shape
            float count = step * arc * st.layers / cardArea, whole = Mathf.Floor(count); int cards = (int)whole + (R() < count - whole ? 1 : 0);
            for (int k = 0; k < cards; k++)
            {
                float u = R(), a = Mathf.Lerp(0.04f, Mathf.PI - 0.04f, R());
                float hh = Mathf.Lerp(H[i], H[i + 1], u), ww = Mathf.Lerp(W[i], W[i + 1], u);
                Vector2 c2 = Vector2.Lerp(P[i], P[i + 1], u), nn = Vector2.Lerp(N[i], N[i + 1], u).normalized;
                Section(hh, ww, a, out float x, out float y, out Vector2 sn);
                bool core = R() < 0.12f; float depth = core ? R2(st.shell, Mathf.Max(st.shell * 1.5f, Mathf.Min(ww, hh) * 0.7f)) : st.shell * Mathf.Pow(R(), 1.6f);
                Vector3 outward = new(nn.x * sn.x, sn.y, nn.y * sn.x); outward.Normalize();
                Vector3 pos = new Vector3(c2.x + nn.x * x, ground(c2.x, c2.y) + y, c2.y + nn.y * x) - outward * depth;
                // leaf tuft direction: mostly outward, with some scatter and a little lift, so the surface is a mix of leaf faces
                Vector3 rnd = new Vector3(R2(-1, 1), R2(-1, 1), R2(-1, 1)).normalized;
                Vector3 up = (outward * 0.7f + rnd * 0.65f + Vector3.up * 0.12f).normalized;
                Vector3 right = Vector3.Cross(up, new Vector3(R2(-1, 1), R2(-1, 1), R2(-1, 1))); if (right.sqrMagnitude < 1e-4f) right = Vector3.right; right.Normalize();
                float sz = st.leaf * R2(0.8f, 1.3f), half = sz * st.aspect * 0.5f; Vector3 tip = pos + up * sz;
                float outer = 1f - Mathf.Clamp01(depth / Mathf.Max(st.shell * 2f, 0.1f)), top = Mathf.Clamp01(y / Mathf.Max(hh, 0.1f));
                float t = Mathf.Clamp01(0.1f + 0.5f * outer + 0.3f * top + 0.22f * (R() - 0.5f)); if (core) t *= 0.3f;
                Vector3 nrm = (outward + Vector3.up * 0.25f).normalized; var col = new Color(t, R(), 0f, 1f);
                int b0 = lv.Count;
                lv.Add(pos - right * half); lv.Add(pos + right * half); lv.Add(tip - right * half); lv.Add(tip + right * half);
                for (int q = 0; q < 4; q++) { ln.Add(nrm); lc.Add(col); }
                luv.Add(new Vector2(0, 0)); luv.Add(new Vector2(1, 0)); luv.Add(new Vector2(0, 1)); luv.Add(new Vector2(1, 1));
                lt.Add(b0); lt.Add(b0 + 2); lt.Add(b0 + 1); lt.Add(b0 + 1); lt.Add(b0 + 2); lt.Add(b0 + 3);
            }
        }

        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(lv); mesh.SetNormals(ln); mesh.SetColors(lc); mesh.SetUVs(0, luv); mesh.SetTriangles(lt, 0); mesh.RecalculateBounds();
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return go;
    }
}
