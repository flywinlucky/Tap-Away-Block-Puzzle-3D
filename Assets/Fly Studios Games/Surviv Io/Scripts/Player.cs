using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class Player : MonoBehaviour
{
    [Header("Setări Cameră și Rotație")]
    public Camera cam;
    public float zDepth = 0f; // Adâncimea Z la care se calculează cursorul

    [Header("Setări Mișcare")]
    public float moveSpeed = 5f; // Viteza de mișcare

    // Smooth movement settings
    [Header("Mișcare Smooth")]
    [Tooltip("Timp (s) folosit de SmoothDamp pentru a netezi schimbarea vitezei.")]
    public float velocitySmoothTime = 0.08f;
    [Tooltip("Dacă vrei decelerare/accelerare personalizată, modifică aceste valori.")]
    public float acceleration = 40f;
    public float deceleration = 40f;

    private Vector3 worldPos; // Poziția cursorului în lume

    public Transform weaponRootPosition;
    public WeaponControler weaponController;
    public bool holdToFire = true; // ține apăsat pentru foc continuu
    public Animator isSwiming;
    // intern
    private Rigidbody2D _rb2;
    private Vector2 _inputDir = Vector2.zero;
    private Vector2 _velocityRef = Vector2.zero;
    private Vector2 _currentVelocity = Vector2.zero;
    public bool weaponFlipped = false; // true dacă arma este întoarsă (X=180)
    private float _weaponInitialY;
    private float _weaponInitialZ;
    private Vector3 _weaponInitialLocalPos;

    [Header("Weapon Recoil (DOTween)")]
    public float recoilDistance = 0.15f;
    public float recoilDuration = 0.08f;
    public Ease recoilEase = Ease.OutQuad;
    public int recoilVibrato = 0;
    private Sequence _recoilSequence; // was Tween _recoilTween

    [Header("Swimming")]
    [Tooltip("Viteza de deplasare în apă.")]
    public float swimmingSpeed = 2.5f;

    [Header("Audio")]
    [Tooltip("Intervalul fix între pași (s).")]
    public float footstepInterval = 0.4f;
    [Tooltip("Viteză minimă pentru a reda pași.")]
    public float minMoveSpeedForStep = 0.05f;

    private AudioManager _audio;
    private float _stepTimer = 0f;

    private bool _inWater = false;
    private float _prevMoveSpeed;

    void Start()
    {
        if (cam == null)
            cam = Camera.main;

        if (cam == null)
            Debug.LogError("Player.cs: 'cam' nu este setată și 'Camera.main' nu a fost găsită!");

        _rb2 = GetComponent<Rigidbody2D>();

        if (weaponRootPosition != null)
        {
            var e = weaponRootPosition.localEulerAngles;
            _weaponInitialY = e.y;
            _weaponInitialZ = e.z;
            _weaponInitialLocalPos = weaponRootPosition.localPosition; // cache local start
        }
        _audio = FindObjectOfType<AudioManager>();
    }

    void Update()
    {
        HandleInput();

        if (cam == null)
            return;

        // Convertim mouse-ul în poziție din lume
        Vector3 mouse = Input.mousePosition;
        mouse.z = -cam.transform.position.z + zDepth;
        worldPos = cam.ScreenToWorldPoint(mouse);

        // Rotația playerului spre cursor
        Vector2 direction = worldPos - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // Flip simplu 0 / 180
        UpdateWeaponFlip();

        // Fire
        if (weaponController != null)
        {
            if (holdToFire)
            {
                if (Input.GetMouseButton(0))
                {
                    int beforeMag = weaponController.CurrentMagazine;
                    weaponController.Fire();
                    if (weaponController.CurrentMagazine == beforeMag - 1)
                        PlayRecoil();
                }
            }
            else
            {
                if (Input.GetMouseButtonDown(0))
                {
                    int beforeMag = weaponController.CurrentMagazine;
                    weaponController.Fire();
                    if (weaponController.CurrentMagazine == beforeMag - 1)
                        PlayRecoil();
                }
            }

            if (Input.GetKeyDown(KeyCode.R))
                weaponController.Reload();
        }

        HandleFootsteps();
    }

    void HandleInput()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        _inputDir = new Vector2(moveX, moveY);

        if (_inputDir.sqrMagnitude > 1f)
            _inputDir = _inputDir.normalized;
    }

    void FixedUpdate()
    {
        ApplyMovement();
    }

    void ApplyMovement()
    {
        Vector2 targetVelocity = _inputDir * moveSpeed;

        if (_rb2 != null)
        {
            Vector2 newVel = Vector2.SmoothDamp(_rb2.velocity, targetVelocity, ref _velocityRef, velocitySmoothTime);

            float maxDelta = (_inputDir.sqrMagnitude > 0.001f ? acceleration : deceleration) * Time.fixedDeltaTime;
            newVel = Vector2.MoveTowards(_rb2.velocity, newVel, maxDelta);

            _rb2.velocity = newVel;
        }
        else
        {
            Vector2 smoothVel = Vector2.SmoothDamp(_currentVelocity, targetVelocity, ref _velocityRef, velocitySmoothTime);
            transform.position += (Vector3)(smoothVel * Time.fixedDeltaTime);
            _currentVelocity = smoothVel;
        }
    }

    // ✔ Metoda FINALĂ: împărțim ecranul în 2 și aplicăm 0 sau 180
    private void UpdateWeaponFlip()
    {
        if (weaponRootPosition == null)
            return;

        bool cursorRight = Input.mousePosition.x >= (Screen.width * 0.5f);

        Vector3 e = weaponRootPosition.localEulerAngles;

        e.x = cursorRight ? 0f : 180f;
        e.y = _weaponInitialY;
        e.z = _weaponInitialZ;

        weaponRootPosition.localEulerAngles = e;

        weaponFlipped = !cursorRight;
    }

    private void PlayRecoil()
    {
        if (weaponRootPosition == null) return;

        if (_recoilSequence != null && _recoilSequence.IsActive())
            _recoilSequence.Kill();

        // hard reset to initial each shot (elimină drift)
        weaponRootPosition.localPosition = _weaponInitialLocalPos;

        float dir = weaponFlipped ? 1f : -1f;
        float backTargetX = _weaponInitialLocalPos.x + dir * recoilDistance;

        _recoilSequence = DOTween.Sequence()
            .Append(weaponRootPosition.DOLocalMoveX(backTargetX, recoilDuration * 0.4f).SetEase(recoilEase))
            .Append(weaponRootPosition.DOLocalMoveX(_weaponInitialLocalPos.x, recoilDuration * 0.6f).SetEase(Ease.OutQuad))
            .OnComplete(() =>
            {
                weaponRootPosition.localPosition = _weaponInitialLocalPos; // ensure exact reset
            });
    }

    private void OnDestroy()
    {
        if (_recoilSequence != null && _recoilSequence.IsActive())
            _recoilSequence.Kill();
        if (weaponRootPosition != null)
            weaponRootPosition.localPosition = _weaponInitialLocalPos;
    }

    /*void OnDrawGizmos()
    {
        if (cam == null)
            cam = Camera.main;

        if (cam == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(worldPos, 0.1f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, worldPos);
    }*/

    // Water enter/exit for 2D physics
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && other.CompareTag("Water"))
            EnterWater();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other != null && other.CompareTag("Water"))
            ExitWater();
    }

    private void EnterWater()
    {
        if (_inWater) return;
        _inWater = true;

        // cache current speed and apply swim speed
        _prevMoveSpeed = moveSpeed;
        moveSpeed = swimmingSpeed;

        // animator bool
        if (isSwiming != null)
            isSwiming.SetBool("isSwiming", true);
    }

    private void ExitWater()
    {
        if (!_inWater) return;
        _inWater = false;

        // restore speed
        moveSpeed = _prevMoveSpeed;

        // animator bool
        if (isSwiming != null)
            isSwiming.SetBool("isSwiming", false);
    }

    private void HandleFootsteps()
    {
        float speed = 0f;
        if (_rb2 != null) speed = _rb2.velocity.magnitude;
        else speed = _currentVelocity.magnitude;

        bool moving = speed > minMoveSpeedForStep && (_inputDir.sqrMagnitude > 0.001f);

        if (moving)
        {
            _stepTimer -= Time.deltaTime;
            if (_stepTimer <= 0f)
            {
                if (_audio == null) _audio = FindObjectOfType<AudioManager>();
                if (_audio != null)
                {
                    if (_inWater) _audio.PlayWaterFootstep();
                    else _audio.PlayFootstep();
                }
                _stepTimer = Mathf.Max(0.05f, footstepInterval);
            }
        }
        else
        {
            _stepTimer = 0f;
        }
    }
}
