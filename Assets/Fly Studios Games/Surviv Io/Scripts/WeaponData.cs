using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "SurvivIo/Weapon Data")]
public class WeaponData : ScriptableObject
{
	public string weaponName = "New Weapon";

	[Header("Visual")]
	public Sprite weaponSpriteIcon;
	public Color weapon_Color = Color.white;
	public GameObject weaponPrefab; // prefab-ul vizual al armei (instanțiat la equip)

	[Header("Audio")]
	public AudioClip weapon_fire_sound;
	public AudioClip weapon_reload_sound;
	public AudioClip weapon_switch_sound; // sunet la echipare (switch)

	[Header("Projectile")]
	public GameObject bulletPrefab;
	public float bulletSpeed = 15f;
	public float damage = 1f;
	[Tooltip("Distanța maximă (unități) pe care o poate parcurge glonțul.")]
	public float range = 15f;

	[Header("Fire")]
	public float fireRate = 0.18f;
	public bool automatic = true;

	[Header("Ammo")]
	public int magazineSize = 6;      // câte gloanțe într-un încărcător
	public int maxReserveAmmo = 30;   // cât rezervă maximă poți avea
	public int startingReserveAmmo = 12;

	[Header("Reload")]
	public float reloadTime = 0.8f; // (opțional, folosește dacă vrei coroutine)
}
