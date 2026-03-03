using UnityEngine;
using System.Runtime.InteropServices;

namespace SoulRender
{
    /// <summary>
    /// C# representation of ShaderVariablesGlobalSubsurface CBUFFER
    /// Must match exactly with HLSL structure layout for ConstantBuffer.PushGlobal
    /// Based on HDRP implementation - uses unsafe fixed arrays for blittable memory layout
    /// Field order and names match HDRP's ShaderVariablesGlobal exactly
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct ShaderVariablesGlobalSubsurface
    {
        // Use float4 to avoid any packing issue between compute and pixel shaders
        // Each Vector4 = 4 floats, so 16 Vector4s = 64 floats
        
        // _ShapeParamsAndMaxScatterDists[16] -> float4[16] = float[64]
        public fixed float _ShapeParamsAndMaxScatterDists[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT * 4];

        // _TransmissionTintsAndFresnel0[16] -> float4[16] = float[64]
        public fixed float _TransmissionTintsAndFresnel0[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT * 4];

        // _WorldScalesAndFilterRadiiAndThicknessRemaps[16] -> float4[16] = float[64]
        public fixed float _WorldScalesAndFilterRadiiAndThicknessRemaps[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT * 4];

        // _DualLobeAndDiffusePower[16] -> float4[16] = float[64]
        public fixed float _DualLobeAndDiffusePower[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT * 4];

        // _BorderAttenuationColor[16] -> float4[16] = float[64]
        public fixed float _BorderAttenuationColor[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT * 4];

        // _DiffusionProfileHashTable[16] -> uint4[16] = uint[64]
        // HDRP uses HashTable (not Hash), and stores as uint4 array (only first component used)
        public fixed uint _DiffusionProfileHashTable[DiffusionProfileConstants.DIFFUSION_PROFILE_COUNT * 4];

        // Control variables (order matches HDRP)
        public uint _EnableSubsurfaceScattering;
        public uint _TexturingModeFlags;
        public uint _TransmissionFlags;
        public uint _DiffusionProfileCount;
    }
}

