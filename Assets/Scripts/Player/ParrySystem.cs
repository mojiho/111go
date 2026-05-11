using System.Collections;
using UnityEngine;

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  패링 시스템
//  Q 키를 누르면 0.25초 패링 윈도우 오픈
//  윈도우 중 피격 시 → 패링 성공
//    · 피해 무효
//    · 슬로우 게이지 +35
//    · 화면 잠깐 슬로우(0.08배속 0.15초)
//    · 주변 적 넉백
//  쿨다운 4초 (성공 시 2초)
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[RequireComponent(typeof(PlayerController))]
public class ParrySystem : MonoBehaviour
{
    [Header("Parry Settings")]
    public float parryWindowDuration = 0.25f;
    public float parryCooldown = 4f;
    public float gaugeReward = 35f;
    public float counterKnockback = 9f;
    public float counterRadius = 3f;
    public float slowMoBurstDuration = 0.15f;

    [Header("Visuals")]
    public Color parryActiveColor = new Color(0.35f, 0.9f, 1f, 1f);
    public Color parrySuccessColor = new Color(1f, 0.92f, 0.2f, 1f);

    public bool IsParrying { get; private set; }
    public float CooldownRatio => parryCooldown > 0f ? Mathf.Clamp01(cooldownTimer / parryCooldown) : 0f;

    private float cooldownTimer;
    private PlayerController controller;
    private SlowMotionSystem slowMo;
    private SpriteRenderer sr;
    private Color originalColor;
    private Coroutine parryRoutine;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) originalColor = sr.color;
    }

    private void Start()
    {
        slowMo = FindFirstObjectByType<SlowMotionSystem>();
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.unscaledDeltaTime;

        if (Input.GetKeyDown(KeyCode.Q)
            && cooldownTimer <= 0f
            && controller.State != PlayerState.Dead
            && controller.State != PlayerState.Dash)
        {
            if (parryRoutine != null) StopCoroutine(parryRoutine);
            parryRoutine = StartCoroutine(DoParryWindow());
        }
    }

    private IEnumerator DoParryWindow()
    {
        IsParrying = true;
        cooldownTimer = parryCooldown;

        if (sr != null) sr.color = parryActiveColor;

        yield return new WaitForSecondsRealtime(parryWindowDuration);

        IsParrying = false;
        if (sr != null) sr.color = originalColor;
    }

    // PlayerStats.TakeDamage에서 IsParrying이 true일 때 이 메서드 호출
    public void OnSuccessfulParry()
    {
        if (parryRoutine != null) StopCoroutine(parryRoutine);
        IsParrying = false;
        cooldownTimer = parryCooldown * 0.5f;   // 성공 시 쿨다운 단축

        // 슬로우 게이지 보상
        slowMo?.AddGauge(gaugeReward);

        // 연출
        StartCoroutine(ParrySuccessEffect());
        StartCoroutine(SlowMoBurst());

        // 화면 흔들림
        HitEffectManager.Instance?.TriggerScreenShake(0.12f, 0.35f);

        // 주변 적 넉백 (약한 데미지 포함으로 피드백 강화)
        PushNearbyEnemies();
    }

    private void PushNearbyEnemies()
    {
        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, counterRadius,
            LayerMask.GetMask("Enemy"));

        foreach (var col in cols)
        {
            EnemyBase enemy = col.GetComponent<EnemyBase>();
            if (enemy == null) continue;

            float dir = enemy.transform.position.x > transform.position.x ? 1f : -1f;
            enemy.TakeDamage(5f, dir * counterKnockback);
        }
    }

    private IEnumerator ParrySuccessEffect()
    {
        if (sr != null) sr.color = parrySuccessColor;
        yield return new WaitForSecondsRealtime(0.12f);
        if (sr != null) sr.color = originalColor;
    }

    private IEnumerator SlowMoBurst()
    {
        Time.timeScale = 0.08f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        yield return new WaitForSecondsRealtime(slowMoBurstDuration);

        float targetScale = (slowMo != null && slowMo.IsActive) ? slowMo.slowTimeScale : 1f;
        Time.timeScale = targetScale;
        Time.fixedDeltaTime = 0.02f * targetScale;
    }
}
