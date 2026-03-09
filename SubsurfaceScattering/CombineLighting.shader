Shader "Hidden/SubsurfaceScattering/CombineLighting"
{
    Properties
    {
        [HideInInspector] _StencilRef("_StencilRef", Int) = 4 //STENCILUSAGE_SUBSURFACE_SCATTERING
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #pragma target 4.5
        #pragma exclude_renderers gles gles3 glcore
        #pragma multi_compile_instancing

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueDepth.hlsl"

        TEXTURE2D_X(_IrradianceSource);
        SAMPLER(sampler_IrradianceSource);

        struct Attributes
        {
            uint vertexID : SV_VertexID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            
            // Full screen triangle
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
            
            return output;
        }

        float4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            
            // Sample SSS filtered diffuse lighting
        #ifdef SSS_HALF_RES
            // Bilinear upsample from half-res filtering buffer using UV coordinates.
            // The hardware interpolator handles the resolution mismatch automatically.
            float3 diffuseLighting = SAMPLE_TEXTURE2D_X(_IrradianceSource, sampler_IrradianceSource, input.texcoord).rgb;
        #else
            float3 diffuseLighting = LOAD_TEXTURE2D_X(_IrradianceSource, input.positionCS.xy).rgb;
        #endif
            
            // Sample depth and reconstruct world position for fog calculation
            float deviceDepth = SampleSceneDepth(input.texcoord);
            float3 positionWS = ComputeWorldSpacePosition(input.texcoord, deviceDepth, UNITY_MATRIX_I_VP);
            float4 positionCS = TransformWorldToHClip(positionWS);
            float fogFactor = ComputeFogFactor(positionCS.z);
            diffuseLighting = MixFog(diffuseLighting, fogFactor);
            
            return float4(diffuseLighting, 0);
        }
        ENDHLSL

        // Pass 0: Combine SSS diffuse lighting with color buffer (additive blend)
        Pass
        {
            Name "CombineLighting"
            
            Stencil
            {
                Ref [_StencilRef]
                ReadMask 255
                WriteMask 0
                Comp Equal
                Pass Keep
            }

            Cull Off
            ZTest Always
            ZWrite Off
            Blend One One // Additive: finalColor = colorBuffer + filteredDiffuse

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ SSS_HALF_RES
            ENDHLSL
        }
    }

    Fallback Off
}

