using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBulletAmmoData", menuName = "SurvivIo/Bullet Ammo Data")]
public class BulletAmmoData : ScriptableObject
{
    [Header("Info")]
    public string ammoName = "Ammo";
    public int ammoCount = 10;

    [Header("Visual")]
    public Sprite ammoIcon;
    public Color ammoColor = Color.white;

    [Header("Compatibility")]
    [Tooltip("Lista de arme compatibile. Dacă e goală => compatibil cu orice armă.")]
    public List<WeaponData> compatibleWeapons = new List<WeaponData>();

    [Header("Audio")]
    public AudioClip ammo_pickup_sound; // sunet la pickup

    public bool IsCompatible(WeaponData weapon)
    {
        if (weapon == null) return false;
        if (compatibleWeapons == null || compatibleWeapons.Count == 0) return true;
        return compatibleWeapons.Contains(weapon);
    }
}
