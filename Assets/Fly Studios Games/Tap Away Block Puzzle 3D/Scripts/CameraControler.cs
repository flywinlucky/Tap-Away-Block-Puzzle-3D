using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Controlează rotația unui obiect "target" în jurul centrului său geometric și zoom-ul camerei.
/// Include o funcție pentru a încadra automat obiectul în vizorul camerei.
/// </summary>
public class CameraControler : MonoBehaviour
{
    [Tooltip("Obiectul părinte care va fi rotit.")]
    public Transform target;

    [Header("Rotation Settings")]
    [Tooltip("Viteza de rotație a obiectului cu mouse-ul.")]
    public float rotationSpeed = 5.0f;
    [Tooltip("Cât de lină să fie mișcarea de rotație (valori mai mici = mai rapid).")]
    [Range(0.01f, 0.5f)]
    public float rotationDamping = 0.05f;

    [Header("Zoom Settings")]
    [Tooltip("Viteza cu care funcționează zoom-ul.")]
    public float zoomSpeed = 10.0f;
    [Tooltip("Cât de lin să fie zoom-ul (valori mai mici = mai rapid).")]
    [Range(0.01f, 0.5f)]
    public float zoomDamping = 0.1f;
    [Tooltip("Distanța minimă la care se poate apropia camera.")]
    public float minDistance = 3f;
    [Tooltip("Distanța maximă la care se poate depărta camera.")]
    public float maxDistance = 50f;
    [Tooltip("Marginea lăsată în jurul obiectului la încadrare (ex: 1.2 = 20% buffer).")]
    public float frameBuffer = 1.5f;

    [Tooltip("Permite rotația target-ului prin input. Dezactivează pentru UI modal (ex: shop).")]
    public bool rotationEnabled = true;

    [Header("Touch Settings")]
    [Tooltip("Factor de ajustare pentru sensibilitatea rotației pe mobil.")]
    public float touchRotationSensitivity = 0.5f;
    [Tooltip("Factor de ajustare pentru sensibilitatea zoom-ului pe mobil.")]
    public float touchZoomSensitivity = 0.1f;

    // --- Variabile Private ---
    private Vector3 _centerPoint;
    private float _currentDistance;
    private float _desiredDistance;
    private Camera _mainCamera;

    // Variabile pentru rotație lină (mouse)
    private Vector2 _rotationInput;
    private Vector2 _smoothVelocity;

    // NOU: Variabile pentru rotație lină (touch)
    private Vector2 _touchRotationInput;
    private Vector2 _touchSmoothVelocity;

    // Variabile pentru gesturi (touch)
    private int activeTouchId = -1; // ID-ul primului deget activ pentru rotație
    private float lastTouchDistance; // Ultima distanță între două degete pentru zoom

    // Schimbat în IEnumerator pentru a ne asigura că totul este inițializat corect
    IEnumerator Start()
    {
        _mainCamera = Camera.main;
        if (target == null || _mainCamera == null)
        {
            Debug.LogError("Target-ul sau Camera principală nu au fost setate.", this);
            this.enabled = false;
            yield break; // Oprește execuția dacă ceva lipsește
        }

        // Așteptăm un singur frame pentru a permite tuturor obiectelor (cuburilor)
        // să se inițializeze înainte de a calcula încadrarea.
        yield return new WaitForEndOfFrame();

        // Acum apelăm funcția de încadrare, care va acționa instantaneu.
        FrameTarget();
    }

    /// <summary>
    /// MODIFICAT: LateUpdate apelează acum funcțiile separate de Input și Aplicare
    /// </summary>
    void LateUpdate()
    {
        if (target)
        {
            HandleInput(); // Gestionează TOATE input-urile (touch și mouse)
            ApplyZoom();   // Aplică zoom-ul lin în fiecare frame
            transform.LookAt(_centerPoint); // Asigură că mereu privim spre centru

            // NOU: Detectăm rotația și declanșăm evenimentul
            if (!_hasRotated && DetectRotation())
            {
                _hasRotated = true;
                OnObjectRotated?.Invoke();
            }
        }
    }

