using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PlayerStats : MonoBehaviour
{
    [Header("Stats")]
    public float maxHp = 200f;
    public float currentHp { get; private set; }

    [Header("World-Space HP Bar (선택)")]
    public Slider worldHpSlider;            // 머리 위 슬라이더
    public bool hideWorldHpBarWhenFull = true;
    public CanvasGroup worldHpBarCanvasGroup;
    public Transform worldHpBarRoot;        // 좌우반전 보정 대상 — 보통 Canvas 자체 드래그

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

        if (worldHpSlider != null)
        {
            worldHpSlider.minValue = 0f;
            worldHpSlider.maxValue = 1f;
            worldHpSlider.wholeNumbers = false;
            worldHpSlider.interactable = false;
            worldHpSlider.value = 1f;
        }
        UpdateWorldHpBarVisibility();
    }

    private void UpdateWorldHpBar()
    {
        if (worldHpSlider != null && maxHp > 0f)
            worldHpSlider.value = Mathf.Clamp01(currentHp / maxHp);
        UpdateWorldHpBarVisibility();
    }

    private void LateUpdate()
    {
        // worldHpBarRoot가 비어있으면 슬라이더 부모 중 Canvas를 자동 탐색
        if (worldHpBarRoot == null && worldHpSlider != null)
        {
            Canvas c = worldHpSlider.GetComponentInParent<Canvas>();
            if (c != null) worldHpBarRoot = c.transform;
        }
        if (worldHpBarRoot == null) return;

        // 부모(플레이어)가 transform.localScale.x로 좌우반전 → 자식 Canvas도 거꾸로 됨
        // 자식 localScale.x 부호를 부모와 같게 두면 lossyScale.x가 항상 양수 → 정방향 유지
        float parentSignX = Mathf.Sign(transform.localScale.x);
        if (parentSignX == 0f) parentSignX = 1f;
        Vector3 s = worldHpBarRoot.localScale;
        s.x = Mathf.Abs(s.x) * parentSignX;
        worldHpBarRoot.localScale = s;
    }

    private void UpdateWorldHpBarVisibility()
    {
        if (worldHpSlider == null) return;
        bool full = currentHp >= maxHp - 0.01f;
        bool show = !(hideWorldHpBarWhenFull && full);
        if (worldHpBarCanvasGroup != null)
            worldHpBarCanvasGroup.alpha = show ? 1f : 0f;
        else
            worldHpSlider.gameObject.SetActive(show);
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
        UpdateWorldHpBar();

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
        UpdateWorldHpBar();
    }
}
