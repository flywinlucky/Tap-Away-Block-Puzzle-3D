public enum InteractMode { Click, KeyPress }

public interface IInteractable
{
    string GetInteractText();
    void Interact();
    InteractMode GetInteractMode(); // Nou: Spune sistemului dacă vrea Click sau Tastă
}