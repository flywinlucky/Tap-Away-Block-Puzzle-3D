using UnityEngine;

[ExecuteAlways] // Face scriptul să ruleze și în modul de editare
public class ObjectColorSetter : MonoBehaviour
{
    public Color myLocalColor = Color.white;

    // Se apelează automat când schimbi ceva în Inspector
    private void OnValidate()
    {
        ApplyColor();
    }

    private void Start()
    {
        ApplyColor();
    }

    // Metodă publică pe care o poți apela și din butoanele tale (WorldButton)
    public void SetNewColor(Color newColor)
    {
        myLocalColor = newColor;
        ApplyColor();
    }

    [ContextMenu("Refresh Color")] // Adaugă o opțiune de Refresh la click dreapta pe script
    public void ApplyColor()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null) return;

        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        
        // Obținem block-ul actual (pentru a păstra alte setări dacă există)
        renderer.GetPropertyBlock(propBlock);
        
        // Aplicăm culoarea locală
        propBlock.SetColor("_Color", myLocalColor);
        
        // Trimitem datele către GPU
        renderer.SetPropertyBlock(propBlock);
    }
}