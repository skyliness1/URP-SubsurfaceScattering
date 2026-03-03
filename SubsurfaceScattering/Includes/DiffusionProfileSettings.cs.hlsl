//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef DIFFUSIONPROFILESETTINGS_CS_HLSL
#define DIFFUSIONPROFILESETTINGS_CS_HLSL
//
// UnityEngine.Rendering.HighDefinition.DiffusionProfileConstants:  static fields
//
#define DIFFUSION_PROFILE_COUNT (16)
#define DIFFUSION_PROFILE_NEUTRAL_ID (0)
#define SSS_PIXELS_PER_SAMPLE (4)

// URP Stencil Usage Definitions
#define STENCILUSAGE_CLEAR (0)
#define STENCILUSAGE_SUBSURFACE_SCATTERING (4)

// VR multi-view support (conditional definition)
#ifndef UNITY_XR_ASSIGN_VIEW_INDEX
    #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
        #define UNITY_XR_ASSIGN_VIEW_INDEX(viewIndex) unity_StereoEyeIndex = viewIndex
    #else
        #define UNITY_XR_ASSIGN_VIEW_INDEX(viewIndex) // No-op for non-VR platforms
    #endif
#endif

uint Get1DAddressFromPixelCoord(uint2 pixCoord, uint2 screenSize, uint eye)
{
    // We need to shift the index to look up the right eye info.
    return (pixCoord.y * screenSize.x + pixCoord.x) + eye * (screenSize.x * screenSize.y);
}

#endif
