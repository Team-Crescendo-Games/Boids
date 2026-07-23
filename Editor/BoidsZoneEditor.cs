using UnityEditor;
using UnityEngine;

namespace TeamCrescendo.Boids.Editor
{
    [CustomEditor(typeof(BoidsZone))]
    [CanEditMultipleObjects]
    public class BoidsZoneEditor : UnityEditor.Editor
    {
        private SerializedProperty typeProp;
        private SerializedProperty dimensionsProp;

        private void OnEnable()
        {
            typeProp = serializedObject.FindProperty("type");
            dimensionsProp = serializedObject.FindProperty("dimensions");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(typeProp);

            switch ((BoidsZone.ZoneType)typeProp.enumValueIndex)
            {
                case BoidsZone.ZoneType.InfiniteSlab:
                {
                    Vector3 dims = dimensionsProp.vector3Value;
                    float thickness = EditorGUILayout.FloatField(
                        new GUIContent("Thickness", "Extent along the local Y axis. X/Z are infinite."),
                        dims.y);
                    dimensionsProp.vector3Value = new Vector3(dims.x, thickness, dims.z);
                    break;
                }
                case BoidsZone.ZoneType.Box:
                {
                    EditorGUILayout.PropertyField(dimensionsProp,
                        new GUIContent("Dimensions", "XYZ extents of the box."));
                    break;
                }
                case BoidsZone.ZoneType.Sphere:
                {
                    Vector3 dims = dimensionsProp.vector3Value;
                    float radius = EditorGUILayout.FloatField(
                        new GUIContent("Radius"),
                        dims.x);
                    dimensionsProp.vector3Value = new Vector3(radius, dims.y, dims.z);
                    break;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
