using System.Collections;
using UnityEngine;

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  엘리트 근접형 적
//  · 일반 보다 HP 2.5배, 넉백 저항 40%
//  · 패턴 1: 돌진 참격 (EnemyMelee와 유사)
//  · 패턴 2: 회전 베기 (주변 범위 스핀 어택)
//  · 페이즈 2 (HP 50% 이하): 색상 변화, 속도·쿨다운 단축
//  · 일정 확률로 피격 시 뒤로 회피 (패링 대응 불가 장치)
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
public class EnemyEliteMelee : EnemyBase
{
    [Header("Rush Attack")]
    public float attackDamage = 35f;
    public float rushSpeed = 16f;
    public float rushDuration = 0.28f;
    public float attackCooldown = 1.8f;
    public float attackWindup = 0.5f;

    [Header("Spin Attack")]
    public float spinDamage = 28f;
    public float spinRadius = 2.5f;
    public float spinCooldown = 6f;
    public LayerMask playerLayer;

    [Header("Phase 2")]
    public float phase2HpThreshold = 0.5f;
    public Color phase2Color = new Color(1f, 0.3f, 0.1f, 1f);
    public float phase2SpeedMultiplier = 1.4f;

    [Header("Dodge")]
    [Range(0f, 1f)] public float dodgeChance = 0.35f;
    public float dodgeSpeed = 20f;
    public float dodgeDuration = 0.18f;
    public float dodgeCooldown = 2.5f;

    [Header("Hitboxes")]
    public EnemyHitBox meleeHitBox;     // 돌진 판정
    public EnemyHitBox spinHitBox;      // 스핀 판정

    private float attackCoolTimer;
    private float spinCoolTimer;
    private float dodgeCoolTimer;
    private bool isPhase2;
    private bool isActing;

    protected override void Awake()
    {
        base.Awake();
        isElite = true;
        maxHp *= 2.5f;
        currentHp = maxHp;
        knockbackResistance = 0.4f;
    }

    protected override void UpdateStateMachine()
    {
        if (attackCoolTimer > 0f) attackCoolTimer -= DeltaTime;
        if (spinCoolTimer > 0f)   spinCoolTimer   -= DeltaTime;
        if (dodgeCoolTimer > 0f)  dodgeCoolTimer  -= DeltaTime;

        // 페이즈 2 전환
        if (!isPhase2 && currentHp < maxHp * phase2HpThreshold)
            EnterPhase2();

        float dist = DistanceToPlayer();
        float speed = moveSpeed * (isPhase2 ? phase2SpeedMultiplier : 1f);
        float curCooldown = isPhase2 ? attackCooldown * 0.65f : attackCooldown;

        switch (State)
        {
            case EnemyState.Idle:
                StopMoving();
                if (dist < detectRange) SetState(EnemyState.Chase);
                break;

            case EnemyState.Chase:
                if (dist > loseRange) { SetState(EnemyState.Idle); break; }

                if (!isActing)
                {
                    // 스핀 공격 우선 (가까울 때)
                    if (dist <= spinRadius * 0.9f && spinCoolTimer <= 0f)
                    {
                        StartCoroutine(DoSpinAttack());
                        break;
                    }
                    // 돌진 공격
                    if (dist <= attackRange && attackCoolTimer <= 0f)
                    {
                        StartCoroutine(DoRushAttack(speed, curCooldown));
                        break;
                    }
                    MoveToward(player.position, speed);
                }
                break;

            case EnemyState.Attack:
            case EnemyState.Hurt:
            case EnemyState.Dead:
                break;
        }
    }

    private void EnterPhase2()
    {
        isPhase2 = true;
        if (sr != null) sr.color = phase2Color;
        HitEffectManager.Instance?.TriggerScreenShake(0.3f, 0.5f);
        StartCoroutine(Phase2FlashRoutine());
    }

