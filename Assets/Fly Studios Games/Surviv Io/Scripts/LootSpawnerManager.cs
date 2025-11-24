using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LootSpawnerManager : MonoBehaviour
{
    [Header("Loot Sources")]
    [Tooltip("Prefabs ce pot apărea ca loot.")]
    public List<GameObject> loot = new List<GameObject>();

    [Header("Spawn Points (Grid over the map)")]
    [Tooltip("Punctele posibile de spawn pentru loot.")]
    public List<Transform> lootSpawnPositions = new List<Transform>();
    [Tooltip("Colectează automat toți copiii din 'spawnPointsRoot' ca puncte de spawn dacă lista e goală.")]
    public bool autoCollectFromChildren = true;
    public Transform spawnPointsRoot;

    [Header("Spawn Output")]
    [Tooltip("Părinte opțional pentru instanțele de loot spawnate.")]
    public Transform lootContainer;

    [Header("Amount Control")]
    [Tooltip("Câte loot-uri minime să apară la start.")]
    public int minLootToSpawn = 25;
    [Tooltip("Câte loot-uri maxime să apară la start.")]
    public int maxLootToSpawn = 45;

    [Header("Distribution (Anti-Clump)")]
    [Tooltip("Previne gruparea prea strânsă a loot-ului.")]
    public bool preventClumping = true;
    [Tooltip("Distanța minimă între două loot-uri acceptate (unități).")]
    public float minSeparation = 2.0f;
    [Tooltip("Câte iterații de relaxare a separării facem dacă nu putem atinge cantitatea dorită.")]
    public int relaxationPasses = 1;

    [Header("Randomization")]
    [Tooltip("Folosește seed fix pentru rezultate deterministe (debug).")]
    public bool useFixedSeed = false;
    public int randomSeed = 12345;

    // Track instanțele pentru a putea curăța/respawna ușor
    private readonly List<GameObject> _spawnedLoot = new List<GameObject>();

    void Start()
    {
        if (useFixedSeed) Random.InitState(randomSeed);

        if (lootContainer == null) lootContainer = transform;
        if (spawnPointsRoot == null) spawnPointsRoot = transform;

        // opțional colectăm puncte dacă lista e goală
        if (autoCollectFromChildren && (lootSpawnPositions == null || lootSpawnPositions.Count == 0))
            CollectSpawnPointsFromRoot();

        SpawnLootOnMap();
    }

    // Colectează din copii lui spawnPointsRoot (grid preplasat în scenă)
    private void CollectSpawnPointsFromRoot()
    {
        lootSpawnPositions = new List<Transform>();
        foreach (Transform t in spawnPointsRoot)
        {
            lootSpawnPositions.Add(t);
        }
    }

    public void SpawnLootOnMap()
    {
        if (loot == null || loot.Count == 0)
        {
            Debug.LogWarning("[LootSpawnerManager] 'loot' e gol. Nimic de spawnat.");
            return;
        }
        if (lootSpawnPositions == null || lootSpawnPositions.Count == 0)
        {
            Debug.LogWarning("[LootSpawnerManager] 'lootSpawnPositions' e gol. Adaugă puncte sau activează autoCollect.");
            return;
        }

        int desiredCount = Mathf.Clamp(Random.Range(minLootToSpawn, maxLootToSpawn + 1), 0, lootSpawnPositions.Count);
        if (desiredCount <= 0) return;

        // Shufflăm punctele
        List<Transform> shuffled = new List<Transform>(lootSpawnPositions);
        Shuffle(shuffled);

        // Selectăm punctele respectând separarea, cu relaxare opțională
        List<Transform> accepted = SelectSpawnPositions(shuffled, desiredCount);

        // Instanțiem loot pe punctele acceptate
        for (int i = 0; i < accepted.Count; i++)
        {
            GameObject prefab = PickRandomLoot();
            if (prefab == null) continue;

            Transform pt = accepted[i];
            GameObject inst = Instantiate(prefab, pt.position, Quaternion.identity, lootContainer);
            _spawnedLoot.Add(inst);
        }

        Debug.Log($"[LootSpawnerManager] Spawned {accepted.Count}/{desiredCount} loot items.");
    }

    public void ClearSpawnedLoot()
    {
        for (int i = _spawnedLoot.Count - 1; i >= 0; i--)
        {
            var go = _spawnedLoot[i];
            if (go != null) Destroy(go);
        }
        _spawnedLoot.Clear();
    }

    public void RerollLoot()
    {
        ClearSpawnedLoot();
        SpawnLootOnMap();
    }

    private List<Transform> SelectSpawnPositions(List<Transform> points, int desiredCount)
    {
        List<Transform> accepted = new List<Transform>();
        if (!preventClumping)
        {
            for (int i = 0; i < points.Count && accepted.Count < desiredCount; i++)
                accepted.Add(points[i]);
            return accepted;
        }

        float currentSep = Mathf.Max(0f, minSeparation);
        int passes = Mathf.Max(1, relaxationPasses + 1); // prima trecere + relaxări

        // lucrăm pe o listă de candidate rămasă
        List<Transform> remaining = new List<Transform>(points);

        for (int pass = 0; pass < passes && accepted.Count < desiredCount; pass++)
        {
            for (int i = 0; i < remaining.Count && accepted.Count < desiredCount; i++)
            {
                var candidate = remaining[i];
                if (IsFarEnough(candidate.position, accepted, currentSep))
                    accepted.Add(candidate);
            }

            // dacă nu am atins numărul dorit, relaxăm separarea și încercăm restul punctelor neacceptate
            if (accepted.Count < desiredCount && pass < passes - 1)
            {
                currentSep *= 0.5f; // relax
                // Recalculează remaining (puncte neacceptate)
                var newRemaining = new List<Transform>();
                for (int i = 0; i < points.Count; i++)
                {
                    if (!accepted.Contains(points[i]))
                        newRemaining.Add(points[i]);
                }
                remaining = newRemaining;
            }
        }

        // ca fallback final: dacă încă suntem sub desired, completăm ignorând separarea
        for (int i = 0; i < points.Count && accepted.Count < desiredCount; i++)
        {
            if (!accepted.Contains(points[i]))
                accepted.Add(points[i]);
        }

        return accepted;
    }

    private bool IsFarEnough(Vector3 candidate, List<Transform> accepted, float sep)
    {
        if (accepted.Count == 0) return true;
        float sepSqr = sep * sep;
        for (int i = 0; i < accepted.Count; i++)
        {
            if ((accepted[i].position - candidate).sqrMagnitude < sepSqr)
                return false;
        }
        return true;
    }

    private GameObject PickRandomLoot()
    {
        if (loot == null || loot.Count == 0) return null;
        return loot[Random.Range(0, loot.Count)];
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}
