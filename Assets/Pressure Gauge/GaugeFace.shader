Shader "Halcyon/GaugeFace"
{
    Properties
    {
        [Header(Face)]
        _FaceColor ("Face Color", Color) = (0.06, 0.055, 0.04, 1)
        _FaceTex ("Face Texture (optional)", 2D) = "white" {}
        
        [Header(Brass Bezel)]
        _BrassColor ("Brass Color", Color) = (0.77, 0.66, 0.31, 1)
        _BrassDark ("Brass Shadow", Color) = (0.42, 0.35, 0.13, 1)
        _BrassSpecular ("Brass Specular", Range(0, 2)) = 1.2
        
        [Header(Glass Overlay)]
        _GlassReflection ("Glass Reflection Strength", Range(0, 0.3)) = 0.05
        _GlassRefractOffset ("Glass Refraction", Range(0, 0.02)) = 0.005
        
        [Header(Vapeur Tint)]
        _VapeurTint ("Vapeur Tint Color", Color) = (0.54, 0.44, 0.69, 0)
        _VapeurIntensity ("Vapeur Intensity", Range(0, 1)) = 0
        
        [Header(Crack Effect)]
        _CrackTex ("Crack Texture", 2D) = "black" {}
        _CrackIntensity ("Crack Intensity", Range(0, 1)) = 0
        
        [Header(Glow)]
        _GlowColor ("Zone Glow Color", Color) = (0.1, 0.29, 0.42, 0)
        _GlowIntensity ("Zone Glow Intensity", Range(0, 1)) = 0
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
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
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            CBUFFER_START(UnityPerMaterial)
                half4 _FaceColor;
                half4 _BrassColor;
                half4 _BrassDark;
                half _BrassSpecular;
                half _GlassReflection;
                half _GlassRefractOffset;
                half4 _VapeurTint;
                half _VapeurIntensity;
                half _CrackIntensity;
                half4 _GlowColor;
                half _GlowIntensity;
            CBUFFER_END
            
            TEXTURE2D(_FaceTex);     SAMPLER(sampler_FaceTex);
            TEXTURE2D(_CrackTex);    SAMPLER(sampler_CrackTex);
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 center = float2(0.5, 0.5);
                float dist = distance(uv, center) * 2.0; // 0 at center, 1 at edge
                
                // Base face color with subtle radial gradient (lighter in center)
                half4 face = _FaceColor;
                half4 faceTex = SAMPLE_TEXTURE2D(_FaceTex, sampler_FaceTex, uv);
                face.rgb *= faceTex.rgb;
                face.rgb *= lerp(1.15, 0.85, dist); // subtle radial shading
                
                // Brass bezel ring (outer 8% of radius)
                half bezelMask = smoothstep(0.88, 0.92, dist);
                half bezelHighlight = pow(saturate(1.0 - abs(dist - 0.94) * 20.0), 2.0) * _BrassSpecular;
                half3 bezelColor = lerp(_BrassDark.rgb, _BrassColor.rgb, bezelHighlight);
                face.rgb = lerp(face.rgb, bezelColor, bezelMask);
                
                // Glass reflection — subtle elliptical highlight upper-left
                float2 reflectCenter = float2(0.38, 0.35);
                float reflectDist = length((uv - reflectCenter) * float2(1.0, 1.5));
                half glassHighlight = saturate(1.0 - reflectDist * 3.0) * _GlassReflection;
                face.rgb += glassHighlight;
                
                // Vapeur purple tint overlay
                face.rgb = lerp(face.rgb, _VapeurTint.rgb * 0.5 + face.rgb * 0.5, _VapeurIntensity * 0.3);
                
                // Crack overlay
                if (_CrackIntensity > 0.01)
                {
                    half4 crack = SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, uv);
                    face.rgb = lerp(face.rgb, crack.rgb * _BrassColor.rgb, crack.a * _CrackIntensity);
                }
                
                // Zone glow (emissive edge)
                if (_GlowIntensity > 0.01)
                {
                    half glowRing = saturate(1.0 - abs(dist - 0.85) * 15.0);
                    face.rgb += _GlowColor.rgb * glowRing * _GlowIntensity;
                }
                
                // Circular mask — clip outside the gauge
                face.a *= saturate((1.0 - dist) * 50.0);
                
                return face;
            }
            ENDHLSL
        }
    }
    
    FallBack "UI/Default"
}
