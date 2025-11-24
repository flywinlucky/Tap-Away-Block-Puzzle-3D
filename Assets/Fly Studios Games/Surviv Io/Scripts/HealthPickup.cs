using UnityEngine;

public class HealthPickup : MonoBehaviour
{
	public float amount;
	public AudioClip health_pickup_sound; // sunet redat la colectare

	private PlayerUI _currentPlayerUI;

	private void OnTriggerEnter2D(Collider2D other) { ShowPrompt(other.gameObject); }
	private void OnTriggerStay2D(Collider2D other) { if (Input.GetKeyDown(KeyCode.F)) ApplyPickup(other.gameObject); }
	private void OnTriggerExit2D(Collider2D other) { HidePrompt(other.gameObject); }

	private void OnTriggerEnter(Collider other) { ShowPrompt(other.gameObject); }
	private void OnTriggerStay(Collider other) { if (Input.GetKeyDown(KeyCode.F)) ApplyPickup(other.gameObject); }
	private void OnTriggerExit(Collider other) { HidePrompt(other.gameObject); }

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
		if (other == null) return;
		var playerUI = other.GetComponentInChildren<PlayerUI>();
		if (playerUI == null) return;

		playerUI.ShowFtoSellect($"Health +{amount}");
		_currentPlayerUI = playerUI;
	}

	private void HidePrompt(GameObject other)
	{
		var ui = other != null ? other.GetComponentInChildren<PlayerUI>() : _currentPlayerUI;
		if (ui != null) ui.HideFtoSellect();
		if (ui == _currentPlayerUI) _currentPlayerUI = null;
	}

	private void ApplyPickup(GameObject other)
	{
		if (other == null) return;
		var ph = other.GetComponentInChildren<PlayerHealth>();
		var ui = other.GetComponentInChildren<PlayerUI>();
		if (ph == null) return;

		ph.Heal(amount);

		// play pickup sound
		if (health_pickup_sound != null)
		{
			if (AudioManager.Instance != null)
				AudioManager.Instance.Play2DSound(health_pickup_sound);
			else
				AudioSource.PlayClipAtPoint(health_pickup_sound, Vector3.zero);
		}

		if (ui != null) ui.HideFtoSellect();
		Destroy(gameObject);
	}
}