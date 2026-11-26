// The lighting values and small helpers the tree shaders use. Set the _CC_ globals from your own sun and sky (see the README).
#ifndef CHILLCUT_COMMON_INCLUDED
#define CHILLCUT_COMMON_INCLUDED

float4 _CC_SunDir;                               // xyz: direction to the sun
float4 _CC_SunColor;                             // linear sun colour, no intensity
float4 _CC_SunRad;                               // sun colour x intensity
float4 _CC_HemiSky, _CC_HemiGround;              // hemisphere light colour x intensity
float4 _CC_Grass;                                // x height, y wind, z glow, w lawn size
float4 _CC_Wind;                                 // xy: wind direction on the ground, z: speed (m/s), w: how strong cloud shadows are
float4 _CC_Gloss;                                // x: world gloss 0..1

#define CC_INV_PI 0.31830988618

float ccHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float ccNoise(float2 p)
{
    float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(ccHash(i), ccHash(i + float2(1, 0)), f.x), lerp(ccHash(i + float2(0, 1)), ccHash(i + float2(1, 1)), f.x), f.y);
}

float ccFbm(float2 p) { float a = 0.5, s = 0.0; for (int i = 0; i < 4; i++) { s += a * ccNoise(p); p = p * 2.03 + float2(17.0, 9.0); a *= 0.5; } return s; }

// Soft patches of shade drifting over the land with the wind, as if clouds were passing in front of the sun. 1 = full sun.
float CloudShade(float3 posWS)
{
    float2 q = (posWS.xz - _CC_Wind.xy * _CC_Wind.z * _Time.y) / 150.0;
    float n = ccFbm(q) * 0.75 + ccFbm(q * 2.3 + 7.0) * 0.25;
    return lerp(1.0, 0.62, smoothstep(0.5, 0.7, n) * _CC_Wind.w);
}

// A highlight from the sun on a surface of normal n seen from view (surface to eye). Tighter and brighter as the world gloss goes up; none at 0.
float3 ccSpec(float3 n, float3 view, float strength)
{
    float g = _CC_Gloss.x;
    float3 h = normalize(_CC_SunDir.xyz + view);
    return _CC_SunRad.rgb * pow(saturate(dot(n, h)), lerp(8.0, 150.0, g)) * g * g * strength;
}

#endif
