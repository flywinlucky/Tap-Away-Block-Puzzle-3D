using System.Collections.Generic;
using UnityEngine;

/* Cube Escape 3D - Game Template: v1.0
 * By Fly Studios Assets
 * 
 * Support: flystudiosassets@gmail.com
 */

namespace CubeEscape3D
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow Camera Settings")]
        [Space]

        public Transform target;
        public Vector3 initialOffset;
        public float smoothTime = 0.25f;
        public float paddingOffset;
        private Vector3 currentVelocity;

        private CameraMargins cameraMargins;
        private PolygonCollider2D cameraMarginsPolygonCollider2D;

        private void Start()
        {
            cameraMargins = FindObjectOfType<CameraMargins>();

            if (cameraMargins != null)
            {
                cameraMarginsPolygonCollider2D = cameraMargins.GetComponent<PolygonCollider2D>();
            }
            else
            {
                Debug.LogWarning("Camera margins collider not found!");
            }

            if (target != null)
            {
                // Set the initial position of the camera based on the target and offset
                transform.position = target.position + initialOffset;
            }
        }

        private void LateUpdate()
        {
            if (target == null || cameraMargins == null)
                return;

            // Calculate the desired position of the camera
            Vector3 desiredPosition = target.position + initialOffset;

            // Calculate the bounds with padding
            Bounds bounds = cameraMarginsPolygonCollider2D.bounds;
            bounds.Expand(paddingOffset);

            // Clamp the desired position within the bounds
            Vector3 clampedPosition = ClampToBox(desiredPosition, bounds);

            // Smoothly move the camera towards the clamped position
            transform.position = Vector3.SmoothDamp(transform.position, clampedPosition, ref currentVelocity, smoothTime);
        }

        private Vector3 ClampToBox(Vector3 position, Bounds bounds)
        {
            Vector3 clampedPosition = position;
            clampedPosition.x = Mathf.Clamp(clampedPosition.x, bounds.min.x, bounds.max.x);
            clampedPosition.y = Mathf.Clamp(clampedPosition.y, bounds.min.y, bounds.max.y);
            return clampedPosition;
        }
    }
}