using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEditorInternal;

namespace UnityEngine.Rendering.HighDefinition
{
    /// <summary>
    /// </summary>
    [CustomEditor(typeof(EllipsoidOccluder))]
    public class EllipsoidOccluderEditor : Editor
    {
        static Color color = new Color(127.0f/255.0f, 121.0f/255.0f, 156.0f/255.0f);

        static EditMode.SceneViewEditMode[] k_EditModes = new EditMode.SceneViewEditMode[]{
            (EditMode.SceneViewEditMode)100, (EditMode.SceneViewEditMode)101, (EditMode.SceneViewEditMode)102
        };
        static GUIContent[] k_ModesContent;

        SerializedProperty centerOS, radiusOS, anglesOS, scalingOS, influenceRadiusScale;

        void OnEnable()
        {
            centerOS = serializedObject.FindProperty("centerOS");
            radiusOS = serializedObject.FindProperty("radiusOS");
            anglesOS = serializedObject.FindProperty("anglesOS");
            scalingOS = serializedObject.FindProperty("scalingOS");
            influenceRadiusScale = serializedObject.FindProperty("influenceRadiusScale");

            if (k_ModesContent == null)
                k_ModesContent = new GUIContent[]{
                    EditorGUIUtility.TrIconContent("MoveTool", "Translate."),
                    EditorGUIUtility.TrIconContent("RotateTool", "Rotate."),
                    EditorGUIUtility.TrIconContent("ScaleTool", "Scale.")
                };
        }

        internal static Func<Bounds> GetBoundsGetter(Editor o)
        {
            return () =>
            {
                var bounds = new Bounds();
                var rp = ((Component)o.target).transform;
                var b = rp.position;
                bounds.Encapsulate(b);
                return bounds;
            };
        }

        public override void OnInspectorGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditMode.DoInspectorToolbar(k_EditModes, k_ModesContent, GetBoundsGetter(this), this);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            DrawDefaultInspector();
        }

        private void DrawEllipsoid(Color color)
        {
            Handles.color = color;
            Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 1.0f);
            Handles.DrawWireDisc(Vector3.zero, Vector3.up, 1.0f);
            Handles.DrawWireDisc(Vector3.zero, Vector3.right, 1.0f);
        }

        public void OnSceneGUI()
        {
            Transform tr = (target as MonoBehaviour).transform;
            Quaternion rot = Quaternion.Euler(anglesOS.vector3Value);

            serializedObject.Update();

            Handles.matrix = (target as EllipsoidOccluder).TRS;
            Handles.color = Color.red;
            Handles.DrawLine(Vector3.zero, Vector3.forward);

            DrawEllipsoid(color);

            Vector3 lossyScale = tr.lossyScale;
            float influenceRadius = Mathf.Max(Mathf.Max(lossyScale.x, lossyScale.y), lossyScale.z) * influenceRadiusScale.floatValue * radiusOS.floatValue;
            influenceRadius *= Mathf.Max(1.0f, scalingOS.floatValue);

            Handles.matrix = Matrix4x4.TRS(tr.TransformPoint(centerOS.vector3Value), (tr.rotation * rot).normalized, Vector3.one * influenceRadius);
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            DrawEllipsoid(Color.blue);

            Handles.color = Color.white;
            Handles.matrix = Matrix4x4.TRS(tr.TransformPoint(Vector3.zero), tr.rotation, Vector3.one);
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            var mode = ArrayUtility.IndexOf(k_EditModes, EditMode.editMode);
            if (EditMode.editMode == k_EditModes[0])
            {
                EditorGUI.BeginChangeCheck();
                Vector3 new_centerOS = Handles.PositionHandle(centerOS.vector3Value, rot);
                if (EditorGUI.EndChangeCheck())
                    centerOS.vector3Value = new_centerOS;
            }
            else if (EditMode.editMode == k_EditModes[1])
            {
                EditorGUI.BeginChangeCheck();
                rot = Handles.RotationHandle(rot, centerOS.vector3Value);
                if (EditorGUI.EndChangeCheck())
                    anglesOS.vector3Value = rot.eulerAngles;
            }
            else if (EditMode.editMode == k_EditModes[2])
            {
                EditorGUI.BeginChangeCheck();
                float scale = Handles.ScaleSlider(scalingOS.floatValue, centerOS.vector3Value, rot * Vector3.forward, rot, 1.0f, 1.0f);
                if (EditorGUI.EndChangeCheck())
                    scalingOS.floatValue = scale;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
