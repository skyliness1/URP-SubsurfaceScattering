using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using System.Collections.Generic;

namespace SoulRender
{
    /// <summary>
    /// Legacy rendering path for Subsurface Scattering (non-RenderGraph)
    /// </summary>
    public partial class SubsurfaceScatteringPass
    {
        private static readonly List<ShaderTagId> s_SSSBufferShaderTagList = new List<ShaderTagId> { SSSShaderIDs.SSSBufferPass };
        private readonly RenderTargetIdentifier[] m_MRTArray = new RenderTargetIdentifier[3];

        // Legacy render targets
        private RTHandle m_DiffuseRT;
        private RTHandle m_SSSBufferRT;
        private RTHandle m_FilteringRT;
        private RTHandle m_DownsampleRT;
        private RTHandle m_DiffusionProfileIndexRT;
        private RTHandle m_ResolvedStencilRT;
        private RTHandle m_ResolvedDepthRT;
        private RTHandle m_SeparableIntermediateRT;
        private ComputeBuffer m_CoarseStencilBuffer;

        /// <summary>
        /// Setup render targets for legacy path
        /// </summary>
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var cameraDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            AllocateLegacyRenderTargets(cameraDescriptor);
        }

        /// <summary>
        /// Allocate render targets for legacy rendering
        /// </summary>
        private void AllocateLegacyRenderTargets(RenderTextureDescriptor cameraDescriptor)
        {
            int width = cameraDescriptor.width;
            int height = cameraDescriptor.height;
            int msaaSamples = cameraDescriptor.msaaSamples;

            // Diffuse lighting buffer (B10G11R11 format)
            var diffuseDesc = cameraDescriptor;
            diffuseDesc.graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32;
            diffuseDesc.depthBufferBits = 0;
            diffuseDesc.msaaSamples = msaaSamples; // Match camera MSAA
            diffuseDesc.useDynamicScale = false;
            RenderingUtils.ReAllocateIfNeeded(ref m_DiffuseRT, diffuseDesc, FilterMode.Bilinear, 
                TextureWrapMode.Clamp, name: SSSShaderIDs.SSSDiffuseLightingName);

            // SSS Buffer (R8G8B8A8_SRGB format)
            var sssBufferDesc = cameraDescriptor;
            sssBufferDesc.graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;
            sssBufferDesc.depthBufferBits = 0;
            sssBufferDesc.msaaSamples = msaaSamples; // Match camera MSAA
            sssBufferDesc.useDynamicScale = false;
            RenderingUtils.ReAllocateIfNeeded(ref m_SSSBufferRT, sssBufferDesc, FilterMode.Point, 
                TextureWrapMode.Clamp, name: SSSShaderIDs.SSSBufferTextureName);

            // Filtering buffer (no MSAA, used for compute output)
            var filteringDesc = cameraDescriptor;
            filteringDesc.graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32;
            filteringDesc.depthBufferBits = 0;
            filteringDesc.msaaSamples = 1; // No MSAA for compute shader output
            filteringDesc.enableRandomWrite = true;
            filteringDesc.useDynamicScale = false;
            RenderingUtils.ReAllocateIfNeeded(ref m_FilteringRT, filteringDesc, FilterMode.Bilinear, 
                TextureWrapMode.Clamp, name: SSSShaderIDs.SSSFilteringTextureName);

            // Downsample buffer (if needed)
            if (m_DownsampleSteps > 0)
            {
                float scale = 1.0f / (1u << m_DownsampleSteps);
                int downsampleWidth = Mathf.Max(1, (int)(width * scale));
                int downsampleHeight = Mathf.Max(1, (int)(height * scale));

                var downsampleDesc = cameraDescriptor;
                downsampleDesc.width = downsampleWidth;
                downsampleDesc.height = downsampleHeight;
                downsampleDesc.graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                downsampleDesc.depthBufferBits = 0;
                downsampleDesc.msaaSamples = 1;
                downsampleDesc.enableRandomWrite = true;
                downsampleDesc.useDynamicScale = false;
                RenderingUtils.ReAllocateIfNeeded(ref m_DownsampleRT, downsampleDesc, FilterMode.Bilinear, 
                    TextureWrapMode.Clamp, name: SSSShaderIDs.SSSDownsampledTextureName);
            }

            // Diffusion profile index texture (if occlusion is enabled)
            if (m_SubsurfaceScatteringAttenuation)
            {
                var profileIndexDesc = cameraDescriptor;
                profileIndexDesc.width = Mathf.Max(1, width / 2);
                profileIndexDesc.height = height;
                profileIndexDesc.graphicsFormat = GraphicsFormat.R8_UInt;
                profileIndexDesc.depthBufferBits = 0;
                profileIndexDesc.msaaSamples = 1;
                profileIndexDesc.enableRandomWrite = true;
                profileIndexDesc.useDynamicScale = false;
                RenderingUtils.ReAllocateIfNeeded(ref m_DiffusionProfileIndexRT, profileIndexDesc, FilterMode.Point, 
                    TextureWrapMode.Clamp, name: SSSShaderIDs.SSSProfileIndexTextureName);
            }

            // Resolved stencil and depth buffers (if MSAA is enabled)
            if (msaaSamples > 1)
            {
                var resolvedStencilDesc = cameraDescriptor;
                resolvedStencilDesc.graphicsFormat = GraphicsFormat.R8G8_UInt;
                resolvedStencilDesc.depthBufferBits = 0;
                resolvedStencilDesc.msaaSamples = 1;
                resolvedStencilDesc.enableRandomWrite = true;
                resolvedStencilDesc.useDynamicScale = false;
                RenderingUtils.ReAllocateIfNeeded(ref m_ResolvedStencilRT, resolvedStencilDesc, FilterMode.Point, 
                    TextureWrapMode.Clamp, name: "StencilBufferResolved");

                var resolvedDepthDesc = cameraDescriptor;
                resolvedDepthDesc.graphicsFormat = GraphicsFormat.R32_SFloat;
                resolvedDepthDesc.depthBufferBits = 0;
                resolvedDepthDesc.msaaSamples = 1;
                resolvedDepthDesc.enableRandomWrite = true;
                resolvedDepthDesc.useDynamicScale = false;
                RenderingUtils.ReAllocateIfNeeded(ref m_ResolvedDepthRT, resolvedDepthDesc, FilterMode.Point, 
                    TextureWrapMode.Clamp, name: "DepthBufferResolved");
            }

            // Separable intermediate buffer (horizontal pass output, same format as m_FilteringRT)
            if (m_FilterMode == SubsurfaceScattering.SSSFilterMode.SeparableFilter)
            {
                var separableDesc = cameraDescriptor;
                separableDesc.graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                separableDesc.depthBufferBits = 0;
                separableDesc.msaaSamples = 1;
                separableDesc.enableRandomWrite = true;
                separableDesc.useDynamicScale = false;
                RenderingUtils.ReAllocateIfNeeded(ref m_SeparableIntermediateRT, separableDesc, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: SSSShaderIDs.SSSSeparableIntermediateName);
            }
        }

