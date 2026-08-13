// VFX pilot (task-animation-pilot.md follow-up) — sprite unlit, màu [HDR] để URP Bloom Volume bắt
// được (Bloom chỉ sáng những pixel render ra > ngưỡng threshold, sprite thường (0..1) không bao
// giờ vượt ngưỡng dù đặt màu trắng tuyệt đối — cần property [HDR] để Inspector cho phép kéo
// Intensity > 1). Không dùng cho toàn bộ sprite game — chỉ cho VFX/vũ khí muốn phát sáng.
Shader "TurnBase/HDREmissiveSprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Color (kéo Intensity > 1 để phát sáng Bloom)", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Color;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                return tex * _Color * IN.color;
            }
            ENDHLSL
        }
    }
}
