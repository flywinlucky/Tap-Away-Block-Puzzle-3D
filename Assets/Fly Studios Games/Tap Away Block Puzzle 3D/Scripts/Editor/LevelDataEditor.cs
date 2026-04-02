using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (target == null) return;
        serializedObject.Update();
        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (prop.name == "blocks" || prop.name == "customGridLength" || prop.name == "customGridHeight" || prop.name == "seed") 
                continue; // Sărim variabilele ascunse
            EditorGUILayout.PropertyField(prop, true);
        }
        serializedObject.ApplyModifiedProperties();
    }
}