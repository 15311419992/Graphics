using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System.Linq;

// Include material common properties names
using static UnityEngine.Rendering.HighDefinition.HDMaterialProperties;

namespace UnityEditor.Rendering.HighDefinition
{
    /// <summary>
    /// ShaderGraph material UI block.
    /// This class will display every non-hidden properties inside a shader and thus it can also be used on non-shadergraph shaders.
    /// </summary>
    public class ShaderGraphUIBlock : MaterialUIBlock
    {
        /// <summary>ShaderGraph UI Block features.</summary>
        [Flags]
        public enum Features
        {
            /// <summary>Nothing is displayed.</summary>
            None = 0,
            /// <summary>Display the exposed properties.</summary>
            ExposedProperties = 1 << 1,
            /// <summary>Display the default exposed diffusion profile from the graph.</summary>
            DiffusionProfileAsset = 1 << 2,
            /// <summary>Display the shadow matte options.</summary>
            ShadowMatte = 1 << 5,
            /// <summary>Display all the Unlit fields.</summary>
            Unlit = ExposedProperties | ShadowMatte,
            /// <summary>Display all the fields.</summary>
            All = ~0,
        }

        internal static class Styles
        {
            public const string header = "Exposed Properties";
        }

        ExpandableBit  m_ExpandableBit;
        Features    m_Features;

        /// <summary>
        /// Construct the ShaderGraph material UI block.
        /// </summary>
        /// <param name="expandableBit">Bit index used to store the foldout state.</param>
        /// <param name="features">Features enabled in the block.</param>
        public ShaderGraphUIBlock(ExpandableBit expandableBit = ExpandableBit.ShaderGraph, Features features = Features.All)
        {
            m_ExpandableBit = expandableBit;
            m_Features = features;
        }

        /// <summary>
        /// Use this function to load the material properties you need in your block.
        /// </summary>
        public override void LoadMaterialProperties() {}

        /// <summary>
        /// Renders the properties in your block.
        /// </summary>
        public override void OnGUI()
        {
            using (var header = new MaterialHeaderScope(Styles.header, (uint)m_ExpandableBit, materialEditor))
            {
                if (header.expanded)
                    DrawShaderGraphGUI();
            }
        }

        MaterialProperty[] oldProperties;

        bool CheckPropertyChanged(MaterialProperty[] properties)
        {
            bool propertyChanged = false;

            if (oldProperties != null)
            {
                // Check if shader was changed (new/deleted properties)
                if (properties.Length != oldProperties.Length)
                {
                    propertyChanged = true;
                }
                else
                {
                    for (int i = 0; i < properties.Length; i++)
                    {
                        if (properties[i].type != oldProperties[i].type)
                            propertyChanged = true;
                        if (properties[i].displayName != oldProperties[i].displayName)
                            propertyChanged = true;
                        if (properties[i].flags != oldProperties[i].flags)
                            propertyChanged = true;
                        if (properties[i].name != oldProperties[i].name)
                            propertyChanged = true;
                        if (properties[i].floatValue != oldProperties[i].floatValue)
                            propertyChanged = true;
                        if (properties[i].vectorValue != oldProperties[i].vectorValue)
                            propertyChanged = true;
                        if (properties[i].colorValue != oldProperties[i].colorValue)
                            propertyChanged = true;
                        if (properties[i].textureValue != oldProperties[i].textureValue)
                            propertyChanged = true;
                    }
                }
            }

            oldProperties = properties;

            return propertyChanged;
        }

        void DrawShaderGraphGUI()
        {
            // Filter out properties we don't want to draw:
            if ((m_Features & Features.ExposedProperties) != 0)
                PropertiesDefaultGUI(properties);

            // If we change a property in a shadergraph, we trigger a material keyword reset
            if (CheckPropertyChanged(properties))
            {
                foreach (var material in materials)
                    HDShaderUtils.ResetMaterialKeywords(material);
            }

            if ((m_Features & Features.DiffusionProfileAsset) != 0)
                DrawDiffusionProfileUI();

            if ((m_Features & Features.ShadowMatte) != 0 && materials.All(m => m.HasProperty(kShadowMatteFilter)))
                DrawShadowMatteToggle();
        }

        /// <summary>
        /// Draw the material properties
        /// </summary>
        /// <param name="properties">List of Material Properties to draw</param>
        protected void PropertiesDefaultGUI(MaterialProperty[] properties)
        {
            for (var i = 0; i < properties.Length; i++)
            {
                if ((properties[i].flags & (MaterialProperty.PropFlags.HideInInspector | MaterialProperty.PropFlags.PerRendererData)) != 0)
                    continue;

                float h = materialEditor.GetPropertyHeight(properties[i], properties[i].displayName);
                Rect r = EditorGUILayout.GetControlRect(true, h, EditorStyles.layerMaskField);

                materialEditor.ShaderProperty(r, properties[i], properties[i].displayName);
            }
        }

        /// <summary>
        /// Draw the Shadow Matte settings (Only available on Unlit materials)
        /// </summary>
        protected void DrawShadowMatteToggle()
        {
            uint exponent = 0b10000000; // 0 as exponent
            uint mantissa = 0x007FFFFF;

            float value = materials[0].GetFloat(HDMaterialProperties.kShadowMatteFilter);
            uint uValue = HDShadowUtils.Asuint(value);
            uint filter = uValue & mantissa;

            bool shadowFilterPoint  = (filter & (uint)LightFeatureFlags.Punctual)       != 0;
            bool shadowFilterDir    = (filter & (uint)LightFeatureFlags.Directional)    != 0;
            bool shadowFilterRect   = (filter & (uint)LightFeatureFlags.Area)           != 0;
            uint finalFlag = 0x00000000;
            finalFlag |= EditorGUILayout.Toggle("Point/Spot Shadow",    shadowFilterPoint) ? (uint)LightFeatureFlags.Punctual    : 0x00000000u;
            finalFlag |= EditorGUILayout.Toggle("Directional Shadow",   shadowFilterDir)   ? (uint)LightFeatureFlags.Directional : 0x00000000u;
            finalFlag |= EditorGUILayout.Toggle("Area Shadow",          shadowFilterRect)  ? (uint)LightFeatureFlags.Area        : 0x00000000u;
            finalFlag &= mantissa;
            finalFlag |= exponent;

            materials[0].SetFloat(HDMaterialProperties.kShadowMatteFilter, HDShadowUtils.Asfloat(finalFlag));
        }

        /// <summary>
        /// Draw the built-in exposed diffusion profile when a material have sub-surface scattering or transmission.
        /// </summary>
        protected void DrawDiffusionProfileUI()
        {
            if (DiffusionProfileMaterialUI.IsSupported(materialEditor))
                DiffusionProfileMaterialUI.OnGUI(materialEditor, FindProperty("_DiffusionProfileAsset"), FindProperty("_DiffusionProfileHash"), 0);
        }
    }
}
