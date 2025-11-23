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
			Destroy(gameObject);
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