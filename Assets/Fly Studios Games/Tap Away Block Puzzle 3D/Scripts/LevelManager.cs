// LevelManager.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
    [Header("Level Configuration")]
    // Lista de nivele - adaugă în inspector nivelurile dorite
    public List<LevelData> levelList = new List<LevelData>();
    [Tooltip("Indexul nivelului curent în listă (0 = primul nivel)")]
    public int currentLevelIndex = 0;

    private int _currentLevelNumber = 1; // Începem cu nivelul 1

    [Header("Object References")]
    public GameObject singleBlockPrefab;
    public Transform levelContainer;
    public Vector3 rotationLevelContainer;
    public UIManager uiManager; // Referință către UIManager
    public AudioManager audioManager; // NOU: referință la AudioManager (lege în inspector)
    [Tooltip("Referință la NotificationManager pentru mesaje către jucător")]
    public NotificationManager notificationManager;
    public ShopManager shopManager; // NOU: legi în inspector
    public CameraControler cameraControler;
    public SuperPowerUI removePowerUI;
    public RemoverPowerSimpleStepTutorialHand removerPowerSimpleStepTutorialHand;
    public BackgroundImageManager backgroundImageManager; 

    [Header("Grid Settings")]
    public float gridUnitSize = 0.5f;

    private List<Block> _activeBlocks = new List<Block>();

    // NOU: flag pentru modul "remove" (următorul click va distruge block-ul selectat)
    private bool _awaitingRemoveSelection = false;

    // Flag pentru a evita multiple tranziții simultane
    private bool _isTransitioning = false;

    [Header("Coins")]
    [Tooltip("Câte coins se acordă la finalizarea unui nivel.")]
    public int coinsPerLevel = 5; // poți schimba în inspector

    private const string CoinsKey = "PlayerCoins";
    private const string LevelNumberKey = "PlayerLevelNumber"; // persistence key for global level counter
    private int _overallLevelNumber = 1; // persistent global level number shown to player

    // NOU: Undo stack - păstrează ultimele mov-uri / distrugeri
    private struct MoveRecord
    {
        public bool wasDestroyed;
        public Vector3 startPos;
        public Vector3 endPos;
        public Vector3Int gridPos;
        public MoveDirection direction;
        public Quaternion rotation;
        public Vector3 scale;
    }
    private Stack<MoveRecord> _undoStack = new Stack<MoveRecord>();
    public int maxUndo = 20;

    // NOU: Power-up inventory & costs
    [Header("PowerUps")]
    [Tooltip("Numărul curent de Undo disponibile.")]
    public int undoCount = 0;
    [Tooltip("Numărul curent de Smash disponibile.")]
    public int smashCount = 0;

    [Tooltip("Cost coins pentru a cumpăra un Undo.")]
    public int undoCost = 5;
    [Tooltip("Cost coins pentru a cumpăra un Smash.")]
    public int smashCost = 10;

    private const string UndoKey = "PlayerUndo";
    private const string SmashKey = "PlayerSmash";

    [Header("Random Cycle Settings")]
    [Tooltip("Comma-separated list of level indices to IGNORE when doing random cycling (e.g. \"0,2,5\"). Indices out of range are ignored.")]
    public string ignoredLevelIndicesCSV = "0"; // implicit ignorăm indexul 0 (opțional)
    [Tooltip("When the sequential list ends, enable infinite random cycling through allowed levels.")]
    public bool enableRandomCycleOnEnd = true;

    [Header("Level Manager For Editor")]
    public bool isLevelEditorManager;
    public LevelData runCurrentLevel;
    // runtime cache
    private HashSet<int> _ignoredIndices = new HashSet<int>();
    private List<int> _allowedRandomIndices = new List<int>();

    // NOU: numărul inițial de blocuri din nivel (folosit pentru progres)
    private int _initialBlockCount = 0;

    // NOU: verificare periodică a progresului (corectează discrepanțele)
    private float _progressCheckInterval = 0.5f;
    private float _progressCheckTimer = 0f;

    // Helper: returnează LevelData curent sau null
    private LevelData GetCurrentLevelData()
    {
        if (levelList == null || levelList.Count == 0) return null;
        if (currentLevelIndex < 0) currentLevelIndex = 0;
        if (currentLevelIndex >= levelList.Count) currentLevelIndex = levelList.Count - 1;
        return levelList[currentLevelIndex];
    }

    // Actualizează numărul afișat pentru nivel (folosim contorul global)
    private void UpdateCurrentLevelNumber()
    {
        _currentLevelNumber = _overallLevelNumber;
        if (uiManager != null) uiManager.UpdateLevelDisplay(_currentLevelNumber);
    }

    void Start()
    {
        // Dacă este Level Editor Manager, ignorăm lista de nivele și folosim doar runCurrentLevel
        if (isLevelEditorManager)
        {
            levelList.Clear(); // Curățăm lista de nivele
            if (runCurrentLevel != null)
            {
                levelList.Add(runCurrentLevel); // Adăugăm doar nivelul curent pentru consistență
            }
            else
            {
                Debug.LogWarning("runCurrentLevel nu este setat! Asigurați-vă că ați atribuit un LevelData.");
            }
        }

        // Initializăm UI-ul de coins și afișajul nivelului la start
        if (uiManager != null)
        {
            uiManager.UpdateGlobalCoinsDisplay(GetCoins());
            uiManager.UpdateWinCoinsDisplay(0); // resetează textul din panelul de win la start
        }

        // Load overall level number from PlayerPrefs before updating display
        _overallLevelNumber = PlayerPrefs.GetInt(LevelNumberKey, 1);

        // Încărcăm nivelul curent salvat
        currentLevelIndex = PlayerPrefs.GetInt(CurrentLevelIndexKey, 0);
        currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, levelList.Count - 1); // Asigurăm că indexul este valid

        // Actualizăm numărul curent de nivel
        UpdateCurrentLevelNumber();

        // NOU: încărcăm powerup-urile persistente la start și actualizăm UI
        undoCount = PlayerPrefs.GetInt(UndoKey, undoCount);
        smashCount = PlayerPrefs.GetInt(SmashKey, smashCount);
        if (uiManager != null) uiManager.UpdatePowerUpCounts(undoCount, smashCount);

        // Asigurăm starea inițială a rotației camerei (dacă există)
        if (cameraControler != null)
        {
            cameraControler.rotationEnabled = true;
        }

        // Actualizăm fundalul pentru nivelul curent
        if (backgroundImageManager != null)
        {
            backgroundImageManager.UpdateBackground(_overallLevelNumber);
        }

        // Generăm nivelul curent (dacă există)
        GenerateLevel();
    }

    private void Update()
    {
        //if (Input.GetKeyDown(KeyCode.Space))
        //{
            //AddCoins(100);
        //}

        // Apasă F pentru a re-încadra instantaneu obiectul
        //if (Input.GetKeyDown(KeyCode.F))
        //{
            //cameraControler.FrameTarget();
        //}

        // Adăugăm verificare periodică a progresului pentru a corecta discrepanțele rare
        _progressCheckTimer += Time.deltaTime;
        if (_progressCheckTimer >= _progressCheckInterval)
        {
            _progressCheckTimer = 0f;
            if (uiManager != null && uiManager.currentProgresSlider != null)
            {
                float expected = Mathf.Clamp(_initialBlockCount - (_activeBlocks != null ? _activeBlocks.Count : 0), 0, Mathf.Max(1, _initialBlockCount));
                float current = uiManager.currentProgresSlider.value;
                if (Mathf.Abs(current - expected) > 0.01f)
                {
                    uiManager.UpdateProgressByCounts(_initialBlockCount, _activeBlocks != null ? _activeBlocks.Count : 0);
                }
            }
        }
    }

    // NOU: cumpărare power-ups din shop
    public bool BuyUndo()
    {
        if (SpendCoins(undoCost))
        {
            undoCount++;
            PlayerPrefs.SetInt(UndoKey, undoCount);
            if (uiManager != null) uiManager.UpdatePowerUpCounts(undoCount, smashCount);
            return true;
        }
        // notificăm jucătorul că nu are fonduri suficiente
        if (notificationManager != null) notificationManager.ShowNotification("Not enough coins", 2f);
        return false;
    }

    public bool BuySmash()
    {
        if (SpendCoins(smashCost))
        {
            smashCount++;
            PlayerPrefs.SetInt(SmashKey, smashCount);
            if (uiManager != null) uiManager.UpdatePowerUpCounts(undoCount, smashCount);
            if (notificationManager != null) notificationManager.ShowNotification("Remover Buyed + 1", 2f);
            return true;
        }
        if (notificationManager != null) notificationManager.ShowNotification("Not enough coins", 2f);
        return false;
    }

    public void GenerateLevel()
    {
        levelContainer.rotation = Quaternion.Euler(0f, 0f, 0f);
        
        LevelData currentLevelData = GetCurrentLevelData();
        if (currentLevelData == null || singleBlockPrefab == null)
        {
            if (levelList == null || levelList.Count == 0)
                Debug.LogError("No levels in levelList or Single Block Prefab is not set!");
            else
                Debug.LogError("Single Block Prefab is not set!");
            return;
        }

        // Curățăm nivelul anterior
        foreach (Transform child in levelContainer) { Destroy(child.gameObject); }
        _activeBlocks.Clear();

        // Obținem lista de blocuri
        List<BlockData> blocksToGenerate = currentLevelData.GetBlocks();

        // --- Populăm scena cu blocuri ---
        if (blocksToGenerate != null)
        {
            int blockIndex = 0; // Contor pentru redenumirea cuburilor
            foreach (BlockData data in blocksToGenerate)
            {
                Vector3 worldPosition = (Vector3)data.position * gridUnitSize;
                Quaternion finalRotation = GetStableLookRotation(data.direction);

                GameObject newBlockObj = Instantiate(singleBlockPrefab, worldPosition, finalRotation, levelContainer);
                newBlockObj.name = $"Block_{blockIndex}"; // Redenumim cubul
                blockIndex++;

                Block blockScript = newBlockObj.GetComponent<Block>();
                // calculăm poziția pe grid pe baza poziției world
                Vector3Int gridPos = Vector3Int.RoundToInt(worldPosition / gridUnitSize);
                blockScript.Initialize(data.direction, this, gridUnitSize, gridPos);

                // Aplicăm skin-ul curent dacă există (dacă ShopManager este setat)
                if (shopManager != null && shopManager.selectedMaterial != null)
                {
                    blockScript.ApplySkin(shopManager.selectedMaterial);
                }

                _activeBlocks.Add(blockScript);
            }
        }

        // setăm numărul inițial de blocuri și inițializăm UI-ul de progres
        _initialBlockCount = _activeBlocks != null ? _activeBlocks.Count : 0;
        if (uiManager != null)
        {
            uiManager.InitProgress(_initialBlockCount);
            uiManager.UpdateProgressByCounts(_initialBlockCount, _activeBlocks.Count); // va seta 0 inițial
        }

        // că Renderer.bounds sunt actualizate după instanțiere.
        if (cameraControler != null)
        {
            StartCoroutine(FrameCameraNextFrame());
        }
        else
        {
            Debug.LogWarning("CameraControler is not assigned; cannot frame target.", this);
        }

        if (uiManager)
        {
            uiManager.safeAreaUI.SetActive(true);
        }
        levelContainer.rotation = Quaternion.Euler(rotationLevelContainer);
    }

    // Așteaptă finalul frame-ului și apoi centrează camera (avoid incorrect bounds)
    private IEnumerator FrameCameraNextFrame()
    {
        yield return new WaitForEndOfFrame();
        if (cameraControler != null)
        {
            cameraControler.FrameTarget();
        }
    }

    // NOU: expune block-urile active (ShopManager folosește aceasta)
    public List<Block> GetActiveBlocks()
    {
        // returnăm o copie pentru a evita modificări neintenționate din exterior
        return new List<Block>(_activeBlocks);
    }

    public void OnBlockRemoved(Block block)
    {
        if (_activeBlocks.Remove(block))
        {
            // NOU: actualizăm progress UI pe baza numerelor totale/ramase
            if (uiManager != null)
            {
                uiManager.UpdateProgressByCounts(_initialBlockCount, _activeBlocks.Count);
            }

            CheckWinCondition();
        }
    }

    private void CheckWinCondition()
    {
        if (_activeBlocks.Count == 0)
        {
            Debug.Log($"Felicitări! Ai câștigat nivelul {_currentLevelNumber}!");

            if (isLevelEditorManager)
            {
                Debug.Log("Level Editor Mode: Nivel finalizat. Nu se salvează progresul și nu se trece automat la următorul nivel.");
                return; // Ieșim fără a salva progresul sau a trece la alt nivel
            }

            // Acordăm coins o singură dată și apoi afișăm panelul de win
            if (!_isTransitioning)
            {
                // acordăm coins
                AddCoins(coinsPerLevel);

                // actualizăm textul din win panel (dacă există)
                if (uiManager != null)
                {
                    uiManager.UpdateWinCoinsDisplay(coinsPerLevel);
                    uiManager.UpdateGlobalCoinsDisplay(GetCoins());
                }

                // Activăm panelul de level win din UI (dacă e setat)
                if (uiManager != null && uiManager.levelWin_panel != null)
                {
                    _isTransitioning = true; // blocăm alte acțiuni până la alegerea jucătorului
                    
                    StartCoroutine(levelWinDelay());

                    IEnumerator levelWinDelay()
                    {
                        yield return new WaitForSeconds(0.6f);
                        if (uiManager)
                        {
                            uiManager.safeAreaUI.SetActive(false);
                        }

                        if (uiManager)
                        {
                             uiManager.levelWin_panel.SetActive(true);
                        }
                       
                    }
                }
                else
                {
                    // fallback: dacă nu avem UI, trecem imediat la nivelul următor
                    StartCoroutine(ProceedToNextLevelCoroutine());
                }
            }
        }
    }

    private IEnumerator ProceedToNextLevelCoroutine()
    {
        if (isLevelEditorManager)
        {
            Debug.Log("Level Editor Mode: Nu se trece automat la următorul nivel.");
            yield break; // Ieșim fără a trece la următorul nivel
        }

        _isTransitioning = true;
        // așteptăm 1 secundă înainte de a trece nivelul
        yield return new WaitForSeconds(1f);

        // încercăm să trecem la nivelul următor din listă
        if (levelList != null && currentLevelIndex + 1 < levelList.Count)
        {
            currentLevelIndex++;
            SaveCurrentLevelIndex(); // Salvăm nivelul curent
            AdvanceOverallLevelNumber();

            // Actualizăm fundalul pentru următorul nivel
            if (backgroundImageManager != null)
            {
                backgroundImageManager.UpdateBackground(_overallLevelNumber);
            }

            UpdateCurrentLevelNumber();
            GenerateLevel();
        }
        else
        {
            // Am ajuns la sfârșit secvențial.
            if (enableRandomCycleOnEnd && levelList != null && levelList.Count > 0)
            {
                // Asigurăm că cache-ul de indici e actual (în caz că user a schimbat CSV în runtime)
                ParseIgnoredIndicesAndBuildAllowed();

                // incrementăm contorul global (fiecare trecere contează)
                AdvanceOverallLevelNumber();

                int nextIdx = GetRandomNextLevelIndex(excludeCurrent: true);
                currentLevelIndex = nextIdx;
                UpdateCurrentLevelNumber();
                GenerateLevel();
            }
            else
            {
                Debug.Log("Ai terminat toate nivelele din listă!");
                // Poți adăuga aici logică pentru restart / meniuri etc.
            }
        }

        _isTransitioning = false;
    }

    // --- Funcțiile ajutătoare rămân neschimbate ---

    private Quaternion GetStableLookRotation(MoveDirection dir)
    {
        Vector3 directionVector = GetDirectionVector(dir);
        if (directionVector == Vector3.zero) return Quaternion.identity;
        Vector3 upReference = (dir == MoveDirection.Up || dir == MoveDirection.Down) ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(directionVector, upReference);
    }

    private Vector3 GetDirectionVector(MoveDirection dir)
    {
        switch (dir)
        {
            case MoveDirection.Forward: return Vector3.forward;
            case MoveDirection.Back: return Vector3.back;
            case MoveDirection.Up: return Vector3.up;
            case MoveDirection.Down: return Vector3.down;
            case MoveDirection.Left: return Vector3.left;
            case MoveDirection.Right: return Vector3.right;
        }
        return Vector3.forward;
    }

    // Public API apelabil din UI (legate la butoanele din levelWin_panel)
    public void NextLevel()
    {
        // Închidem panelul de win
        if (uiManager != null && uiManager.levelWin_panel != null)
            uiManager.levelWin_panel.SetActive(false);

        _isTransitioning = true;

        // Trecem la nivelul următor din listă imediat
        if (levelList != null && currentLevelIndex + 1 < levelList.Count)
        {
            currentLevelIndex++;
            SaveCurrentLevelIndex(); // Salvăm nivelul curent
            AdvanceOverallLevelNumber();

            // Actualizăm fundalul pentru următorul nivel
            if (backgroundImageManager != null)
            {
                backgroundImageManager.UpdateBackground(_overallLevelNumber);
            }

            UpdateCurrentLevelNumber();
            GenerateLevel();
        }
        else
        {
            // final secvență => random cycle if enabled
            if (enableRandomCycleOnEnd && levelList != null && levelList.Count > 0)
            {
                ParseIgnoredIndicesAndBuildAllowed();
                AdvanceOverallLevelNumber();
                int nextIdx = GetRandomNextLevelIndex(excludeCurrent: true);
                currentLevelIndex = nextIdx;
                UpdateCurrentLevelNumber();
                GenerateLevel();
            }
            else
            {
                Debug.Log("Nu există nivel următor. Ai terminat toate nivelele din listă!");
                // Poți deschide shop sau afișa ecran final aici
            }
        }

        _isTransitioning = false;
    }

    // Public API pentru butonul "Open Shop" din panelul de win
    public void OpenShop()
    {
        if (uiManager != null && uiManager.shop_panel != null)
        {
            uiManager.shop_panel.SetActive(true);
        }
    }

    // Închide panelul de win (buton cancel/close)
    public void CloseLevelWin()
    {
        if (uiManager != null && uiManager.levelWin_panel != null)
        {
            uiManager.levelWin_panel.SetActive(false);
        }
        _isTransitioning = false;
    }

    // Inchide shop panel
    public void CloseShop()
    {
        if (uiManager != null && uiManager.shop_panel != null)
        {
            uiManager.shop_panel.SetActive(false);
        }
    }

    // --- Funcții pentru coins (persistență simplă) ---
    public int GetCoins()
    {
        return PlayerPrefs.GetInt(CoinsKey, 0);
    }

    private void SetCoins(int amount)
    {
        PlayerPrefs.SetInt(CoinsKey, amount);
        PlayerPrefs.Save();
        // actualizăm UI global imediat dacă există
        if (uiManager != null) uiManager.UpdateGlobalCoinsDisplay(amount);
    }

    public void AddCoins(int amount)
    {
        int newAmount = GetCoins() + amount;
        SetCoins(newAmount);
    }

    // Returnează true dacă tranzacția a avut succes (suficiente coins)
    public bool SpendCoins(int amount)
    {
        int current = GetCoins();
        if (current >= amount)
        {
            SetCoins(current - amount);
            return true;
        }
        return false;
    }

    // NOU: Block-uri apelează asta înainte de a porni mutarea/distrugerea
    public void RegisterMove(Block block, Vector3 startPos, Vector3 endPos, bool wasDestroyed, Vector3Int gridPos, MoveDirection dir, Quaternion rot, Vector3 scale)
    {
        MoveRecord rec = new MoveRecord
        {
            wasDestroyed = wasDestroyed,
            startPos = startPos,
            endPos = endPos,
            gridPos = gridPos,
            direction = dir,
            rotation = rot,
            scale = scale
        };

        _undoStack.Push(rec);
        if (_undoStack.Count > maxUndo) // păstrăm doar ultimele N
        {
            // pierdem cel mai vechi (pop all to temp queue approach)
            var arr = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = 0; i < Mathf.Min(arr.Length, maxUndo); i++) _undoStack.Push(arr[Mathf.Min(arr.Length - 1, i)]);
        }
    }

    // NOU: Undo - inversează ultima acțiune (mișcare sau distrugere)
    public void UseUndo()
    {
        // Verificăm dacă avem item Undo
        if (undoCount <= 0)
        {
            if (notificationManager != null) notificationManager.ShowNotification("No undos available", 2f);
            return;
        }

        // Verificăm dacă există ceva de refăcut în istoric
        if (_undoStack == null || _undoStack.Count == 0)
        {
            if (notificationManager != null) notificationManager.ShowNotification("No moves to undo", 2f);
            return;
        }

        // Consumăm Undo doar după ce știm că putem anula ceva
        undoCount--;
        PlayerPrefs.SetInt(UndoKey, undoCount);
        if (uiManager != null) uiManager.UpdatePowerUpCounts(undoCount, smashCount);

        MoveRecord rec = _undoStack.Pop();

        // Calculează poziția locală bazată pe gridPos (independentă de rotația/poziția curentă a levelContainer)
        Vector3 localPos = (Vector3)rec.gridPos * gridUnitSize;

        if (rec.wasDestroyed)
        {
            // reacrea block (există deja în cod)
            GameObject newBlockObj = Instantiate(singleBlockPrefab, levelContainer);
            newBlockObj.transform.localPosition = localPos;
            newBlockObj.transform.localRotation = rec.rotation;
            newBlockObj.transform.localScale = rec.scale;

            Block blockScript = newBlockObj.GetComponent<Block>();
            blockScript.Initialize(rec.direction, this, gridUnitSize, rec.gridPos);
            _activeBlocks.Add(blockScript);

            // NOU: la refacere prin undo actualizăm progresul UI corect
            if (uiManager != null)
            {
                uiManager.UpdateProgressByCounts(_initialBlockCount, _activeBlocks.Count);
            }
        }
        else
        {
            // găsim block-ul cel mai apropiat de endPos (fallback), dar setăm poziția în local space pe gridPos
            Block closest = null;
            float best = float.MaxValue;
            foreach (var b in _activeBlocks)
            {
                if (b == null) continue;
                float d = Vector3.Distance(b.transform.position, rec.endPos);
                if (d < best)
                {
                    best = d;
                    closest = b;
                }
            }
            if (closest != null)
            {
                // setăm poziția locală și rotația locală (astfel va ține cont de rotația/poziția curentă a levelContainer)
                closest.transform.SetParent(levelContainer, true);
                closest.transform.localPosition = localPos;
                closest.transform.localRotation = rec.rotation;
            }
        }

        // actualizăm UI coins etc. (dacă este cazul)
        if (uiManager != null) uiManager.UpdateGlobalCoinsDisplay(GetCoins());
    }

    // NOU: găsește un block pentru smash (folosim aceeași logică ca la hint) și îl distrugem
    public void UseSmashHint()
    {
        // Verificăm dacă avem item Smash
        if (smashCount <= 0)
        {
            if (notificationManager != null) notificationManager.ShowNotification("No smash items available", 2f);
            return;
        }

        // Căutăm mai întâi o țintă validă, fără a consuma nimic
        Block candidate = null;
        foreach (var b in new List<Block>(_activeBlocks))
        {
            if (b == null) continue;
            RaycastHit hit;
            if (!Physics.Raycast(b.transform.position, b.transform.forward, out hit, _GetGridDetectionDistance(b)))
            {
                candidate = b;
                break;
            }
        }

        // Dacă nu găsim țintă, notificăm și nu consumăm
        if (candidate == null)
        {
            if (notificationManager != null) notificationManager.ShowNotification("No smash target found", 2f);
            return;
        }

        // Avem țintă -> consumăm item-ul și aplicăm efectul
        smashCount--;
        PlayerPrefs.SetInt(SmashKey, smashCount);
        if (uiManager != null) uiManager.UpdatePowerUpCounts(undoCount, smashCount);

        candidate.Smash();
    }

    // NOU: pornește modul în care următorul click pe un block îl va distruge (consumă un Smash la confirmare)
    public void StartRemoveMode()
    {
        if (smashCount <= 0)
        {
            if (notificationManager != null) notificationManager.ShowNotification("No smash items available", 2f);
            removePowerUI.ToggleBuyPowerPanel();
            return;
        }

        // dacă nu există blocuri în scenă, nu porni modul
        if (_activeBlocks == null || _activeBlocks.Count == 0)
        {
            if (notificationManager != null) notificationManager.ShowNotification("No blocks to remove", 2f);
            return;
        }

        _awaitingRemoveSelection = true;

        // notificare importantă, mai lungă
        if (notificationManager != null) notificationManager.ShowNotification("Select a block to remove", 5f);
    }

    public bool IsAwaitingRemove()
    {
        return _awaitingRemoveSelection;
    }

    // NOU: apelată când jucătorul a dat click pe un Block în modul Remove
    public void ConfirmRemoveAtBlock(Block block)
    {
        if (!_awaitingRemoveSelection || block == null) return;

        if (smashCount <= 0)
        {
            if (notificationManager != null) notificationManager.ShowNotification("No smash items available", 2f);
            _awaitingRemoveSelection = false;
            return;
        }

        // consumăm un Smash
        smashCount--;
        PlayerPrefs.SetInt(SmashKey, smashCount);
        if (uiManager != null) uiManager.UpdatePowerUpCounts(undoCount, smashCount);

        // înregistrare pentru undo (opțional) și distrugere
        // Block.Smash se ocupă de animatie și notificare OnBlockRemoved
        block.Smash();

        _awaitingRemoveSelection = false;
    }

    // Helper mic: distanța pentru testul de blocaj (folosim gridUnitSize)
    private float _GetGridDetectionDistance(Block b)
    {
        return gridUnitSize * 0.6f; // detectăm obstacol imediat din față
    }

    // Parse CSV în _ignoredIndices și construiește _allowedRandomIndices cu validări
    private void ParseIgnoredIndicesAndBuildAllowed()
    {
        _ignoredIndices.Clear();
        _allowedRandomIndices.Clear();

        if (string.IsNullOrWhiteSpace(ignoredLevelIndicesCSV))
        {
            // nimic de ignorat
        }
        else
        {
            var parts = ignoredLevelIndicesCSV.Split(',');
            foreach (var p in parts)
            {
                string t = p.Trim();
                if (string.IsNullOrEmpty(t)) continue;
                int val;
                if (!int.TryParse(t, out val))
                {
                    Debug.LogWarning($"Ignored level index '{t}' is not a valid integer and will be ignored.");
                    continue;
                }
                if (val < 0)
                {
                    Debug.LogWarning($"Ignored level index '{val}' is negative and will be ignored.");
                    continue;
                }
                _ignoredIndices.Add(val);
            }
        }

        // Construim lista de indici permisi pe baza levelList actual
        if (levelList != null && levelList.Count > 0)
        {
            for (int i = 0; i < levelList.Count; i++)
            {
                if (!_ignoredIndices.Contains(i))
                    _allowedRandomIndices.Add(i);
            }
        }

        // Dacă nu avem indici permisi, fallback la toate indici validi (exceptare: păstrăm ignorările doar ca warning)
        if ((_allowedRandomIndices == null || _allowedRandomIndices.Count == 0) && levelList != null && levelList.Count > 0)
        {
            Debug.LogWarning("No allowed random indices after parsing ignoredLevelIndicesCSV. Falling back to all level indices.");
            _allowedRandomIndices = new List<int>();
            for (int i = 0; i < levelList.Count; i++) _allowedRandomIndices.Add(i);
        }
    }

    // Returnează un index ales aleator din allowed list. Dacă excludeCurrent=true încearcă să nu aleagă același index imediat (dacă e posibil).
    private int GetRandomNextLevelIndex(bool excludeCurrent = true)
    {
        if (_allowedRandomIndices == null || _allowedRandomIndices.Count == 0)
        {
            // fallback: toate indici validi
            if (levelList == null || levelList.Count == 0) return 0;
            return Random.Range(0, levelList.Count);
        }

        // dacă există mai mult de un candidat și vrem să excludem curentul, filtrăm temporar
        List<int> candidates = _allowedRandomIndices;
        if (excludeCurrent && candidates.Count > 1)
        {
            candidates = candidates.FindAll(i => i != currentLevelIndex);
            if (candidates.Count == 0)
            {
                // nu putem exclude curentul (doar el era permis)
                candidates = new List<int>(_allowedRandomIndices);
            }
        }

        int pick = candidates[Random.Range(0, candidates.Count)];
        return Mathf.Clamp(pick, 0, Mathf.Max(0, levelList != null ? levelList.Count - 1 : 0));
    }

    // Increment global level counter, persist and update UI
    private void AdvanceOverallLevelNumber()
    {
        _overallLevelNumber = Mathf.Max(1, _overallLevelNumber) + 1;
        PlayerPrefs.SetInt(LevelNumberKey, _overallLevelNumber);
        PlayerPrefs.Save();
        UpdateCurrentLevelNumber();
    }

    private const string CurrentLevelIndexKey = "CurrentLevelIndex"; // Cheie pentru salvarea nivelului curent

    private void SaveCurrentLevelIndex()
    {
        PlayerPrefs.SetInt(CurrentLevelIndexKey, currentLevelIndex);
        PlayerPrefs.Save();
    }

    // NOU: Resetare nivel (apelată din UI sau alte scripturi)
    public void ResetLevel()
    {
        // Dacă este Level Editor Manager, nu facem resetare automată
        if (isLevelEditorManager)
        {
            Debug.Log("Level Editor Mode: Reset level not performed.");
            return;
        }

        // Curățăm nivelul curent (blocuri, efecte, etc.)
        foreach (Transform child in levelContainer) { Destroy(child.gameObject); }
        _activeBlocks.Clear();

        // Reaplicăm skin-ul și culoarea săgeții la toate blocurile
        if (shopManager != null && shopManager.selectedSkin != null)
        {
            foreach (var block in _activeBlocks)
            {
                if (block != null)
                {
                    block.ApplySkin(shopManager.selectedSkin.material);
                    block.arrowCollor = shopManager.selectedSkin.arrowColor;
                    block.ApplyArrowColor();
                }
            }
        }

        // Resetăm progresul UI
        if (uiManager != null)
        {
            uiManager.UpdateProgressByCounts(_initialBlockCount, 0);
        }

        Debug.Log("Nivel resetat cu succes.");
    }

    public void CheckForAvailableMoves()
    {
        // Ensure there are at least two blocks in the level
        if (_activeBlocks == null || _activeBlocks.Count < 2) return;

        foreach (var block in _activeBlocks)
        {
            if (block == null) continue;

            Vector3 direction = block.transform.forward;
            if (!Physics.Raycast(block.transform.position, direction, _GetGridDetectionDistance(block)))
            {
                // At least one block has a valid move
                return;
            }
        }

        // No moves available, show notification
        if (notificationManager != null)
        {
            notificationManager.ShowNotification("No Moves! Use Remover or Restart!", 4f);
            removerPowerSimpleStepTutorialHand.ShowHand();
        }
    }
}