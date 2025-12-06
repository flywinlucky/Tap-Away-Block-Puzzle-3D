using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(CharacterController))]
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

    // Private variables
    private CharacterController cc;
    private Transform camTransform;
    private Vector3 velocity;           // Gravitatie
    private Vector3 currentHorizontalVelocity;
    private bool isGroundedCustom;      // Variabila noastra, nu a Unity-ului

    void Start()
    {
        cc = GetComponent<CharacterController>();

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
    }

    void Update()
    {
        CheckGround(); // Verificam solul manual

        // trimitem starea de grounded catre Animator (folosit pentru a reveni din Jump)
        if (animator != null) animator.SetBool("isGrounded", isGroundedCustom);

        HandleMovement();
        HandleGravityAndJump();
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
        Vector3 targetVelocity = targetDirection * walkSpeed;

        float currentAccel = (targetDirection.magnitude > 0) ? acceleration : deceleration;

        // Folosim MoveTowards pentru oprire instanta (stil Roblox)
        currentHorizontalVelocity = Vector3.MoveTowards(currentHorizontalVelocity, targetVelocity, currentAccel * Time.deltaTime);

        if (targetDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

            // Play run animation
            if (animator != null) animator.SetBool("run", true);
        }
        else
        {
            // Stop run animation
            if (animator != null) animator.SetBool("run", false);
        }

        cc.Move(currentHorizontalVelocity * Time.deltaTime);
    }

    void HandleGravityAndJump()
    {
        // Resetam gravitatia daca suntem pe jos (folosind verificarea noastra custom)
        if (isGroundedCustom && velocity.y < 0)
        {
            velocity.y = -2f; // O forta mica in jos ca sa stam lipiti
        }

        // SĂRITURA
        if (Input.GetKeyDown(KeyCode.Space) && isGroundedCustom)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            // Declansam trigger-ul de jump si dezactivam run in animator (daca exista)
            if (animator != null)
            {
                animator.SetTrigger("jump");
                animator.SetBool("run", false);
            }
        }

        // Aplicam gravitatia constant
        velocity.y += gravity * Time.deltaTime;

        // Miscare verticala
        cc.Move(velocity * Time.deltaTime);
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