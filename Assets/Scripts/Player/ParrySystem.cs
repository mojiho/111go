using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// 패링: Left/Right Shift
// 0.25초 윈도우 중 피격 시 성공 → 피해 무효 + 슬로우 버스트 + 게이지 +35 + 주변 적 넉백
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
    public Color parryActiveColor  = new Color(0.35f, 0.9f, 1f,  1f);
    public Color parrySuccessColor = new Color(1f,    0.92f, 0.2f, 1f);

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

        if (Keyboard.current == null) return;

        // Shift (좌/우) 로 패링
        bool shiftPressed = Keyboard.current.leftShiftKey.wasPressedThisFrame
                         || Keyboard.current.rightShiftKey.wasPressedThisFrame;

        if (shiftPressed
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

    public void OnSuccessfulParry()
    {
        if (parryRoutine != null) StopCoroutine(parryRoutine);
        IsParrying = false;
        cooldownTimer = parryCooldown * 0.5f;

        slowMo?.AddGauge(gaugeReward);

        StartCoroutine(ParrySuccessEffect());
        StartCoroutine(SlowMoBurst());

        HitEffectManager.Instance?.TriggerScreenShake(0.12f, 0.35f);
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
