using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class LevelEditorSceneRoot : MonoBehaviour
{
    public GameObject levelEditorLevelRoot;

#if UNITY_EDITOR
    private void OnEnable()
    {
        // Ascultă schimbarea stării PlayMode
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        // Curățăm evenimentul când obiectul e dezactivat
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (levelEditorLevelRoot == null)
            return;

        // Când începe play mode
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            levelEditorLevelRoot.SetActive(false);
            Debug.Log("Level Editor Root dezactivat (Play Mode)");
        }

        // Când revenim în editor
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            levelEditorLevelRoot.SetActive(true);
            Debug.Log("Level Editor Root reactivat (Edit Mode)");
        }
    }
#endif
}
