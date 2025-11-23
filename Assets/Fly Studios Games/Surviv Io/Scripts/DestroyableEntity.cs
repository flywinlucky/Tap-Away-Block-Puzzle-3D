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

	private Tween _shakeTween;

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

		health = Mathf.Max(0f, health - amount);

		PlayHitFeedback();

		if (health <= 0f)
		{
			TrySpawnLoot(); // spawn drops before destroy
			Destroy(gameObject);
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
}