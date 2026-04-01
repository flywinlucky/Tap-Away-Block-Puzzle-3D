using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum MoveDirection { Forward, Back, Up, Down, Left, Right }
public enum Difficulty { Custom }

[System.Serializable]
public class BlockData
{
    public Vector3Int position;
    public MoveDirection direction;
    public Quaternion randomVisualRotation;
}

public class LevelData : ScriptableObject
{
    [Header("Generation Settings")]
    [HideInInspector]
    [Range(2, 10)]
    public int customGridLength = 3; // Lungimea nivelului

    [HideInInspector]
    [Range(2, 10)]
    public int customGridHeight = 3; // Înălțimea nivelului

    [HideInInspector]
    public int seed = 0;

    // Make this field serialized so Unity stores it inside the asset
    [SerializeField]
    [HideInInspector]
    private List<BlockData> blocks = new List<BlockData>();

    private const int MinGridSize = 2;
    private const int MaxGridSize = 10;

    public List<BlockData> GetBlocks() => blocks ?? (blocks = new List<BlockData>());

    public int GetGridLength() => Mathf.Clamp(customGridLength, MinGridSize, MaxGridSize);
    public int GetGridHeight() => Mathf.Clamp(customGridHeight, MinGridSize, MaxGridSize);

    /// <summary>
    /// Punctul de intrare pentru generarea nivelului.
    /// </summary>
    public void Generate()
    {
        try
        {
            GenerateSolvableLevel();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("LevelData.Generate failed: " + ex.Message + "\n" + ex.StackTrace);
        }
    }

    /// <summary>
    /// Algoritm nou care construiește o soluție de la primul la ultimul bloc, prevenind ciclurile.
    /// Funcționează prin a găsi mai întâi blocurile care pot ieși, apoi pe cele care se pot muta în spațiile eliberate.
    /// </summary>
    private void GenerateSolvableLevel()
    {
        blocks = new List<BlockData>();
        Random.InitState(seed);
        int gridLength = GetGridLength();
        int gridHeight = GetGridHeight();

        if (gridLength <= 0 || gridHeight <= 0)
        {
            Debug.LogWarning("Invalid grid dimensions for generation.");
            return;
        }

        // 1. Creăm un bloc pentru fiecare celulă din grilă
        List<BlockData> generatedBlocks = new List<BlockData>();
        int offsetLength = gridLength / 2;
        int offsetHeight = gridHeight / 2;
        for (int x = -offsetLength; x < gridLength - offsetLength; x++)
        {
            for (int y = -offsetHeight; y < gridHeight - offsetHeight; y++)
            {
                for (int z = -offsetLength; z < gridLength - offsetLength; z++)
                {
                    generatedBlocks.Add(new BlockData { position = new Vector3Int(x, y, z) });
                }
            }
        }

        // 2. Pregătim structurile de date pentru algoritmul "forward"
        List<BlockData> remainingBlocks = new List<BlockData>(generatedBlocks);
        HashSet<Vector3Int> clearedPositions = new HashSet<Vector3Int>(); // Poziții considerate "libere" pentru mișcare

        // 3. Atribuim direcții în "pase", construind calea de rezolvare
        // Acest lucru previne ciclurile, deoarece un bloc poate fi eliberat doar pe baza blocurilor eliberate în pasele ANTERIOARE.
        while (remainingBlocks.Count > 0)
        {
            List<BlockData> blocksClearedThisPass = new List<BlockData>();
            
            // Amestecăm blocurile pentru a asigura că soluția nu este mereu aceeași
            remainingBlocks = remainingBlocks.OrderBy(b => Random.value).ToList();

            // Faza 1: Găsim TOATE blocurile care pot fi eliberate în acest pas, pe baza stării anterioare
            foreach (var currentBlock in remainingBlocks)
            {
                // Căutăm o direcție în care blocul poate fi mișcat (spre exterior sau spre o poziție deja eliberată)
                MoveDirection? possibleDirection = FindForwardPath(currentBlock.position, clearedPositions, gridLength, gridHeight);

                if (possibleDirection.HasValue)
                {
                    // Atribuim temporar direcția și adăugăm blocul la lista pentru acest pas
                    currentBlock.direction = possibleDirection.Value;
                    blocksClearedThisPass.Add(currentBlock);
                }
            }


            if (blocksClearedThisPass.Count == 0 && remainingBlocks.Count > 0)
            {
                // Acest caz nu ar trebui să se întâmple cu noua logică, dar rămâne ca o siguranță.
                Debug.LogError($"Failed to generate a solvable level. A deadlock was detected with {remainingBlocks.Count} blocks left. This indicates a flaw in the generation logic.");
                break; // Ieșim pentru a preveni o buclă infinită
            }
            
            // Faza 2: Validăm și actualizăm starea pentru toate blocurile găsite în acest pas
            foreach (var block in blocksClearedThisPass)
            {
                clearedPositions.Add(block.position);
                remainingBlocks.Remove(block);
            }
            
        }


        // 4. Setăm rotațiile vizuale aleatorii pentru estetică
        foreach (var block in generatedBlocks)
        {
            block.randomVisualRotation = Quaternion.Euler(
                Random.Range(0, 4) * 90f,
                Random.Range(0, 4) * 90f,
                Random.Range(0, 4) * 90f
            );
        }

        this.blocks = generatedBlocks;
        Debug.Log($"Level successfully generated – {blocks.Count} blocks placed. Dimensions: {gridLength}x{gridHeight}.");
    }
    
