using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(CharacterController))]
public class PlayerControler : MonoBehaviour
{
	[Header("Roblox Settings")]
    public float walkSpeed = 16f;       // Roblox default e cam 16 studs/s. În Unity încearcă 8-12.
    public float jumpHeight = 5f;       // Înălțimea săriturii
    public float gravity = -30f;        // Gravitație mai puternică pentru a simți greutatea (Roblox e ~196 studs/s^2)
    
    [Header("Smoothing (Feel)")]
    public float turnSpeed = 15f;       // Cât de repede se rotește (mai mare = mai snappy)
    public float acceleration = 100f;   // Cât de repede ajunge la viteza maximă (foarte mare = instant)
    public float deceleration = 100f;   // Cât de repede se oprește (foarte mare = fără alunecare)

    // Private variables
    private CharacterController cc;
    private Transform camTransform;
    private Vector3 velocity;           // Vectorul pentru gravitație (Y)
    private Vector3 currentHorizontalVelocity; // Vectorul pentru mișcarea pe sol (X, Z)

    void Start()
    {
        cc = GetComponent<CharacterController>();
        
        // Cache la camera pentru performanță
        if (Camera.main != null)
        {
            camTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("Nu am găsit MainCamera! Asigură-te că ai o cameră cu tag-ul MainCamera.");
        }
    }

    void Update()
    {
        HandleMovement();
        HandleGravityAndJump();
    }

    void HandleMovement()
    {
        // 1. Input (Raw pentru răspuns instant)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // Calculăm direcția bazată pe Cameră (dar ignorăm înclinarea camerei sus/jos)
        Vector3 camForward = camTransform.forward;
        Vector3 camRight = camTransform.right;
        
        camForward.y = 0;
        camRight.y = 0;
        
        camForward.Normalize();
        camRight.Normalize();

        Vector3 targetDirection = (camForward * v + camRight * h).normalized;

        // 2. Calculăm viteza țintă
        Vector3 targetVelocity = targetDirection * walkSpeed;

        // 3. Aplicăm Accelerare/Decelerare (ca să nu fie 100% robotic, dar nici să nu alunece)
        // Dacă playerul apasă taste, accelerăm. Dacă nu, decelerăm brusc.
        float currentAccel = (targetDirection.magnitude > 0) ? acceleration : deceleration;

        // MoveTowards e mai "fix" și mai stabil decât SmoothDamp pentru stilul Roblox
        currentHorizontalVelocity = Vector3.MoveTowards(currentHorizontalVelocity, targetVelocity, currentAccel * Time.deltaTime);

        // 4. Rotim caracterul spre direcția de mers
        if (targetDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            // Slerp rapid pentru rotație fluidă dar fermă
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        // 5. Aplicăm mișcarea orizontală
        cc.Move(currentHorizontalVelocity * Time.deltaTime);
    }

    void HandleGravityAndJump()
    {
        // Resetăm viteza pe Y când suntem pe jos
        if (cc.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // O forță mică în jos ca să țină playerul "lipit" de sol pe pante
        }

        // Săritura (Doar dacă e pe sol)
        if (Input.GetButtonDown("Jump") && cc.isGrounded)
        {
            // Formula fizică pentru săritură precisă: v = sqrt(h * -2 * g)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Aplicăm gravitația
        velocity.y += gravity * Time.deltaTime;

        // Aplicăm mișcarea verticală
        cc.Move(velocity * Time.deltaTime);
    }
}
