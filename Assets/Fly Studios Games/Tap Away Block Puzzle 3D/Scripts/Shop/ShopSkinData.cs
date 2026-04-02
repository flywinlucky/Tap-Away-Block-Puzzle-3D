using UnityEngine;

[CreateAssetMenu(fileName = "NewShopSkin", menuName = "Tap Away Block Puzzle 3D/Skin")]
public class ShopSkinData : ScriptableObject
{
    public string id; // ID unic pentru skin
    public string displayName; // Numele skin-ului
    public int price; // Prețul skin-ului
    public Sprite icon; // Iconița skin-ului
    public Material material; // Materialul skin-ului
    public Color arrowColor; // Culoarea săgeții asociată skin-ului
}
