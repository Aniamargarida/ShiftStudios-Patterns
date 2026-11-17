// Eyelids drawn over the whole view of each eye (works in VR single-pass and in the editor's Game view).
// _Closure 0 = eyes wide open, 1 = fully shut (solid colour). The opening is an almond shape that shrinks
// towards a line a little below the middle, because the upper lid travels further than the lower one.
// Drawn by EyelidOverlay.cs; you don't need to put this on a material yourself.
Shader "ShiftStudio/Eyelids"
{
    Properties
    {
        _Closure ("Closure", Range(0, 1)) = 0
        _Dim ("Dim (darkens the open part)", Range(0, 1)) = 0
        _Color ("Lid Colour", Color) = (0.02, 0.01, 0.01, 1)
        _Softness ("Edge Softness", Range(0.001, 0.6)) = 0.18
        _Width ("Eye Width", Range(0.5, 2)) = 0.9
        _UpperHeight ("Upper Lid Height (open)", Range(0.3, 2)) = 0.95
        _LowerHeight ("Lower Lid Height (open)", Range(0.3, 2)) = 0.8
        _CloseLine ("Where The Lids Meet (0 bottom, 1 top)", Range(0, 1)) = 0.42
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Eyelids"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Closure;
                float _Dim;
                float _Softness;
                float _Width;
                float _UpperHeight;
                float _LowerHeight;
                float _CloseLine;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Ignore where the quad is: stretch it over the full view of each eye.
                float2 ndc = input.uv * 2.0 - 1.0;
                output.positionCS = float4(ndc.x, ndc.y * _ProjectionParams.x, UNITY_NEAR_CLIP_VALUE, 1.0);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float closure = saturate(_Closure);
                float open = 1.0 - closure;

                float2 centre = float2(0.5, lerp(0.5, _CloseLine, closure));
                float2 d = input.uv - centre;
                float halfHeight = (d.y > 0.0 ? _UpperHeight : _LowerHeight) * open;
                float halfWidth = _Width * lerp(1.0, 0.85, closure);

                // 0 in the middle of the eye opening, 1 on the edge of the lids.
                float e = sqrt((d.x * d.x) / (halfWidth * halfWidth) + (d.y * d.y) / max(halfHeight * halfHeight, 1e-6));
                float lid = smoothstep(1.0 - _Softness, 1.0, e);

                float alpha = closure >= 0.999 ? 1.0 : max(lid, saturate(_Dim));
                return half4(_Color.rgb, alpha * _Color.a);
            }
            ENDHLSL
        }
    }
}
