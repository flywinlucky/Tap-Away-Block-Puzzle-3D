using UnityEngine;

[CreateAssetMenu(fileName = "NewEquipmentData", menuName = "SurvivIo/Equipment Data")]
public class EquipmentData : ScriptableObject
{
    public string itemName;
    public enum EquipmentType
    {
        Helmets,
        Vests
    }

    [Header("Equipment Properties")]
    public EquipmentType equipmentType;   // Dropdown for selecting Helmets or Vests
    public Sprite equipmentSpriteIcon;    // Icon for the equipment
    public int equipmentLevel;            // Level of the equipment
    public float damageReduction;         // Damage reduction provided by the equipment
    public Color equipament_Color;
    public AudioClip equipment_pickup_sound; // Sound played on pickup
}