using UnityEngine;

namespace CubeEscape3D
{
    public class DestroyAfter : MonoBehaviour
    {
        public float destroyTime; // Time in seconds after which the object will be destroyed

        void Start()
        {
            Destroy(gameObject, destroyTime); // Destroy the object after the specified time
        }
    }
}