    private System.Collections.IEnumerator Phase2FlashRoutine()
    {
        for (int i = 0; i < 4; i++)
        {
            if (sr != null) sr.color = Color.white;
            yield return new WaitForSeconds(0.08f);
            if (sr != null) sr.color = phase2Color;
            yield return new WaitForSeconds(0.08f);
        }
    }

    private IEnumerator DoRushAttack(float speed, float cooldown)
    {
        isActing = true;
        SetState(EnemyState.Attack);
        attackCoolTimer = cooldown;
        StopMoving();

        anim?.Play(animIdle, 0, 0f);  // 윈드업 — 잠깐 멈춤
        yield return new WaitForSeconds(attackWindup * (isPhase2 ? 0.65f : 1f));

        // 윈드업 중 피격됐으면 공격 취소
        if (isDead || State == EnemyState.Hurt) { isActing = false; yield break; }

        float dir = player != null
            ? (player.position.x > transform.position.x ? 1f : -1f)
            : 1f;

        float actualRushSpeed = rushSpeed * (isPhase2 ? phase2SpeedMultiplier : 1f);
        rb.linearVelocity = new Vector2(dir * actualRushSpeed, 0f);

        // 히트박스를 돌진 방향 쪽으로 배치
        if (meleeHitBox != null)
        {
            Vector3 hp = meleeHitBox.transform.localPosition;
            meleeHitBox.transform.localPosition = new Vector3(Mathf.Abs(hp.x) * dir, hp.y, hp.z);
        }

        meleeHitBox?.Activate(attackDamage);

        yield return new WaitForSeconds(rushDuration);

        meleeHitBox?.Deactivate();
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.2f, rb.linearVelocity.y);

        isActing = false;
        if (!isDead) SetState(EnemyState.Chase);
    }

    private IEnumerator DoSpinAttack()
    {
        isActing = true;
        SetState(EnemyState.Attack);
        spinCoolTimer = spinCooldown;
        StopMoving();

        float prevGravity = rb.gravityScale;
        rb.gravityScale = 0f;
        anim?.Play(animAttack, 0, 0f);

        yield return new WaitForSeconds(0.15f);

        if (isDead) { rb.gravityScale = prevGravity; isActing = false; yield break; }

        spinHitBox?.Activate(spinDamage);

        // 스핀 범위 내 플레이어 직접 데미지 (OverlapCircle 보조)
        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, spinRadius, playerLayer);
        foreach (var col in cols)
        {
            PlayerStats ps = col.GetComponent<PlayerStats>();
            if (ps != null)
            {
                Vector2 dir = (Vector2)col.transform.position - (Vector2)transform.position;
                ps.TakeDamage(spinDamage, dir);
            }
        }

        HitEffectManager.Instance?.TriggerScreenShake(0.12f, 0.25f);

        yield return new WaitForSeconds(0.35f);

        spinHitBox?.Deactivate();
        rb.gravityScale = prevGravity;
        isActing = false;
        if (!isDead) SetState(EnemyState.Chase);
    }

    public override void TakeDamage(float damage, float knockbackX)
    {
        if (isDead) return;

        // 회피 판정 (피격 중이 아닐 때, 쿨다운 없을 때)
        if (!isActing && dodgeCoolTimer <= 0f && Random.value < dodgeChance)
        {
            StartCoroutine(DodgeRoutine(knockbackX));
            return;
        }

        base.TakeDamage(damage, knockbackX);
    }

    private IEnumerator DodgeRoutine(float attackDir)
    {
        dodgeCoolTimer = dodgeCooldown;
        // 공격 방향 반대로 회피
        float dodgeDir = attackDir > 0f ? -1f : 1f;
        rb.linearVelocity = new Vector2(dodgeDir * dodgeSpeed, rb.linearVelocity.y + 1.5f);

        anim?.Play(animWalk, 0, 0f);  // 회피 중 Walk 재생
        yield return new WaitForSeconds(dodgeDuration);

        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.3f, rb.linearVelocity.y);
    }

    protected override void Die()
    {
        meleeHitBox?.Deactivate();
        spinHitBox?.Deactivate();
        base.Die();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, spinRadius);
    }
}
