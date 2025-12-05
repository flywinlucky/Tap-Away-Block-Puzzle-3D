using UnityEngine;

// Atașează acest script pe orice GameObject care are un Renderer.
// Setează culoarea în inspector; va fi aplicată doar acelui renderer (fără a modifica Material asset-ul).
[ExecuteAlways]
public class ObjectColor : MonoBehaviour
{
    public Color objectColor = Color.white;

    Renderer rend;
    MaterialPropertyBlock mpb;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend == null) return;
        mpb = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        ApplyColor();
    }

    void OnValidate()
    {
        // aplica imediat in editor cand modifici culoarea in inspector
        ApplyColor();
    }

    public void ApplyColor()
    {
        if (rend == null) return;
        rend.GetPropertyBlock(mpb);
        mpb.SetColor("_Color", objectColor);
        rend.SetPropertyBlock(mpb);
    }
}
