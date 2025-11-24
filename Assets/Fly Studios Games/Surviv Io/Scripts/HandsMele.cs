using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class HandsMele : MonoBehaviour
{
    public Transform LeftHandPosition;
    public Transform RightHandPosition;
    public CircleCollider2D handColider2D;

    [Header("Melee Settings")]
    public float meleeDamage = 1f;
    public float attackCooldown = 0.35f;
    public float activeHitDuration = 0.12f;
    [Tooltip("Mică distanță de punch pe axa X pentru feedback.")]
    public float punchDistance = 0.15f;
    public float punchDuration = 0.1f;

    [Header("Detection")]
    public LayerMask hitMask = ~0; // all by default
    [Tooltip("Dacă true, folosește atât LeftHandPosition cât și RightHandPosition pentru scan.")]
    public bool useBothHandsForScan = false;

    [Header("Audio")]
    public AudioClip hitSound;

    private float _nextAttackTime = 0f;
    private bool _attackActive = false;
    private HashSet<DestroyableEntity> _hitThisAttack = new HashSet<DestroyableEntity>();
    private Tween _punchTween;
    private Coroutine _scanRoutine;
    private Rigidbody2D _rbKinematic;

    // Start is called before the first frame update
    void Start()
    {
        if (handColider2D == null)
            handColider2D = GetComponent<CircleCollider2D>();

        if (handColider2D != null)
            handColider2D.isTrigger = true; // revenim la trigger

        // Asigurăm un Rigidbody2D kinematic pentru a garanta evenimente trigger
        _rbKinematic = GetComponent<Rigidbody2D>();
        if (_rbKinematic == null)
        {
            _rbKinematic = gameObject.AddComponent<Rigidbody2D>();
            _rbKinematic.bodyType = RigidbodyType2D.Kinematic;
            _rbKinematic.simulated = true;
            _rbKinematic.useFullKinematicContacts = false;
        }
    }

    public bool CanAttack => Time.time >= _nextAttackTime;

    public void Attack(bool flipped)
    {
        if (!CanAttack) return;
        _nextAttackTime = Time.time + attackCooldown;
        _attackActive = true;
        _hitThisAttack.Clear();

        // Punch vizual (mic recoil/lovește înainte)
        if (_punchTween != null && _punchTween.IsActive())
            _punchTween.Kill();

        float dir = flipped ? 1f : -1f;
        var startPos = transform.localPosition;
        var punchTarget = startPos + new Vector3(dir * punchDistance, 0f, 0f);

        _punchTween = DOTween.Sequence()
            .Append(transform.DOLocalMove(punchTarget, punchDuration * 0.5f).SetEase(Ease.OutQuad))
            .Append(transform.DOLocalMove(startPos, punchDuration * 0.5f).SetEase(Ease.InQuad));

        // Start scan pentru obiecte fără rigidbody
        if (_scanRoutine != null) StopCoroutine(_scanRoutine);
        _scanRoutine = StartCoroutine(ScanDuringActiveWindow());

        // Închidem fereastra de hit după activeHitDuration
        Invoke(nameof(EndAttackWindow), activeHitDuration);

               PlayHitSound();
    }

    private IEnumerator ScanDuringActiveWindow()
    {
        float endTime = Time.time + activeHitDuration;
        while (_attackActive && Time.time < endTime)
        {
            PerformAreaScan();
            yield return null; // every frame
        }
    }

    private void PerformAreaScan()
    {
        if (handColider2D == null) return;

        float radius = handColider2D.radius * Mathf.Abs(transform.lossyScale.x);
        if (radius <= 0f) radius = punchDistance * 0.5f;

        // scan main hand
        ScanPosition(transform.position, radius);

        if (useBothHandsForScan)
        {
            if (LeftHandPosition != null) ScanPosition(LeftHandPosition.position, radius);
            if (RightHandPosition != null) ScanPosition(RightHandPosition.position, radius);
        }
    }

    private void ScanPosition(Vector3 pos, float radius)
    {
        var cols = Physics2D.OverlapCircleAll(pos, radius, hitMask);
        if (cols == null || cols.Length == 0) return;
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == handColider2D) continue;
            var dest = c.GetComponent<DestroyableEntity>();
            if (dest == null) continue;
            if (_hitThisAttack.Contains(dest)) continue;

            // aplicăm damage
            dest.TakeDamage(meleeDamage);
            _hitThisAttack.Add(dest);
        }
    }

    private void EndAttackWindow()
    {
        _attackActive = false;
        if (_scanRoutine != null)
        {
            StopCoroutine(_scanRoutine);
            _scanRoutine = null;
        }
    }

    // Trigger-based lovituri (funcționează dacă cealaltă parte are Rigidbody2D)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_attackActive || other == null) return;
        var dest = other.GetComponent<DestroyableEntity>();
        if (dest == null) return;
        if (_hitThisAttack.Contains(dest)) return;

        dest.TakeDamage(meleeDamage);
        _hitThisAttack.Add(dest);
        PlayHitSound();
    }

    private void PlayHitSound()
    {
        if (hitSound == null) return;
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play2DSound(hitSound);
        else
            AudioSource.PlayClipAtPoint(hitSound, Vector3.zero);
    }

    public void EnableHands(bool enable)
    {
        gameObject.SetActive(enable);
    }

    private void OnDestroy()
    {
        if (_punchTween != null && _punchTween.IsActive())
            _punchTween.Kill();
        if (_scanRoutine != null)
            StopCoroutine(_scanRoutine);
    }
}
