using System.Collections;
using UnityEngine;

namespace CubeEscape3D
{
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Initiates a camera shake with custom duration and intensity.
        /// </summary>
        /// <param name="duration">The duration of the camera shake.</param>
        /// <param name="intensity">The intensity of the camera shake.</param>
        public void CallCameraShake(float duration, float intensity)
        {
            StartCoroutine(ShakeCameraCoroutine(duration, intensity));
        }

        private IEnumerator ShakeCameraCoroutine(float duration, float intensity)
        {
            // Store the original position of the camera
            Vector3 originalPosition = transform.position;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Generate random offsets for shaking the camera
                float offsetX = Random.Range(-intensity, intensity);
                float offsetY = Random.Range(-intensity, intensity);
                float offsetZ = Random.Range(-intensity, intensity);

                // Create a shake offset vector
                Vector3 shakeOffset = new Vector3(offsetX, offsetY, offsetZ);

                // Apply the shake offset to the camera's position
                transform.position = originalPosition + shakeOffset;

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Reset the camera position to its original position after the shake duration
            transform.position = originalPosition;
        }
    }
}