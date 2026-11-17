// A compact disc. The top side shows a printed label (any square image, drawn edge to edge over the disc),
// the bottom side and edge are mirror-silver with a rainbow sheen that shifts with the viewing angle,
// and the middle is the clear plastic hub. Radii come from the mesh itself (object space), so it needs no special UVs.
// Built for Quest: one pass, main light + ambient + reflection probe, no shadows, no transparency.
Shader "ShiftStudios/CDDisc"
{
    Properties
    {
        _LabelTex ("Label (square image, top side)", 2D) = "white" {}
        _LabelTint ("Label Tint", Color) = (1, 1, 1, 1)
        _DataColor ("Silver Color", Color) = (0.8, 0.82, 0.86, 1)
        _HubColor ("Clear Hub Color", Color) = (0.72, 0.76, 0.8, 1)
        _Iridescence ("Rainbow Sheen", Range(0, 1)) = 0.6
        _DiscRadius ("Disc Radius (object units)", Float) = 0.00418
        _LabelInner ("Label Inner Radius (0-1)", Range(0, 1)) = 0.38
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_LabelTex);
            SAMPLER(sampler_LabelTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _LabelTex_ST;
                half4 _LabelTint;
                half4 _DataColor;
                half4 _HubColor;
                half _Iridescence;
                float _DiscRadius;
                half _LabelInner;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 grooveWS : TEXCOORD3;   // direction of the data grooves (around the disc)
                half topSide : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                ZERO_INITIALIZE(Varyings, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionOS = v.positionOS.xyz;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.grooveWS = TransformObjectToWorldDir(float3(-v.positionOS.y, v.positionOS.x, 0));
                o.topSide = v.normalOS.z > 0.5 ? 1 : 0;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float r = length(i.positionOS.xy) / _DiscRadius;   // 0 at the centre, 1 at the rim
                half3 n = normalize(i.normalWS);
                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light = GetMainLight();
                half3 halfDir = normalize(light.direction + viewDir);
                half3 diffuse = SampleSH(n) + light.color * saturate(dot(n, light.direction));
                half3 reflection = GlossyEnvironmentReflection(reflect(-viewDir, n), 0.15h, 1.0h);
                half specular = pow(saturate(dot(n, halfDir)), 64.0h);

                // Silver data side with a rainbow that slides around as the angle to the grooves changes.
                half3 groove = normalize(i.grooveWS + 1e-5);
                float shift = dot(groove, halfDir) * 2.5 + dot(groove, viewDir) * 1.5 + r * 1.2;
                half3 rainbow = saturate(abs(frac(shift + float3(0.0, 0.333, 0.667)) * 6.0 - 3.0) - 1.0);
                half3 color = _DataColor.rgb * (reflection * 0.8h + diffuse * 0.35h)
                            + rainbow * _Iridescence * (0.25h + 0.75h * specular)
                            + light.color * specular * 0.5h;

                // Printed label on the top side.
                float2 labelUV = i.positionOS.xy / (2.0 * _DiscRadius) + 0.5;
                labelUV = labelUV * _LabelTex_ST.xy + _LabelTex_ST.zw;
                half4 label = SAMPLE_TEXTURE2D(_LabelTex, sampler_LabelTex, labelUV) * _LabelTint;
                half labelMask = i.topSide * smoothstep(_LabelInner, _LabelInner + 0.015, r) * (1.0 - smoothstep(0.975, 0.99, r)) * label.a;
                half3 labelColor = label.rgb * diffuse + light.color * specular * 0.08h;
                color = lerp(color, labelColor, labelMask);

                // Clear plastic hub in the middle (both sides).
                half hub = 1.0 - smoothstep(_LabelInner - 0.06, _LabelInner - 0.04, r);
                color = lerp(color, _HubColor.rgb * diffuse * 0.6h + reflection * 0.35h + light.color * specular * 0.4h, hub);

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