        /// <summary>
        /// Execute legacy rendering path
        /// </summary>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, m_SSSSampler))
            {
                var cameraData = renderingData.cameraData;
                var camera = cameraData.camera;
                var cameraDescriptor = cameraData.cameraTargetDescriptor;

                // Get view count for VR support
                int viewCount = cameraData.xr.enabled ? cameraData.xr.viewCount : 1;

                // Step 1: Render SSS Buffer MRT pass
                ExecuteRenderSSSBuffer(cmd, context, ref renderingData);

                // Step 2: Build coarse stencil and resolve MSAA if needed
                RTHandle depthTextureForSSS = GetDepthTextureHandle(ref renderingData);
                Vector4 coarseStencilBufferSize;
                ExecuteBuildCoarseStencilAndResolve(cmd, depthTextureForSSS, cameraDescriptor, viewCount, 
                    out coarseStencilBufferSize, out depthTextureForSSS);

                // Step 3: Execute SSS filtering
                ExecuteSSSFiltering(cmd, cameraDescriptor, depthTextureForSSS, coarseStencilBufferSize, viewCount);

                // Step 4: Combine lighting (add filtered diffuse to color buffer)
                ExecuteCombineLighting(cmd, ref renderingData);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <summary>
        /// Render SSS Buffer MRT pass (color, diffuse, sss buffer)
        /// </summary>
        private void ExecuteRenderSSSBuffer(CommandBuffer cmd, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            using (new ProfilingScope(cmd, m_SSSBufferSampler))
            {
                var cameraData = renderingData.cameraData;
                var camera = cameraData.camera;

                // Setup MRT: Target0=ColorBuffer, Target1=Diffuse, Target2=SSSBuffer
                // Use camera's active color texture for Target0
                RTHandle colorTarget = cameraData.renderer.cameraColorTargetHandle;
                RTHandle depthTarget = cameraData.renderer.cameraDepthTargetHandle;

                // Clear diffuse and SSS buffers to black (following URP pattern)
                CoreUtils.SetRenderTarget(cmd, m_DiffuseRT, ClearFlag.Color, Color.black);
                CoreUtils.SetRenderTarget(cmd, m_SSSBufferRT, ClearFlag.Color, Color.black);

                // Setup MRT with depth buffer (using cached array to avoid GC)
                m_MRTArray[0] = colorTarget.nameID;
                m_MRTArray[1] = m_DiffuseRT.nameID;
                m_MRTArray[2] = m_SSSBufferRT.nameID;
                CoreUtils.SetRenderTarget(cmd, m_MRTArray, depthTarget.nameID, ClearFlag.None);

                // Execute command buffer before DrawRenderers (URP standard practice)
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Create drawing settings for SSSBuffer pass (following Unity 2022 DrawObjectsPass pattern)
                var sortFlags = cameraData.defaultOpaqueSortFlags;
                DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(s_SSSBufferShaderTagList, ref renderingData, sortFlags);

                // Draw renderers
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings);

                // Set global textures for compute shaders
                cmd.SetGlobalTexture(SSSShaderIDs.k_SSSDiffuseLightingID, m_DiffuseRT);
                cmd.SetGlobalTexture(SSSShaderIDs.k_SSSBufferTextureID, m_SSSBufferRT);
            }
        }

