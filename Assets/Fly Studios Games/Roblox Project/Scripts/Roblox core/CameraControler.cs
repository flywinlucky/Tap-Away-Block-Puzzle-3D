using UnityEngine;

public class CameraControler : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;        // TPS target (player)
    public Transform targetFPS;     // FPS target (cap / ochi)
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0);

    [Header("Rotation Settings")]
    public float mouseSensitivity = 3.0f;
    public Vector2 pitchLimits = new Vector2(-80, 85);

    [Header("Zoom Settings")]
    public float distance = 5.0f;
    public float minDistance = 0.5f;
    public float maxDistance = 15.0f;
    public float zoomSpeed = 5.0f;

    [Header("Collision (Anti-Clip)")]
    public LayerMask collisionLayers;
    public float wallOffset = 0.2f;

    [Header("UI")]
    public GameObject fakeCursorUI;

    [HideInInspector] public bool isFPS = false;

    // Private
    private Vector3 currentRotation;
    private float currentDistance;
    private Transform currentTarget;
    private Camera cam;

    void Start()
    {
        currentDistance = distance;
        currentRotation = transform.eulerAngles;
        currentTarget = target;
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (!currentTarget) return;

        // ---------------- ZOOM ----------------
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance -= scroll * zoomSpeed;
        distance = Mathf.Clamp(distance, 0, maxDistance);

        isFPS = distance <= minDistance;
        currentTarget = isFPS && targetFPS != null ? targetFPS : target;

        // ---------------- ROTATION ----------------
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        if (isFPS || Input.GetMouseButton(1))
        {
            currentRotation.y += mouseX;
            currentRotation.x -= mouseY;
        }

        currentRotation.x = Mathf.Clamp(currentRotation.x, pitchLimits.x, pitchLimits.y);

        // ---------------- CURSOR ----------------
        if (isFPS)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (fakeCursorUI) fakeCursorUI.SetActive(true);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (fakeCursorUI) fakeCursorUI.SetActive(false);
        }

        // ---------------- POSITION ----------------
        Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0f);
        Vector3 targetPos = currentTarget.position + targetOffset;

        Vector3 desiredPosition = isFPS
            ? targetPos
            : targetPos - rotation * Vector3.forward * distance;

        // ---------------- COLLISION TPS ----------------
        if (!isFPS)
        {
            RaycastHit hit;
            if (Physics.Linecast(targetPos, desiredPosition, out hit, collisionLayers))
            {
                float correctedDistance = Vector3.Distance(targetPos, hit.point) - wallOffset;
                desiredPosition = targetPos - rotation * Vector3.forward * correctedDistance;
            }
        }

        transform.position = desiredPosition;
        transform.rotation = rotation;
    }
}
