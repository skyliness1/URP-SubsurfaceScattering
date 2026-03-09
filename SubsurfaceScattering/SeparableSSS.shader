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
    // Access _TexturingModeFlags and _EnableSubsurfaceScattering for post-scatter albedo
    #include "Packages/com.unity.render-pipelines.universal/ArtShaders/Scene/SubsurfaceScattering/Includes/ShaderVariablesGlobalSubsurface.hlsl"

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

                // Clamp the step to prevent artifacts when camera is very close to the surface.
                // When depth is tiny, scale→∞ and the outermost kernel samples (at offset ±RANGE)
                // extend far beyond screen boundaries. sampler_LinearClamp repeats edge pixels,
                // creating visible stripe artifacts around screen edges.
                // Cap the outermost sample to 10% of screen in UV space.
                #define KERNEL_RANGE 3.0 // max kernel offset for nSamples=25 with RANGE=3
                float maxStepPerUnit = 0.1 / KERNEL_RANGE; // max UV step per kernel unit
                float stepLen = length(finalStep);
                finalStep *= (stepLen > maxStepPerUnit) ? (maxStepPerUnit / stepLen) : 1.0;

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

                // Apply post-scatter albedo (vertical pass only) to match 5S compute behavior.
                // The 5S compute does: result = postScatterAlbedo * blur(diffuseLighting)
                // For 4S separable: H-pass blurs, V-pass blurs + applies albedo once.
                // Detect vertical pass: _SSSSDirection = (width,0) for H, (0,width) for V.
                if (abs(_SSSSDirection.x) < 0.0001)
                {
                    // Post-scatter texturing mode (mirrors 5S compute SHADERPASS_SUBSURFACE_SCATTERING logic):
                    //   PreAndPostScatter (bit=0): albedo = sqrt(diffuseColor)
                    //   PostScatter (bit=1): albedo = diffuseColor (identity)
                    float3 postScatterAlbedo = sssBuffer.rgb;
                    if (_EnableSubsurfaceScattering != 0)
                    {
                        bool isPostScatter = ((_TexturingModeFlags >> profileIndex) & 1u) != 0;
                        if (!isPostScatter)
                            postScatterAlbedo = sqrt(postScatterAlbedo);
                    }
                    colorBlurred.rgb *= postScatterAlbedo;
                }

                return colorBlurred;
            }
            ENDHLSL
        }
    }
}
