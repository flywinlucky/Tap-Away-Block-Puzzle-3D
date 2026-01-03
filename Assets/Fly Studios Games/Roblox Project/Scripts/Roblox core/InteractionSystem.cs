using UnityEngine;
using UnityEngine.UI;

public class InteractionSystem : MonoBehaviour
{
    [Header("Detection Settings")]
    public float fpsInteractRange = 3f;  
    public float tpsInteractRange = 10f; 
    public LayerMask interactableLayer;
    public Transform playerTransform;
    
    [Header("Input Settings")]
    public KeyCode interactKey = KeyCode.E; 

    [Header("UI Reference")]
    public Text helpText;

    private Camera cam;
    private CameraControler camScript; 

    void Start()
    {
        cam = GetComponent<Camera>();
        camScript = FindObjectOfType<CameraControler>();
    }

    void Update()
    {
        HandleInteraction();
    }

    void HandleInteraction()
    {
        Ray ray;
        float currentRange;

        // Detectare mod Cameră
        if (camScript != null && camScript.isFPS)
        {
            ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            currentRange = fpsInteractRange;
        }
        else
        {
            ray = cam.ScreenPointToRay(Input.mousePosition);
            currentRange = tpsInteractRange;
        }

        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, currentRange, interactableLayer))
        {
            if (playerTransform != null && hit.transform == playerTransform) 
            {
                ClearUI();
                return;
            }

            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null)
            {
                InteractMode mode = interactable.GetInteractMode();

                // Setăm textul UI în funcție de mod
                if (helpText != null) 
                {
                    if (mode == InteractMode.KeyPress)
                        helpText.text = "[" + interactKey.ToString() + "] " + interactable.GetInteractText();
                    else
                        helpText.text = "[Click] " + interactable.GetInteractText();
                }

                // Verificăm input-ul bazat pe alegerea din Dropdown-ul obiectului
                if (mode == InteractMode.KeyPress && Input.GetKeyDown(interactKey))
                {
                    interactable.Interact();
                }
                else if (mode == InteractMode.Click && Input.GetMouseButtonDown(0))
                {
                    interactable.Interact();
                }
            }
            else
            {
                ClearUI();
            }
        }
        else
        {
            ClearUI();
        }
    }

    void ClearUI()
    {
        if (helpText != null && helpText.text != "") helpText.text = "";
    }
}