//
// Shader Pass constants for SSS rendering
// Define SHADERPASS before including SubsurfaceScattering.hlsl to enable texturing mode logic
//

#ifndef SHADERPASS_CS_HLSL
#define SHADERPASS_CS_HLSL

// Forward pass for SSS (outputs to SSSBuffer)
#define SHADERPASS_FORWARD_SSSBUFFER (0)

// Subsurface scattering compute pass (blur pass)
#define SHADERPASS_SUBSURFACE_SCATTERING (1)

#endif // SHADERPASS_CS_HLSL

