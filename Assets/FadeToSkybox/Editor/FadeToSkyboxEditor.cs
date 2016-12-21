//
// Custom editor for FadeToSkybox
//
using UnityEngine;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditor(typeof(FadeToSkybox))]
public class FadeToSkyboxEditor : Editor
{
    SerializedProperty _useRadialDistance;
    SerializedProperty _startDistance;

    void OnEnable()
    {
        _useRadialDistance = serializedObject.FindProperty("_useRadialDistance");
        _startDistance = serializedObject.FindProperty("_startDistance");
    }

    public override void OnInspectorGUI()
    {
        bool componentSupported = true;

        if (!FadeToSkybox.CheckSkybox())
        {
            EditorGUILayout.HelpBox("This component only supports cubed skyboxes.", MessageType.Warning);
            componentSupported = false;
        }

        if (!RenderSettings.fog)
        {
            EditorGUILayout.HelpBox("This component requires fog to be enabled (Window -> Lighting -> Fog).", MessageType.Warning);
            componentSupported = false;
        }

        if (componentSupported)
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_useRadialDistance);
            EditorGUILayout.PropertyField(_startDistance);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
