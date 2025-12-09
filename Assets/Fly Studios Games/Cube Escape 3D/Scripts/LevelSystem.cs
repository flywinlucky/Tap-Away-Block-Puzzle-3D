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
                currentLevelIndex = PlayerPrefs.GetInt("Levels_Levels_System", currentLevelIndex);
                randomizeLevelsAfterLastLevel = PlayerPrefs.GetInt("Random_bool_LS", randomizeLevelsAfterLastLevel ? 1 : 0) == 1;
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
            currentLevelIndex++;
            PlayerPrefs.SetInt("Levels_Levels_System", currentLevelIndex);
        }


        public void GenerateNextLevel()
        {
            if (randomizeLevelsAfterLastLevel)
            {
                Destroy(instantiatedCurrentLevel);

                int randomIndex;
                do
                {
                    randomIndex = Random.Range(0, levelPrefabs.Length);
                } while (randomIndex == lastLevelIndex);
                lastLevelIndex = randomIndex;
                currentLevelIndex = randomIndex;

                instantiatedCurrentLevel = Instantiate(levelPrefabs[randomIndex], instantiateInParent);
            }
            else
            {
                if (currentLevelIndex >= levelPrefabs.Length)
                {
                    currentLevelIndex = 0;
                    randomizeLevelsAfterLastLevel = true;
                    PlayerPrefs.SetInt("Random_bool_LS", randomizeLevelsAfterLastLevel ? 1 : 0);
                }

                Destroy(instantiatedCurrentLevel);
                instantiatedCurrentLevel = Instantiate(levelPrefabs[currentLevelIndex], instantiateInParent);
                currentLeveltext.text = "Level " + currentLevelIndex.ToString();
            }
        }

        private void OnApplicationQuit()
        {
            PlayerPrefs.SetInt("Random_bool_LS", randomizeLevelsAfterLastLevel ? 1 : 0);
            PlayerPrefs.SetInt("Levels_Levels_System", currentLevelIndex);
        }
    }
}