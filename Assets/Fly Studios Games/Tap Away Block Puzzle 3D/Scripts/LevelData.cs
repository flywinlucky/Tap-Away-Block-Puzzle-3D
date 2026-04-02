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

    [HideInInspector]
    [Range(0.25f, 1f)]
    public float fillRatio = 1f;

    [HideInInspector]
    public bool adaptiveDensity = true;

    // Make this field serialized so Unity stores it inside the asset
    [SerializeField]
    [HideInInspector]
    private List<BlockData> blocks = new List<BlockData>();

    private const int MinGridSize = 2;
    private const int MaxGridSize = 10;
    private const int MaxGenerateAttempts = 8;

    public List<BlockData> GetBlocks() => blocks ?? (blocks = new List<BlockData>());

    public int GetGridLength() => Mathf.Clamp(customGridLength, MinGridSize, MaxGridSize);
    public int GetGridHeight() => Mathf.Clamp(customGridHeight, MinGridSize, MaxGridSize);

    public float GetEffectiveFillRatio()
    {
        float ratio = Mathf.Clamp(fillRatio, 0.25f, 1f);
        if (!adaptiveDensity)
        {
            return ratio;
        }

        int volume = GetGridLength() * GetGridHeight() * GetGridLength();

        // Keep large levels responsive while preserving smaller levels near full density.
        if (volume >= 700)
        {
            ratio = Mathf.Min(ratio, 0.45f);
        }
        else if (volume >= 500)
        {
            ratio = Mathf.Min(ratio, 0.55f);
        }
        else if (volume >= 300)
        {
            ratio = Mathf.Min(ratio, 0.7f);
        }

        return ratio;
    }

    public int GetEstimatedBlockCount()
    {
        int volume = GetGridLength() * GetGridHeight() * GetGridLength();
        return Mathf.Clamp(Mathf.RoundToInt(volume * GetEffectiveFillRatio()), 1, volume);
    }

    /// <summary>
    /// Punctul de intrare pentru generarea nivelului.
    /// </summary>
    public void Generate()
    {
        try
        {
            if (!GenerateSolvableLevelWithRetries())
            {
                Debug.LogError("Level generation failed after all retries.");
            }
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
    private bool GenerateSolvableLevelWithRetries()
    {
        int baseSeed = seed;

        for (int attempt = 0; attempt < MaxGenerateAttempts; attempt++)
        {
            int attemptSeed = baseSeed + (attempt * 7919);
            if (TryGenerateSolvableLevel(attemptSeed, out List<BlockData> generated))
            {
                blocks = generated;
                Debug.Log("Level generated: " + blocks.Count + " blocks. Attempt " + (attempt + 1) + ".");
                return true;
            }
        }

        return false;
    }

    private bool TryGenerateSolvableLevel(int attemptSeed, out List<BlockData> generatedBlocks)
    {
        generatedBlocks = new List<BlockData>();
        Random.InitState(attemptSeed);

        int gridLength = GetGridLength();
        int gridHeight = GetGridHeight();

        if (gridLength <= 0 || gridHeight <= 0)
        {
            Debug.LogWarning("Invalid grid dimensions for generation.");
            return false;
        }

        // 1. Build all candidate cells in the grid.
        List<Vector3Int> allPositions = new List<Vector3Int>();
        int offsetLength = gridLength / 2;
        int offsetHeight = gridHeight / 2;
        for (int x = -offsetLength; x < gridLength - offsetLength; x++)
        {
            for (int y = -offsetHeight; y < gridHeight - offsetHeight; y++)
            {
                for (int z = -offsetLength; z < gridLength - offsetLength; z++)
                {
                    allPositions.Add(new Vector3Int(x, y, z));
                }
            }
        }

        // 2. Keep only a target amount for adaptive density.
        int targetBlockCount = Mathf.Clamp(GetEstimatedBlockCount(), 1, allPositions.Count);
        List<Vector3Int> chosenPositions = allPositions.OrderBy(_ => Random.value).Take(targetBlockCount).ToList();

        for (int i = 0; i < chosenPositions.Count; i++)
        {
            generatedBlocks.Add(new BlockData { position = chosenPositions[i] });
        }

        // 3. Prepare structures for forward solvable assignment.
        List<BlockData> remainingBlocks = new List<BlockData>(generatedBlocks);
        HashSet<Vector3Int> clearedPositions = new HashSet<Vector3Int>();

        // 4. Assign directions in passes. A block is movable if there is a clear path to exit.
        while (remainingBlocks.Count > 0)
        {
            List<BlockData> blocksClearedThisPass = new List<BlockData>();
            List<BlockData> orderedRemaining = remainingBlocks.OrderBy(_ => Random.value).ToList();
            HashSet<Vector3Int> blockedByRemaining = new HashSet<Vector3Int>(remainingBlocks.Select(b => b.position));

            foreach (var currentBlock in orderedRemaining)
            {
                blockedByRemaining.Remove(currentBlock.position);
                MoveDirection? possibleDirection = FindForwardPath(
                    currentBlock.position,
                    blockedByRemaining,
                    clearedPositions,
                    gridLength,
                    gridHeight);
                blockedByRemaining.Add(currentBlock.position);

                if (possibleDirection.HasValue)
                {
                    currentBlock.direction = possibleDirection.Value;
                    blocksClearedThisPass.Add(currentBlock);
                }
            }

            if (blocksClearedThisPass.Count == 0 && remainingBlocks.Count > 0)
            {
                return false;
            }

            foreach (var block in blocksClearedThisPass)
            {
                clearedPositions.Add(block.position);
                remainingBlocks.Remove(block);
            }
        }

        // 5. Add stable random visual rotations.
        foreach (var block in generatedBlocks)
        {
            block.randomVisualRotation = Quaternion.Euler(
                Random.Range(0, 4) * 90f,
                Random.Range(0, 4) * 90f,
                Random.Range(0, 4) * 90f
            );
        }

        return true;
    }
    
    /// <summary>
    /// Caută o cale de mișcare "înainte". O cale este validă dacă duce în afara grilei sau într-o locație deja eliberată.
    /// </summary>
    private MoveDirection? FindForwardPath(
        Vector3Int blockPos,
        HashSet<Vector3Int> blockedByRemaining,
        HashSet<Vector3Int> clearedPositions,
        int gridLength,
        int gridHeight)
    {
        var directions = System.Enum.GetValues(typeof(MoveDirection))
                                    .Cast<MoveDirection>()
                                    .OrderBy(_ => Random.value);

        foreach (var dir in directions)
        {
            if (HasClearPathToExit(blockPos, dir, blockedByRemaining, gridLength, gridHeight))
            {
                return dir;
            }

            Vector3Int targetPos = blockPos + GetVectorFromEnum(dir);
            if (clearedPositions.Contains(targetPos))
            {
                return dir;
            }
        }

        return null;
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

    private bool HasClearPathToExit(
        Vector3Int origin,
        MoveDirection direction,
        HashSet<Vector3Int> blockedByRemaining,
        int gridLength,
        int gridHeight)
    {
        Vector3Int dir = GetVectorFromEnum(direction);
        Vector3Int cursor = origin + dir;

        while (IsInBounds(cursor, gridLength, gridHeight))
        {
            if (blockedByRemaining.Contains(cursor))
            {
                return false;
            }

            cursor += dir;
        }

        return true;
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