        /// <summary>
        /// Build coarse stencil buffer and resolve MSAA if needed
        /// </summary>
        private void ExecuteBuildCoarseStencilAndResolve(CommandBuffer cmd, RTHandle depthStencilBuffer, 
            RenderTextureDescriptor cameraDescriptor, int viewCount, out Vector4 coarseStencilBufferSize, 
            out RTHandle resolvedDepthBuffer)
        {
            using (new ProfilingScope(cmd, m_ResolveStencilSampler))
            {
                bool MSAAEnabled = cameraDescriptor.msaaSamples > 1;
                int msaaSamples = cameraDescriptor.msaaSamples;

                // Calculate coarse stencil buffer dimensions (8x8 tile compression)
                int width = cameraDescriptor.width;
                int height = cameraDescriptor.height;
                int coarseStencilWidth = (width + 7) / 8;
                int coarseStencilHeight = (height + 7) / 8;

                // Calculate buffer size
                coarseStencilBufferSize = new Vector4(
                    coarseStencilWidth,
                    coarseStencilHeight,
                    1.0f / coarseStencilWidth,
                    1.0f / coarseStencilHeight);

                // Create or resize coarse stencil buffer
                int bufferSize = coarseStencilWidth * coarseStencilHeight * viewCount;
                if (m_CoarseStencilBuffer == null || m_CoarseStencilBuffer.count != bufferSize)
                {
                    m_CoarseStencilBuffer?.Release();
                    m_CoarseStencilBuffer = new ComputeBuffer(bufferSize, sizeof(uint), ComputeBufferType.Structured);
                }

                // Setup compute shader
                var cs = m_ResolveStencilCS;
                
                // Setup MSAA keywords
                CoreUtils.SetKeyword(cs, "MSAA2X", msaaSamples == 2);
                CoreUtils.SetKeyword(cs, "MSAA4X", msaaSamples == 4);
                CoreUtils.SetKeyword(cs, "MSAA8X", msaaSamples == 8);

                // Set keywords for coarse stencil and resolve
                CoreUtils.SetKeyword(cs, "COARSE_STENCIL", true);
                CoreUtils.SetKeyword(cs, "RESOLVE", MSAAEnabled);

                // Bind parameters
                cmd.SetComputeVectorParam(cs, SSSShaderIDs._CoarseStencilBufferSize, coarseStencilBufferSize);
                cmd.SetComputeBufferParam(cs, m_ResolveStencilKernel, SSSShaderIDs._CoarseStencilBuffer, m_CoarseStencilBuffer);
                cmd.SetComputeTextureParam(cs, m_ResolveStencilKernel, SSSShaderIDs._StencilTexture, 
                    depthStencilBuffer, 0, RenderTextureSubElement.Stencil);

                // Bind resolve outputs if MSAA is enabled
                if (MSAAEnabled)
                {
                    cmd.SetComputeTextureParam(cs, m_ResolveStencilKernel, SSSShaderIDs._DepthTexture, 
                        depthStencilBuffer, 0, RenderTextureSubElement.Depth);
                    cmd.SetComputeTextureParam(cs, m_ResolveStencilKernel, SSSShaderIDs._OutputStencilBuffer, 
                        m_ResolvedStencilRT);
                    cmd.SetComputeTextureParam(cs, m_ResolveStencilKernel, SSSShaderIDs._OutputDepthBuffer, 
                        m_ResolvedDepthRT);
                    
                    resolvedDepthBuffer = m_ResolvedDepthRT;
                }
                else
                {
                    resolvedDepthBuffer = depthStencilBuffer;
                }

                // Dispatch compute shader
                cmd.DispatchCompute(cs, m_ResolveStencilKernel, coarseStencilWidth, coarseStencilHeight, viewCount);
            }
        }

