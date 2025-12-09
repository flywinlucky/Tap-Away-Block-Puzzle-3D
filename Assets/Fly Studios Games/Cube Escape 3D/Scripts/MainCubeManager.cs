using UnityEngine;

namespace CubeEscape3D
{
    public class MainCubeManager : MonoBehaviour
    {
        public static MainCubeManager Instance { get; private set; }

        [Header("Touch Tags Settings")]
        [Space]
        public string killZoneTag;
        public string winZoneTag;

        [Header("Encouraging Text Settings")]
        [Space]
        public LayerMask encouragingTextHitLayerMask; // Layer mask to filter raycast hits
        public Transform encouragingTextInstantiatePosition; // Position to instantiate the encouraging text
        public GameObject encouragingTextPrefab; // Prefab for the encouraging text

        [Header("Folow Target")]
        [Space]
        public Transform cameraFollowTarget; // Point for the camera to follow
        public Transform cubeToPlace; // Point for the camera to follow

        private float rayLength = 1; // Length of the raycast
        private int hitsCount; // Number of hits from raycasting

        private void Awake()
        {
            Instance = this;
        }

        public void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(killZoneTag))
            {
                GameController.Instance.GameOver();
                Destroy(gameObject);
            }

            if (other.CompareTag(winZoneTag))
            {
                GameController.Instance.GameWin();
                Destroy(gameObject);
            }
        }

        public void ResetPosition(Transform lastPoint)
        {
            transform.position = lastPoint.position;
            Raycast(transform);
        }

        public void Raycast(Transform hitTargetPosition)
        {
            Ray rayUp = new Ray(hitTargetPosition.position, hitTargetPosition.up);
            Debug.DrawRay(rayUp.origin, rayUp.direction * rayLength, Color.red, 2);

            if (Physics.Raycast(rayUp, out RaycastHit hitUp, rayLength, encouragingTextHitLayerMask))
                hitsCount++;

            Ray rayDown = new Ray(hitTargetPosition.position, -hitTargetPosition.up);
            Debug.DrawRay(rayDown.origin, rayDown.direction * rayLength, Color.red, 2);

            if (Physics.Raycast(rayDown, out RaycastHit rayDown_, rayLength, encouragingTextHitLayerMask))
                hitsCount++;

            Ray rayRight = new Ray(hitTargetPosition.position, hitTargetPosition.right);
            Debug.DrawRay(rayRight.origin, rayRight.direction * rayLength, Color.red, 2);

            if (Physics.Raycast(rayRight, out RaycastHit hitRight, rayLength, encouragingTextHitLayerMask))
                hitsCount++;

            Ray rayLeft = new Ray(hitTargetPosition.position, -hitTargetPosition.right);
            Debug.DrawRay(rayLeft.origin, rayLeft.direction * rayLength, Color.red, 2);

            if (Physics.Raycast(rayLeft, out RaycastHit hitLeft, rayLength, encouragingTextHitLayerMask))
                hitsCount++;

            if (hitsCount >= 1)
            {
                GameObject newEncouragingTextPrefab = Instantiate(encouragingTextPrefab, encouragingTextInstantiatePosition.position, Quaternion.identity, encouragingTextInstantiatePosition);
                EncouragingText encouragingText = newEncouragingTextPrefab.GetComponent<EncouragingText>();
                if (encouragingText != null)
                    encouragingText.ShowText(hitsCount);

            }

            Invoke("ResetHitsCount", 0.1f);
        }

        private void ResetHitsCount()
        {
            Vector3 newPosition = cameraFollowTarget.position;
            newPosition.z = hitsCount;
            cameraFollowTarget.position = newPosition;
            hitsCount = 0;
        }
    }
}