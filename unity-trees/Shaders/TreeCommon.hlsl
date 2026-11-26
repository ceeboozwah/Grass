// Shared by the tree shaders: the same sun, sky light and glow as the grass (Common.hlsl), leaf-card shapes, and the stylized bark pattern.
#ifndef CHILLCUT_TREE_COMMON
#define CHILLCUT_TREE_COMMON
#include "Common.hlsl"

float TH1(float n) { return frac(sin(n * 12.9898) * 43758.5453); }
float2 TH2(float2 p) { p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3))); return frac(sin(p) * 43758.5453); }

// ---------------------------------------------------------------- leaves
// A leaf card is a small twig: a fan of leaf blades (or needles) growing from the middle of its lower edge.
// uv.x runs across the card, uv.y from the base to the tip; aspect is the card's width over its height.
struct LeafHit { float a, yl, vein, id; };
LeafHit LeafShape(float2 uv, float aspect, float count, float spread, float leafW, float needle)
{
    LeafHit h; h.a = 0; h.yl = 0; h.vein = 0; h.id = 0;
    float2 p = float2((uv.x - 0.5) * aspect, uv.y - 0.02);
    int n = (int)count;
    float px = max(fwidth(p.x) + fwidth(p.y), 1e-4);
    [loop] for (int k = 0; k < n; k++)
    {
        float f = n > 1 ? (k / (float)(n - 1) - 0.5) * 2.0 : 0.0;
        float a = f * spread * 0.5, c = cos(a), s = sin(a);
        float2 q = float2(c * p.x - s * p.y, s * p.x + c * p.y);
        float len = 1.0 - 0.3 * abs(f);
        float y = q.y / len;
        float w = needle > 0.5 ? leafW * (1.0 - 0.6 * saturate(y)) : leafW * sin(3.14159 * pow(saturate(y), 0.7));
        float dx = abs(q.x);
        float cov = saturate((w - dx) / px + 0.5) * step(0.0, y) * step(y, 1.0);
        if (cov > h.a) { h.a = cov; h.yl = saturate(y); h.vein = 1.0 - smoothstep(0.0, needle > 0.5 ? 0.012 : 0.03, dx); h.id = k; }
    }
    return h;
}

// Wind: the same gusts the grass uses, scaled up the tree and out along each card, and a fast flutter on the leaves.
float3 LeafSway(float3 posWS, float rnd, float cardY)
{
    float baseY = unity_ObjectToWorld._m13, hh = saturate((posWS.y - baseY) / 7.0);
    float t = _Time.y, w = _CC_Grass.y;
    float g = sin(dot(posWS.xz, float2(0.31, 0.18)) + t * 1.25) * 0.55 + sin(dot(posWS.xz, float2(-0.23, 0.41)) * 1.7 + t * 2.3) * 0.3;
    float sway = g * w * 0.22 * (0.15 + hh) * (0.35 + 0.65 * cardY);
    float flutter = sin(t * 7.0 + rnd * 40.0 + posWS.x * 1.7) * w * 0.035 * cardY * (0.4 + hh);
    return float3(0.87 * sway + flutter * 0.6, flutter, 0.5 * sway - flutter * 0.4);
}

// ---------------------------------------------------------------- lighting (the grass shader's model)
#ifndef TREE_NO_LIGHT
float3 TreeLight(float3 albedo, float3 n, float3 posWS, float wrap, float shadowMul)
{
    Light light = GetMainLight(TransformWorldToShadowCoord(posWS));
    float ndl = saturate((dot(n, _CC_SunDir.xyz) + wrap) / (1.0 + wrap));
    float3 direct = _CC_SunRad.rgb * ndl * lerp(1.0, light.shadowAttenuation, shadowMul) * CloudShade(posWS);
    float3 hemi = lerp(_CC_HemiGround.rgb, _CC_HemiSky.rgb, 0.5 * n.y + 0.5);
    hemi = lerp(hemi, dot(hemi, float3(0.3, 0.59, 0.11)).xxx, 0.55);          // the lawn's green bounce should not stain a trunk
    return albedo * (direct + hemi) * CC_INV_PI;
}
#endif

// ---------------------------------------------------------------- bark
// u runs round the trunk in whole plates (the period N is stored per tube so the pattern closes), v runs up it in metres.
float2 BarkVor(float2 p, float period)
{
    float2 ip = floor(p), fp = frac(p); float d1 = 8, d2 = 8, id = 0;
    [unroll] for (int j = -1; j <= 1; j++)
    [unroll] for (int i = -1; i <= 1; i++)
    {
        float2 g = float2(i, j), cell = ip + g;
        cell.x = cell.x - period * floor(cell.x / period);
        float2 o = TH2(cell); float2 r = g + o - fp; float d = dot(r, r);
        if (d < d1) { d2 = d1; d1 = d; id = TH1(cell.x * 7.0 + cell.y * 13.0); } else if (d < d2) d2 = d;
    }
    return float2(sqrt(d2) - sqrt(d1), id);          // distance to the plate edge, plate id
}

// Returns height 0 (crevice) .. 1 (top of a plate), the plate id, and a crevice darkness.
void BarkPattern(float4 uv, float plateH, float warp, out float h, out float id, out float crev)
{
    float N = uv.z, TAU = 6.2831853;
    float u = uv.x, v = uv.y + uv.w * 9.0;
    // the plates wobble and drift as they climb; sines of whole turns round the trunk keep it seamless
    float wx = sin(u / N * TAU * 2.0 + v * 1.1) * 0.22 + sin(u / N * TAU * 5.0 - v * 2.3) * 0.08;
    float wy = sin(u / N * TAU * 3.0 + v * 0.7) * 0.35;
    float2 p = float2(u + wx * warp, v / plateH + wy * warp);
    float2 e = BarkVor(p, N);
    float edge = e.x; id = e.y;
    float2 fine = BarkVor(float2(u * 3.0 + 0.37, v / (plateH * 0.45)), N * 3.0);
    // a rounded plate: it rises quickly off the crevice and flattens on top, like the sculpted bark in the references
    float plate = smoothstep(0.0, 0.26, edge);
    plate = plate * (2.0 - plate);
    float crack = smoothstep(0.0, 0.07, fine.x);
    h = plate * (0.7 + 0.3 * id) * lerp(0.86, 1.0, crack);
    crev = smoothstep(0.0, 0.085, edge) * lerp(0.88, 1.0, crack);
}

// Birch: pale papery bark with dark horizontal scars.
void BirchPattern(float4 uv, out float h, out float id, out float crev)
{
    float N = uv.z, v = uv.y + uv.w * 9.0, u = uv.x;
    float2 p = float2(u, v / 0.34);
    float2 ip = floor(p), fp = frac(p); h = 1; id = 0; crev = 1;
    [unroll] for (int j = -1; j <= 1; j++)
    [unroll] for (int i = -1; i <= 1; i++)
    {
        float2 cell = ip + float2(i, j); cell.x = cell.x - N * floor(cell.x / N);
        float2 o = TH2(cell); float present = step(0.52, TH1(cell.x * 3.1 + cell.y * 5.7));
        float2 q = fp - float2(i, j) - float2(o.x, o.y * 0.7 + 0.15);
        float len = 0.35 + 0.35 * o.y, thick = 0.07 + 0.08 * o.x;
        float lens = 1.0 - saturate(abs(q.x) / len);
        float m = present * smoothstep(0.0, 0.12, lens * thick * 2.2 - abs(q.y) * lens);
        if (m > 1.0 - crev) { crev = 1.0 - m; id = o.x; }
    }
    h = lerp(0.5, 1.0, crev);
}
#endif
