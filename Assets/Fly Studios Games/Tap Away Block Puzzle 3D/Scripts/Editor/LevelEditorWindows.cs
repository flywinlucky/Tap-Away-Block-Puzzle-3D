using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelEditorWindow : EditorWindow
{
    private const string LevelEditorScenePath = "Assets/Fly Studios Games/Tap Away Block Puzzle 3D/Scenes/Level Editor.unity";
    private const string GameScenePath = "Assets/Fly Studios Games/Tap Away Block Puzzle 3D/Scenes/Game Scene.unity";
    private const string SceneRootName = "LevelEditorSceneRoot";

    private const string PrefsBlockPrefabPathKey = "LevelEditor.BlockPrefabPath";
    private const string PrefsGridUnitKey = "LevelEditor.GridUnitSize";
    private const string PrefsAutoOpenSceneKey = "LevelEditor.AutoOpenScene";
    private const string PrefsAutoGenerateKey = "LevelEditor.AutoGenerate";
    private const string PrefsAutoSaveAssetKey = "LevelEditor.AutoSaveAsset";
    private const string PrefsAutoSavePrefabKey = "LevelEditor.AutoSavePrefab";
    private const string PrefsSceneSyncIntervalKey = "LevelEditor.SceneSyncInterval";

    private const double AutoSaveDelaySeconds = 0.7d;

    private LevelData _currentLevel;
    private GameObject _blockPrefab;
    private List<LevelData> _allLevels = new List<LevelData>();
    private Vector2 _levelsScroll;

    private float _gridUnitSize = 0.5f;
    private bool _autoOpenEditorScene = true;
    private bool _autoGenerateOnSettingsChange = true;
    private bool _autoSaveLevelAsset = true;
    private bool _autoSavePrefabSnapshot = true;
    private float _sceneSyncIntervalSeconds = 0.25f;

    private bool _isDirty;
    private double _nextAutoSaveTime;
    private double _nextSceneSyncTime;

    private Scene _editorScene;
    private bool _editorSceneOpen;
    private GameObject _sceneRoot;
    private bool _suppressSceneSync;
    private int _lastSceneHash;

    private PreviewRenderUtility _previewUtility;
    private readonly List<GameObject> _previewInstances = new List<GameObject>();
    private bool _previewNeedsRebuild = true;
    private Vector2 _previewOrbit = new Vector2(135f, 25f);
    private float _previewDistance = 12f;

    [MenuItem("Tools/Tap Away Block Puzzle 3D/Level Editor")]
    public static void ShowWindow()
    {
        GetWindow<LevelEditorWindow>("Level Editor");
    }

    private void OnEnable()
    {
        LoadPrefs();
        RefreshLevelList();

        if (_currentLevel == null && _allLevels.Count > 0)
        {
            _currentLevel = _allLevels[0];
        }

        InitializePreview();

        EditorApplication.update -= EditorUpdate;
        EditorApplication.update += EditorUpdate;

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        if (_autoOpenEditorScene && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OpenLevelInScene();
        }

        RequestPreviewRebuild();
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

        if (_isDirty && _currentLevel != null)
        {
            if (_autoSaveLevelAsset)
            {
                SaveChangesInternal(true);
            }
            else
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Unsaved changes",
                    "Save changes before closing the Level Editor?",
                    "Save",
                    "Don't Save",
                    "Cancel");

                if (choice == 0)
                {
                    SaveChangesInternal(true);
                }
            }
        }

        if (_editorSceneOpen && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            CloseEditorScene(true);
        }

        DisposePreview();
        SavePrefs();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            RequestPreviewRebuild();
            Repaint();
        }
    }

    private void OnGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Level editing is disabled in Play Mode.", MessageType.Warning);
            return;
        }

        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawLevelListPanel();
        DrawMainPanel();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("New Level", EditorStyles.toolbarButton))
        {
            CreateNewLevelAsset();
        }

        GUI.enabled = _currentLevel != null;
        if (GUILayout.Button("Save", EditorStyles.toolbarButton))
        {
            SaveChangesInternal(true);
        }

        if (GUILayout.Button("Generate", EditorStyles.toolbarButton))
        {
            GenerateCurrentLevel(true);
        }
        GUI.enabled = true;

        if (GUILayout.Button("Open Editor Scene", EditorStyles.toolbarButton))
        {
            OpenLevelInScene();
        }

        if (GUILayout.Button("Focus Scene", EditorStyles.toolbarButton))
        {
            FocusSceneView();
        }

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
        {
            RefreshLevelList();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLevelListPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(270f), GUILayout.ExpandHeight(true));
        EditorGUILayout.LabelField("Levels", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("New", GUILayout.Width(80f)))
        {
            CreateNewLevelAsset();
        }

        GUI.enabled = _currentLevel != null;
        if (GUILayout.Button("Delete", GUILayout.Width(80f)))
        {
            DeleteCurrentLevelAsset();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);

        _levelsScroll = EditorGUILayout.BeginScrollView(_levelsScroll, GUILayout.ExpandHeight(true));
        for (int i = 0; i < _allLevels.Count; i++)
        {
            LevelData level = _allLevels[i];
            if (level == null)
            {
                continue;
            }

            bool selected = level == _currentLevel;
            bool pressed = GUILayout.Toggle(selected, level.name, "Button");
            if (pressed && !selected)
            {
                TryChangeSelection(level);
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    private void DrawMainPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        EditorGUILayout.LabelField("Editor Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space(2f);

        LevelData levelSelection = (LevelData)EditorGUILayout.ObjectField("Current Level", _currentLevel, typeof(LevelData), false);
        if (levelSelection != _currentLevel)
        {
            TryChangeSelection(levelSelection);
        }

        GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField("Block Prefab", _blockPrefab, typeof(GameObject), false);
        if (newPrefab != _blockPrefab)
        {
            _blockPrefab = newPrefab;
            SavePrefs();
            RequestPreviewRebuild();

            if (_editorSceneOpen)
            {
                PopulateEditorSceneFromData();
            }
        }

        EditorGUI.BeginChangeCheck();
        _gridUnitSize = Mathf.Max(0.05f, EditorGUILayout.FloatField("Grid Unit Size", _gridUnitSize));
        _autoOpenEditorScene = EditorGUILayout.Toggle("Auto Open Scene", _autoOpenEditorScene);
        _autoGenerateOnSettingsChange = EditorGUILayout.Toggle("Auto Generate", _autoGenerateOnSettingsChange);
        _autoSaveLevelAsset = EditorGUILayout.Toggle("Auto Save Asset", _autoSaveLevelAsset);
        _autoSavePrefabSnapshot = EditorGUILayout.Toggle("Auto Save Prefab", _autoSavePrefabSnapshot);
        _sceneSyncIntervalSeconds = EditorGUILayout.Slider("Scene Sync Interval", _sceneSyncIntervalSeconds, 0.05f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            SavePrefs();
            RequestPreviewRebuild();
        }

        EditorGUILayout.Space(8f);

        if (_currentLevel == null)
        {
            EditorGUILayout.HelpBox("Select or create a level asset.", MessageType.Info);
            DrawPreviewArea();
            EditorGUILayout.EndVertical();
            return;
        }

        DrawLevelControls();
        DrawPreviewArea();

        EditorGUILayout.EndVertical();
    }

    private void DrawLevelControls()
    {
        EditorGUILayout.LabelField("Level Generation", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int newLength = EditorGUILayout.IntSlider("Grid Length", _currentLevel.customGridLength, 2, 10);
        int newHeight = EditorGUILayout.IntSlider("Grid Height", _currentLevel.customGridHeight, 2, 10);
        int newSeed = EditorGUILayout.IntField("Seed", _currentLevel.seed);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_currentLevel, "Modify level generation settings");
            _currentLevel.customGridLength = newLength;
            _currentLevel.customGridHeight = newHeight;
            _currentLevel.seed = newSeed;

            if (_autoGenerateOnSettingsChange)
            {
                GenerateCurrentLevel(false);
            }
            else
            {
                MarkLevelDirty();
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate Now", GUILayout.Height(28f)))
        {
            GenerateCurrentLevel(true);
        }

        GUI.enabled = _editorSceneOpen;
        if (GUILayout.Button("Pull Scene -> Data", GUILayout.Height(28f)))
        {
            PullSceneToDataNow();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = _editorSceneOpen;
        if (GUILayout.Button("Push Data -> Scene", GUILayout.Height(24f)))
        {
            PopulateEditorSceneFromData();
            FocusSceneView();
        }
        GUI.enabled = true;

        GUI.enabled = _blockPrefab != null;
        if (GUILayout.Button("Save Prefab Snapshot", GUILayout.Height(24f)))
        {
            SavePrefabSnapshotInternal(true);
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        List<BlockData> blocks = _currentLevel.GetBlocks();
        int blockCount = blocks != null ? blocks.Count : 0;
        EditorGUILayout.HelpBox(
            "Blocks: " + blockCount +
            " | Scene Sync: " + (_editorSceneOpen ? "ON" : "OFF") +
            " | Dirty: " + (_isDirty ? "YES" : "NO"),
            MessageType.None);
    }

    private void DrawPreviewArea()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Live Preview", EditorStyles.boldLabel);

        Rect previewRect = GUILayoutUtility.GetRect(10f, 280f, GUILayout.ExpandWidth(true));
        DrawPreview(previewRect);
    }

    private void GenerateCurrentLevel(bool immediateSave)
    {
        if (_currentLevel == null)
        {
            return;
        }

        Undo.RecordObject(_currentLevel, "Generate Level Data");
        _currentLevel.Generate();
        MarkLevelDirty();

        if (_editorSceneOpen)
        {
            PopulateEditorSceneFromData();
            FocusSceneView();
        }

        if (immediateSave)
        {
            SaveChangesInternal(true);
        }
    }

    private bool TryChangeSelection(LevelData newLevel)
    {
        if (newLevel == _currentLevel)
        {
            return true;
        }

        if (!ConfirmSaveIfNeeded())
        {
            return false;
        }

        _currentLevel = newLevel;
        _isDirty = false;

        RequestPreviewRebuild();
        SyncLevelManagerReference();

        if (_editorSceneOpen)
        {
            PopulateEditorSceneFromData();
            FocusSceneView();
        }

        Repaint();
        return true;
    }

    private bool ConfirmSaveIfNeeded()
    {
        if (!_isDirty || _currentLevel == null)
        {
            return true;
        }

        int choice = EditorUtility.DisplayDialogComplex(
            "Unsaved changes",
            "Save changes to " + _currentLevel.name + " before switching?",
            "Save",
            "Don't Save",
            "Cancel");

        if (choice == 0)
        {
            SaveChangesInternal(true);
            return true;
        }

        if (choice == 1)
        {
            _isDirty = false;
            return true;
        }

        return false;
    }

    private void MarkLevelDirty()
    {
        if (_currentLevel == null)
        {
            return;
        }

        EditorUtility.SetDirty(_currentLevel);
        _isDirty = true;
        RequestPreviewRebuild();
        SyncLevelManagerReference();

        if (_autoSaveLevelAsset)
        {
            _nextAutoSaveTime = EditorApplication.timeSinceStartup + AutoSaveDelaySeconds;
        }
    }

    private void SaveChangesInternal(bool manualSave)
    {
        if (_currentLevel == null)
        {
            return;
        }

        EditorUtility.SetDirty(_currentLevel);
        AssetDatabase.SaveAssets();
        _isDirty = false;

        if (_autoSavePrefabSnapshot)
        {
            SavePrefabSnapshotInternal(manualSave);
        }

        if (manualSave)
        {
            RefreshLevelList();
        }
    }

    private bool SavePrefabSnapshotInternal(bool showLogs)
    {
        if (_currentLevel == null || _blockPrefab == null)
        {
            return false;
        }

        string levelAssetPath = AssetDatabase.GetAssetPath(_currentLevel);
        if (string.IsNullOrEmpty(levelAssetPath))
        {
            return false;
        }

        string levelFolder = Path.GetDirectoryName(levelAssetPath);
        if (string.IsNullOrEmpty(levelFolder))
        {
            return false;
        }

        levelFolder = levelFolder.Replace("\\", "/");
        string prefabFolder = levelFolder + "/GeneratedPrefabs";
        EnsureFolderExists(prefabFolder);

        string prefabPath = prefabFolder + "/" + _currentLevel.name + ".prefab";

        bool temporaryRootCreated = false;
        GameObject rootToSave = null;

        if (_editorSceneOpen && _sceneRoot != null && _sceneRoot.transform.childCount > 0)
        {
            rootToSave = _sceneRoot;
        }
        else
        {
            rootToSave = BuildTemporaryRootFromLevelData();
            temporaryRootCreated = rootToSave != null;
        }

        if (rootToSave == null)
        {
            return false;
        }

        PrefabUtility.SaveAsPrefabAsset(rootToSave, prefabPath);

        if (temporaryRootCreated)
        {
            DestroyImmediate(rootToSave);
        }

        AssetDatabase.SaveAssets();

        if (showLogs)
        {
            Debug.Log("Prefab snapshot saved: " + prefabPath);
        }

        return true;
    }

    private void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parentPath = Path.GetDirectoryName(folderPath);
        if (string.IsNullOrEmpty(parentPath))
        {
            return;
        }

        parentPath = parentPath.Replace("\\", "/");
        EnsureFolderExists(parentPath);

        string folderName = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(folderName) && !AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }

    private GameObject BuildTemporaryRootFromLevelData()
    {
        if (_currentLevel == null || _blockPrefab == null)
        {
            return null;
        }

        GameObject tempRoot = new GameObject("LevelSnapshotRoot");
        tempRoot.hideFlags = HideFlags.HideAndDontSave;

        List<BlockData> blocks = _currentLevel.GetBlocks();
        if (blocks == null)
        {
            return tempRoot;
        }

        for (int i = 0; i < blocks.Count; i++)
        {
            BlockData data = blocks[i];
            GameObject instance = PrefabUtility.InstantiatePrefab(_blockPrefab) as GameObject;
            if (instance == null)
            {
                instance = Instantiate(_blockPrefab);
            }

            instance.transform.SetParent(tempRoot.transform, false);
            instance.transform.position = (Vector3)data.position * _gridUnitSize;
            instance.transform.rotation = GetStableLookRotation(data.direction) * data.randomVisualRotation;
            instance.name = "Block_" + i;
        }

        return tempRoot;
    }

    private void CreateNewLevelAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create New Level",
            "LevelData_New.asset",
            "asset",
            "Select destination for new LevelData asset.");

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        LevelData newLevel = ScriptableObject.CreateInstance<LevelData>();
        AssetDatabase.CreateAsset(newLevel, path);
        AssetDatabase.SaveAssets();

        RefreshLevelList();
        TryChangeSelection(newLevel);

        if (_autoGenerateOnSettingsChange)
        {
            GenerateCurrentLevel(true);
        }
        else
        {
            SaveChangesInternal(true);
        }

        Selection.activeObject = newLevel;
    }

    private void DeleteCurrentLevelAsset()
    {
        if (_currentLevel == null)
        {
            return;
        }

        string path = AssetDatabase.GetAssetPath(_currentLevel);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (!EditorUtility.DisplayDialog("Delete Level", "Delete " + _currentLevel.name + "?", "Delete", "Cancel"))
        {
            return;
        }

        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();

        _currentLevel = null;
        _isDirty = false;
        RefreshLevelList();

        if (_editorSceneOpen)
        {
            ClearSceneRootChildren();
        }

        RequestPreviewRebuild();
    }

    private void RefreshLevelList()
    {
        _allLevels.Clear();

        string[] guids = AssetDatabase.FindAssets("t:LevelData");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level != null)
            {
                _allLevels.Add(level);
            }
        }

        _allLevels.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        if (_currentLevel != null && !_allLevels.Contains(_currentLevel))
        {
            _currentLevel = null;
            _isDirty = false;
        }

        if (_currentLevel == null && _allLevels.Count > 0)
        {
            _currentLevel = _allLevels[0];
        }
    }

    private void OpenLevelInScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (_editorSceneOpen)
        {
            FocusSceneView();
            return;
        }

        if (!File.Exists(LevelEditorScenePath))
        {
            EditorUtility.DisplayDialog("Missing Scene", "Cannot find level editor scene at:\n" + LevelEditorScenePath, "OK");
            return;
        }

        _editorScene = EditorSceneManager.OpenScene(LevelEditorScenePath, OpenSceneMode.Single);
        _editorSceneOpen = true;

        EnsureSceneRoot();
        PopulateEditorSceneFromData();
        FocusSceneView();
    }

    private void CloseEditorScene(bool openGameScene)
    {
        if (!_editorSceneOpen)
        {
            return;
        }

        _editorSceneOpen = false;
        _sceneRoot = null;
        _lastSceneHash = 0;

        if (!openGameScene)
        {
            return;
        }

        if (File.Exists(GameScenePath))
        {
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        }
    }

    private void EnsureSceneRoot()
    {
        if (_sceneRoot != null)
        {
            return;
        }

        _sceneRoot = GameObject.Find(SceneRootName);
        if (_sceneRoot == null)
        {
            _sceneRoot = new GameObject(SceneRootName);
            if (_editorScene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(_sceneRoot, _editorScene);
            }
        }
    }

    private void ClearSceneRootChildren()
    {
        if (_sceneRoot == null)
        {
            return;
        }

        for (int i = _sceneRoot.transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(_sceneRoot.transform.GetChild(i).gameObject);
        }
    }

    private void PopulateEditorSceneFromData()
    {
        if (!_editorSceneOpen)
        {
            return;
        }

        EnsureSceneRoot();

        if (_sceneRoot == null)
        {
            return;
        }

        _suppressSceneSync = true;
        ClearSceneRootChildren();

        if (_currentLevel != null && _blockPrefab != null)
        {
            List<BlockData> blocks = _currentLevel.GetBlocks();
            int count = blocks != null ? blocks.Count : 0;

            for (int i = 0; i < count; i++)
            {
                BlockData data = blocks[i];
                GameObject instance = PrefabUtility.InstantiatePrefab(_blockPrefab, _editorScene) as GameObject;
                if (instance == null)
                {
                    instance = Instantiate(_blockPrefab);
                }

                instance.transform.SetParent(_sceneRoot.transform, false);
                instance.transform.position = (Vector3)data.position * _gridUnitSize;
                instance.transform.rotation = GetStableLookRotation(data.direction) * data.randomVisualRotation;
                instance.name = "Block_" + i;

                if (instance.GetComponent<Collider>() == null)
                {
                    instance.AddComponent<BoxCollider>();
                }
            }
        }

        _lastSceneHash = ComputeLevelHash(CaptureBlocksFromSceneRoot());
        _suppressSceneSync = false;
        RequestPreviewRebuild();
    }

    private void PullSceneToDataNow()
    {
        if (!_editorSceneOpen)
        {
            return;
        }

        List<BlockData> fromScene = CaptureBlocksFromSceneRoot();
        ApplyBlocksToCurrentLevel(fromScene);
        _lastSceneHash = ComputeLevelHash(fromScene);
        MarkLevelDirty();
    }

    private void EditorUpdate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;

        if (_editorSceneOpen && now >= _nextSceneSyncTime)
        {
            _nextSceneSyncTime = now + Mathf.Max(0.05f, _sceneSyncIntervalSeconds);
            TrySyncSceneToData();
        }

        if (_isDirty && _autoSaveLevelAsset && now >= _nextAutoSaveTime)
        {
            SaveChangesInternal(false);
        }
    }

    private void TrySyncSceneToData()
    {
        if (_suppressSceneSync || _currentLevel == null || _sceneRoot == null)
        {
            return;
        }

        List<BlockData> sceneBlocks = CaptureBlocksFromSceneRoot();
        int currentSceneHash = ComputeLevelHash(sceneBlocks);
        if (currentSceneHash == _lastSceneHash)
        {
            return;
        }

        ApplyBlocksToCurrentLevel(sceneBlocks);

        _lastSceneHash = currentSceneHash;
        MarkLevelDirty();
        Repaint();
    }

    private List<BlockData> CaptureBlocksFromSceneRoot()
    {
        List<BlockData> captured = new List<BlockData>();
        if (_sceneRoot == null)
        {
            return captured;
        }

        float safeGrid = Mathf.Max(0.0001f, _gridUnitSize);

        for (int i = 0; i < _sceneRoot.transform.childCount; i++)
        {
            Transform t = _sceneRoot.transform.GetChild(i);
            MoveDirection direction = GetDirectionFromForward(t.forward);
            Quaternion stable = GetStableLookRotation(direction);
            Quaternion randomVisual = Quaternion.Inverse(stable) * t.rotation;

            BlockData data = new BlockData
            {
                position = Vector3Int.RoundToInt(t.position / safeGrid),
                direction = direction,
                randomVisualRotation = randomVisual
            };

            captured.Add(data);
        }

        SortBlocks(captured);
        return captured;
    }

    private void ApplyBlocksToCurrentLevel(List<BlockData> blocks)
    {
        if (_currentLevel == null)
        {
            return;
        }

        List<BlockData> target = _currentLevel.GetBlocks();
        target.Clear();

        for (int i = 0; i < blocks.Count; i++)
        {
            BlockData src = blocks[i];
            target.Add(new BlockData
            {
                position = src.position,
                direction = src.direction,
                randomVisualRotation = src.randomVisualRotation
            });
        }

        EditorUtility.SetDirty(_currentLevel);
    }

    private void SyncLevelManagerReference()
    {
        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager == null)
        {
            return;
        }

        if (levelManager.runCurrentLevel == _currentLevel)
        {
            return;
        }

        Undo.RecordObject(levelManager, "Assign runCurrentLevel");
        levelManager.runCurrentLevel = _currentLevel;
        EditorUtility.SetDirty(levelManager);
    }

    private void FocusSceneView()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            return;
        }

        Bounds bounds = CalculateLevelBounds();
        float size = Mathf.Max(2f, bounds.size.magnitude * 1.2f);
        sceneView.LookAt(bounds.center, Quaternion.Euler(30f, -35f, 0f), size, false, true);
    }

    private Bounds CalculateLevelBounds()
    {
        if (_sceneRoot != null && _sceneRoot.transform.childCount > 0)
        {
            Renderer[] renderers = _sceneRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                return bounds;
            }
        }

        if (_currentLevel != null)
        {
            List<BlockData> blocks = _currentLevel.GetBlocks();
            if (blocks != null && blocks.Count > 0)
            {
                Bounds bounds = new Bounds((Vector3)blocks[0].position * _gridUnitSize, Vector3.zero);
                for (int i = 1; i < blocks.Count; i++)
                {
                    bounds.Encapsulate((Vector3)blocks[i].position * _gridUnitSize);
                }

                return bounds;
            }
        }

        return new Bounds(Vector3.zero, Vector3.one * 3f);
    }

    private void InitializePreview()
    {
        if (_previewUtility != null)
        {
            return;
        }

        _previewUtility = new PreviewRenderUtility();
        _previewUtility.cameraFieldOfView = 35f;
        _previewUtility.camera.nearClipPlane = 0.01f;
        _previewUtility.camera.farClipPlane = 1000f;
        _previewUtility.lights[0].intensity = 1.2f;
        _previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
        _previewUtility.lights[1].intensity = 1f;
    }

    private void DisposePreview()
    {
        ClearPreviewObjects();

        if (_previewUtility != null)
        {
            _previewUtility.Cleanup();
            _previewUtility = null;
        }
    }

    private void RequestPreviewRebuild()
    {
        _previewNeedsRebuild = true;
        Repaint();
    }

    private void ClearPreviewObjects()
    {
        for (int i = 0; i < _previewInstances.Count; i++)
        {
            if (_previewInstances[i] != null)
            {
                DestroyImmediate(_previewInstances[i]);
            }
        }

        _previewInstances.Clear();
    }

    private void RebuildPreviewObjects()
    {
        ClearPreviewObjects();

        if (_previewUtility == null || _currentLevel == null)
        {
            _previewNeedsRebuild = false;
            return;
        }

        List<BlockData> blocks = _currentLevel.GetBlocks();
        if (blocks == null || blocks.Count == 0)
        {
            _previewNeedsRebuild = false;
            return;
        }

        Material previewMaterial = GetPreviewMaterial();
        float scale = Mathf.Max(0.05f, _gridUnitSize * 0.9f);

        for (int i = 0; i < blocks.Count; i++)
        {
            BlockData data = blocks[i];
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.hideFlags = HideFlags.HideAndDontSave;

            Collider col = cube.GetComponent<Collider>();
            if (col != null)
            {
                DestroyImmediate(col);
            }

            cube.transform.position = (Vector3)data.position * _gridUnitSize;
            cube.transform.rotation = GetStableLookRotation(data.direction) * data.randomVisualRotation;
            cube.transform.localScale = Vector3.one * scale;

            if (previewMaterial != null)
            {
                MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = previewMaterial;
                }
            }

            _previewUtility.AddSingleGO(cube);
            _previewInstances.Add(cube);
        }

        _previewNeedsRebuild = false;
    }

    private Material GetPreviewMaterial()
    {
        if (_blockPrefab == null)
        {
            return null;
        }

        Renderer renderer = _blockPrefab.GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.sharedMaterial : null;
    }

    private void DrawPreview(Rect rect)
    {
        GUI.Box(rect, GUIContent.none);

        if (_previewUtility == null)
        {
            EditorGUI.LabelField(rect, "Preview unavailable.");
            return;
        }

        if (_previewNeedsRebuild)
        {
            RebuildPreviewObjects();
        }

        HandlePreviewInput(rect);

        if (_previewInstances.Count == 0)
        {
            EditorGUI.LabelField(rect, "Generate a level to see preview.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Bounds bounds = CalculatePreviewBounds();
        float minDistance = Mathf.Max(2f, bounds.extents.magnitude * 2.4f);
        float distance = Mathf.Max(minDistance, _previewDistance);

        Quaternion orbit = Quaternion.Euler(_previewOrbit.y, _previewOrbit.x, 0f);
        Vector3 cameraDirection = orbit * Vector3.back;
        Vector3 cameraPosition = bounds.center + cameraDirection * distance;

        _previewUtility.BeginPreview(rect, GUIStyle.none);

        Camera cam = _previewUtility.camera;
        cam.transform.position = cameraPosition;
        cam.transform.rotation = Quaternion.LookRotation(bounds.center - cameraPosition, Vector3.up);
        cam.clearFlags = CameraClearFlags.Color;
        cam.backgroundColor = new Color(0.14f, 0.14f, 0.16f, 1f);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = Mathf.Max(100f, distance * 6f);

        _previewUtility.Render();
        Texture previewTexture = _previewUtility.EndPreview();
        GUI.DrawTexture(rect, previewTexture, ScaleMode.StretchToFill, false);
    }

    private void HandlePreviewInput(Rect rect)
    {
        Event e = Event.current;
        if (!rect.Contains(e.mousePosition))
        {
            return;
        }

        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            _previewOrbit.x += e.delta.x * 0.6f;
            _previewOrbit.y -= e.delta.y * 0.6f;
            _previewOrbit.y = Mathf.Clamp(_previewOrbit.y, -85f, 85f);
            e.Use();
            Repaint();
        }

        if (e.type == EventType.ScrollWheel)
        {
            _previewDistance = Mathf.Clamp(_previewDistance + e.delta.y * 0.4f, 2f, 250f);
            e.Use();
            Repaint();
        }
    }

    private Bounds CalculatePreviewBounds()
    {
        if (_previewInstances.Count == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one * 2f);
        }

        MeshRenderer firstRenderer = _previewInstances[0] != null ? _previewInstances[0].GetComponent<MeshRenderer>() : null;
        Bounds bounds = firstRenderer != null
            ? firstRenderer.bounds
            : new Bounds(_previewInstances[0].transform.position, Vector3.one);

        for (int i = 1; i < _previewInstances.Count; i++)
        {
            GameObject go = _previewInstances[i];
            if (go == null)
            {
                continue;
            }

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            else
            {
                bounds.Encapsulate(go.transform.position);
            }
        }

        return bounds;
    }

    private void LoadPrefs()
    {
        string prefabPath = EditorPrefs.GetString(PrefsBlockPrefabPathKey, string.Empty);
        if (!string.IsNullOrEmpty(prefabPath))
        {
            _blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        _gridUnitSize = Mathf.Max(0.05f, EditorPrefs.GetFloat(PrefsGridUnitKey, 0.5f));
        _autoOpenEditorScene = EditorPrefs.GetBool(PrefsAutoOpenSceneKey, true);
        _autoGenerateOnSettingsChange = EditorPrefs.GetBool(PrefsAutoGenerateKey, true);
        _autoSaveLevelAsset = EditorPrefs.GetBool(PrefsAutoSaveAssetKey, true);
        _autoSavePrefabSnapshot = EditorPrefs.GetBool(PrefsAutoSavePrefabKey, true);
        _sceneSyncIntervalSeconds = Mathf.Clamp(EditorPrefs.GetFloat(PrefsSceneSyncIntervalKey, 0.25f), 0.05f, 1f);
    }

    private void SavePrefs()
    {
        string prefabPath = _blockPrefab != null ? AssetDatabase.GetAssetPath(_blockPrefab) : string.Empty;
        EditorPrefs.SetString(PrefsBlockPrefabPathKey, prefabPath);
        EditorPrefs.SetFloat(PrefsGridUnitKey, _gridUnitSize);
        EditorPrefs.SetBool(PrefsAutoOpenSceneKey, _autoOpenEditorScene);
        EditorPrefs.SetBool(PrefsAutoGenerateKey, _autoGenerateOnSettingsChange);
        EditorPrefs.SetBool(PrefsAutoSaveAssetKey, _autoSaveLevelAsset);
        EditorPrefs.SetBool(PrefsAutoSavePrefabKey, _autoSavePrefabSnapshot);
        EditorPrefs.SetFloat(PrefsSceneSyncIntervalKey, _sceneSyncIntervalSeconds);
    }

    private int ComputeLevelHash(List<BlockData> blocks)
    {
        if (blocks == null)
        {
            return 0;
        }

        List<BlockData> sorted = new List<BlockData>(blocks);
        SortBlocks(sorted);

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < sorted.Count; i++)
            {
                BlockData b = sorted[i];
                hash = hash * 31 + b.position.x;
                hash = hash * 31 + b.position.y;
                hash = hash * 31 + b.position.z;
                hash = hash * 31 + (int)b.direction;
                hash = hash * 31 + Quantize(b.randomVisualRotation.x);
                hash = hash * 31 + Quantize(b.randomVisualRotation.y);
                hash = hash * 31 + Quantize(b.randomVisualRotation.z);
                hash = hash * 31 + Quantize(b.randomVisualRotation.w);
            }

            return hash;
        }
    }

    private static int Quantize(float value)
    {
        return Mathf.RoundToInt(value * 1000f);
    }

    private static void SortBlocks(List<BlockData> blocks)
    {
        blocks.Sort((a, b) =>
        {
            int x = a.position.x.CompareTo(b.position.x);
            if (x != 0) return x;

            int y = a.position.y.CompareTo(b.position.y);
            if (y != 0) return y;

            int z = a.position.z.CompareTo(b.position.z);
            if (z != 0) return z;

            return a.direction.CompareTo(b.direction);
        });
    }

    private MoveDirection GetDirectionFromForward(Vector3 forward)
    {
        Vector3 abs = new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z));
        if (abs.x >= abs.y && abs.x >= abs.z)
        {
            return forward.x >= 0f ? MoveDirection.Right : MoveDirection.Left;
        }

        if (abs.y >= abs.x && abs.y >= abs.z)
        {
            return forward.y >= 0f ? MoveDirection.Up : MoveDirection.Down;
        }

        return forward.z >= 0f ? MoveDirection.Forward : MoveDirection.Back;
    }

    private Quaternion GetStableLookRotation(MoveDirection dir)
    {
        Vector3 direction = GetDirectionVector(dir);
        if (direction == Vector3.zero)
        {
            return Quaternion.identity;
        }

        Vector3 up = (dir == MoveDirection.Up || dir == MoveDirection.Down) ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(direction, up);
    }

    private Vector3 GetDirectionVector(MoveDirection dir)
    {
        switch (dir)
        {
            case MoveDirection.Forward:
                return Vector3.forward;
            case MoveDirection.Back:
                return Vector3.back;
            case MoveDirection.Up:
                return Vector3.up;
            case MoveDirection.Down:
                return Vector3.down;
            case MoveDirection.Left:
                return Vector3.left;
            case MoveDirection.Right:
                return Vector3.right;
            default:
                return Vector3.forward;
        }
    }
}