    /// <summary>
    /// Calculează și aplică INSTANTANEU poziția optimă a camerei pentru a încadra obiectul.
    /// (Funcția aceasta rămâne neschimbată)
    /// </summary>
    public void FrameTarget()
    {
        // Pasul 1: Calculează "cutia" invizibilă (Bounds) care înconjoară toți copiii
        Bounds totalBounds = new Bounds();
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            _centerPoint = target.position;
            Debug.LogWarning("Target-ul nu are componente Renderer. Încadrarea poate fi imprecisă.", target);
            totalBounds = new Bounds(target.position, Vector3.one); // O cutie de rezervă
        }
        else
        {
            totalBounds = renderers[0].bounds;
            foreach (Renderer rend in renderers)
            {
                totalBounds.Encapsulate(rend.bounds);
            }
        }

        // Actualizăm punctul central pentru rotație, bazat pe centrul real al obiectului compus
        _centerPoint = totalBounds.center;

        // Pasul 2: Calculează distanța necesară pentru a vedea întreaga "cutie"
        float objectSize = Mathf.Max(totalBounds.size.x, totalBounds.size.y); // Luăm cea mai mare dimensiune (lățime sau înălțime)
        float cameraView = 2.0f * Mathf.Tan(0.5f * _mainCamera.fieldOfView * Mathf.Deg2Rad); // Cât vede camera la 1 unitate distanță
        float distanceToFit = (objectSize / _mainCamera.aspect) / cameraView; // Calculăm distanța

        // Adăugăm un buffer pentru a nu fi exact la margine, folosind variabila publică
        float finalDistance = distanceToFit * frameBuffer;

        // Pasul 3: Setăm noua distanță și o aplicăm IMEDIAT
        float targetDistance = Mathf.Clamp(finalDistance, minDistance, maxDistance);

        // Setăm ambele distanțe (curentă și dorită) la aceeași valoare pentru a anula orice efect de Lerp
        _desiredDistance = targetDistance;
        _currentDistance = targetDistance;

