using System.Collections;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Basic Attack")]
    public HitBox[] comboHitBoxes;          // Inspector에서 콤보별 히트박스 연결 (3개)
    public float[] comboTimes = { 0.25f, 0.25f, 0.35f };
    public float comboDamage = 30f;
    public float comboResetTime = 0.6f;
    public float attackKnockback = 4f;

    [Header("Skill 1 - 돌진 참격 (K)")]
    public HitBox skill1HitBox;
    public float skill1Damage = 55f;
    public float skill1DashSpeed = 28f;
    public float skill1Duration = 0.22f;
    public float skill1Cooldown = 4f;
    public float skill1Knockback = 8f;

    [Header("Skill 2 - 범위 회전베기 (L)")]
    public HitBox skill2HitBox;
    public float skill2Damage = 40f;
    public float skill2Duration = 0.5f;
    public float skill2Cooldown = 6f;
    public float skill2Knockback = 6f;

    [Header("Ultimate - 난격 (R / 게이지 80% 필요)")]
    public float ultimateRadius = 2.2f;
    public float ultimateDamage = 55f;
    public int ultimateHitCount = 6;
    public float ultimateHitInterval = 0.08f;
    public float ultimateGaugeCost = 80f;
    public LayerMask enemyLayer;

    [Header("Slow Gauge Gain")]
    public float gaugePerHit = 15f;

    public bool IsLocked { get; private set; }

    private PlayerController controller;
    private Rigidbody2D rb;
    private SlowMotionSystem slowMo;

    private int comboIndex;
    private float comboTimer;
    private bool comboQueued;

    private float skill1CoolTimer;
    private float skill2CoolTimer;
    private bool isUltimate;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();
        slowMo = FindFirstObjectByType<SlowMotionSystem>();
    }

    private void Update()
    {
        if (comboTimer > 0f)
        {
            comboTimer -= Time.unscaledDeltaTime;
            if (comboTimer <= 0f) comboIndex = 0;
        }

        if (skill1CoolTimer > 0f) skill1CoolTimer -= Time.unscaledDeltaTime;
        if (skill2CoolTimer > 0f) skill2CoolTimer -= Time.unscaledDeltaTime;
    }

    // ───── 기본 공격 ─────

    public void TryAttack()
    {
        if (IsLocked)
        {
            comboQueued = true;
            return;
        }
        StartCoroutine(DoComboAttack());
    }

    private IEnumerator DoComboAttack()
    {
        IsLocked = true;
        controller.SetState(PlayerState.Attack);
        comboQueued = false;

        int idx = comboIndex;
        if (comboHitBoxes.Length > 0)
            comboIndex = (comboIndex + 1) % comboHitBoxes.Length;
        comboTimer = comboResetTime;

        // Attack 애니메이션 재생 (히트 타이밍마다 처음부터)
        controller.PlayAttackAnim();

        yield return new WaitForSeconds(comboTimes[idx] * 0.4f);

        if (comboHitBoxes.Length > idx && comboHitBoxes[idx] != null)
            comboHitBoxes[idx].Activate(comboDamage, attackKnockback * controller.FacingDirection, OnHitEnemy);

        yield return new WaitForSeconds(comboTimes[idx] * 0.6f);

        if (comboHitBoxes.Length > idx && comboHitBoxes[idx] != null)
            comboHitBoxes[idx].Deactivate();

        IsLocked = false;

        if (comboQueued)
            StartCoroutine(DoComboAttack());
    }

    // ───── 스킬 1 : 돌진 참격 ─────

    public void TrySkill1()
    {
        if (IsLocked || skill1CoolTimer > 0f) return;
        StartCoroutine(DoSkill1());
    }

    private IEnumerator DoSkill1()
    {
        IsLocked = true;
        skill1CoolTimer = skill1Cooldown;
        controller.SetState(PlayerState.Skill1);

        controller.PlaySkill1Anim();   // Dash-Attack 애니메이션

        float dir = controller.FacingDirection;
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(dir * skill1DashSpeed, 0f);

        yield return new WaitForSeconds(skill1Duration * 0.3f);

        skill1HitBox?.Activate(skill1Damage, skill1Knockback * dir, OnHitEnemy);

        yield return new WaitForSeconds(skill1Duration * 0.7f);

        skill1HitBox?.Deactivate();
        rb.gravityScale = 3f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.1f, 0f);
        IsLocked = false;
    }

    // ───── 스킬 2 : 범위 회전베기 ─────

    public void TrySkill2()
    {
        if (IsLocked || skill2CoolTimer > 0f) return;
        StartCoroutine(DoSkill2());
    }

    private IEnumerator DoSkill2()
    {
        IsLocked = true;
        skill2CoolTimer = skill2Cooldown;
        controller.SetState(PlayerState.Skill2);

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;

        controller.PlaySkill2Anim();   // Attack 애니메이션 재사용

        yield return new WaitForSeconds(skill2Duration * 0.2f);

        skill2HitBox?.Activate(skill2Damage, skill2Knockback, OnHitEnemy);

        yield return new WaitForSeconds(skill2Duration * 0.6f);

        skill2HitBox?.Deactivate();

        yield return new WaitForSeconds(skill2Duration * 0.2f);

        rb.gravityScale = 3f;
        IsLocked = false;
    }

    // ───── 필살기 : 난격 ─────

    public void TryUltimate()
    {
        if (IsLocked || isUltimate) return;
        if (slowMo == null || slowMo.CurrentGauge < ultimateGaugeCost) return;
        StartCoroutine(DoUltimate());
    }

    private IEnumerator DoUltimate()
    {
        isUltimate = true;
        IsLocked = true;
        controller.SetState(PlayerState.Ultimate);

        slowMo.ConsumeGauge(ultimateGaugeCost);

        GetComponent<PlayerStats>()?.SetInvincible(ultimateHitCount * ultimateHitInterval + 0.5f);
        float prevGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        // ── 발동 연출 ──
        HitEffectManager.Instance?.TriggerHitStop(0.12f);
        HitEffectManager.Instance?.TriggerScreenShake(0.1f, 0.4f);
        yield return new WaitForSecondsRealtime(0.22f);

        // ── 난격 루프 ──
        for (int i = 0; i < ultimateHitCount; i++)
        {
            if (controller.State == PlayerState.Dead) break;

            controller.PlayAttackAnim();

            Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, ultimateRadius, enemyLayer);
            foreach (var col in cols)
            {
                EnemyBase enemy = col.GetComponent<EnemyBase>();
                if (enemy == null) continue;

                float hitDir = enemy.transform.position.x > transform.position.x ? 1f : -1f;
                enemy.TakeDamage(ultimateDamage, hitDir * 4f);
                HitEffectManager.Instance?.SpawnHitEffect(enemy.transform.position);
                slowMo?.AddGauge(3f);
            }

            HitEffectManager.Instance?.TriggerHitStop(0.05f);
            HitEffectManager.Instance?.TriggerScreenShake(0.06f, 0.18f);

            yield return new WaitForSecondsRealtime(ultimateHitInterval);
        }

        HitEffectManager.Instance?.TriggerScreenShake(0.2f, 0.5f);

        rb.gravityScale = prevGravity;
        isUltimate = false;
        IsLocked = false;
    }

    // ───── 공통 히트 콜백 ─────

    private void OnHitEnemy(EnemyBase enemy)
    {
        HitEffectManager.Instance?.TriggerHitStop(0.08f);
        HitEffectManager.Instance?.TriggerScreenShake(0.1f, 0.18f);
        HitEffectManager.Instance?.SpawnHitEffect(enemy.transform.position);

        slowMo?.AddGauge(gaugePerHit);
    }

    public float Skill1CooldownRatio => skill1CoolTimer / skill1Cooldown;
    public float Skill2CooldownRatio => skill2CoolTimer / skill2Cooldown;
    public bool UltimateReady => slowMo != null && slowMo.CurrentGauge >= ultimateGaugeCost && !isUltimate;
}
