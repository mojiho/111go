using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PlayerStats : MonoBehaviour
{
    [Header("Stats")]
    public float maxHp = 200f;
    public float currentHp { get; private set; }

    [Header("Screen Shake — On Hurt")]
    public float hurtShakeDuration  = 0.2f;
    public float hurtShakeMagnitudeMin = 0.22f;   // 작은 피해
    public float hurtShakeMagnitudeMax = 0.4f;    // 큰 피해 (damage 50 기준)
    public float hurtShakeDamageRef = 50f;        // magnitudeMax 도달 기준 데미지
    [Range(0f, 1f)] public float hurtShakeBias = 0.85f;

    public UnityEvent<float, float> OnHpChanged;   // current, max
    public UnityEvent OnDead;

    private bool isInvincible;
    private PlayerController controller;
    private SpriteRenderer spriteRenderer;
    private ParrySystem parrySystem;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        parrySystem = GetComponent<ParrySystem>();
        currentHp = maxHp;
    }

    public void TakeDamage(float damage) => TakeDamage(damage, Vector2.zero);

    public void TakeDamage(float damage, Vector2 hitDirection)
    {
        if (isInvincible || controller.State == PlayerState.Dead) return;

        // 패링 성공 시 피해 무효
        if (parrySystem != null && parrySystem.IsParrying)
        {
            parrySystem.OnSuccessfulParry();
            return;
        }

        currentHp = Mathf.Max(0f, currentHp - damage);
        OnHpChanged?.Invoke(currentHp, maxHp);

        HitEffectManager.Instance?.TriggerHitFlash(spriteRenderer);

        // 피격 셰이크 — 데미지 비율 따라 강도 가변 (Inspector 노출값 사용)
        float hitMag = Mathf.Lerp(hurtShakeMagnitudeMin, hurtShakeMagnitudeMax,
                                  Mathf.Clamp01(damage / Mathf.Max(0.01f, hurtShakeDamageRef)));
        if (hitDirection.sqrMagnitude > 0.0001f)
            HitEffectManager.Instance?.TriggerDirectionalShake(hurtShakeDuration, hitMag, hitDirection, hurtShakeBias);
        else
            HitEffectManager.Instance?.TriggerScreenShake(hurtShakeDuration, hitMag);

        controller.PlayHurtAnim();

        if (currentHp <= 0f)
        {
            OnDead?.Invoke();
            controller.Die();
        }
        else
        {
            SetInvincible(0.4f);
        }
    }

    public void SetInvincible(float duration)
    {
        StopCoroutine(nameof(InvincibleRoutine));
        StartCoroutine(InvincibleRoutine(duration));
    }

    private IEnumerator InvincibleRoutine(float duration)
    {
        isInvincible = true;
        yield return new WaitForSeconds(duration);
        isInvincible = false;
    }

    public void Heal(float amount)
    {
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        OnHpChanged?.Invoke(currentHp, maxHp);
    }
}
