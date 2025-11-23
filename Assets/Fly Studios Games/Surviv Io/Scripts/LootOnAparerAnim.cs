using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class LootOnAparerAnim : MonoBehaviour
{
    public bool enableAnimationOnEnable;
    [Header("Dispersal Settings")]
    public float disperseRadius = 3f;
    public float jumpPower = 1.5f;
    public float jumpDuration = 0.5f;
    public int jumpCount = 0;
    public float randomDelayMax = 0f;
    public Ease jumpEase = Ease.OutQuad;
    [Header("Optional")]
    public bool scalePop = true;
    public float popScale = 1f;
    public float popDuration = 0.25f;

    private Vector3 _centerPos;

    void OnEnable()
    {
        //AnimateItem();
    }   

    private void AnimateItem()
    {
        if (!enableAnimationOnEnable) return;

        _centerPos = transform.position;

        // Start from center (optional scale pop)
        if (scalePop)
        {
            transform.localScale = Vector3.zero;
            transform.DOScale(popScale, popDuration).SetEase(Ease.OutBack);
        }

        Vector2 rnd = Random.insideUnitCircle * disperseRadius;
        Vector3 target = _centerPos + new Vector3(rnd.x, rnd.y, 0f);

        float delay = Random.Range(0f, randomDelayMax);

        Sequence seq = DOTween.Sequence();
        seq.AppendInterval(delay);
        seq.Append(transform.DOJump(target, jumpPower, jumpCount, jumpDuration).SetEase(jumpEase));
        seq.Join(transform.DORotate(new Vector3(0f, 0f, Random.Range(-180f, 180f)), jumpDuration, RotateMode.FastBeyond360));
    }
}