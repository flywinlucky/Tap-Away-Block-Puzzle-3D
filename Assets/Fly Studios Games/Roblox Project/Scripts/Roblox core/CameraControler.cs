using UnityEngine;

public class CameraControler : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0);

    [Header("Settings")]
    public float mouseSensitivity = 3.0f;
    public float rotationSmoothTime = 0.05f; 
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
    public GameObject fakeCursorUI; // Imaginea de cursor din centrul ecranului

    private Vector3 currentRotation;
    private Vector3 rotationSmoothVelocity;
    private float currentDistance;
    
    [HideInInspector] public bool isFPS = false;

    void Start()
    {
        currentDistance = distance;
        currentRotation = transform.eulerAngles;
    }

    void LateUpdate()
    {
        if (!target) return;

        // 1. ZOOM - Calculăm distanța mai întâi pentru a ști dacă suntem în FPS
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance -= scroll * zoomSpeed;
        distance = Mathf.Clamp(distance, 0, maxDistance);
        isFPS = (distance <= minDistance);

        // 2. INPUT ROTIRE & CURSOR LOGIC
        if (isFPS)
        {
            // --- MOD FPS: Stil Roblox (Centrat și activ) ---
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            currentRotation.y += mouseX;
            currentRotation.x -= mouseY;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false; // Îl ascundem pe cel de sistem ca să nu aibă lag
            if (fakeCursorUI != null) fakeCursorUI.SetActive(true); // Îl activăm pe cel UI (fără lag)
        }
        else if (Input.GetMouseButton(1))
        {
            // --- MOD TPS: Drag to Move (Cum era înainte) ---
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            currentRotation.y += mouseX;
            currentRotation.x -= mouseY;

            Cursor.lockState = CursorLockMode.None; // Cursorul e liber
            Cursor.visible = true;
            if (fakeCursorUI != null) fakeCursorUI.SetActive(false);
        }
        else
        {
            // --- MOD TPS: Liber (Nu rotim camera) ---
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (fakeCursorUI != null) fakeCursorUI.SetActive(false);
        }

        currentRotation.x = Mathf.Clamp(currentRotation.x, pitchLimits.x, pitchLimits.y);

        // 3. CALCULEAZĂ POZIȚIA
        Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0);
        Vector3 trueTargetPosition = target.position + targetOffset;
        
        Vector3 desiredPosition = isFPS ? trueTargetPosition : trueTargetPosition - (rotation * Vector3.forward * distance);

        // 4. WALL CLIP (doar în modul Third Person)
        if (!isFPS)
        {
            RaycastHit hit;
            if (Physics.Linecast(trueTargetPosition, desiredPosition, out hit, collisionLayers))
            {
                float tempDistance = Vector3.Distance(trueTargetPosition, hit.point) - wallOffset;
                desiredPosition = trueTargetPosition - (rotation * Vector3.forward * tempDistance);
            }
        }

        transform.position = desiredPosition;
        transform.rotation = rotation;
    }
}