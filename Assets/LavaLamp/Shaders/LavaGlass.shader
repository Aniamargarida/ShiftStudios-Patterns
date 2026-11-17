// Glass + coloured liquid of the lava lamp. One unlit transparent pass:
// the liquid is brighter near the bulb at the bottom and the edges get a glassy rim.
// Expects the mesh UV.y to run from 0 (bottom of the glass) to 1 (top).
Shader "ShiftStudios/LavaGlass"
{
    Properties
    {
        _LiquidBottom ("Liquid Color (bottom, near bulb)", Color) = (1, 0.42, 0.18, 0.4)
        _LiquidTop ("Liquid Color (top)", Color) = (0.4, 0.08, 0.55, 0.22)
        _RimColor ("Glass Rim Color", Color) = (1, 0.9, 1, 0.55)
        _RimPower ("Glass Rim Sharpness", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LiquidBottom;
                half4 _LiquidTop;
                half4 _RimColor;
                half _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 heightAndRim : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                ZERO_INITIALIZE(Varyings, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);

                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(positionWS);
                o.heightAndRim = half2(v.uv.y, 1.0 - saturate(dot(normalWS, viewWS)));
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 liquid = lerp(_LiquidBottom, _LiquidTop, saturate(i.heightAndRim.x));
                half rim = pow(saturate(i.heightAndRim.y), _RimPower) * _RimColor.a;
                return half4(liquid.rgb + _RimColor.rgb * rim, saturate(liquid.a + rim));
            }
            ENDHLSL
        }
    }
}
