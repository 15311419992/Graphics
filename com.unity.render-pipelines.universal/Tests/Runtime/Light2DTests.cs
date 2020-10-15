using System.Collections;
using NUnit.Framework;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.TestTools;

namespace UnityEngine.Rendering.Universal.Tests
{
    [TestFixture]
    class MultipleObjectLight2DTests
    {
        GameObject m_TestObject1;
        GameObject m_TestObject2;
        GameObject m_TestObject3;
        GameObject m_TestObject4;
        GameObject m_TestObjectCached;

        [SetUp]
        public void Setup()
        {
            m_TestObject1 = new GameObject("Test Object 1");
            m_TestObject2 = new GameObject("Test Object 2");
            m_TestObject3 = new GameObject("Test Object 3");
            m_TestObject4 = new GameObject("Test Object 4");
            m_TestObjectCached = new GameObject("Test Object Cached");
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(m_TestObjectCached);
            Object.DestroyImmediate(m_TestObject4);
            Object.DestroyImmediate(m_TestObject3);
            Object.DestroyImmediate(m_TestObject2);
            Object.DestroyImmediate(m_TestObject1);
        }

        [Test]
        public void LightsAreSortedByLightOrder()
        {
            var light1 = m_TestObject1.AddComponent<Light2D>();
            var light2 = m_TestObject2.AddComponent<Light2D>();
            var light3 = m_TestObject3.AddComponent<Light2D>();

            light1.lightOrder = 1;
            light2.lightOrder = 2;
            light3.lightOrder = 0;

            var camera = m_TestObject4.AddComponent<Camera>();
            var cameraPos = camera.transform.position;
            light1.transform.position = cameraPos;
            light2.transform.position = cameraPos;
            light3.transform.position = cameraPos;

            light1.UpdateMesh(true);
            light1.UpdateBoundingSphere();
            light2.UpdateMesh(true);
            light2.UpdateBoundingSphere();
            light3.UpdateMesh(true);
            light3.UpdateBoundingSphere();

            var cullResult = new Light2DCullResult();
            var cullingParams = new ScriptableCullingParameters();
            camera.TryGetCullingParameters(out cullingParams);
            cullResult.SetupCulling(ref cullingParams, camera);

            Assert.AreSame(light3, cullResult.visibleLights[0]);
            Assert.AreSame(light1, cullResult.visibleLights[1]);
            Assert.AreSame(light2, cullResult.visibleLights[2]);
        }

        [Test]
        public void LightIsInVisibleListIfInCameraView()
        {
            var camera = m_TestObject1.AddComponent<Camera>();
            var light = m_TestObject2.AddComponent<Light2D>();
            light.transform.position = camera.transform.position;
            light.UpdateMesh(true);
            light.UpdateBoundingSphere();

            var cullResult = new Light2DCullResult();
            var cullingParams = new ScriptableCullingParameters();
            camera.TryGetCullingParameters(out cullingParams);
            cullResult.SetupCulling(ref cullingParams, camera);

            Assert.Contains(light, cullResult.visibleLights);
        }

        [Test]
        public void LightIsNotInVisibleListIfNotInCameraView()
        {
            var camera = m_TestObject1.AddComponent<Camera>();
            var light = m_TestObject2.AddComponent<Light2D>();
            light.transform.position = camera.transform.position + new Vector3(9999.0f, 0.0f, 0.0f);
            light.UpdateMesh(true);
            light.UpdateBoundingSphere();

            var cullResult = new Light2DCullResult();
            var cullingParams = new ScriptableCullingParameters();
            camera.TryGetCullingParameters(out cullingParams);
            cullResult.SetupCulling(ref cullingParams, camera);

            Assert.IsFalse(cullResult.visibleLights.Contains(light));
        }

        [Test]
        public void CachedMeshDataIsUpdatedOnChange()
        {
            var shapePath = new Vector3[4] { new Vector3( 0, 0, 0), new Vector3(1,0,0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) };
            var cachedMesh = m_TestObjectCached.AddComponent<LightCachedMeshData>();
            var light = m_TestObjectCached.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Freeform;

            cachedMesh.allowUpdateOnTests = true;
            light.SetShapePath(shapePath);
            light.UpdateMesh(true);

            Assert.AreEqual(true, cachedMesh.HasGeometryData() );
        }

        [UnityTest]
        public IEnumerator CachedMeshDataIsUsedOnRuntime()
        {
            var shapePath = new Vector3[4] { new Vector3( 0, 0, 0), new Vector3(1,0,0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) };
            var cachedMesh = m_TestObjectCached.AddComponent<LightCachedMeshData>();
            var light = m_TestObjectCached.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Freeform;
            cachedMesh.allowUpdateOnTests = true;
            light.SetShapePath(shapePath);
            light.UpdateMesh(true);

            yield return new EnterPlayMode();
            yield return null;

            // Check if Cached Data and the actual data are the same.
            Assert.AreEqual(true, cachedMesh.HasGeometryData());
            Assert.AreEqual(light.lightMesh.triangles.Length, cachedMesh.indices.Length );
            Assert.AreEqual(light.lightMesh.vertices.Length, cachedMesh.vertices.Length );

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CachedMeshDataIsOverriddenByRuntimeChanges()
        {
            var shapePath = new Vector3[4] { new Vector3( 0, 0, 0), new Vector3(1,0,0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) };
            var cachedMesh = m_TestObjectCached.AddComponent<LightCachedMeshData>();
            var light = m_TestObjectCached.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Freeform;
            cachedMesh.allowUpdateOnTests = true;
            light.SetShapePath(shapePath);
            light.UpdateMesh(true);

            yield return new EnterPlayMode();
            yield return null;

            // Check if Cached Data and the actual data are the same.
            Assert.AreEqual(true, cachedMesh.HasGeometryData());
            Assert.AreEqual(light.lightMesh.triangles.Length, cachedMesh.indices.Length );
            Assert.AreEqual(light.lightMesh.vertices.Length, cachedMesh.vertices.Length );

            yield return null;

            // Simulate Runtime Behavior.
            cachedMesh.allowUpdateOnTests = false;
            cachedMesh.requiresUpload = false;
            var shapePathChanged = new Vector3[5] { new Vector3( 0, 0, 0), new Vector3(1,0,0), new Vector3(1, 1, 0), new Vector3(0.5f, 1.5f, 0), new Vector3(0, 1, 0) };
            light.SetShapePath(shapePathChanged);
            light.UpdateMesh(false);

            // Check if Cached Data and the actual data are no longer the same. (We don't save cache on Runtime)
            Assert.AreNotEqual(light.lightMesh.triangles.Length, cachedMesh.indices.Length );
            Assert.AreNotEqual(light.lightMesh.vertices.Length, cachedMesh.vertices.Length );
            yield return new ExitPlayMode();
        }

    }
}
