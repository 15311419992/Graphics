using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal
{
    struct CachedSkyContext
    {
        public Type                 type;
        public SkyRenderingContext  renderingContext;
        public int                  hash;
        public int                  refCount;

        public void Reset()
        {
            // We keep around the renderer and the rendering context to avoid useless allocation if they get reused.
            hash = 0;
            refCount = 0;
            if (renderingContext != null)
                renderingContext.ClearAmbientProbe();
        }

        public void Cleanup()
        {
            Reset();

            if (renderingContext != null)
            {
                renderingContext.Cleanup();
                renderingContext = null;
            }
        }
    }

    class SkyManager
    {
        // TODO: Add to pipeline/renderer settings or update from RenderSettings.defaultReflectionResolution?
        const int k_Resolution = 128;

        Material m_StandardSkyboxMaterial; // This is the Unity standard skybox material. Used to pass the correct cubemap to Enlighten.
        SphericalHarmonicsL2 m_BlackAmbientProbe = new SphericalHarmonicsL2();
        Matrix4x4[] m_FacePixelCoordToViewDirMatrices = new Matrix4x4[6];

        // TODO: Release SkyUpdateContext and associated SkyRenderingContext for any Camera that doesn't render anymore.
        Dictionary<Camera, SkyUpdateContext> m_VisualSkiesCache = new Dictionary<Camera, SkyUpdateContext>();
        // TODO: Increase initial size to 2 when static sky is implemented.
        DynamicArray<CachedSkyContext> m_CachedSkyContexts = new DynamicArray<CachedSkyContext>(1);

        public void UpdateCurrentSkySettings(ref CameraData cameraData)
        {
            // TODO Editor preview camera

            var volumeStack = VolumeManager.instance.stack;

            cameraData.skyAmbientMode = volumeStack.GetComponent<VisualEnvironment>().skyAmbientMode.value;

            if (!m_VisualSkiesCache.ContainsKey(cameraData.camera))
            {
                m_VisualSkiesCache[cameraData.camera] = new SkyUpdateContext();
            }
            cameraData.visualSky = m_VisualSkiesCache[cameraData.camera];
            cameraData.visualSky.skySettings = GetSkySettings(volumeStack);

            // TODO Lighting override
            cameraData.lightingSky = cameraData.visualSky;
        }

        SkySettings GetSkySettings(VolumeStack stack)
        {
            var visualEnvironmet = stack.GetComponent<VisualEnvironment>();
            int skyID = visualEnvironmet.skyType.value;

            Type skyType;
            if (SkyTypesCatalog.skyTypesDict.TryGetValue(skyID, out skyType))
            {
                return stack.GetComponent(skyType) as SkySettings;
            }

            return null;
        }

        public void Build()
        {
            // TODO: Get the shader not from ForwardRenderer. SkyManager is managed by the pipeline.
            var urpRendererData = UniversalRenderPipeline.asset.scriptableRendererData;
            if (urpRendererData is ForwardRendererData forwardRendererData)
            {
                m_StandardSkyboxMaterial = CoreUtils.CreateEngineMaterial(forwardRendererData.shaders.skyboxCubemapPS);
            }

            for (int i = 0; i < 6; ++i)
            {
                var lookAt = Matrix4x4.LookAt(Vector3.zero, CoreUtils.lookAtList[i], CoreUtils.upVectorList[i]);
                var worldToView = lookAt * Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f)); // Need to scale -1.0 on Z to match what is being done in the camera.wolrdToCameraMatrix API. ...

                var cubemapScreenSize = new Vector4(k_Resolution, k_Resolution, 1.0f / k_Resolution, 1.0f / k_Resolution);
                m_FacePixelCoordToViewDirMatrices[i] = ComputePixelCoordToWorldSpaceViewDirectionMatrix(0.5f * Mathf.PI, Vector2.zero, cubemapScreenSize, worldToView, true);
            }
        }

        // TODO: It's copied from HDUtils. Move both to CoreUtils?
        Matrix4x4 ComputePixelCoordToWorldSpaceViewDirectionMatrix(float verticalFoV, Vector2 lensShift, Vector4 screenSize, Matrix4x4 worldToViewMatrix, bool renderToCubemap, float aspectRatio = -1)
        {
            aspectRatio = aspectRatio < 0 ? screenSize.x * screenSize.w : aspectRatio;

            // Compose the view space version first.
            // V = -(X, Y, Z), s.t. Z = 1,
            // X = (2x / resX - 1) * tan(vFoV / 2) * ar = x * [(2 / resX) * tan(vFoV / 2) * ar] + [-tan(vFoV / 2) * ar] = x * [-m00] + [-m20]
            // Y = (2y / resY - 1) * tan(vFoV / 2)      = y * [(2 / resY) * tan(vFoV / 2)]      + [-tan(vFoV / 2)]      = y * [-m11] + [-m21]

            float tanHalfVertFoV = Mathf.Tan(0.5f * verticalFoV);

            // Compose the matrix.
            float m21 = (1.0f - 2.0f * lensShift.y) * tanHalfVertFoV;
            float m11 = -2.0f * screenSize.w * tanHalfVertFoV;

            float m20 = (1.0f - 2.0f * lensShift.x) * tanHalfVertFoV * aspectRatio;
            float m00 = -2.0f * screenSize.z * tanHalfVertFoV * aspectRatio;

            if (renderToCubemap)
            {
                // Flip Y.
                m11 = -m11;
                m21 = -m21;
            }

            var viewSpaceRasterTransform = new Matrix4x4(new Vector4(m00, 0.0f, 0.0f, 0.0f),
                new Vector4(0.0f, m11, 0.0f, 0.0f),
                new Vector4(m20, m21, -1.0f, 0.0f),
                new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

            // Remove the translation component.
            var homogeneousZero = new Vector4(0, 0, 0, 1);
            worldToViewMatrix.SetColumn(3, homogeneousZero);

            // Flip the Z to make the coordinate system left-handed.
            worldToViewMatrix.SetRow(2, -worldToViewMatrix.GetRow(2));

            // Transpose for HLSL.
            return Matrix4x4.Transpose(worldToViewMatrix.transpose * viewSpaceRasterTransform);
        }

        public void Cleanup()
        {
            CoreUtils.Destroy(m_StandardSkyboxMaterial);

            for (int i = 0; i < m_CachedSkyContexts.size; ++i)
                m_CachedSkyContexts[i].Cleanup();
        }

        SphericalHarmonicsL2 GetAmbientProbe(SkyUpdateContext skyContext)
        {
            if (skyContext.IsValid() && IsCachedContextValid(skyContext))
            {
                ref var context = ref m_CachedSkyContexts[skyContext.cachedSkyRenderingContextId];
                return context.renderingContext.ambientProbe;
            }
            else
            {
                return m_BlackAmbientProbe;
            }
        }

        Texture GetSkyCubemap(SkyUpdateContext skyContext)
        {
            if (skyContext.IsValid() && IsCachedContextValid(skyContext))
            {
                ref var context = ref m_CachedSkyContexts[skyContext.cachedSkyRenderingContextId];
                return context.renderingContext.skyboxCubemapRT;
            }
            else
            {
                return CoreUtils.blackCubeTexture;
            }
        }

        Cubemap GetReflectionTexture(SkyUpdateContext skyContext)
        {
            if (skyContext.IsValid() && IsCachedContextValid(skyContext))
            {
                ref var context = ref m_CachedSkyContexts[skyContext.cachedSkyRenderingContextId];
                return context.renderingContext.skyboxCubemap;
            }
            else
            {
                return CoreUtils.blackCubeTexture;
            }
        }

        public void SetupAmbientProbe(ref CameraData cameraData)
        {
            // Working around GI current system
            // When using baked lighting, setting up the ambient probe should be sufficient => We only need to update RenderSettings.ambientProbe with either the static or visual sky ambient probe
            // When using real time GI. Enlighten will pull sky information from Skybox material. So in order for dynamic GI to work, we update the skybox material texture and then set the ambient mode to SkyBox
            // Problem: We can't check at runtime if realtime GI is enabled so we need to take extra care (see useRealtimeGI usage below)

            // Don't overwrite settings from the built-in sky if no lightingSky set.
            var skyContext = cameraData.lightingSky;
            if (!skyContext.IsValid())
                return;

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientProbe = GetAmbientProbe(cameraData.lightingSky);

            // TODO: Use static sky for realtime GI
            m_StandardSkyboxMaterial.SetTexture(SkyShaderConstants._Tex, GetSkyCubemap(cameraData.lightingSky));
            RenderSettings.skybox = m_StandardSkyboxMaterial; // Setup this material to be used by GI

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflection = GetReflectionTexture(cameraData.lightingSky);
        }

        void RenderSkyToCubemap(ref CameraData cameraData, CommandBuffer cmd)
        {
            var skyContext = cameraData.lightingSky;

            var renderingContext = m_CachedSkyContexts[skyContext.cachedSkyRenderingContextId].renderingContext;
            var renderer = skyContext.skyRenderer;

            var faceCameraData = cameraData;

            for (int i = 0; i < 6; ++i)
            {
                faceCameraData.pixelCoordToViewDirMatrix = m_FacePixelCoordToViewDirMatrices[i];

                CoreUtils.SetRenderTarget(cmd, renderingContext.skyboxCubemapRT, ClearFlag.None, 0, (CubemapFace)i);
                renderer.RenderSky(ref faceCameraData, cmd);
            }

            // Generate mipmap for our cubemap
            Debug.Assert(renderingContext.skyboxCubemapRT.rt.autoGenerateMips == false);
            cmd.GenerateMips(renderingContext.skyboxCubemapRT);

            // TODO: Convolution
            cmd.CopyTexture(renderingContext.skyboxCubemapRT, renderingContext.skyboxCubemap);
        }

        // We do our own hash here because Unity does not provide correct hash for builtin types
        // Moreover, we don't want to test every single parameters of the light so we filter them here in this specific function.
        int GetSunLightHashCode(Light light)
        {
            int hash = 13;

            unchecked
            {
                // Sun could influence the sky (like for procedural sky). We need to handle this possibility. If sun property change, then we need to update the sky
                hash = hash * 23 + light.transform.position.GetHashCode();
                hash = hash * 23 + light.transform.rotation.GetHashCode();
                hash = hash * 23 + light.color.GetHashCode();
                hash = hash * 23 + light.colorTemperature.GetHashCode();
                hash = hash * 23 + light.intensity.GetHashCode();

                // TODO Extra per-RP light parameters
            }

            return hash;
        }

        void AllocateNewRenderingContext(SkyUpdateContext skyContext, int slot, int newHash, in SphericalHarmonicsL2 previousAmbientProbe, string name)
        {
            Debug.Assert(m_CachedSkyContexts[slot].hash == 0);
            ref var context = ref m_CachedSkyContexts[slot];
            context.hash = newHash;
            context.refCount = 1;
            context.type = skyContext.skySettings.GetSkyRendererType();

            if (context.renderingContext != null)
            {
                context.renderingContext.Cleanup();
                context.renderingContext = null;
            }

            if (context.renderingContext == null)
                context.renderingContext = new SkyRenderingContext(k_Resolution, previousAmbientProbe, name);
            else
                context.renderingContext.UpdateAmbientProbe(previousAmbientProbe);
            skyContext.cachedSkyRenderingContextId = slot;
        }

        // Returns whether or not the data should be updated
        bool AcquireSkyRenderingContext(SkyUpdateContext updateContext, int newHash, string name = "", bool supportConvolution = true)
        {
            SphericalHarmonicsL2 cachedAmbientProbe = new SphericalHarmonicsL2();
            // Release the old context if needed.
            if (IsCachedContextValid(updateContext))
            {
                ref var cachedContext = ref m_CachedSkyContexts[updateContext.cachedSkyRenderingContextId];
                if (newHash != cachedContext.hash || updateContext.skySettings.GetSkyRendererType() != cachedContext.type)
                {
                    // When a sky just changes hash without changing renderer, we need to keep previous ambient probe to avoid flickering transition through a default black probe
                    if (updateContext.skySettings.GetSkyRendererType() == cachedContext.type)
                    {
                        cachedAmbientProbe = cachedContext.renderingContext.ambientProbe;
                    }

                    ReleaseCachedContext(updateContext.cachedSkyRenderingContextId);
                }
                else
                {
                    // If the hash hasn't changed, keep it.
                    return false;
                }
            }

            // Else allocate a new one
            int firstFreeContext = -1;
            for (int i = 0; i < m_CachedSkyContexts.size; ++i)
            {
                // Try to find a matching slot
                if (m_CachedSkyContexts[i].hash == newHash)
                {
                    m_CachedSkyContexts[i].refCount++;
                    updateContext.cachedSkyRenderingContextId = i;
                    updateContext.skyParametersHash = newHash;
                    return false;
                }

                // Find the first available slot in case we don't find a matching one.
                if (firstFreeContext == -1 && m_CachedSkyContexts[i].hash == 0)
                    firstFreeContext = i;
            }

            if (name == "")
                name = "SkyboxCubemap";

            if (firstFreeContext != -1)
            {
                AllocateNewRenderingContext(updateContext, firstFreeContext, newHash, cachedAmbientProbe, name);
            }
            else
            {
                int newContextId = m_CachedSkyContexts.Add(new CachedSkyContext());
                AllocateNewRenderingContext(updateContext, newContextId, newHash, cachedAmbientProbe, name);
            }

            return true;
        }

        void ReleaseCachedContext(int id)
        {
            if (id == -1)
                return;

            ref var cachedContext = ref m_CachedSkyContexts[id];

            // This can happen if 2 cameras use the same context and release it in the same frame.
            // The first release the context but the next one will still have this id.
            if (cachedContext.refCount == 0)
            {
                Debug.Assert(cachedContext.renderingContext == null); // Context should already have been cleaned up.
                return;
            }

            cachedContext.refCount--;
            if (cachedContext.refCount == 0)
                cachedContext.Cleanup();
        }

        bool IsCachedContextValid(SkyUpdateContext skyContext)
        {
            if (skyContext.skySettings == null) // Sky set to None
                return false;

            int id = skyContext.cachedSkyRenderingContextId;
            // When the renderer changes, the cached context is no longer valid so we sometimes need to check that.
            return id != -1 && (skyContext.skySettings.GetSkyRendererType() == m_CachedSkyContexts[id].type) && (m_CachedSkyContexts[id].hash != 0);
        }

        int ComputeSkyHash(SkyUpdateContext skyContext)
        {
            int skyHash = skyContext.skySettings.GetHashCode();

            return skyHash;
        }

        public void UpdateEnvironment(ref CameraData cameraData,
                                      CommandBuffer  cmd)
        {
            var skyContext = cameraData.lightingSky;

            if (skyContext.IsValid())
            {
                int skyHash = ComputeSkyHash(skyContext);
                bool forceUpdate = false;

                // Acquire the rendering context, if the context was invalid or the hash has changed, this will request for an update.
                forceUpdate |= AcquireSkyRenderingContext(skyContext, skyHash, "SkyboxCubemap");

                ref CachedSkyContext cachedContext = ref m_CachedSkyContexts[skyContext.cachedSkyRenderingContextId];
                var renderingContext = cachedContext.renderingContext;

                if (forceUpdate ||
                    (skyContext.skySettings.updateMode.value == EnvironmentUpdateMode.OnChanged && skyHash != skyContext.skyParametersHash) ||
                    (skyContext.skySettings.updateMode.value == EnvironmentUpdateMode.Realtime && skyContext.currentUpdateTime > skyContext.skySettings.updatePeriod.value))
                {
                    RenderSkyToCubemap(ref cameraData, cmd);

                    renderingContext.UpdateAmbientProbe(skyContext.skyRenderer.GetAmbientProbe(ref cameraData));

                    skyContext.skyParametersHash = skyHash;
                    skyContext.currentUpdateTime = 0.0f;

#if UNITY_EDITOR
                    // In the editor when we change the sky we want to make the GI dirty so when baking again the new sky is taken into account.
                    // Changing the hash of the rendertarget allow to say that GI is dirty
                    renderingContext.skyboxCubemapRT.rt.imageContentsHash = new Hash128((uint)skyContext.skySettings.GetHashCode(), 0, 0, 0);
#endif
                }
            }
            else
            {
                if (skyContext.cachedSkyRenderingContextId != -1)
                {
                    ReleaseCachedContext(skyContext.cachedSkyRenderingContextId);
                    skyContext.cachedSkyRenderingContextId = -1;
                }
            }
        }

        public static void PrerenderSky(ref CameraData cameraData, CommandBuffer cmd)
        {
            var skyContext = cameraData.visualSky;
            if (skyContext.IsValid())
            {
                // TODO
                skyContext.skyRenderer.PrerenderSky(ref cameraData, cmd);
            }
        }

        public static void RenderSky(ref CameraData cameraData, CommandBuffer cmd)
        {
            var skyContext = cameraData.visualSky;
            if (skyContext.IsValid())
            {
                // TODO
                skyContext.skyRenderer.RenderSky(ref cameraData, cmd);
            }
        }
    }

    public static class SkyShaderConstants
    {
        public static readonly int _Tex = Shader.PropertyToID("_Tex");
        public static readonly int _SkyIntensity = Shader.PropertyToID("_SkyIntensity");
        public static readonly int _PixelCoordToViewDirWS = Shader.PropertyToID("_PixelCoordToViewDirWS");
    }
}