    /// <summary>
    /// Caută o cale de mișcare "înainte". O cale este validă dacă duce în afara grilei sau într-o locație deja eliberată.
    /// </summary>
    private MoveDirection? FindForwardPath(Vector3Int blockPos, HashSet<Vector3Int> clearedPositions, int gridLength, int gridHeight)
    {
        var shuffledDirections = System.Enum.GetValues(typeof(MoveDirection))
                                            .Cast<MoveDirection>()
                                            .OrderBy(d => Random.value);

        foreach (var dir in shuffledDirections)
        {
            Vector3Int targetPos = blockPos + GetVectorFromEnum(dir);

            // O cale este validă dacă duce în afara grilei (spre ieșire)
            if (!IsInBounds(targetPos, gridLength, gridHeight))
            {
                return dir;
            }

            // Sau dacă duce într-o poziție care a fost deja eliberată de un alt bloc
            if (clearedPositions.Contains(targetPos))
            {
                return dir;
            }
        }
    
        return null; // Nicio cale de mișcare găsită în acest pas
    }

    #region Helper Functions

    private bool IsInBounds(Vector3Int pos, int gridLength, int gridHeight)
    {
        if (gridLength <= 0 || gridHeight <= 0) return false;

        int xzOffset = gridLength / 2;
        int yOffset = gridHeight / 2;

        int minX = -xzOffset;
        int maxX = gridLength - xzOffset;
        int minY = -yOffset;
        int maxY = gridHeight - yOffset;
        int minZ = -xzOffset;
        int maxZ = gridLength - xzOffset;

        return pos.x >= minX && pos.x < maxX &&
               pos.y >= minY && pos.y < maxY &&
               pos.z >= minZ && pos.z < maxZ;
    }
    
    private MoveDirection GetOppositeDirection(MoveDirection dir)
    {
        switch (dir)
        {
            case MoveDirection.Forward: return MoveDirection.Back;
            case MoveDirection.Back: return MoveDirection.Forward;
            case MoveDirection.Up: return MoveDirection.Down;
            case MoveDirection.Down: return MoveDirection.Up;
            case MoveDirection.Left: return MoveDirection.Right;
            case MoveDirection.Right: return MoveDirection.Left;
            default: throw new System.ArgumentOutOfRangeException();
        }
    }

    private Vector3Int GetVectorFromEnum(MoveDirection dir)
    {
        switch (dir)
        {
            case MoveDirection.Forward: return Vector3Int.forward;
            case MoveDirection.Back: return Vector3Int.back;
            case MoveDirection.Up: return Vector3Int.up;
            case MoveDirection.Down: return Vector3Int.down;
            case MoveDirection.Left: return Vector3Int.left;
            case MoveDirection.Right: return Vector3Int.right;
            default: return Vector3Int.zero;
        }
    }

    #endregion
}

