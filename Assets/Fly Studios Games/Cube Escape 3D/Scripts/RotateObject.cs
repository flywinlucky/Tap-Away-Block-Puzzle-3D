using UnityEngine;

namespace CubeEscape3D
{
    public class RotateObject : MonoBehaviour
    {
        public Vector3 position;
        public float spped;

        void Update()
        {
            transform.Rotate(position, spped * Time.deltaTime);
        }
    }
}