        // Poziționăm camera la distanța corectă de noul centru,
        // menținând direcția curentă a camerei. LookAt() din LateUpdate va corecta unghiul.
        transform.position = _centerPoint - transform.forward * _currentDistance;
    }


    /// <summary>
    /// NOU: Funcție unificată care gestionează TOATE input-urile.
    /// Prioritizează gesturile Touch peste cele de Mouse.
    /// </summary>
    private void HandleInput()
    {
        // Prioritizăm input-ul de la touch
        if (Input.touchCount > 0)
        {
            // --- GESTURI TOUCH ---

            // 1. Zoom (Pinch) - are prioritate
            if (Input.touchCount == 2)
            {
                // Oprim rotația activă dacă se detectează al doilea deget
                activeTouchId = -1;
                _rotationInput = Vector2.zero; // Oprim și inerția de la mouse
                _smoothVelocity = Vector2.zero;
                _touchRotationInput = Vector2.zero; // Oprim și inerția de la touch
                _touchSmoothVelocity = Vector2.zero;


                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);

                // Calculăm distanța curentă între cele două degete
                float currentTouchDistance = Vector2.Distance(touch0.position, touch1.position);

                if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
                {
                    lastTouchDistance = currentTouchDistance; // Inițializăm distanța
                }
                else if(touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
                {
                    // Calculăm diferența de distanță pentru zoom
                    float distanceDelta = currentTouchDistance - lastTouchDistance;
                    _desiredDistance -= distanceDelta * touchZoomSensitivity;
                    _desiredDistance = Mathf.Clamp(_desiredDistance, minDistance, maxDistance);

                    lastTouchDistance = currentTouchDistance; // Actualizăm distanța pentru următorul frame
                }
            }
            // 2. Rotație (un deget)
            else if (Input.touchCount == 1)
            {
                if (!rotationEnabled) return;

                Touch touch = Input.GetTouch(0);
                Vector2 rawTouchInput = Vector2.zero; // NOU: Stocăm input-ul brut aici

                if (activeTouchId == -1) // Înregistrăm primul deget activ
                {
                    activeTouchId = touch.fingerId;
                }

                if (touch.fingerId == activeTouchId) // Verificăm dacă este degetul activ
                {
                    if (touch.phase == TouchPhase.Moved)
                    {
                        // NOU: Stocăm delta-ul ca input brut în loc să-l aplicăm direct
                        rawTouchInput = touch.deltaPosition * touchRotationSensitivity;
                    }
                    else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        activeTouchId = -1; // Resetăm degetul activ
                    }
                }

                // NOU: Aplicăm SmoothDamp la input-ul de la touch
                // Când degetul se oprește, rawTouchInput este (0,0), așa că _touchRotationInput va scădea lin spre 0
                _touchRotationInput = Vector2.SmoothDamp(_touchRotationInput, rawTouchInput, ref _touchSmoothVelocity, rotationDamping);

                // Aplicăm rotația folosind valoarea netezită
                if (_touchRotationInput.magnitude > 0.001f)
                {
                    // Folosim logica de rotație inversată pentru mobil (drag)
                    // Înmulțim cu Time.deltaTime aici, deoarece input-ul (deltaPosition) este per-frame
                    target.RotateAround(_centerPoint, Vector3.up, _touchRotationInput.x * rotationSpeed * Time.deltaTime); 
                    target.RotateAround(_centerPoint, transform.right, -_touchRotationInput.y * rotationSpeed * Time.deltaTime);
                }
            }
        }
        else // --- INPUT MOUSE (dacă nu există touch) ---
        {
            // Resetăm degetul activ și inerția de la touch
            activeTouchId = -1; 
            _touchRotationInput = Vector2.SmoothDamp(_touchRotationInput, Vector2.zero, ref _touchSmoothVelocity, rotationDamping);
            
            // 1. Rotație Mouse (logica din vechiul HandleRotation)
            if (rotationEnabled)
            {
                Vector2 rawInput = Vector2.zero;
                // Preluăm input-ul brut de la mouse doar dacă un buton este apăsat
                if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
                {
                    rawInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                }

                // Netezim input-ul folosind SmoothDamp pentru o mișcare fluidă
                _rotationInput = Vector2.SmoothDamp(_rotationInput, rawInput, ref _smoothVelocity, rotationDamping);

                // Aplicăm rotația pe target în jurul centrului calculat (stil orbit)
                if (_rotationInput.magnitude > 0.001f)
                {
                    target.RotateAround(_centerPoint, Vector3.up, -_rotationInput.x * rotationSpeed);
                    target.RotateAround(_centerPoint, transform.right, _rotationInput.y * rotationSpeed);
                }
            }

            // 2. Zoom Mouse (logica din vechiul HandleZoom)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _desiredDistance -= scroll * zoomSpeed;
                _desiredDistance = Mathf.Clamp(_desiredDistance, minDistance, maxDistance);
            }
        }
    }

    /// <summary>
    /// NOU: Funcție separată care aplică zoom-ul lin în fiecare frame
    /// </summary>
    private void ApplyZoom()
    {
        // Aplicăm zoom-ul cu Lerp pentru o mișcare lină
        _currentDistance = Mathf.Lerp(_currentDistance, _desiredDistance, zoomDamping);

        // Actualizăm poziția camerei
        Vector3 directionFromCenter = (transform.position - _centerPoint).normalized;
        if (directionFromCenter == Vector3.zero) directionFromCenter = -transform.forward; // fallback
        transform.position = _centerPoint + directionFromCenter * _currentDistance;
    }

    // NOU: Eveniment declanșat când obiectul este rotit
    public static event Action OnObjectRotated;

    private bool _hasRotated = false; // Flag pentru a evita multiple semnale

    // NOU: Funcție pentru a detecta dacă obiectul a fost rotit
    private bool DetectRotation()
    {
        return Mathf.Abs(_rotationInput.x) > 0.01f || Mathf.Abs(_rotationInput.y) > 0.01f || Mathf.Abs(_touchRotationInput.x) > 0.01f || Mathf.Abs(_touchRotationInput.y) > 0.01f;
    }

    public void ResetRotationFlag()
    {
        _hasRotated = false; // Resetează flag-ul pentru a permite detectarea rotației din nou
    }
}