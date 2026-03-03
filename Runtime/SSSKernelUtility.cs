using UnityEngine;

namespace SoulRender
{
    /// <summary>
    /// CPU-side utility for precomputing the separable Burley diffusion profile kernel.
    /// </summary>
    public static class SSSKernelUtility
    {
        // Replicates EvalBurleyDiffusionProfile from DiffusionProfile.hlsl
        // R(r, S) = S / (8*PI) * (Exp[-S*r/3] + Exp[-S*r])
        private static Vector3 EvalBurleyDiffusionProfile(float r, Vector3 S)
        {
            // exp_13 = Exp[-S * r / 3]
            float ex = Mathf.Exp(-S.x * r / 3f);
            float ey = Mathf.Exp(-S.y * r / 3f);
            float ez = Mathf.Exp(-S.z * r / 3f);
            // expSum = exp_13 * (1 + exp_13^2) = Exp[-S*r/3] + Exp[-S*r]
            float wx = S.x / (8f * Mathf.PI) * (ex * (1f + ex * ex));
            float wy = S.y / (8f * Mathf.PI) * (ey * (1f + ey * ey));
            float wz = S.z / (8f * Mathf.PI) * (ez * (1f + ez * ez));
            return new Vector3(wx, wy, wz);
        }

        /// <summary>
        /// Precompute a separable 1D Burley kernel for the given diffusion profile.
        /// </summary>
        /// <param name="shapeParamsAndMaxScatterDists">
        ///   Per-profile shape params: .xyz = S = 1/D (shape param), .w = max scatter distance.
        /// </param>
        /// <param name="worldScalesAndFilterRadii">
        ///   Per-profile world scale and filter radius: .x = metersPerUnit, .y = filterRadius (mm).
        /// </param>
        /// <param name="profileIndex">Index of the profile to use.</param>
        /// <param name="kernelSize">Number of kernel taps (should be odd, e.g. 11).</param>
        /// <returns>
        ///   Array of <paramref name="kernelSize"/> Vector4 values:
        ///   .xyz = normalized per-channel weight, .w = normalized offset in [-1, 1].
        /// </returns>
        public static Vector4[] ComputeSeparableKernel(
            Vector4[] shapeParamsAndMaxScatterDists,
            Vector4[] worldScalesAndFilterRadii,
            int profileIndex,
            int kernelSize)
        {
            if (kernelSize < 1) kernelSize = 1;

            var kernel = new Vector4[kernelSize];

            // Retrieve shape params for this profile
            Vector4 shapeParam = (shapeParamsAndMaxScatterDists != null && profileIndex < shapeParamsAndMaxScatterDists.Length)
                ? shapeParamsAndMaxScatterDists[profileIndex]
                : Vector4.one;
            Vector3 S = new Vector3(shapeParam.x, shapeParam.y, shapeParam.z);

            // Filter radius in mm
            float filterRadius = (worldScalesAndFilterRadii != null && profileIndex < worldScalesAndFilterRadii.Length)
                ? worldScalesAndFilterRadii[profileIndex].y
                : 1f;
            if (filterRadius <= 0f) filterRadius = 1f;

            // Compute unnormalized weights at evenly-spaced offsets in [-filterRadius, +filterRadius]
            float[] offsets = new float[kernelSize];
            Vector3[] weights = new Vector3[kernelSize];
            Vector3 weightSum = Vector3.zero;

            for (int i = 0; i < kernelSize; i++)
            {
                // Map tap index to [-1, 1] normalized, then scale to mm
                float t = kernelSize > 1 ? (i / (float)(kernelSize - 1)) * 2f - 1f : 0f;
                float r = Mathf.Abs(t) * filterRadius; // r in mm
                offsets[i] = t;

                Vector3 w = EvalBurleyDiffusionProfile(r, S);
                weights[i] = w;
                weightSum += w;
            }

            // Normalize so sum of weights per channel = 1
            for (int i = 0; i < kernelSize; i++)
            {
                float nx = weightSum.x > 0f ? weights[i].x / weightSum.x : 0f;
                float ny = weightSum.y > 0f ? weights[i].y / weightSum.y : 0f;
                float nz = weightSum.z > 0f ? weights[i].z / weightSum.z : 0f;
                kernel[i] = new Vector4(nx, ny, nz, offsets[i]);
            }

            return kernel;
        }
    }
}