        /// <summary>
        /// Execute SSS filtering compute shader
        /// </summary>
        private void ExecuteSSSFiltering(CommandBuffer cmd, RenderTextureDescriptor cameraDescriptor, 
            RTHandle depthTexture, Vector4 coarseStencilBufferSize, int viewCount)
        {
            using (new ProfilingScope(cmd, m_SSSFilteringSampler))
            {
                if (m_FilterMode == SubsurfaceScattering.SSSFilterMode.SeparableFilter)
                {
                    ExecuteSSSFilteringSeparable(cmd, cameraDescriptor, depthTexture, viewCount);
                    return;
                }

                var cs = m_SubsurfaceScatteringCS;

                // Set keywords
                CoreUtils.SetKeyword(cs, "USE_DOWNSAMPLE", m_DownsampleSteps > 0);
                CoreUtils.SetKeyword(cs, "USE_SSS_OCCLUSION", m_SubsurfaceScatteringAttenuation);

                // Calculate tile count (16x16 thread groups)
                int numTilesX = (cameraDescriptor.width + 15) / 16;
                int numTilesY = (cameraDescriptor.height + 15) / 16;
                Vector2 viewportSize = new Vector2(cameraDescriptor.width, cameraDescriptor.height);

                // Step 1: Downsample if enabled
                if (m_DownsampleSteps > 0)
                {
                    int shift = m_DownsampleSteps - 1;
                    var downsampleCS = m_SubsurfaceScatteringDownsampleCS;
                    
                    cmd.SetComputeIntParam(downsampleCS, SSSShaderIDs._FrameCount, Time.frameCount);
                    cmd.SetComputeIntParam(downsampleCS, SSSShaderIDs._SssDownsampleSteps, m_DownsampleSteps);
                    cmd.SetComputeTextureParam(downsampleCS, m_SubsurfaceScatteringDownsampleKernel, 
                        SSSShaderIDs._SourceTexture, m_DiffuseRT);
                    cmd.SetComputeTextureParam(downsampleCS, m_SubsurfaceScatteringDownsampleKernel, 
                        SSSShaderIDs._OutputTexture, m_DownsampleRT);
                    
                    cmd.DispatchCompute(downsampleCS, m_SubsurfaceScatteringDownsampleKernel,
                        Mathf.Max(1, numTilesX >> shift),
                        Mathf.Max(1, numTilesY >> shift),
                        viewCount);
                }

                // Step 2: Setup main SSS compute parameters
                cmd.SetComputeIntParam(cs, SSSShaderIDs._SssSampleBudget, m_SampleBudget);
                cmd.SetComputeIntParam(cs, SSSShaderIDs._SssDownsampleSteps, m_DownsampleSteps);
               
                cmd.SetComputeFloatParam(cs, SSSShaderIDs._GlobalDetailPreservation, m_globalDetailPreservation);

                // Step 3: Bind input textures
                cmd.SetComputeTextureParam(cs, m_SubsurfaceScatteringKernel, SSSShaderIDs._DepthTexture, depthTexture);
                cmd.SetComputeTextureParam(cs, m_SubsurfaceScatteringKernel, SSSShaderIDs._IrradianceSource, m_DiffuseRT);
                
                if (m_DownsampleSteps > 0 && m_DownsampleRT != null)
                {
                    cmd.SetComputeTextureParam(cs, m_SubsurfaceScatteringKernel, 
                        SSSShaderIDs._IrradianceSourceDownsampled, m_DownsampleRT);
                }
                
                cmd.SetComputeTextureParam(cs, m_SubsurfaceScatteringKernel, SSSShaderIDs.SSSBufferTexture, m_SSSBufferRT);

                // Bind coarse stencil buffer
                cmd.SetComputeVectorParam(cs, SSSShaderIDs._CoarseStencilBufferSize, coarseStencilBufferSize);
                cmd.SetComputeBufferParam(cs, m_SubsurfaceScatteringKernel, SSSShaderIDs._CoarseStencilBuffer, 
                    m_CoarseStencilBuffer);

                // Step 4: Pack diffusion profile indices if occlusion is enabled
                if (m_SubsurfaceScatteringAttenuation && m_DiffusionProfileIndexRT != null)
                {
                    cmd.SetComputeTextureParam(cs, m_PackDiffusionProfileKernel, 
                        SSSShaderIDs._DiffusionProfileIndexTexture, m_DiffusionProfileIndexRT);
                    cmd.SetComputeTextureParam(cs, m_PackDiffusionProfileKernel, 
                        SSSShaderIDs.SSSBufferTexture, m_SSSBufferRT);
                    
                    int xGroupCount = (int)Mathf.Ceil(viewportSize.x / 2.0f / 8.0f);
                    int yGroupCount = (int)Mathf.Ceil(viewportSize.y / 8.0f);
                    
                    cmd.DispatchCompute(cs, m_PackDiffusionProfileKernel, xGroupCount, yGroupCount, viewCount);
                }

                // Bind diffusion profile index texture
                if (m_SubsurfaceScatteringAttenuation && m_DiffusionProfileIndexRT != null)
                {
                    cmd.SetComputeTextureParam(cs, m_SubsurfaceScatteringKernel, 
                        SSSShaderIDs._DiffusionProfileIndexTexture, m_DiffusionProfileIndexRT);
                }
                else
                {
                    // Set diffuse buffer as fallback when occlusion is disabled
                    cmd.SetComputeTextureParam(cs, m_SubsurfaceScatteringKernel, 
                        SSSShaderIDs._DiffusionProfileIndexTexture, m_DiffuseRT);
                }

                // Step 5: Execute main SSS filtering
                cmd.SetComputeTextureParam(cs, m_SubsurfaceScatteringKernel, 
                    SSSShaderIDs._CameraFilteringTexture, m_FilteringRT);
                
                // Clear filtering buffer
                CoreUtils.SetRenderTarget(cmd, m_FilteringRT, ClearFlag.Color, Color.clear);
                
                // Dispatch SSS filtering compute
                cmd.DispatchCompute(cs, m_SubsurfaceScatteringKernel, numTilesX, numTilesY, viewCount);
            }
        }

