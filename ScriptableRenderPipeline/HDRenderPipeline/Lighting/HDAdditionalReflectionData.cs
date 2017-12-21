﻿namespace UnityEngine.Experimental.Rendering
{
    public enum ReflectionInfluenceShape { Box, Sphere };

    [RequireComponent(typeof(ReflectionProbe), typeof(MeshFilter), typeof(MeshRenderer))]
    public class HDAdditionalReflectionData : MonoBehaviour
    {
#pragma warning disable 414 // CS0414 The private field '...' is assigned but its value is never used
        // We can't rely on Unity for our additional data, we need to version it ourself.
        [SerializeField]
        float m_Version = 1.0f;
#pragma warning restore 414

        public ReflectionInfluenceShape influenceShape;
        [Range(0.0f,1.0f)]
        public float dimmer = 1.0f;
        public float influenceSphereRadius = 3.0f;
        public float sphereReprojectionVolumeRadius = 1.0f;
        public bool useSeparateProjectionVolume = false;
        public Vector3 boxReprojectionVolumeSize = Vector3.one;
        public Vector3 boxReprojectionVolumeCenter = Vector3.zero;
        public float maxSearchDistance = 8.0f;
        public Texture previewCubemap;
        public Vector3 blendDistance = Vector3.zero;
        public Vector3 blendDistance2 = Vector3.zero;
        public Vector3 blendNormalDistance = Vector3.zero;
        public Vector3 blendNormalDistance2 = Vector3.zero;

        public Vector3 boxBlendCenterOffset { get { return (blendDistance2 - blendDistance) * 0.5f; } }
        public Vector3 boxBlendSizeOffset { get { return -(blendDistance + blendDistance2); } }
        public Vector3 boxBlendNormalCenterOffset { get { return (blendNormalDistance2 - blendNormalDistance) * 0.5f; } }
        public Vector3 boxBlendNormalSizeOffset { get { return -(blendNormalDistance + blendNormalDistance2); } }


        public float sphereBlendRadiusOffset { get { return -blendDistance.x; } }
        public float sphereBlendNormalRadiusOffset { get { return -blendNormalDistance.x; } }
    }
}
