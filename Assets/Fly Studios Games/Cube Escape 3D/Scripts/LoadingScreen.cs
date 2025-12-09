using System.Collections;
using UnityEngine;

namespace CubeEscape3D
{
    public class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private float duration = 2f;
        [SerializeField] private bool enableOnStart = false;
        [SerializeField] private GameObject loadingScreenObject;

        private void Start()
        {
            if (enableOnStart)
            {
                CallLoadingScreen();
            }
        }

        public void CallLoadingScreen()
        {
            StartCoroutine(ShowLoadingScreen());
        }

        private IEnumerator ShowLoadingScreen()
        {
            loadingScreenObject.SetActive(true);
            yield return new WaitForSeconds(duration);
            loadingScreenObject.SetActive(false);
        }
    }
}