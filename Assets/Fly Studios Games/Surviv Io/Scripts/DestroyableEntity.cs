using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class DestroyableEntity : MonoBehaviour
{
	[Header("Health")]
	public float health = 100;
	[Tooltip("Duration of the shake in seconds.")]
	public float shakeDuration = 0.15f;
	[Tooltip("Shake strength on each axis (Z usually 0 for 2D).")]
	public Vector3 shakeStrength = new Vector3(0.15f, 0.15f, 0f);
	[Tooltip("How much will the shake vibrate.")]
	public int shakeVibrato = 18;
	[Range(0f, 180f)]
	public float shakeRandomness = 90f;
	[Tooltip("If true the shake will smoothly fade out.")]
	public bool shakeFadeOut = true;
 

	[System.Serializable]
	public struct LootDropPoint
	{
		public GameObject loot;
		[Range(0f, 100f)] public float dropChance; // percent 0..100
	}

	[Header("Loot Drop Points (per-item chance)")]
	[Tooltip("Each entry defines a loot prefab + its drop chance (0..100%).")]
	public LootDropPoint[] dropPoints;

	[Header("Loot Settings (Global)")]
	[Range(0f, 100f)] public float dropChancePercent = 100f; // overall chance to drop anything
	public int minItemsToDrop = 1;
	public int maxItemsToDrop = 2;
	public bool uniqueItems = true;

	[Header("Audio")]
	[Tooltip("Sunet redat când entitatea primește damage (non-lethal).")]
	public AudioClip hitSound;
	[Tooltip("Sunet redat când entitatea moare.")]
	public AudioClip deathSound;

	[Header("Death Animation")]
	[Tooltip("Dacă este activ, entitatea va face scale-out înainte să fie distrusă.")]
	public bool useDeathScaleAnimation = true;
	[Tooltip("Durata animației de moarte (scale-out).")]
	public float deathScaleDuration = 0.2f;
	[Tooltip("Easing pentru animația de moarte.")]
	public Ease deathScaleEase = Ease.InBack;

	private Tween _shakeTween;
	private bool _isDying = false;

	// Called when a Bullet hits this entity
	public virtual void OnHitByBullet(Bullet bullet)
	{
		if (bullet == null) return;
		TakeDamage(bullet.GetDamage());
	}

	// Apply damage; play shake feedback; destroy when health <= 0
	public void TakeDamage(float amount)
	{
		if (amount <= 0f) return;
		if (_isDying) return;

		health = Mathf.Max(0f, health - amount);

		// Sunet de lovitură doar dacă entitatea rămâne în viață
		if (health > 0f)
			PlayHitSound();

		PlayHitFeedback();

		if (health <= 0f)
		{
			_isDying = true;
			PlayDeathSound();
			TrySpawnLoot(); // spawn drops before visual death animation
			StartDeathAnimation();
		}
	}

	// Instantiates loot using global percent and per-item chances from dropPoints.
	private void TrySpawnLoot()
	{
		// No loot configured
		if (dropPoints == null || dropPoints.Length == 0) return;

		// Global drop roll (0..100%)
		if (Random.Range(0f, 100f) > Mathf.Clamp(dropChancePercent, 0f, 100f))
			return;

		// Build candidates that passed their per-item chance
		List<GameObject> candidates = BuildEligibleCandidates();
		if (candidates.Count == 0) return;

		// Decide how many to spawn
		int min = Mathf.Max(0, minItemsToDrop);
		int max = Mathf.Max(min, maxItemsToDrop);
		int desired = Random.Range(min, max + 1);
		int toSpawn = Mathf.Clamp(desired, 0, uniqueItems ? candidates.Count : candidates.Count);
		if (toSpawn <= 0) return;

		// Shuffle candidates and take first N
		Shuffle(candidates);

		Vector3 basePos = transform.position;
		for (int i = 0; i < toSpawn; i++)
		{
			GameObject prefab = candidates[i];
			if (prefab == null) continue;

			// small jitter to avoid perfect overlap
			Vector2 jitter = Random.insideUnitCircle * 0.2f;
			var inst = Instantiate(prefab, basePos + new Vector3(jitter.x, jitter.y, 0f), Quaternion.identity);

			// optional gentle impulse if the spawned item has Rigidbody2D
			var rb2 = inst.GetComponent<Rigidbody2D>();
			if (rb2 != null)
				rb2.AddForce(Random.insideUnitCircle.normalized * Random.Range(0.5f, 1.5f), ForceMode2D.Impulse);
		}
	}

	// Rolls per-item chances and returns a list of passed prefabs (unique).
	private List<GameObject> BuildEligibleCandidates()
	{
		List<GameObject> list = new List<GameObject>();
		for (int i = 0; i < dropPoints.Length; i++)
		{
			var dp = dropPoints[i];
			if (dp.loot == null) continue;
			float chance = Mathf.Clamp(dp.dropChance, 0f, 100f);
			if (Random.Range(0f, 100f) <= chance)
				list.Add(dp.loot);
		}
		return list;
	}

	private void Shuffle<T>(List<T> list)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = Random.Range(0, i + 1);
			// swap without tuple deconstruction (compat C#)
			T tmp = list[i];
			list[i] = list[j];
			list[j] = tmp;
		}
	}

	// Modular feedback (override in subclasses if needed)
	protected virtual void PlayHitFeedback()
	{
		if (transform == null) return;

		if (_shakeTween != null && _shakeTween.IsActive())
			_shakeTween.Kill();

		_shakeTween = transform.DOShakePosition(
			shakeDuration,
			shakeStrength,
			shakeVibrato,
			shakeRandomness,
			snapping: false,
			fadeOut: shakeFadeOut
		);
	}

	private void StartDeathAnimation()
	{
		// oprim shake-ul curent dacă există
		if (_shakeTween != null && _shakeTween.IsActive())
		{
			_shakeTween.Kill();
			_shakeTween = null;
		}

		// dezactivăm coliziunea și fizica în timpul morții
		var cols2D = GetComponentsInChildren<Collider2D>(true);
		for (int i = 0; i < cols2D.Length; i++) cols2D[i].enabled = false;
		var cols3D = GetComponentsInChildren<Collider>(true);
		for (int i = 0; i < cols3D.Length; i++) cols3D[i].enabled = false;

		var rb2d = GetComponent<Rigidbody2D>();
		if (rb2d != null) rb2d.simulated = false;
		var rb3d = GetComponent<Rigidbody>();
		if (rb3d != null) rb3d.isKinematic = true;

		if (!useDeathScaleAnimation || deathScaleDuration <= 0f)
		{
			Destroy(gameObject);
			return;
		}

		// scale-out apoi distrugere
		transform.DOScale(Vector3.zero, deathScaleDuration)
			.SetEase(deathScaleEase)
			.OnComplete(() => Destroy(gameObject));
	}

	private void PlayHitSound()
	{
		if (hitSound == null) return;
		if (AudioManager.Instance != null)
			AudioManager.Instance.Play2DSound(hitSound);
		else
			AudioSource.PlayClipAtPoint(hitSound, Vector3.zero);
	}

	private void PlayDeathSound()
	{
		if (deathSound == null) return;
		if (AudioManager.Instance != null)
			AudioManager.Instance.Play2DSound(deathSound);
		else
			AudioSource.PlayClipAtPoint(deathSound, Vector3.zero);
	}
}