using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace CubeEscape3D
{
    public class GameController : MonoBehaviour
    {
        public static GameController Instance { get; private set; }

        [Header("Level Manager Settings")]
        [Space]
        public float explosionForce;
        public float explosionRadius;
        public float cubeChangePlaceSpeed = 0.5f;

        [Header("UI Panels")]
        [Space]

        public GameObject levelWinPanel;
        public GameObject levelOverPanel;

        [Header("Game Objects")]
        [Space]
        public GameObject cubeToCreate;
        public GameObject cubeManagerBrain;
        public GameObject vfx;

        [Header("UI Animations")]
        [Space]
        public Animator uiPanel;
        public Animator brokenScreenPanel;

        [Header("Transforms")]
        [Space]
        public Transform cubeSpawner;
        public Transform cubeToPlace;

        [Header("Game Events")]
        [Space]
        public UnityEvent onPasteNewCube;
        [Space]
        public UnityEvent onGameWin;
        public UnityEvent onGameLose;

        private CubePosition newCube;
        private GameObject possibleCubeToCreate;
        private List<GameObject> allCubes = new List<GameObject>();
        private List<Vector3> allCubePositions = new List<Vector3>();
        private List<Vector3> positions = new List<Vector3>();

        private bool cubePlaced;
        private bool onlyOnceGameOver;
        private bool first;
        private bool fullArowControl;

        private void Awake()
        {
            Instance = this;

            for (int i = 0; i < cubeSpawner.childCount; i++)
            {
                Transform cube = cubeSpawner.GetChild(i);
                allCubePositions.Add(cube.position);
                allCubes.Add(cube.gameObject);
            }

            if (allCubePositions.Count > 0)
            {
                Vector3 lastPosition = allCubePositions[allCubePositions.Count - 1];
                newCube = new CubePosition((int)lastPosition.x, (int)lastPosition.y, (int)lastPosition.z);

                cubeManagerBrain.transform.position = lastPosition;
            }

            StartCoroutine(ShowCubePlace());
        }

        private IEnumerator ShowCubePlace()
        {
            while (true)
            {
                SpawnPosition();
                yield return new WaitForSeconds(cubeChangePlaceSpeed);
            }
        }

        private void SpawnPosition()
        {
            if (cubeToPlace != null)
            {
                List<Vector3> positions = new List<Vector3>();
                if (IsPositionEmpty(new Vector3(newCube.x + 1, newCube.y, newCube.z)) && newCube.x + 1 != cubeToPlace.position.x)
                    positions.Add(new Vector3(newCube.x + 1, newCube.y, newCube.z));
                if (IsPositionEmpty(new Vector3(newCube.x, newCube.y + 1, newCube.z)) && newCube.y + 1 != cubeToPlace.position.y)
                    positions.Add(new Vector3(newCube.x, newCube.y + 1, newCube.z));
                if (IsPositionEmpty(new Vector3(newCube.x, newCube.y - 1, newCube.z)) && newCube.y - 1 != cubeToPlace.position.y)
                    positions.Add(new Vector3(newCube.x, newCube.y - 1, newCube.z));
                if (IsPositionEmpty(new Vector3(newCube.x - 1, newCube.y, newCube.z)) && newCube.x - 1 != cubeToPlace.position.x)
                    positions.Add(new Vector3(newCube.x - 1, newCube.y, newCube.z));

                if (positions.Count == 0)
                {
                    if (cubePlaced)
                    {
                        GameOver();
                    }
                }
                else
                {
                    cubeToPlace.position = positions[UnityEngine.Random.Range(0, positions.Count)];
                }
            }
        }

        public void SpawnCube()
        {
            if (cubeToPlace)
            {
                possibleCubeToCreate = cubeToCreate;

                if (!first)
                {
                    uiPanel.SetBool("uiTransition", true);
                    first = true;
                }

                Vector3 roundedPosition = RoundPosition(cubeToPlace.position);
                GameObject createCube = possibleCubeToCreate;
                GameObject newCube = Instantiate(createCube, roundedPosition, Quaternion.identity, cubeSpawner);
                allCubes.Add(newCube);
                this.newCube.SetVector(roundedPosition);
                MainCubeManager.Instance.ResetPosition(newCube.transform);

                allCubePositions.Add(roundedPosition);

                Instantiate(vfx, roundedPosition, Quaternion.identity);
                onPasteNewCube?.Invoke();

                if (fullArowControl == false)
                {
                    SpawnPosition();
                }

                cubePlaced = true;
            }

        }

        private Vector3 RoundPosition(Vector3 position)
        {
            int roundedX = Mathf.RoundToInt(position.x);
            int roundedY = Mathf.RoundToInt(position.y);
            int roundedZ = Mathf.RoundToInt(position.z);
            return new Vector3(roundedX, roundedY, roundedZ);
        }


        private bool IsPositionEmpty(Vector3 targetPos)
        {
            if (targetPos.y == 0)
            {
                return false;
            }

            foreach (Vector3 pos in allCubePositions)
            {
                if (pos == targetPos)
                {
                    return false;
                }
            }

            return true;
        }

        public void GameOver()
        {
            if (!onlyOnceGameOver)
            {
                foreach (GameObject cube in allCubes)
                {
                    Rigidbody rb = cube.GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        rb = cube.AddComponent<Rigidbody>();
                    }
                    rb.AddExplosionForce(explosionForce, -Vector3.forward, explosionRadius);
                    cube.transform.SetParent(null);
                }

                brokenScreenPanel.SetBool("BrokenScreen", true);
                onlyOnceGameOver = true;
                onGameLose?.Invoke();
                levelOverPanel.SetActive(true);
            }
        }

        public void UiTransitionTrigger(bool value)
        {
            uiPanel.SetBool("uiTransition", value);
        }

        public void GameWin()
        {
            onGameWin?.Invoke();
            levelWinPanel.SetActive(true);
        }

        public void RestartCurrentScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void NextLevel()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    #region Cube Position
    struct CubePosition
    {
        public int x, y, z;

        public CubePosition(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3 GetVector()
        {
            return new Vector3(x, y, z);
        }

        public void SetVector(Vector3 pos)
        {
            x = Mathf.RoundToInt(pos.x);
            y = Mathf.RoundToInt(pos.y);
            z = Mathf.RoundToInt(pos.z);
        }
    }
    #endregion
}