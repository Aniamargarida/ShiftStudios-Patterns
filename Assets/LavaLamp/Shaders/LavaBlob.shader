// Wax for the lava lamp. Unlit + emissive, with a cheap vertex wobble so the
// wax surface looks soft and alive. Built for Quest: no lighting, no textures, no transparency.
// Wobble values are in the mesh's units (meters for the generated wax mesh).
Shader "ShiftStudios/LavaBlob"
{
    Properties
    {
        _CoreColor ("Core Color", Color) = (1, 0.55, 0.12, 1)
        _EdgeColor ("Edge Color", Color) = (1, 0.12, 0.28, 1)
        _Emission ("Brightness", Range(0, 3)) = 1.3
        _WobbleAmount ("Wobble Amount (m)", Range(0, 0.01)) = 0.002
        _WobbleSpeed ("Wobble Speed", Range(0, 5)) = 1.1
        _WobbleFrequency ("Wobble Frequency (waves per m)", Range(0, 400)) = 140
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _EdgeColor;
                half _Emission;
                half _WobbleAmount;
                half _WobbleSpeed;
                half _WobbleFrequency;
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
                half rim : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                ZERO_INITIALIZE(Varyings, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // The object's world position seeds the wobble, so two lamps never wobble in sync.
                // The wax moves through the pattern as it rises and sinks, which makes it look like it flows.
                float3 seed = TransformObjectToWorld(float3(0, 0, 0)) * 37.0;
                float t = _Time.y * _WobbleSpeed;
                float3 p = v.positionOS.xyz * _WobbleFrequency;
                float wobble = (sin(p.x + t + seed.y)
                              + sin(p.y * 1.3 + t * 1.17 + seed.x)
                              + sin(p.z * 0.9 - t * 0.83 + seed.z)) / 3.0;

                float3 positionOS = v.positionOS.xyz + v.normalOS * wobble * _WobbleAmount;
                float3 positionWS = TransformObjectToWorld(positionOS);
                o.positionCS = TransformWorldToHClip(positionWS);

                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(positionWS);
                o.rim = 1.0 - saturate(dot(normalWS, viewWS));
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 color = lerp(_CoreColor.rgb, _EdgeColor.rgb, i.rim * i.rim) * _Emission;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
