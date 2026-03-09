using UnityEngine;

namespace SoulRender
{
    /// <summary>
    /// CPU-side utility for precomputing the separable SSS kernel.
    /// Implements the Jimenez multi-Gaussian skin scattering profile (Separable SSS, 2015),
    /// which is inherently separable (sum of Gaussians) and correct for two-pass H+V filtering.
    /// The Burley/Disney profile is a radial (non-separable) function and must NOT be used
    /// directly as a 1-D filter — applying it as H then V passes gives the wrong 2-D result.
    /// Reference: J. Jimenez et al., "Separable Subsurface Scattering",
    ///            based on the pre-integrated skin profile by d'Eon & Luebke.
    /// </summary>
    public static class SSSKernelUtility
    {
        // Spatial range of the kernel taps before normalisation to [-1, 1].
        // Matches the reference implementation (RANGE = 2 → quadratic distribution).
        private const float RANGE    = 2.0f;
        private const float EXPONENT = 2.0f;

        /// <summary>
        /// Evaluate a single Gaussian lobe with per-channel falloff.
        /// variance: σ² of the Gaussian.
        /// r:        distance from the centre (arbitrary spatial unit).
        /// falloff:  per-channel width modifier – larger value → wider lobe.
        /// </summary>
        private static Vector3 Gaussian(float variance, float r, Vector3 falloff)
        {
            Vector3 g = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                float rr = r / (0.001f + falloff[i]);
                g[i] = Mathf.Exp(-(rr * rr) / (2.0f * variance)) / (2.0f * Mathf.PI * variance);
            }
            return g;
        }

        /// <summary>
        /// Pre-integrated skin diffusion profile: sum of 5 Gaussians (d'Eon & Luebke).
        /// Each Gaussian is separable, so the whole sum is separable – the key property
        /// that makes two-pass filtering work correctly.
        /// </summary>
        private static Vector3 Profile(float r, Vector3 falloff)
        {
            return  0.100f * Gaussian(0.0484f, r, falloff)
                  + 0.118f * Gaussian(0.187f,  r, falloff)
                  + 0.113f * Gaussian(0.567f,  r, falloff)
                  + 0.358f * Gaussian(1.99f,   r, falloff)
                  + 0.078f * Gaussian(7.41f,   r, falloff);
        }

