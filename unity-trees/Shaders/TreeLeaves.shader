// Stylized foliage: cards that are small twigs of leaves, coloured with the grass's root / middle / tip gradient (inner and low leaves dark,
// outer and high leaves bright), lit with the same sun and sky light, glowing and swaying with the same sliders. Back-lit leaves light up.
Shader "ChillCut/TreeLeaves"
{
    Properties
    {
        _Root ("Root colour", Color) = (0.06, 0.36, 0.2, 1)
        _Mid ("Middle colour", Color) = (0.18, 0.68, 0.32, 1)
        _Tip ("Tip colour", Color) = (0.72, 0.94, 0.38, 1)
        _Aspect ("Card width / height", Float) = 1.6
        _Leaves ("Leaves per card", Float) = 5
        _Spread ("Fan angle (radians)", Float) = 1.6
        _LeafW ("Leaf width", Float) = 0.3
        _Needle ("Needles (0 leaves, 1 needles)", Float) = 0
        _GradBias ("Gradient bias", Float) = 0
        _Glow ("Glow", Float) = 0.6
        _Back ("Back light", Float) = 0.7
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off AlphaToMask On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "TreeCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Root, _Mid, _Tip; float _Aspect, _Leaves, _Spread, _LeafW, _Needle, _GradBias, _Glow, _Back;
            CBUFFER_END
            struct A { float3 p : POSITION; float3 n : NORMAL; float4 c : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 w : TEXCOORD0; float3 n : TEXCOORD1; float4 c : TEXCOORD2; float2 uv : TEXCOORD3; half fog : TEXCOORD4; };
            V vert(A i)
            {
                V o; float3 w = TransformObjectToWorld(i.p);
                w += LeafSway(w, i.c.g, i.uv.y);
                o.w = w; o.n = TransformObjectToWorldNormal(i.n); o.c = i.c; o.uv = i.uv;
                o.pos = TransformWorldToHClip(w); o.fog = ComputeFogFactor(o.pos.z); return o;
            }
            half4 frag(V i) : SV_Target
            {
                LeafHit h = LeafShape(i.uv, _Aspect, _Leaves, _Spread, _LeafW, _Needle);
                clip(h.a - 0.01);
                float rnd = i.c.g, leafRnd = TH1(h.id * 3.7 + rnd * 91.0);
                float t = saturate(i.c.r * 0.68 + h.yl * 0.3 + (rnd - 0.5) * 0.12 + (leafRnd - 0.5) * 0.14);
                t = saturate((t - 0.5) * 1.35 + 0.5);                              // more contrast: dark inside, bright on the outside
                t = pow(t, lerp(1.7, 0.6, _GradBias * 0.5 + 0.5));
                float3 gc = t < 0.5 ? lerp(_Root.rgb, _Mid.rgb, t * 2.0) : lerp(_Mid.rgb, _Tip.rgb, (t - 0.5) * 2.0);
                gc *= 1.0 + h.vein * 0.14;
                float3 n = normalize(i.n);
                float3 view = normalize(_WorldSpaceCameraPos - i.w);
                float back = pow(saturate(dot(-view, _CC_SunDir.xyz)), 3.0);
                float3 lit = TreeLight(gc, n, i.w, 0.35, 1.0);
                lit += ccSpec(n, view, 0.5) * saturate(0.4 + t);
                lit += gc * (0.1 + 0.08 * n.y);
                lit += gc * _CC_Grass.z * _Glow * (0.04 + 0.5 * t * t);
                lit += _CC_SunColor.rgb * gc * back * t * _Back;
                return half4(MixFog(lit, i.fog), h.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off ColorMask 0 ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #define TREE_NO_LIGHT
            #include "TreeCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Root, _Mid, _Tip; float _Aspect, _Leaves, _Spread, _LeafW, _Needle, _GradBias, _Glow, _Back;
            CBUFFER_END
            float3 _LightDirection; float3 _LightPosition;
            struct A { float3 p : POSITION; float3 n : NORMAL; float4 c : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i)
            {
                V o; float3 w = TransformObjectToWorld(i.p); w += LeafSway(w, i.c.g, i.uv.y);
                float3 n = TransformObjectToWorldNormal(i.n);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - w);
#else
                float3 ld = _LightDirection;
#endif
                o.pos = TransformWorldToHClip(ApplyShadowBias(w, n, ld));
#if UNITY_REVERSED_Z
                o.pos.z = min(o.pos.z, UNITY_NEAR_CLIP_VALUE * o.pos.w);
#else
                o.pos.z = max(o.pos.z, UNITY_NEAR_CLIP_VALUE * o.pos.w);
#endif
                o.uv = i.uv; return o;
            }
            half4 frag(V i) : SV_Target { LeafHit h = LeafShape(i.uv, _Aspect, _Leaves, _Spread, _LeafW, _Needle); clip(h.a - 0.5); return 0; }
            ENDHLSL
        }
    }
}
