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
    public float minDistance = 0.5f; // Distanța la care intră în FPS
    public float maxDistance = 15.0f;
    public float zoomSpeed = 5.0f;

    [Header("Collision (Anti-Clip)")]
    public LayerMask collisionLayers;
    public float wallOffset = 0.2f;

    private Vector3 currentRotation;
    private Vector3 rotationSmoothVelocity;
    private float currentDistance;
    
    // Variabilă publică să știe și Player-ul dacă suntem FPS
    [HideInInspector] public bool isFPS = false;

    void Start()
    {
        currentDistance = distance;
        currentRotation = transform.eulerAngles;
        // Cursorul în Roblox e vizibil dar blocat deseori sau ascuns în FPS
        // Pentru început, îl lăsăm vizibil dar îl centrăm dacă e FPS
    }

    void LateUpdate()
    {
        if (!target) return;

        // 1. INPUT - Rotire (În FPS se rotește mereu, în TPS doar cu Click Dreapta ca în Roblox)
        if (Input.GetMouseButton(1) || isFPS) 
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            currentRotation.y += mouseX;
            currentRotation.x -= mouseY;
            currentRotation.x = Mathf.Clamp(currentRotation.x, pitchLimits.x, pitchLimits.y);

            // În mod FPS, blocăm cursorul în centru
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            // În mod TPS, când nu ținem click dreapta, eliberăm mouse-ul
            Cursor.lockState = CursorLockMode.None;
        }

        // 2. ZOOM
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance -= scroll * zoomSpeed;
        distance = Mathf.Clamp(distance, 0, maxDistance); // Permitem 0 pentru FPS

        // Detectăm dacă suntem în modul FPS
        isFPS = (distance <= minDistance);

        // 3. CALCULEAZĂ POZIȚIA
        Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0);
        Vector3 trueTargetPosition = target.position + targetOffset;
        
        // Dacă suntem FPS, camera stă fix în targetOffset
        Vector3 desiredPosition = isFPS ? trueTargetPosition : trueTargetPosition - (rotation * Vector3.forward * distance);

        // 4. WALL CLIP (doar dacă nu suntem FPS)
        if (!isFPS)
        {
            RaycastHit hit;
            if (Physics.Linecast(trueTargetPosition, desiredPosition, out hit, collisionLayers))
            {
                currentDistance = Vector3.Distance(trueTargetPosition, hit.point) - wallOffset;
                if (currentDistance < minDistance) isFPS = true; // Forțăm FPS dacă peretele e prea aproape
                desiredPosition = trueTargetPosition - (rotation * Vector3.forward * currentDistance);
            }
        }

        transform.position = desiredPosition;
        transform.rotation = rotation;
    }
}