using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class ZombieManager : MonoBehaviour
{
    public static ZombieManager Instance;

    [Header("Referințe")]
    public Player player;
    public GameObject zombiePrefab;
    [Space]
    public Text WaveCountAndTimer;
    // --- Wave System ---
    private enum WavePhase { Preparation, Combat, Breather }
    [Header("Wave Phases Durations")]
    public float preparationDuration = 35f;
    public float breatherDuration = 15f;
    [Header("Spawn & Scaling")]
    public int spawnCountIncrementPerWave = 15;
    public float baseZombieHealth = 20f;
    public float zombieHealthAddPerWave = 20f;
    public float baseZombieAttackDamage = 5f;
    public float zombieAttackAddPerWave = 2f;
    [Tooltip("Health folosit OBLIGATORIU la primul val (ignora valoarea din baseZombieHealth dacă diferă).")]
    public float firstWaveHealth = 20f;
    [Tooltip("Durata maximă a fazei de luptă (dacă playerul nu termină mai repede).")]
    public float maxCombatDuration = 80f;

    private WavePhase _phase = WavePhase.Preparation;
    private float _phaseEndTime;
    private int _waveNumber = 1;
    private bool _combatStarted = false;

    [Header("Setări Valuri (Waves)")]
    public int initialCount;
    public float spawnDistance;
    public float timeBetweenWaves;

    [Header("Tactică de Încercuire")]
    public float surroundRadius; // Cât de larg e cercul în jurul playerului
    public float updateRate;     // Cât de des recalculăm pozițiile (optimizare)

    [Header("Dynamic Formation")]
    public float breatheAmplitude = 0.25f;      // % expansion around base (surroundRadius)
    public float breatheSpeed = 0.5f;           // Speed of sinus "lung" effect
    public float noiseScale = 0.5f;             // Perlin noise influence on radius
    public float positionalJitter = 0.75f;      // Random jitter offset per zombie

    [Header("Separation (Anti-Clump)")]
    public float separationRadius = 1.2f;       // Radius to start pushing zombies apart
    public float separationForce = 0.75f;       // Scaling factor for repulsion

    [Header("Safe Spawn")]
    public float navSampleRadius = 2f;          // Radius to search for valid navmesh
    public int navSampleMaxAttempts = 8;        // Attempts before fallback

    // Lista cu toți zombii activi din scenă
    public List<ZombieAI> activeZombies = new List<ZombieAI>();
[Space]
    public BoxCollider2D safeAreaSpawn;

    // Per-zombie angle noise cache to keep their slot feel persistent
    private Dictionary<ZombieAI, float> _angleNoise = new Dictionary<ZombieAI, float>();

    // (legacy unused after refactor)
    private float _nextWaveTime;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (player == null) player = FindObjectOfType<Player>();
        StartCoroutine(UpdateTacticsRoutine());
        BeginPreparationPhase();      // wave 1 health aplicat când începe faza Combat
    }

    void Update()
    {
        UpdateWaveSystem();
    }

    private void UpdateWaveSystem()
    {
        switch (_phase)
        {
            case WavePhase.Preparation:
                UpdatePreparation();
                break;
            case WavePhase.Combat:
                UpdateCombat();
                break;
            case WavePhase.Breather:
                UpdateBreather();
                break;
        }
    }

    private void BeginPreparationPhase()
    {
        _phase = WavePhase.Preparation;
        _combatStarted = false;
        _phaseEndTime = Time.time + preparationDuration;
        UpdateWaveText($"Wave {_waveNumber} incoming in: {Mathf.CeilToInt(preparationDuration)}s");
    }

    private void UpdatePreparation()
    {
        float remaining = _phaseEndTime - Time.time;
        if (remaining > 0f)
        {
            UpdateWaveText($"Wave {_waveNumber} incoming in: {Mathf.CeilToInt(remaining)}s");
        }
        else
        {
            BeginCombatPhase();
        }
    }

    private void BeginCombatPhase()
    {
        _phase = WavePhase.Combat;
        _combatStarted = true;
        _phaseEndTime = Time.time + maxCombatDuration;
        SpawnWave(ComputeSpawnCountForWave(_waveNumber));
        UpdateWaveText($"Wave {_waveNumber} started! Zombies: {activeZombies.Count}");
    }

    private void UpdateCombat()
    {
        // Update UI
        UpdateWaveText($"Wave {_waveNumber} - Remaining: {activeZombies.Count}");
        // Early finish
        if (activeZombies.Count == 0)
        {
            BeginBreatherPhase();
            return;
        }
        // Timeout
        if (Time.time >= _phaseEndTime)
        {
            // Force end: clear leftover (optional)
            BeginBreatherPhase();
        }
    }

    private void BeginBreatherPhase()
    {
        _phase = WavePhase.Breather;
        _phaseEndTime = Time.time + breatherDuration;
        UpdateWaveText($"Breather: {Mathf.CeilToInt(breatherDuration)}s (Wave {_waveNumber} cleared)");
    }

    private void UpdateBreather()
    {
        float remaining = _phaseEndTime - Time.time;
        if (remaining > 0f)
        {
            UpdateWaveText($"Breather {Mathf.CeilToInt(remaining)}s - Prepare (Next Wave {_waveNumber + 1})");
        }
        else
        {
            _waveNumber++;
            BeginPreparationPhase();
        }
    }

    private int ComputeSpawnCountForWave(int wave)
    {
        return Mathf.Max(1, initialCount + (wave - 1) * spawnCountIncrementPerWave * 4);
    }

    private float ComputeZombieAttackDamage(int wave)
    {
        return baseZombieAttackDamage + (wave - 1) * zombieAttackAddPerWave;
    }

    private void UpdateWaveText(string msg)
    {
        if (WaveCountAndTimer != null)
            WaveCountAndTimer.text = msg;
    }

    // Modified spawn logic to apply scaling and remove legacy wave trigger
    void SpawnWave(int count)
    {
        Debug.Log($"[ZombieManager] Spawning Wave {_waveNumber} with {count} zombies.");
        for (int i = 0; i < count; i++)
        {
            // Random radial direction with varied distance
            Vector2 rndDir = Random.insideUnitCircle.normalized;
            float dist = spawnDistance * (0.75f + Random.value * 0.5f);
            Vector3 desired = player != null
                ? player.transform.position + (Vector3)(rndDir * dist)
                : transform.position + (Vector3)(rndDir * dist);

            // NEW: clamp to safe area before navmesh sampling
            desired = ConstrainToSafeArea(desired);

            Vector3 spawnPos = SafeSpawnPosition(desired);

            // Creăm zombiul
            GameObject newZ = Instantiate(zombiePrefab, spawnPos, Quaternion.identity);
            ZombieAI ai = newZ.GetComponent<ZombieAI>();

            if (ai != null)
            {
                activeZombies.Add(ai);
                // Cache persistent angle noise (small offset)
                _angleNoise[ai] = Random.Range(-10f, 10f) * Mathf.Deg2Rad;
                ai.InitializeVariance(); // Randomize movement characteristics

                // ELIMINAT: dest.health = h; (ApplyWaveStats face deja asta)
                ai.ApplyWaveStats(_waveNumber);
            }
        }
    }

    // Safe NavMesh spawn sampling
    Vector3 SafeSpawnPosition(Vector3 desired)
    {
        desired = ConstrainToSafeArea(desired); // ensure inside before sampling

        NavMeshHit hit;
        // Try desired first with jitter attempts
        for (int attempt = 0; attempt < navSampleMaxAttempts; attempt++)
        {
            Vector3 probe = desired + (Vector3)Random.insideUnitCircle * navSampleRadius;
            probe = ConstrainToSafeArea(probe); // clamp jitter inside area
            if (NavMesh.SamplePosition(probe, out hit, navSampleRadius, NavMesh.AllAreas))
                return hit.position;
        }
        // Fallback: keep original
        if (NavMesh.SamplePosition(desired, out hit, navSampleRadius, NavMesh.AllAreas))
            return hit.position;
        return desired; // Last resort
    }

    // NEW: clamps a position to safeAreaSpawn bounds (if set)
    private Vector3 ConstrainToSafeArea(Vector3 pos)
    {
        if (safeAreaSpawn == null) return pos;
        Bounds b = safeAreaSpawn.bounds;
        pos.x = Mathf.Clamp(pos.x, b.min.x, b.max.x);
        pos.y = Mathf.Clamp(pos.y, b.min.y, b.max.y);
        return pos;
    }

    // Corutina care rulează infinit și le spune zombilor unde să meargă
    IEnumerator UpdateTacticsRoutine()
    {
        while (true)
        {
            if (player != null && activeZombies.Count > 0)
            {
                float angleStep = 360f / activeZombies.Count;
                // Breathing + noise radius
                float dynamicRadius = surroundRadius *
                                      (1f + Mathf.Sin(Time.time * breatheSpeed) * breatheAmplitude);
                dynamicRadius += (Mathf.PerlinNoise(Time.time * 0.2f, 0f) - 0.5f) * noiseScale;

                for (int i = 0; i < activeZombies.Count; i++)
                {
                    ZombieAI z = activeZombies[i];
                    if (z == null) continue;

                    // Base slot angle + persistent noise
                    float angle = (i * angleStep) * Mathf.Deg2Rad;
                    if (_angleNoise.TryGetValue(z, out float noiseAng))
                        angle += noiseAng;

                    // Position on ring
                    Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * dynamicRadius;

                    // Add jitter (fuzzy spacing)
                    offset += (Vector3)Random.insideUnitCircle * positionalJitter;

                    Vector3 targetPos = player.transform.position + offset;

                    // Separation: push away from nearby zombies to avoid overlap
                    Vector3 separation = Vector3.zero;
                    for (int j = 0; j < activeZombies.Count; j++)
                    {
                        if (j == i) continue;
                        ZombieAI other = activeZombies[j];
                        if (other == null) continue;

                        Vector3 toSelf = z.transform.position - other.transform.position;
                        float d = toSelf.magnitude;
                        if (d > 0f && d < separationRadius)
                        {
                            float push = (1f - (d / separationRadius)); // Stronger when closer
                            separation += toSelf.normalized * push;
                        }
                    }
                    targetPos += separation * separationForce;

                    z.SetTacticalTarget(targetPos);
                }
            }
            yield return new WaitForSeconds(updateRate);
        }
    }

    // Funcție apelată de Zombie când moare
    public void UnregisterZombie(ZombieAI z)
    {
        if (activeZombies.Contains(z))
            activeZombies.Remove(z);
        if (_angleNoise.ContainsKey(z))
            _angleNoise.Remove(z);

        if (_phase == WavePhase.Combat && activeZombies.Count == 0)
            BeginBreatherPhase();
    }

    public float GetCurrentWaveAttackDamage()
    {
        return ComputeZombieAttackDamage(_waveNumber);
    }
}