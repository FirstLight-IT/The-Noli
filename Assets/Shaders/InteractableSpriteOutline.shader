Shader "The Noli/Interactable Sprite Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1, 0.82, 0.2, 1)
        _OutlineThickness ("Outline Thickness", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            half4 _OutlineColor;
            float _OutlineThickness;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * _OutlineThickness;
                half centerAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                half surroundingAlpha = 0;

                surroundingAlpha = max(surroundingAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2( offset.x, 0)).a);
                surroundingAlpha = max(surroundingAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-offset.x, 0)).a);
                surroundingAlpha = max(surroundingAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0,  offset.y)).a);
                surroundingAlpha = max(surroundingAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, -offset.y)).a);
                surroundingAlpha = max(surroundingAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2( offset.x,  offset.y)).a);
                surroundingAlpha = max(surroundingAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-offset.x,  offset.y)).a);
                surroundingAlpha = max(surroundingAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2( offset.x, -offset.y)).a);
                surroundingAlpha = max(surroundingAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-offset.x, -offset.y)).a);

                half outlineAlpha = saturate(surroundingAlpha - centerAlpha) * _OutlineColor.a;
                return half4(_OutlineColor.rgb, outlineAlpha);
            }
            ENDHLSL
        }
    }
}
