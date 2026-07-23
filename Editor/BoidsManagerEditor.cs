using UnityEditor;
using UnityEngine;

namespace TeamCrescendo.Boids.Editor
{
    [CustomEditor(typeof(BoidsManager))]
    public class BoidsManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            Rect lineRect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(lineRect, new Color(0.5f, 0.5f, 0.5f, 1f));
            EditorGUILayout.Space();

            if (GUILayout.Button("Get Force Providers In Children"))
            {
                serializedObject.Update();
                FillFromChildren<BoidsForceProvider>(serializedObject.FindProperty("forceProviders"));
                serializedObject.ApplyModifiedProperties();
            }

            if (GUILayout.Button("Get Zones In Children"))
            {
                serializedObject.Update();
                FillFromChildren<BoidsZone>(serializedObject.FindProperty("zones"));
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void FillFromChildren<T>(SerializedProperty listProp) where T : Component
        {
            T[] found = ((BoidsManager)target).GetComponentsInChildren<T>(true);
            listProp.arraySize = found.Length;
            for (int i = 0; i < found.Length; i++)
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        }
    }
}