        /// <summary>
        /// Execute the two-pass separable SSS filter (horizontal then vertical).
        /// Writes final result to m_FilteringRT so ExecuteCombineLighting() works unchanged.
        /// </summary>
        private void ExecuteSSSFilteringSeparable(CommandBuffer cmd, RenderTextureDescriptor cameraDescriptor,
            RTHandle depthTexture, int viewCount)
        {
            var cs = m_SubsurfaceScatteringSeparableCS;
            if (cs == null || m_SSSKernelData == null) return;

            int width  = cameraDescriptor.width;
            int height = cameraDescriptor.height;

            // Set keywords
            CoreUtils.SetKeyword(cs, "USE_DOWNSAMPLE", m_DownsampleSteps > 0);

            // Tile count for 8×8 thread groups
            int tilesX = (width  + 7) / 8;
            int tilesY = (height + 7) / 8;

            // Bind kernel data
            cmd.SetComputeVectorArrayParam(cs, SSSShaderIDs._SSSKernel, m_SSSKernelData);
            cmd.SetComputeIntParam(cs, SSSShaderIDs._SSSKernelSize, m_SeparableKernelSize);
            cmd.SetComputeFloatParam(cs, SSSShaderIDs._GlobalDetailPreservation, m_globalDetailPreservation);
            cmd.SetComputeIntParam(cs, SSSShaderIDs._SssDownsampleSteps, m_DownsampleSteps);

            // Shared input textures
            cmd.SetComputeTextureParam(cs, m_SSSFilterHorizontalKernel, SSSShaderIDs._DepthTexture, depthTexture);
            cmd.SetComputeTextureParam(cs, m_SSSFilterHorizontalKernel, SSSShaderIDs._IrradianceSource, m_DiffuseRT);
            cmd.SetComputeTextureParam(cs, m_SSSFilterHorizontalKernel, SSSShaderIDs.SSSBufferTexture, m_SSSBufferRT);

            cmd.SetComputeTextureParam(cs, m_SSSFilterVerticalKernel, SSSShaderIDs._DepthTexture, depthTexture);
            cmd.SetComputeTextureParam(cs, m_SSSFilterVerticalKernel, SSSShaderIDs._IrradianceSource, m_DiffuseRT);
            cmd.SetComputeTextureParam(cs, m_SSSFilterVerticalKernel, SSSShaderIDs.SSSBufferTexture, m_SSSBufferRT);

            // Downsample pass
            if (m_DownsampleSteps > 0 && m_DownsampleRT != null)
            {
                int shift = m_DownsampleSteps - 1;
                var downsampleCS = m_SubsurfaceScatteringDownsampleCS;
                cmd.SetComputeIntParam(downsampleCS, SSSShaderIDs._FrameCount, UnityEngine.Time.frameCount);
                cmd.SetComputeIntParam(downsampleCS, SSSShaderIDs._SssDownsampleSteps, m_DownsampleSteps);
                cmd.SetComputeTextureParam(downsampleCS, m_SubsurfaceScatteringDownsampleKernel, SSSShaderIDs._SourceTexture, m_DiffuseRT);
                cmd.SetComputeTextureParam(downsampleCS, m_SubsurfaceScatteringDownsampleKernel, SSSShaderIDs._OutputTexture, m_DownsampleRT);
                int dsX = Mathf.Max(1, tilesX >> shift);
                int dsY = Mathf.Max(1, tilesY >> shift);
                cmd.DispatchCompute(downsampleCS, m_SubsurfaceScatteringDownsampleKernel, dsX, dsY, viewCount);

                cmd.SetComputeTextureParam(cs, m_SSSFilterHorizontalKernel, SSSShaderIDs._IrradianceSourceDownsampled, m_DownsampleRT);
                cmd.SetComputeTextureParam(cs, m_SSSFilterVerticalKernel, SSSShaderIDs._IrradianceSourceDownsampled, m_DownsampleRT);
            }

            // Horizontal pass → m_SeparableIntermediateRT
            cmd.SetComputeTextureParam(cs, m_SSSFilterHorizontalKernel, SSSShaderIDs._CameraFilteringTexture, m_SeparableIntermediateRT);
            cmd.DispatchCompute(cs, m_SSSFilterHorizontalKernel, tilesX, tilesY, viewCount);

            // Vertical pass: read from intermediate, write to m_FilteringRT
            cmd.SetComputeTextureParam(cs, m_SSSFilterVerticalKernel, SSSShaderIDs._SeparableInput, m_SeparableIntermediateRT);
            cmd.SetComputeTextureParam(cs, m_SSSFilterVerticalKernel, SSSShaderIDs._CameraFilteringTexture, m_FilteringRT);
            cmd.DispatchCompute(cs, m_SSSFilterVerticalKernel, tilesX, tilesY, viewCount);
        }

