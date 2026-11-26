// Stylized bark, drawn procedurally: chunky plates with dark crevices, warm highlights on the raised edges, moss at the foot, and a bump
// worked out from the plate heights so the light finds every ridge. Birch mode paints pale bark with dark scars. No textures.
Shader "ChillCut/TreeBark"
{
    Properties
    {
        _Base ("Plate colour", Color) = (0.45, 0.28, 0.17, 1)
        _High ("Highlight colour", Color) = (0.78, 0.5, 0.3, 1)
        _Crevice ("Crevice colour", Color) = (0.1, 0.06, 0.05, 1)
        _Moss ("Moss colour", Color) = (0.22, 0.4, 0.12, 1)
        _Mode ("Pattern (0 plates, 1 birch)", Float) = 0
        _PlateH ("Plate height (m)", Float) = 0.55
        _Warp ("Plate wobble", Float) = 1
        _Bump ("Bump strength", Float) = 1.2
        _MossAmt ("Moss at the foot", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
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
                half4 _Base, _High, _Crevice, _Moss; float _Mode, _PlateH, _Warp, _Bump, _MossAmt;
            CBUFFER_END
            struct A { float3 p : POSITION; float3 n : NORMAL; float4 c : COLOR; float4 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 w : TEXCOORD0; float3 n : TEXCOORD1; float4 c : TEXCOORD2; float4 uv : TEXCOORD3; half fog : TEXCOORD4; };
            V vert(A i)
            {
                V o; o.w = TransformObjectToWorld(i.p); o.n = TransformObjectToWorldNormal(i.n); o.c = i.c; o.uv = i.uv;
                o.pos = TransformWorldToHClip(o.w); o.fog = ComputeFogFactor(o.pos.z); return o;
            }
            void Pat(float4 uv, out float h, out float id, out float crev)
            {
                if (_Mode > 0.5) BirchPattern(uv, h, id, crev); else BarkPattern(uv, _PlateH, _Warp, h, id, crev);
            }
            half4 frag(V i) : SV_Target
            {
                float h, id, crev; Pat(i.uv, h, id, crev);
                // bump from the height field: surface gradient from screen-space derivatives (Mikkelsen)
                float3 N = normalize(i.n), dpx = ddx(i.w), dpy = ddy(i.w); float dhx = ddx(h), dhy = ddy(h);
                float3 r1 = cross(dpy, N), r2 = cross(N, dpx); float det = dot(dpx, r1);
                float3 grad = sign(det) * (dhx * r1 + dhy * r2);
                float3 n = normalize(abs(det) * N - _Bump * 0.05 * grad);

                float3 col;
                if (_Mode > 0.5)
                {
                    float3 paper = lerp(_Base.rgb * 0.82, _Base.rgb, 0.5 + 0.5 * TH1(floor(i.uv.y * 3.0 + i.uv.w)));
                    col = lerp(_Crevice.rgb, paper, crev);
                    col = lerp(col, _High.rgb, 0.25 * smoothstep(0.6, 1.0, h) * crev);
                }
                else
                {
                    float3 plate = _Base.rgb * (0.86 + 0.28 * id);
                    col = lerp(_Crevice.rgb, plate, crev);
                    col = lerp(col, _High.rgb, pow(saturate(h), 2.2) * (0.3 + 0.45 * id));
                    // the top of a plate catches the light a little more than its sides
                    col *= 0.82 + 0.3 * smoothstep(0.2, 0.95, h);
                }
                float moss = i.c.r * _MossAmt * smoothstep(0.35, 0.9, h) * (0.5 + 0.5 * TH1(id * 17.0));
                col = lerp(col, _Moss.rgb, saturate(moss));
                col *= lerp(0.7, 1.0, crev);
                col *= lerp(0.8, 1.0, saturate(i.c.g + 0.2));
                float3 lit = TreeLight(col, n, i.w, 0.25, 1.0);
                lit += ccSpec(n, normalize(_WorldSpaceCameraPos - i.w), 0.6) * smoothstep(0.1, 0.9, h);
                lit += col * (0.2 + 0.1 * n.y) + col * _CC_Grass.z * 0.04;      // fill, so a trunk under its own crown does not go black
                return half4(MixFog(lit, i.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0 ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Base, _High, _Crevice, _Moss; float _Mode, _PlateH, _Warp, _Bump, _MossAmt;
            CBUFFER_END
            float3 _LightDirection; float3 _LightPosition;
            struct A { float3 p : POSITION; float3 n : NORMAL; };
            struct V { float4 pos : SV_POSITION; };
            V vert(A i)
            {
                V o; float3 w = TransformObjectToWorld(i.p), n = TransformObjectToWorldNormal(i.n);
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
                return o;
            }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
