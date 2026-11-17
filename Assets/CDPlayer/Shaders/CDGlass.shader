// Smoked glass for the CD player lid: mostly see-through when looked at straight on, more reflective at grazing
// angles (fresnel), with the sky/reflection probe and a sharp highlight from the main light.
// Built for Quest: one unlit-style transparent pass, no grab pass or refraction.
Shader "ShiftStudios/CDGlass"
{
    Properties
    {
        _Tint ("Tint (alpha = how dark the glass is)", Color) = (0.16, 0.18, 0.22, 0.22)
        _Reflection ("Reflection Strength", Range(0, 1)) = 0.7
        _FresnelPower ("Fresnel Power", Range(1, 8)) = 4
        _Highlight ("Highlight Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha   // premultiplied, so reflections and highlights stay bright
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Reflection;
                half _FresnelPower;
                half _Highlight;
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
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                ZERO_INITIALIZE(Varyings, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n = normalize(i.normalWS);
                half3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light = GetMainLight();

                half fresnel = pow(1.0h - saturate(dot(n, viewDir)), _FresnelPower);
                half3 reflection = GlossyEnvironmentReflection(reflect(-viewDir, n), 0.05h, 1.0h);
                half highlight = pow(saturate(dot(n, normalize(light.direction + viewDir))), 256.0h) * _Highlight;

                half reflectAmount = lerp(0.06h, 1.0h, fresnel) * _Reflection;
                half alpha = saturate(_Tint.a + reflectAmount);
                half3 color = _Tint.rgb * _Tint.a + reflection * reflectAmount + light.color * highlight;
                return half4(color, saturate(alpha + highlight));
            }
            ENDHLSL
        }
    }
}
