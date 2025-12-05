using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControler : MonoBehaviour
{
	[Header("Target Settings")]
    public Transform target;           // Trage aici Player-ul
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0); // Offset să ne uităm la cap/umeri

    [Header("Settings")]
    public float mouseSensitivity = 3.0f;
    public float rotationSmoothTime = 0.12f; // Cât de fină e rotirea
    public Vector2 pitchLimits = new Vector2(-40, 85); // Limita sus-jos (ca să nu te dai peste cap)

    [Header("Zoom Settings")]
    public float distance = 5.0f;     // Distanța curentă
    public float minDistance = 1.0f;  // Cât de aproape poți veni (First Person)
    public float maxDistance = 15.0f; // Cât de departe poți pleca
    public float zoomSpeed = 5.0f;

    [Header("Collision (Anti-Clip)")]
    public LayerMask collisionLayers; // Ce layere blochează camera (ex: Default, Ground)
    public float wallOffset = 0.2f;   // Distanța față de perete ca să nu intre textura în el

    // Variabile interne
    private Vector3 currentRotation;
    private Vector3 rotationSmoothVelocity;
    private float currentDistance;

    void Start()
    {
        currentDistance = distance;
        currentRotation = transform.eulerAngles;
    }

    void LateUpdate()
    {
        if (!target) return;

        // 1. INPUT - Rotire doar cu Click Dreapta
        if (Input.GetMouseButton(1)) 
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            currentRotation.y += mouseX;
            currentRotation.x -= mouseY;
            
            // Limităm rotirea sus-jos
            currentRotation.x = Mathf.Clamp(currentRotation.x, pitchLimits.x, pitchLimits.y);
        }

        // 2. ZOOM - Scroll Wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance -= scroll * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        // 3. CALCULEAZĂ POZIȚIA IDEALĂ (Fără coliziuni)
        // Aplicăm smoothing la rotație
        Vector3 nextRotation = Vector3.SmoothDamp(transform.eulerAngles, currentRotation, ref rotationSmoothVelocity, rotationSmoothTime);
        Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0);

        // Poziția unde ar vrea să fie camera
        Vector3 desiredPosition = target.position + targetOffset - (rotation * Vector3.forward * distance);

        // 4. WALL CLIP (Coliziunea cu pereții)
        // Tragem o linie de la Player spre Cameră. Dacă lovim ceva, mutăm camera acolo.
        RaycastHit hit;
        Vector3 trueTargetPosition = target.position + targetOffset;
        
        // Verificăm dacă e zid între player și locul unde vrea camera să stea
        if (Physics.Linecast(trueTargetPosition, desiredPosition, out hit, collisionLayers))
        {
            // Dacă lovim un zid, punem camera la punctul de impact (minus un mic offset)
            currentDistance = Vector3.Distance(trueTargetPosition, hit.point) - wallOffset;
            // Ne asigurăm că nu intrăm în capul playerului de tot (min distance)
            if (currentDistance < minDistance) currentDistance = minDistance;
            
            transform.position = trueTargetPosition - (rotation * Vector3.forward * currentDistance);
        }
        else
        {
            // Dacă nu e zid, stăm la distanța normală
            currentDistance = distance; 
            transform.position = desiredPosition;
        }

        // 5. Apply Rotation
        transform.rotation = rotation;
    }
}
