using TMPro;
using UnityEngine;

namespace CubeEscape3D
{
    public class LevelSystem : MonoBehaviour
    {
        public static LevelSystem Instance { get; private set; }

        [Header("Level Manager Settings")]
        [Space]
        public bool initializeOnStart; // Flag to indicate if the level system should be initialized on start

        //[HideInInspector]
        public bool randomizeLevelsAfterLastLevel; // Flag to indicate if levels should be randomized
        public Transform instantiateInParent; // Parent transform to instantiate levels

        [Header("Level Prefabs")]
        [Space]
        public GameObject[] levelPrefabs; // Array of level prefabs

        private GameObject instantiatedCurrentLevel; // Reference to the current level object
        public TMP_Text currentLeveltext;

        private int currentLevelIndex; // Index of the current level
        private int lastLevelIndex = -1; // Index of the last generated level

        private void Awake()
        {
            Instance = this;

            if (initializeOnStart)
            {
                currentLevelIndex = PlayerPrefs.GetInt("Levels_Levels_System", 0);
                randomizeLevelsAfterLastLevel = PlayerPrefs.GetInt("Random_bool_LS", randomizeLevelsAfterLastLevel ? 1 : 0) == 1;

                // Clamp index to valid range (start from 0 if out of range)
                if (levelPrefabs != null && levelPrefabs.Length > 0)
                {
                    if (currentLevelIndex < 0 || currentLevelIndex >= levelPrefabs.Length)
                        currentLevelIndex = 0;
                }
                else
                {
                    Debug.LogWarning("LevelPrefabs array is empty.");
                }

                GenerateNextLevel();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                GenerateNextLevel();
            }
        }



        public void NextLevel()
        {
            // Increase index and persist it, then load the next level
            currentLevelIndex++;
            PlayerPrefs.SetInt("Levels_Levels_System", currentLevelIndex);

            GenerateNextLevel();
        }


        public void GenerateNextLevel()
        {
            // If array invalid, bail out
            if (levelPrefabs == null || levelPrefabs.Length == 0)
            {
                Debug.LogWarning("No level prefabs assigned.");
                return;
            }

            // If we're in random mode -> pick a random level (avoid same as last)
            if (randomizeLevelsAfterLastLevel)
            {
                if (instantiatedCurrentLevel != null)
                    Destroy(instantiatedCurrentLevel);

                int randomIndex;
                do
                {
                    randomIndex = Random.Range(0, levelPrefabs.Length);
                } while (randomIndex == lastLevelIndex && levelPrefabs.Length > 1);

                lastLevelIndex = randomIndex;
                currentLevelIndex = randomIndex;

                instantiatedCurrentLevel = Instantiate(levelPrefabs[randomIndex], instantiateInParent);
                currentLeveltext.text = "Level " + (currentLevelIndex + 1).ToString();
            }
            else
            {
                // If we've gone past the last level, enable random mode and pick a random one
                if (currentLevelIndex >= levelPrefabs.Length)
                {
                    currentLevelIndex = 0; // optional: reset or keep as-is; we switch to random mode now
                    randomizeLevelsAfterLastLevel = true;
                    PlayerPrefs.SetInt("Random_bool_LS", randomizeLevelsAfterLastLevel ? 1 : 0);

                    // Now call GenerateNextLevel again to enter random branch
                    GenerateNextLevel();
                    return;
                }

                if (instantiatedCurrentLevel != null)
                    Destroy(instantiatedCurrentLevel);

                instantiatedCurrentLevel = Instantiate(levelPrefabs[currentLevelIndex], instantiateInParent);
                currentLeveltext.text = "Level " + (currentLevelIndex + 1).ToString();

                // Remember last level index for future random phase
                lastLevelIndex = currentLevelIndex;
            }
        }

        private void OnApplicationQuit()
        {
            PlayerPrefs.SetInt("Random_bool_LS", randomizeLevelsAfterLastLevel ? 1 : 0);
            PlayerPrefs.SetInt("Levels_Levels_System", currentLevelIndex);
        }
    }
}