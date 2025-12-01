using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PlayerHealth : MonoBehaviour
{
	[Header("Health")]
	public float maxHealth;
	private float _currentHealth;

	[Header("Armor")]
	public float maxArmor = 100f; // Maximum armor the player can have
	private float _currentArmor = 0f; // Player starts with no armor
	private float _damageReductionPercentage = 0f; // Current damage reduction percentage

	[Header("Vignette Overlay (UI)")]
	[Tooltip("Image used as fullscreen vignette. If null the script will try to find one in children named 'Vignette'.")]
	public Image vignetteImage;
	[Tooltip("Default vignette color (black) and alpha (0..1).")]
	public Color defaultVignetteColor = new Color(0f, 0f, 0f, 0.5f);
	[Tooltip("Damage vignette color (red). Alpha used as base and scaled by low-health factor.")]
	public Color damageVignetteColor = new Color(1f, 0f, 0f, 0.7f);
	[Tooltip("Heal / armor vignette color (white).")]
	public Color healVignetteColor = new Color(1f, 1f, 1f, 0.7f);
	[Tooltip("How long the vignette fades back to default.")]
	public float vignetteFadeDuration = 0.6f;
	[Tooltip("How long the vignette holds at full intensity before fading.")]
	public float vignetteHoldDuration = 0.18f;

	// internal
	private Tween _vignetteTween;
	private enum VignetteState { None, Damage, Heal }
	private VignetteState _currentVignette = VignetteState.None;

	// Event invocat când health sau armor se schimbă: (currentHealth, maxHealth, currentArmor, maxArmor)
	public event Action<float, float, float, float> OnStatsChanged;

	public float CurrentHealth => _currentHealth;
	public float MaxHealth => maxHealth;
	public float CurrentArmor => _currentArmor;
	public float MaxArmor => maxArmor;

	private void Awake()
	{
		// Initialize health and armor to their maximum values
		_currentHealth = Mathf.Clamp(maxHealth, 0f, 100f);
		_currentArmor = 0f; // Start with no armor

		// Trigger the stats changed event to update any listeners (e.g., UI)
		OnStatsChanged?.Invoke(_currentHealth, maxHealth, _currentArmor, maxArmor);

		// Ensure vignette image initialized
		if (vignetteImage == null)
		{
			// try to find by name or first child Image
			var found = GetComponentInChildren<Image>(true);
			if (found != null && string.Equals(found.gameObject.name, "Vignette", StringComparison.InvariantCultureIgnoreCase))
				vignetteImage = found;
			else
			{
				// fallback: first Image child
				Image[] imgs = GetComponentsInChildren<Image>(true);
				if (imgs != null && imgs.Length > 0)
					vignetteImage = imgs[0];
			}
		}

		if (vignetteImage != null)
		{
			// set default immediately (no tween)
			vignetteImage.color = defaultVignetteColor;
		}
	}

	// Apply damage reduction from equipment
	public void ApplyDamageReduction(float reductionPercentage)
	{
		_damageReductionPercentage = Mathf.Clamp(reductionPercentage, 0f, 100f);
		Debug.Log($"Damage reduction applied: {_damageReductionPercentage}%");
	}

	// Add armor to the player
	public void AddArmor(float amount)
	{
		if (amount <= 0f) return;

		float previousArmor = _currentArmor;
		_currentArmor = Mathf.Min(maxArmor, _currentArmor + amount);
		Debug.Log($"Armor added: {amount}. Previous Armor: {previousArmor}, Current Armor: {_currentArmor}");

		OnStatsChanged?.Invoke(_currentHealth, maxHealth, _currentArmor, maxArmor);

		// show heal/armor vignette when gaining armor
		ApplyHealVignette();
	}

	// Set the player's armor to a new maximum value and apply damage reduction
	public void SetArmor(float maxArmorValue)
	{
		maxArmor = maxArmorValue; // Update the maximum armor value
		_currentArmor = maxArmor; // Set current armor to the new maximum
		ApplyDamageReduction(maxArmorValue); // Apply damage reduction based on the new armor

		OnStatsChanged?.Invoke(_currentHealth, maxHealth, _currentArmor, maxArmor);

		// show heal/armor vignette when equipping armor
		ApplyHealVignette();
	}

	[Button]
	public void TakeDamage(float amount)
	{
		if (amount <= 0f) return;

		// Reduce damage based on the current damage reduction percentage
		float reducedDamage = amount * (1f - _damageReductionPercentage / 100f);

		// Damage is first absorbed by armor
		if (_currentArmor > 0f)
		{
			float remainingDamage = Mathf.Max(0f, reducedDamage - _currentArmor);
			_currentArmor = Mathf.Max(0f, _currentArmor - reducedDamage);
			reducedDamage = remainingDamage;
		}

		// Remaining damage is applied to health
		if (reducedDamage > 0f)
		{
			_currentHealth = Mathf.Max(0f, _currentHealth - reducedDamage);
		}

		// update UI listeners
		OnStatsChanged?.Invoke(_currentHealth, maxHealth, _currentArmor, maxArmor);

		// play vignette damage effect
		ApplyDamageVignette();

		if (_currentHealth <= 0f)
		{
			// gestionare moarte (poți extinde)
			Destroy(gameObject);
		}
	}

	[Button]
	public void Heal(float amount)
	{
		if (amount <= 0f) return;

		float previousHealth = _currentHealth;
		_currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);

		OnStatsChanged?.Invoke(_currentHealth, maxHealth, _currentArmor, maxArmor);

		// Apply heal vignette (white flash)
		ApplyHealVignette();
	}

	// --- Vignette helpers ---

	private void ApplyDamageVignette()
	{
		if (vignetteImage == null) return;

		// compute extra intensity when health is low (after damage)
		float healthPct = maxHealth > 0f ? Mathf.Clamp01(_currentHealth / maxHealth) : 0f;
		float lowHealthFactor = 1f - healthPct; // 0..1
		float extra = Mathf.Lerp(0f, 0.25f, lowHealthFactor); // small boost up to +0.25 alpha

		Color target = damageVignetteColor;
		target.a = Mathf.Clamp01(damageVignetteColor.a + extra);

		ApplyVignette(target, VignetteState.Damage);
	}

	private void ApplyHealVignette()
	{
		if (vignetteImage == null) return;

		Color target = healVignetteColor;
		ApplyVignette(target, VignetteState.Heal);
	}

	private void ApplyVignette(Color targetColor, VignetteState newState)
	{
		// Cancel previous tween to avoid stacking issues
		if (_vignetteTween != null && _vignetteTween.IsActive())
		{
			_vignetteTween.Kill();
			_vignetteTween = null;
		}

		_currentVignette = newState;

		// Immediately set to target color so effect is visible at once
		vignetteImage.color = targetColor;

		// Sequence: hold at target briefly, then tween color back to default
		Sequence seq = DOTween.Sequence();
		seq.AppendInterval(vignetteHoldDuration);
		seq.Append(vignetteImage.DOColor(defaultVignetteColor, vignetteFadeDuration).SetEase(Ease.OutQuad));
		_vignetteTween = seq.OnComplete(() => { _currentVignette = VignetteState.None; });
	}
}
