using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // Needed for NavMesh.SamplePosition

public class ZombieManager : MonoBehaviour
{
    public static ZombieManager Instance;

    [Header("Referințe")]
    public Player player;
    public GameObject zombiePrefab;

    [Header("Setări Valuri (Waves)")]
    public int initialCount = 4;
    public float spawnDistance = 12f;
    public float timeBetweenWaves = 10f;

    [Header("Tactică de Încercuire")]
    public float surroundRadius = 3.5f; // Cât de larg e cercul în jurul playerului
    public float updateRate = 0.5f;     // Cât de des recalculăm pozițiile (optimizare)

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

    private float _nextWaveTime;
    private int _waveNumber = 1;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (player == null) player = FindObjectOfType<Player>();
        
        // Pornim logica de poziționare tactică
        StartCoroutine(UpdateTacticsRoutine());
        
        // Pornim primul val
        SpawnWave(initialCount);
    }

    void Update()
    {
        // Verificăm dacă toți zombii au murit
        if (activeZombies.Count == 0 && Time.time > _nextWaveTime)
        {
            _waveNumber++;
            _nextWaveTime = Time.time + timeBetweenWaves;
            // Creștem dificultatea
            SpawnWave(initialCount + (_waveNumber * 2));
        }
    }

    void SpawnWave(int count)
    {
        Debug.Log($"[ZombieManager] Spawning Wave {_waveNumber} with {count} zombies.");
        for (int i = 0; i < count; i++)
        {
            // Random radial direction with varied distance
            Vector2 rndDir = Random.insideUnitCircle.normalized;
            float dist = spawnDistance * (0.75f + Random.value * 0.5f);
            Vector3 desired = player.transform.position + (Vector3)(rndDir * dist);

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
    }
}