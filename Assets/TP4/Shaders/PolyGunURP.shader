// Portage URP du shader "PolyGun/Gun Shader" (pack gratuit PolyGun, pipeline Built-in uniquement).
// Mêmes propriétés et même logique de masques couleur : il suffit de changer le shader des matériaux.
// Compatible VR (single-pass instanced) et SRP Batcher.
Shader "TP4/PolyGun URP"
{
    Properties
    {
        _CamoTexture ("Camo Texture", 2D) = "white" {}
        _CamoColor ("Camo Color", Color) = (1,1,1,1)
        _CamoMetallic ("Camo Metallic", Range(0, 1)) = 0.3
        _CamoGloss ("Camo Gloss", Range(0, 1)) = 0.2
        _Color1 ("Color 1", Color) = (0.8,0.8,0.8,1)
        _Color1Metallic ("Color 1 Metallic", Range(0, 1)) = 0.9
        _Color1Gloss ("Color 1 Gloss", Range(0, 1)) = 0.7
        _Color2 ("Color 2", Color) = (0.5137255,0.5137255,0.5137255,1)
        _Color2Metallic ("Color 2 Metallic", Range(0, 1)) = 1
        _Color2Gloss ("Color 2 Gloss", Range(0, 1)) = 0.05
        _Color3 ("Color 3", Color) = (0,0,0,1)
        _Color3Metallic ("Color 3 Metallic", Range(0, 1)) = 0
        _Color3Gloss ("Color 3 Gloss", Range(0, 1)) = 0.15
        _Color4 ("Color 4", Color) = (0.1176471,0.1176471,0.1176471,1)
        _Color4Metallic ("Color 4 Metallic", Range(0, 1)) = 0
        _Color4Gloss ("Color 4 Gloss", Range(0, 1)) = 0.15
        _Color5 ("Color 5", Color) = (0.3019608,0.3019608,0.3019608,1)
        _Color5Metallic ("Color 5 Metallic", Range(0, 1)) = 0.75
        _Color5Gloss ("Color 5 Gloss", Range(0, 1)) = 0.6
        _Color6 ("Color 6", Color) = (0.5137255,0.5137255,0.5137255,1)
        _Color6Metallic ("Color 6 Metallic", Range(0, 1)) = 0.75
        _Color6Gloss ("Color 6 Gloss", Range(0, 1)) = 0.6
        _Emission1 ("Emission 1", Color) = (1,0,0,1)
        _Emission2 ("Emission 2", Color) = (0,1,0,1)
        _Emission3 ("Emission 3", Color) = (0,0,1,1)
        [HideInInspector]_TextureMask ("Texture Mask", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_CamoTexture);  SAMPLER(sampler_CamoTexture);
        TEXTURE2D(_TextureMask);  SAMPLER(sampler_TextureMask);

        CBUFFER_START(UnityPerMaterial)
            float4 _CamoTexture_ST;
            float4 _TextureMask_ST;
            half4 _CamoColor;  half _CamoMetallic;  half _CamoGloss;
            half4 _Color1;     half _Color1Metallic; half _Color1Gloss;
            half4 _Color2;     half _Color2Metallic; half _Color2Gloss;
            half4 _Color3;     half _Color3Metallic; half _Color3Gloss;
            half4 _Color4;     half _Color4Metallic; half _Color4Gloss;
            half4 _Color5;     half _Color5Metallic; half _Color5Gloss;
            half4 _Color6;     half _Color6Metallic; half _Color6Gloss;
            half4 _Emission1;  half4 _Emission2;     half4 _Emission3;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = nrm.normalWS;
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half3 Mask(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_TextureMask, sampler_TextureMask, TRANSFORM_TEX(uv, _TextureMask)).rgb;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 uv = i.uv;
                // Même découpage de la texture de masque que le shader d'origine.
                half3 m1 = Mask(uv);
                half3 m2 = Mask(uv + float2(0.9339, 1.0));
                half  m4 = 1.0h - (m2.r + m2.g + m2.b);
                half3 m3 = Mask(uv + float2(0.868, 1.0));
                half  camoMask = Mask(uv * float2(0.01, 0.5)).r;

                half3 camo = SAMPLE_TEXTURE2D(_CamoTexture, sampler_CamoTexture, TRANSFORM_TEX(uv, _CamoTexture)).rgb
                             * _CamoColor.rgb * (1.0h - camoMask);

                half3 albedo = lerp(lerp(lerp(camo, _Color1.rgb, m1.r), _Color2.rgb, m1.g), _Color3.rgb, m1.b)
                             + m2.r * _Color4.rgb + m2.g * _Color5.rgb + m2.b * _Color6.rgb;

                half metallic = lerp(lerp(lerp(_CamoMetallic, _Color1Metallic, m1.r), _Color2Metallic, m1.g), _Color3Metallic, m1.b) * m4
                              + m2.r * _Color4Metallic + m2.g * _Color5Metallic + m2.b * _Color6Metallic;

                half gloss = lerp(lerp(lerp(_CamoGloss, _Color1Gloss, m1.r), _Color2Gloss, m1.g), _Color3Gloss, m1.b) * m4
                           + m2.r * _Color4Gloss + m2.g * _Color5Gloss + m2.b * _Color6Gloss;

                half3 emission = m3.r * _Emission1.rgb + m3.g * _Emission2.rgb + m3.b * _Emission3.rgb;

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(i.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = saturate(albedo);
                surface.metallic = saturate(metallic);
                surface.smoothness = saturate(gloss);
                surface.occlusion = 1.0h;
                surface.emission = emission;
                surface.alpha = 1.0h;
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct AttributesS
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsS
            {
                float4 positionCS : SV_POSITION;
            };

            VaryingsS vertShadow(AttributesS v)
            {
                VaryingsS o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 fragShadow(VaryingsS i) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
