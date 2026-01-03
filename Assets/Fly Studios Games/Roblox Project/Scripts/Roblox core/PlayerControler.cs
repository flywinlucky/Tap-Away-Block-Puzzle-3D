using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// Replace CharacterController with Rigidbody + CapsuleCollider
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerControler : MonoBehaviour
{
	[Header("Roblox Settings")]
    public float walkSpeed = 16f;
    public float jumpHeight = 5f;
    public float gravity = -30f;

    [Header("Smoothing (Feel)")]
    public float turnSpeed = 15f;
    public float acceleration = 100f;
    public float deceleration = 100f;

    [Header("Ground Detection (Raycast/Sphere)")]
    public LayerMask groundLayer;      // Selecteaza aici layer-ul podelei (ex: Default sau Ground)
    public float groundCheckRadius = 0.25f; // Cat de mare e sfera de verificare
    public Vector3 groundCheckOffset = new Vector3(0, -0.1f, 0); // Pozitia sferei fata de picioare
    public Transform raycastPosition;  // Punctul care va fi folosit pentru verificarea solului (setezi in inspector)

    // Animation
    [Header("Animation")]
    public Animator animator; // seteaza in Inspector sau va fi gasit automat in Start

    [Header("Audio")]
    public SoundFootsteps footsteps;

    // Private variables
    // private CharacterController cc;
    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Transform camTransform;
    private Vector3 velocity;           // Gravitatie (vertical only now)
    private Vector3 currentHorizontalVelocity;
    private bool isGroundedCustom;      // Variabila noastra, nu a Unity-ului
    private bool jumpRequested;
    public CameraControler camScript;

    void Start()
    {
        // cc = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        rb.useGravity = false; // custom gravity
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (Camera.main != null)
        {
            camTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("Lipsa MainCamera!");
        }

        // Daca animatorul nu e setat in Inspector, cautam in copii
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("Animator not assigned on PlayerControler and none found in children.");
            }
        }

        if (footsteps == null)
        {
            footsteps = GetComponent<SoundFootsteps>();
        }
        if (footsteps != null)
        {
            footsteps.referenceWalkSpeed = walkSpeed;
        }
    }

    void Update()
    {
        CheckGround();

        if (animator != null) 
        {
            animator.SetBool("isGrounded", isGroundedCustom);
            // Folosim viteza verticală din Rigidbody
            float vy = rb != null ? rb.velocity.y : velocity.y;
            animator.SetFloat("verticalVelocity", vy);
        }

        HandleMovement();
        HandleGravityAndJump(); // now only captures jump and handles animator

        if (footsteps != null)
        {
            footsteps.UpdateFootsteps(isGroundedCustom, currentHorizontalVelocity);
        }
    }

    // Functie personalizata pentru a detecta solul
    void CheckGround()
    {
        // Daca exista raycastPosition folosit de utilizator, luăm poziția aceea, altfel revenim la transform.position + offset
        Vector3 spherePosition = (raycastPosition != null) ? raycastPosition.position : (transform.position + groundCheckOffset);
        isGroundedCustom = Physics.CheckSphere(spherePosition, groundCheckRadius, groundLayer);
    }

void HandleMovement()
{
    float h = Input.GetAxisRaw("Horizontal");
    float v = Input.GetAxisRaw("Vertical");

    Vector3 camForward = camTransform.forward;
    Vector3 camRight = camTransform.right;

    camForward.y = 0;
    camRight.y = 0;
    camForward.Normalize();
    camRight.Normalize();

    Vector3 targetDirection = (camForward * v + camRight * h).normalized;
    
    // --- LOGICA STIL ROBLOX ---
    if (camScript != null && camScript.isFPS)
    {
        // În FPS, jucătorul se rotește mereu după camera pe axa Y
        Vector3 lookDir = camTransform.forward;
        lookDir.y = 0;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), turnSpeed * Time.deltaTime);
    }
    else if (targetDirection.magnitude > 0.1f)
    {
        // În TPS, se rotește doar când ne mișcăm
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }
    // --------------------------

    Vector3 targetVelocity = targetDirection * walkSpeed;
    float currentAccel = (targetDirection.magnitude > 0) ? acceleration : deceleration;
    currentHorizontalVelocity = Vector3.MoveTowards(currentHorizontalVelocity, targetVelocity, currentAccel * Time.deltaTime);

    // Animații
    if (animator != null)
    {
        bool isMoving = targetDirection.magnitude > 0.1f;
        animator.SetBool("run", isMoving && isGroundedCustom);
    }

    // cc.Move(currentHorizontalVelocity * Time.deltaTime); // removed, applied in FixedUpdate via rb.velocity
}
  void HandleGravityAndJump()
    {
        // Capture jump in Update; physics applied in FixedUpdate
        if (Input.GetKeyDown(KeyCode.Space) && isGroundedCustom)
        {
            jumpRequested = true;

            if (animator != null)
            {
                animator.SetBool("run", false);
                animator.SetTrigger("jump");
            }
        }
        // No cc.Move or gravity integration here anymore
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        Vector3 vel = rb.velocity;

        // Horizontal from computed acceleration
        vel.x = currentHorizontalVelocity.x;
        vel.z = currentHorizontalVelocity.z;

        // Stick-to-ground
        if (isGroundedCustom && vel.y < 0f)
            vel.y = -2f;

        // Jump
        if (jumpRequested && isGroundedCustom)
        {
            vel.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Gravity
        vel.y += gravity * Time.fixedDeltaTime;

        rb.velocity = vel;
        jumpRequested = false;
    }

    // Asta deseneaza sfera in Editor ca sa vezi daca atinge pamantul (Gizmos)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        // Desenăm sfera la raycastPosition dacă e setată, altfel la poziția implicită
        Vector3 spherePosition = (raycastPosition != null) ? raycastPosition.position : (transform.position + groundCheckOffset);
        Gizmos.DrawWireSphere(spherePosition, groundCheckRadius);
    }
}