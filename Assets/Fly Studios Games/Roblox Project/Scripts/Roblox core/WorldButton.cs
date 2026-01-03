using UnityEngine;

public class WorldButton : MonoBehaviour, IInteractable
{
    public string buttonName = "Generator";
    public InteractMode interactionType; // Dropdown în Inspector

    public string GetInteractText()
    {
        return "interact with " + buttonName;
    }

    public InteractMode GetInteractMode() 
    {
        return interactionType; 
    }

    public void Interact()
    {
        Debug.Log("[INTERACT] Ai activat: " + buttonName);
        GetComponent<Renderer>().material.color = new Color(Random.value, Random.value, Random.value);
    }
}