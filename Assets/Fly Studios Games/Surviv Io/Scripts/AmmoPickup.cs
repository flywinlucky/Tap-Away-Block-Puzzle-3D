using UnityEngine;

public class AmmoPickup : MonoBehaviour
{
	[Tooltip("ScriptableObject ce definește tipul de ammo.")]
	public BulletAmmoData bulletAmmoData;

	[Header("Visual (Optional)")]
	public SpriteRenderer item_color;
	public SpriteRenderer ammo_icon;

	private PlayerUI _currentPlayerUI;

	private void Start()
	{
		if (bulletAmmoData != null)
		{
			if (ammo_icon != null) ammo_icon.sprite = bulletAmmoData.ammoIcon;
			//if (item_color != null) item_color.color = bulletAmmoData.ammoColor;
		}
	}

	private void OnTriggerEnter2D(Collider2D other) { ShowPrompt(other.gameObject); }
	private void OnTriggerStay2D(Collider2D other) { if (Input.GetKeyDown(KeyCode.F)) ApplyPickup(other.gameObject); }
	private void OnTriggerExit2D(Collider2D other) { HidePrompt(other.gameObject); }

	private void OnTriggerEnter(Collider other) { ShowPrompt(other.gameObject); }
	private void OnTriggerStay(Collider other) { if (Input.GetKeyDown(KeyCode.F)) ApplyPickup(other.gameObject); }
	private void OnTriggerExit(Collider other) { HidePrompt(other.gameObject); }

	private void ApplyPickup(GameObject other)
	{
		if (other == null || bulletAmmoData == null) return;

		var weaponUI = other.GetComponentInChildren<WeaponUI>();
		var playerUI = other.GetComponentInChildren<PlayerUI>();
		if (weaponUI == null)
		{
			Debug.Log("[AmmoPickup] Player WeaponUI missing; cannot store ammo.");
			return;
		}

		// Colectăm ammo — WeaponUI va decide dacă se aplică pe arma curentă sau se stochează pending
		weaponUI.CollectAmmo(bulletAmmoData);

		PlayPickupSound(); // moved into helper

		if (playerUI != null) playerUI.HideFtoSellect();
		Destroy(gameObject);
	}

	private void PlayPickupSound()
	{
		if (bulletAmmoData != null && bulletAmmoData.ammo_pickup_sound != null)
		{
			if (AudioManager.Instance != null)
				AudioManager.Instance.Play2DSound(bulletAmmoData.ammo_pickup_sound);
			else
				AudioSource.PlayClipAtPoint(bulletAmmoData.ammo_pickup_sound, Vector3.zero);
		}
	}

	private void OnDisable()
	{
		if (_currentPlayerUI != null)
		{
			_currentPlayerUI.HideFtoSellect();
			_currentPlayerUI = null;
		}
	}

	private void ShowPrompt(GameObject other)
	{
		if (other == null || bulletAmmoData == null) return;
		var wc = other.GetComponentInChildren<WeaponControler>();
		if (wc == null) return;
		var playerUI = other.GetComponentInChildren<PlayerUI>();
		if (playerUI == null) return;

		// Afișăm promptul indiferent; compatibilitatea verificată la pickup
		playerUI.ShowFtoSellect(bulletAmmoData.ammoName);
		_currentPlayerUI = playerUI;
	}

	private void HidePrompt(GameObject other)
	{
		var ui = other != null ? other.GetComponentInChildren<PlayerUI>() : _currentPlayerUI;
		if (ui != null) ui.HideFtoSellect();
		if (ui == _currentPlayerUI) _currentPlayerUI = null;
	}
}