        /// <summary>
        /// Combine filtered diffuse lighting with color buffer (following URP Blitter pattern)
        /// </summary>
        private void ExecuteCombineLighting(CommandBuffer cmd, ref RenderingData renderingData)
        {
            using (new ProfilingScope(cmd, m_CombineLightingSampler))
            {
                if (m_CombineLightingMaterial == null)
                {
                    return;
                }

                var cameraData = renderingData.cameraData;
                RTHandle colorTarget = cameraData.renderer.cameraColorTargetHandle;
                RTHandle depthTarget = cameraData.renderer.cameraDepthTargetHandle;
                
                m_CombineLightingMaterial.SetTexture(SSSShaderIDs._IrradianceSource, m_FilteringRT);
                
                CoreUtils.SetRenderTarget(cmd, colorTarget, 
                    RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                    depthTarget,
                    RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
                
                Blitter.BlitTexture(cmd, m_FilteringRT, new Vector4(1, 1, 0, 0), m_CombineLightingMaterial, 0);
            }
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // Cleanup temporary global textures
            cmd.SetGlobalTexture(SSSShaderIDs.k_SSSDiffuseLightingID, (Texture)null);
            cmd.SetGlobalTexture(SSSShaderIDs.k_SSSBufferTextureID, (Texture)null);
            cmd.SetGlobalTexture(SSSShaderIDs.k_SSSFilteringTextureID, (Texture)null);
        }

        /// <summary>
        /// Get depth texture handle from renderer
        /// </summary>
        private RTHandle GetDepthTextureHandle(ref RenderingData renderingData)
        {
            // Try to get depth texture from renderer
            return renderingData.cameraData.renderer.cameraDepthTargetHandle;
        }

        /// <summary>
        /// Dispose all legacy resources
        /// </summary>
        public void DisposeLegacy()
        {
            m_DiffuseRT?.Release();
            m_SSSBufferRT?.Release();
            m_FilteringRT?.Release();
            m_DownsampleRT?.Release();
            m_DiffusionProfileIndexRT?.Release();
            m_ResolvedStencilRT?.Release();
            m_ResolvedDepthRT?.Release();
            m_SeparableIntermediateRT?.Release();
            m_CoarseStencilBuffer?.Release();

            m_DiffuseRT = null;
            m_SSSBufferRT = null;
            m_FilteringRT = null;
            m_DownsampleRT = null;
            m_DiffusionProfileIndexRT = null;
            m_ResolvedStencilRT = null;
            m_ResolvedDepthRT = null;
            m_SeparableIntermediateRT = null;
            m_CoarseStencilBuffer = null;
        }
    }
}

