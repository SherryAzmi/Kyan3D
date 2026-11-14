// Stylised-realistic flowing water for URP (rivers and waterfalls).
// - No textures: ripples are procedural noise that flows along uv.y (metres), so any length looks the same.
// - Fresnel reflection of the sky, sun glints, deeper colour in the middle, foam along the banks.
// - _Streaks > 0 adds the falling white streaks of a waterfall.
// _BaseColor is the shallow colour; scripts may change it (shimmer, the Dry Bed brightening).
Shader "Kyan/Water"
{
    Properties
    {
        _BaseColor ("Shallow Colour", Color) = (0.3, 0.75, 0.9, 0.7)
        _DeepColor ("Deep Colour", Color) = (0.05, 0.28, 0.42, 0.9)
        _FoamColor ("Foam Colour (alpha = amount)", Color) = (1, 1, 1, 0.85)
        _FlowSpeed ("Flow Speed", Float) = 0.6
        _RippleScale ("Ripple Scale", Float) = 1.2
        _NormalStrength ("Ripple Strength", Range(0, 3)) = 1.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
        _Fresnel ("Fresnel Power", Range(0.5, 8)) = 4
        _EdgeFoam ("Bank Foam Width", Range(0, 0.5)) = 0.12
        _Streaks ("Waterfall Streaks", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float _FlowSpeed;
                float _RippleScale;
                float _NormalStrength;
                float _Smoothness;
                float _Fresnel;
                float _EdgeFoam;
                float _Streaks;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x),
                            lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
            }

            float fbm(float2 p)
            {
                float s = 0.0, a = 0.5;
                [unroll] for (int k = 0; k < 4; k++) { s += a * vnoise(p); p = p * 2.03 + 17.0; a *= 0.5; }
                return s;
            }

            float heightAt(float2 p, float t)
            {
                float2 a = p + float2(0.0, -t * _FlowSpeed);
                float2 b = p * 1.7 + float2(0.37, -t * _FlowSpeed * 1.35);
                return fbm(a) + 0.5 * fbm(b);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                // uv.x across (0..1), uv.y along the flow in metres
                float2 p = float2(i.uv.x * 3.0, i.uv.y) * _RippleScale;

                // procedural ripple normal (finite differences of a flowing height field)
                const float e = 0.06;
                float h = heightAt(p, t);
                float hx = heightAt(p + float2(e, 0), t);
                float hy = heightAt(p + float2(0, e), t);
                float3 n = normalize(i.normalWS);
                float3 tangent = normalize(cross(n, float3(0, 0, 1)) + 1e-4);
                float3 bitangent = normalize(cross(tangent, n));
                n = normalize(n - (tangent * (hx - h) + bitangent * (hy - h)) / e * 0.12 * _NormalStrength);

                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light L = GetMainLight();
                float ndl = saturate(dot(n, L.direction));
                float3 H = normalize(L.direction + V);
                float spec = pow(saturate(dot(n, H)), exp2(10.0 * _Smoothness + 1.0)) * _Smoothness * 1.5;
                float fres = pow(1.0 - saturate(abs(dot(n, V))), _Fresnel);
                float3 sky = GlossyEnvironmentReflection(reflect(-V, n), 1.0 - _Smoothness, 1.0);

                // deeper (darker, more opaque) in the middle of the river
                float across = 1.0 - abs(i.uv.x - 0.5) * 2.0;
                half4 water = lerp(_BaseColor, _DeepColor, saturate(across * across) * 0.65);

                float3 rgb = water.rgb * (0.55 + 0.45 * ndl) * L.color;
                rgb = lerp(rgb, sky, saturate(fres * 0.65));
                rgb += spec * L.color;

                // foam: along the banks, broken up by noise, plus waterfall streaks
                float edgeDist = min(i.uv.x, 1.0 - i.uv.x);
                float edge = saturate(1.0 - edgeDist / max(_EdgeFoam, 0.001));
                float foamNoise = fbm(p * 3.1 + float2(0, -t * _FlowSpeed * 1.2));
                float foam = saturate(edge * edge * (foamNoise * 1.8 - 0.3));
                float streak = _Streaks * saturate(fbm(float2(i.uv.x * 22.0, i.uv.y * 0.5 - t * _FlowSpeed * 1.6)) * 2.2 - 0.75);
                foam = saturate(foam + streak);
                rgb = lerp(rgb, _FoamColor.rgb * (0.7 + 0.3 * L.color), foam * _FoamColor.a);

                float alpha = saturate(water.a + fres * 0.25 + foam * 0.5 + spec * 0.3);
                alpha *= saturate(edgeDist * 14.0);   // soft shoreline instead of a hard edge

                rgb = MixFog(rgb, i.fogFactor);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
