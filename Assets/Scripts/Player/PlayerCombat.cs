using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [Header("Basic Attack")]
    public HitBox[] comboHitBoxes;          // Inspector에서 콤보별 히트박스 연결 (3개)
    public float[] comboTimes = { 0.25f, 0.25f, 0.35f };
    public float comboDamage = 30f;
    public float comboResetTime = 0.6f;
    public float attackKnockback = 4f;
    public float attackStepSpeed = 6f;      // 방향키 누른 채 공격 시 전진 속도
    public float attackStepDecay = 0.25f;   // 스텝 후 잔속도 감쇠 배수 (0=즉시정지)

    [Header("Skill 1 - 돌진 참격 (K)")]
    public HitBox skill1HitBox;
    public float skill1Damage = 55f;
    public float skill1DashSpeed = 28f;
    public float skill1Duration = 0.22f;
    public float skill1Cooldown = 4f;
    public float skill1Knockback = 8f;
    public float deshGravityScale = 11f;

    [Header("Skill 2 - 범위 회전베기 (L)")]
    public HitBox skill2HitBox;
    public float skill2Damage = 40f;
    public float skill2Duration = 0.5f;
    public float skill2Cooldown = 6f;
    public float skill2Knockback = 6f;

    [Header("Ultimate - 난격 (V / 게이지 전량 소비)")]
    public float ultimateRadius = 2.2f;
    public float ultimateDamage = 55f;
    public int ultimateHitCountMin = 4;   // 최소 게이지(ultimateMinGauge)일 때 히트수
    public int ultimateHitCountMax = 10;  // 게이지 100% 일 때 히트수
    public float ultimateHitInterval = 0.08f;
    public float ultimateMinGauge = 30f;  // 발동에 필요한 최소 게이지
    public LayerMask enemyLayer;

    [Header("Slow Gauge Gain")]
    public float gaugePerHit = 15f;

    [Header("Slash Effect")]
    public float slashLength = 3.5f;             // 길게
    public float slashYOffset = 0.5f;
    public Color slashCombo1Color = new Color(1f, 0.2f, 0.15f, 1f);   // 1타 — 붉은색
    public Color slashCombo2Color = new Color(1f, 0.85f, 0.2f, 1f);   // 2타 — 노란색
    public Color slashSkill1Color = new Color(1f, 0.5f, 0.1f, 1f);    // 돌진 — 오렌지
    public Color slashSkill2Color = new Color(1f, 0.8f, 0.3f, 1f);

    [Header("Screen Shake — Attack Hit")]
    public float hitShakeDuration  = 0.12f;
    public float hitShakeMagnitude = 0.18f;
    [Range(0f, 1f)] public float hitShakeBias = 0.8f;

    [Header("Screen Shake — Skill1 (X) Hit")]
    public float skill1ShakeDuration  = 0.22f;
    public float skill1ShakeMagnitude = 0.45f;
    [Range(0f, 1f)] public float skill1ShakeBias = 0.85f;

    [Header("Screen Shake — Ultimate Each Hit")]
    public float ultShakeDuration  = 0.08f;
    public float ultShakeMagnitude = 0.15f;
    [Range(0f, 1f)] public float ultShakeBias = 0.8f;

    // 현재 진행 중인 공격의 슬래시 파라미터 (OnHitEnemy에서 사용)
    private float pendingSlashAngle;
    private float pendingSlashLength;
    private Color pendingSlashColor;

    public bool IsLocked { get; private set; }

    // 피격 등 외부에서 강제로 전투 잠금 해제 (히트박스도 비활성)
    public void ForceUnlock()
    {
        IsLocked = false;
        isUltimate = false;
        foreach (var hb in comboHitBoxes) hb?.Deactivate();
        skill1HitBox?.Deactivate();
        skill2HitBox?.Deactivate();
        StopAllCoroutines();
    }

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
    }

    private void Start()
    {
        // 모든 Awake 완료 후 찾아야 null 방지
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
        // ── 방향 전환 먼저 (애니메이션 재생 이전에) ──
        var kb = Keyboard.current;
        float stepDir = 0f;
        if (kb != null)
        {
            if (kb.rightArrowKey.isPressed)     stepDir = 1f;
            else if (kb.leftArrowKey.isPressed) stepDir = -1f;
        }
        if (stepDir != 0f)
            controller.SetFacing((int)stepDir);

        IsLocked = true;
        controller.SetState(PlayerState.Attack);
        comboQueued = false;

        int idx = comboIndex;
        if (comboHitBoxes.Length > 0)
            comboIndex = (comboIndex + 1) % comboHitBoxes.Length;
        comboTimer = comboResetTime;

        // 짝수타 → Attack, 홀수타 → Attack2 (두 모션 번갈아)
        controller.PlayAttackAnim(idx);

        // 슬래시 이펙트 파라미터 — OnHitEnemy에서 적 centerPoint 위치에 생성됨
        // 1타: 위→아래 (-45°, \), 2타: 아래→위 (+45°, /) — 같은 기울기로 거울 대칭
        // 좌우 반전: 왼쪽 보면 angle = 180 - angle
        float baseAngle = (idx % 2 == 0) ? -45f : 45f;
        pendingSlashAngle  = (controller.FacingDirection >= 0) ? baseAngle : 180f - baseAngle;
        pendingSlashColor  = (idx % 2 == 0) ? slashCombo1Color : slashCombo2Color;
        pendingSlashLength = slashLength;

        // 방향키 누른 쪽으로 짧게 전진 (눌려 있지 않으면 제자리)
        if (stepDir != 0f)
            rb.linearVelocity = new Vector2(stepDir * attackStepSpeed, rb.linearVelocity.y);
        else
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        yield return new WaitForSeconds(comboTimes[idx] * 0.4f);

        // 히트 발생 시점에서 잔속도 감쇠 — 너무 멀리 미끄러지지 않게
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * attackStepDecay, rb.linearVelocity.y);

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
        bool wasGrounded = controller.IsGrounded;

        if (wasGrounded)
        {
            // 지상: 수평 돌진 — Y를 약간 음수로 눌러서 경사/계단 충돌 후 튀어오름 방지
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(dir * skill1DashSpeed, -0.5f);
        }
        else
        {
            // 공중: 중력 강화 + 수평 속도 부여 → 빠르게 대각선 낙하
            rb.gravityScale = deshGravityScale;
            rb.linearVelocity = new Vector2(dir * skill1DashSpeed, rb.linearVelocity.y);
        }

        // 슬래시 각도 — 지상은 가로(0°), 공중은 진행방향 기준 완만한 대각선 (-20°: 오른쪽 기준)
        float slashAngle = wasGrounded ? 0f : -20f * dir;
        pendingSlashAngle  = slashAngle;
        pendingSlashColor  = slashSkill1Color;
        pendingSlashLength = slashLength * 1.6f;

        yield return new WaitForSeconds(skill1Duration * 0.3f);

        skill1HitBox?.Activate(skill1Damage, skill1Knockback * dir, OnHitEnemySkill1);

        yield return new WaitForSeconds(skill1Duration * 0.7f);

        skill1HitBox?.Deactivate();
        rb.gravityScale = 3f;   // 지상/공중 모두 원래 중력으로 복귀
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.1f, rb.linearVelocity.y);
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
        if (slowMo == null || slowMo.CurrentGauge < ultimateMinGauge) return;
        StartCoroutine(DoUltimate());
    }

    private IEnumerator DoUltimate()
    {
        isUltimate = true;
        IsLocked = true;
        controller.SetState(PlayerState.Ultimate);

        // 현재 게이지 전량 소비 → 게이지 비율로 히트수 결정
        float gaugeRatio = slowMo.CurrentGauge / slowMo.maxGauge;
        int hitCount = Mathf.RoundToInt(
            Mathf.Lerp(ultimateHitCountMin, ultimateHitCountMax, gaugeRatio));
        slowMo.ConsumeGauge(slowMo.CurrentGauge);

        GetComponent<PlayerStats>()?.SetInvincible(hitCount * ultimateHitInterval + 0.5f);
        float prevGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        // ── 발동 연출 ──
        HitEffectManager.Instance?.TriggerHitStop(0.12f);
        HitEffectManager.Instance?.TriggerScreenShake(0.1f, 0.4f);
        yield return new WaitForSecondsRealtime(0.22f);

        // ── 난격 루프 ──
        for (int i = 0; i < hitCount; i++)
        {
            if (controller.State == PlayerState.Dead) break;

            controller.PlayAttackAnim(i);   // 난격 — 두 모션 번갈아 빠르게

            // 난격 슬래시 파라미터 — 적 centerPoint에 \ / 교차 (대칭 45°)
            float ultBase  = (i % 2 == 0) ? -45f : 45f;
            float ultAngle = (controller.FacingDirection >= 0) ? ultBase : 180f - ultBase;
            Color ultColor = (i % 2 == 0) ? slashCombo1Color : slashCombo2Color;

            Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, ultimateRadius, enemyLayer);
            foreach (var col in cols)
            {
                EnemyBase enemy = col.GetComponent<EnemyBase>();
                if (enemy == null) continue;

                float hitDir = enemy.transform.position.x > transform.position.x ? 1f : -1f;
                enemy.TakeDamage(ultimateDamage, hitDir * 4f);
                HitEffectManager.Instance?.SpawnHitEffect(enemy.transform.position);

                // 적 centerPoint에 슬래시 스폰
                Vector3 ep = (Vector3)enemy.BodyPosition;
                ep.z = 0f;
                SlashEffect.Spawn(ep, ultAngle, slashLength * 1.1f, ultColor, 0.12f, 0.3f);
            }

            HitEffectManager.Instance?.TriggerHitStop(0.05f);

            // 난격 각 히트마다 슬래시 방향으로 약한 셰이크
            float ultRad = ultAngle * Mathf.Deg2Rad;
            Vector2 ultDir = new Vector2(Mathf.Cos(ultRad), Mathf.Sin(ultRad));
            HitEffectManager.Instance?.TriggerDirectionalShake(ultShakeDuration, ultShakeMagnitude, ultDir, ultShakeBias);

            yield return new WaitForSecondsRealtime(ultimateHitInterval);
        }

        HitEffectManager.Instance?.TriggerScreenShake(0.2f, 0.5f);

        rb.gravityScale = prevGravity;
        isUltimate = false;
        IsLocked = false;
    }

    // ───── 공통 히트 콜백 ─────

    private void OnHitEnemy(EnemyBase enemy) =>
        DoHitEffects(enemy, hitShakeDuration, hitShakeMagnitude, hitShakeBias, 0.08f);

    // Skill1 전용 콜백 — 더 강한 셰이크 + 약간 더 긴 hit stop
    private void OnHitEnemySkill1(EnemyBase enemy) =>
        DoHitEffects(enemy, skill1ShakeDuration, skill1ShakeMagnitude, skill1ShakeBias, 0.12f);

    private void DoHitEffects(EnemyBase enemy, float shakeDur, float shakeMag, float shakeBias, float hitStop)
    {
        HitEffectManager.Instance?.TriggerHitStop(hitStop);

        // 슬래시 진행 방향으로 편향된 셰이크
        float rad = pendingSlashAngle * Mathf.Deg2Rad;
        Vector2 slashDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        HitEffectManager.Instance?.TriggerDirectionalShake(shakeDur, shakeMag, slashDir, shakeBias);

        HitEffectManager.Instance?.SpawnHitEffect(enemy.transform.position);

        Vector3 sp = (Vector3)enemy.BodyPosition;
        sp.z = 0f;
        SlashEffect.Spawn(sp, pendingSlashAngle, pendingSlashLength, pendingSlashColor);

        slowMo?.AddGauge(gaugePerHit);
    }

    public float Skill1CooldownRatio => skill1CoolTimer / skill1Cooldown;
    public float Skill2CooldownRatio => skill2CoolTimer / skill2Cooldown;
    public bool UltimateReady => slowMo != null && slowMo.CurrentGauge >= ultimateMinGauge && !isUltimate;
}