        /// <summary>
        /// Precompute a separable 1-D SSS kernel using the Jimenez multi-Gaussian model.
        ///
        /// Returns <paramref name="kernelSize"/> Vector4 values where:
        ///   .xyz = normalised per-channel weight (channels sum to 1).
        ///   .w   = normalised spatial offset in [-1, 1]
        ///          (multiply by filterRadius_mm in the shader to obtain the mm offset).
        /// </summary>
        /// <param name="shapeParamsAndMaxScatterDists">
        ///   Per-profile: .xyz = S = 1/D (shape parameter), .w = max scatter distance.
        /// </param>
        /// <param name="worldScalesAndFilterRadii">
        ///   Per-profile: .x = metersPerUnit, .y = filterRadius (mm).
        /// </param>
        /// <param name="profileIndex">Index of the diffusion profile to use.</param>
        /// <param name="kernelSize">Number of kernel taps (e.g. 11).</param>
        public static Vector4[] ComputeSeparableKernel(
            Vector4[] shapeParamsAndMaxScatterDists,
            Vector4[] worldScalesAndFilterRadii,
            int profileIndex,
            int kernelSize)
        {
            if (kernelSize < 2) kernelSize = 2;

            var kernel = new Vector4[kernelSize];

            // ── Derive per-channel falloff from the diffusion profile ─────────────────
            // D = 1/S is the scattering distance (larger D → more scattering / wider lobe).
            // Normalising by max(D) maps the widest channel to falloff = 1 and keeps the
            // relative per-channel widths intact (e.g. R > G > B for skin).
            Vector4 shapeParam = (shapeParamsAndMaxScatterDists != null && profileIndex < shapeParamsAndMaxScatterDists.Length)
                ? shapeParamsAndMaxScatterDists[profileIndex]
                : Vector4.one;

            Vector3 S = new Vector3(
                Mathf.Max(shapeParam.x, 0.0001f),
                Mathf.Max(shapeParam.y, 0.0001f),
                Mathf.Max(shapeParam.z, 0.0001f));

            Vector3 D    = new Vector3(1f / S.x, 1f / S.y, 1f / S.z);
            float   maxD = Mathf.Max(Mathf.Max(D.x, D.y), D.z);
            Vector3 falloff = maxD > 0f
                ? new Vector3(D.x / maxD, D.y / maxD, D.z / maxD)
                : Vector3.one;

            // Strength is kept at (1,1,1): per-pixel modulation is handled by
            // subsurfaceMask inside the compute shader, not baked into the kernel.
            Vector3 strength = Vector3.one;

            // ── Step 1: Quadratically-distributed offsets in [-RANGE, +RANGE] ─────────
            // More taps near the centre → better sampling of the peaked skin profile.
            float step = 2.0f * RANGE / (kernelSize - 1);
            for (int i = 0; i < kernelSize; i++)
            {
                float o    = -RANGE + i * step;
                float sign = o < 0.0f ? -1.0f : 1.0f;
                kernel[i].w = RANGE * sign * Mathf.Abs(Mathf.Pow(o, EXPONENT)) / Mathf.Pow(RANGE, EXPONENT);
            }

            // ── Step 2: Trapezoidal-integration weights via the skin Profile ──────────
            for (int i = 0; i < kernelSize; i++)
            {
                float   w0   = i > 0              ? Mathf.Abs(kernel[i].w - kernel[i - 1].w) : 0.0f;
                float   w1   = i < kernelSize - 1 ? Mathf.Abs(kernel[i].w - kernel[i + 1].w) : 0.0f;
                float   area = (w0 + w1) / 2.0f;
                Vector3 wt   = area * Profile(kernel[i].w, falloff);
                kernel[i].x  = wt.x;
                kernel[i].y  = wt.y;
                kernel[i].z  = wt.z;
            }

            // ── Step 3: Move the centre tap (w ≈ 0) to index 0 ───────────────────────
            int     center = kernelSize / 2;
            Vector4 t0     = kernel[center];
            for (int i = center; i > 0; i--)
                kernel[i] = kernel[i - 1];
            kernel[0] = t0;

            // ── Step 4: Normalise weights so each channel sums to 1 ──────────────────
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < kernelSize; i++)
            {
                sum.x += kernel[i].x;
                sum.y += kernel[i].y;
                sum.z += kernel[i].z;
            }
            for (int i = 0; i < kernelSize; i++)
            {
                kernel[i].x /= sum.x > 0f ? sum.x : 1f;
                kernel[i].y /= sum.y > 0f ? sum.y : 1f;
                kernel[i].z /= sum.z > 0f ? sum.z : 1f;
            }

            // ── Step 5: Apply strength (centre tap lerp, outer taps scale) ───────────
            // With strength = (1,1,1) this is a no-op; kept for consistency with the
            // reference so it is easy to wire up per-profile strength control later.
            kernel[0].x = (1.0f - strength.x) * 1.0f + strength.x * kernel[0].x;
            kernel[0].y = (1.0f - strength.y) * 1.0f + strength.y * kernel[0].y;
            kernel[0].z = (1.0f - strength.z) * 1.0f + strength.z * kernel[0].z;
            for (int i = 1; i < kernelSize; i++)
            {
                kernel[i].x *= strength.x;
                kernel[i].y *= strength.y;
                kernel[i].z *= strength.z;
            }

            // ── Step 6: Normalise offsets to [-1, 1] (divide by RANGE) ───────────────
            // The compute shader maps these normalised offsets to mm via:
            //   offsetMm = kernel[i].w * filterRadius_mm
            for (int i = 0; i < kernelSize; i++)
                kernel[i].w /= RANGE;

            return kernel;
        }
    }
}
