Shader "Hidden/SubsurfaceScattering/SeparableSSS"
{
    Properties
    {
        [HideInInspector] _StencilRef("_StencilRef", Int) = 4 // STENCILUSAGE_SUBSURFACE_SCATTERING
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

    #define nSamples 25

    float _SSSSDepthFalloff;
    float _DistanceToProjectionWindow;
    float2 _SSSSDirection;
    float4 _Kernel[nSamples];
    float4 _CameraDepthTexture_TexelSize;

    TEXTURE2D_X(_MainTex);
    TEXTURE2D_X(_CameraDepthTexture);
    SAMPLER(sampler_CameraDepthTexture);

    // SSSBuffer: RGB = diffuseColor, A = packed(subsurfaceMask, diffusionProfileIndex)
    TEXTURE2D(_SSSBufferTexture);

    #pragma target 3.0
    ENDHLSL

    SubShader
    {
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "SeparableSSS"

            Stencil
            {
                Ref [_StencilRef]
                ReadMask 255
                WriteMask 0
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM

            #pragma multi_compile _ SSSS_FOLLOW_SURFACE
            #pragma vertex FullscreenVert
            #pragma fragment frag

            float4 frag(Varyings input) : SV_TARGET
            {
                float2 texcoord = input.uv.xy;
                float4 colorM = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, texcoord);

                // Read per-pixel subsurface mask from SSSBuffer alpha channel.
                // SSSBuffer.a = PackFloatInt8bit(subsurfaceMask, profileIndex, 16)
                // subsurfaceMask: 0 = no SSS blur, 1 = full SSS blur
                float4 sssBuffer = LOAD_TEXTURE2D(_SSSBufferTexture, uint2(input.positionCS.xy));
                float subsurfaceMask;
                uint profileIndex;
                UnpackFloatInt8bit(sssBuffer.a, 16, subsurfaceMask, profileIndex);

                float dSceneDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, texcoord);
                float depthM = LinearEyeDepth(dSceneDepth, _ZBufferParams);

                float scale = _DistanceToProjectionWindow / depthM;
                // Scale blur step by subsurfaceMask so per-pixel masking controls blur radius
                float2 finalStep = _SSSSDirection.xy * scale * _CameraDepthTexture_TexelSize.xy * subsurfaceMask;

                float4 colorBlurred = colorM;
                colorBlurred.rgb *= _Kernel[0].rgb;

                for (int i = 1; i < nSamples; i++)
                {
                    float2 offset = texcoord + _Kernel[i].a * finalStep;
                    float4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, offset);

                #ifdef SSSS_FOLLOW_SURFACE
                    float depth = LinearEyeDepth(SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, offset), _ZBufferParams);
                    // Depth sensitivity scaling factor (from Jimenez et al. reference implementation)
                    // Higher values = more aggressive depth discontinuity rejection
                    float s = 1 - exp(-_SSSSDepthFalloff * 10.0 * _DistanceToProjectionWindow * abs(depthM - depth));
                    color.rgb = lerp(color.rgb, colorM.rgb, s);
                #endif

                    colorBlurred.rgb += _Kernel[i].rgb * color.rgb;
                }

                return colorBlurred;
            }
            ENDHLSL
        }
    }
}
