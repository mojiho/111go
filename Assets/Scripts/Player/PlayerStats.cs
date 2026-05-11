using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PlayerStats : MonoBehaviour
{
    [Header("Stats")]
    public float maxHp = 200f;
    public float currentHp { get; private set; }

    public UnityEvent<float, float> OnHpChanged;   // current, max
    public UnityEvent OnDead;

    private bool isInvincible;
    private PlayerController controller;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHp = maxHp;
    }

    public void TakeDamage(float damage)
    {
        if (isInvincible || controller.State == PlayerState.Dead) return;

        currentHp = Mathf.Max(0f, currentHp - damage);
        OnHpChanged?.Invoke(currentHp, maxHp);

        HitEffectManager.Instance?.TriggerHitFlash(spriteRenderer);
        HitEffectManager.Instance?.TriggerScreenShake(0.15f, 0.25f);

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
