using System.Collections;
using UnityEngine;

// 근접 적 — Idle → 감지 → 추격 → 정지 후 제자리 공격
public class EnemyMelee : EnemyBase
{
    [Header("Melee Attack")]
    public float attackDamage = 20f;
    public float attackCooldown = 2f;
    public float attackWindup = 0.4f;       // 공격 전 멈추는 시간
    public float attackActiveTime = 0.25f;  // 히트박스 활성 시간
    public float attackRecover = 0.2f;      // 공격 후 경직

    [Header("Hitbox")]
    public EnemyHitBox meleeHitBox;   // EnemyHitBox: 플레이어에게 데미지

    private float attackCoolTimer;
    private bool isActing;

    protected override void UpdateStateMachine()
    {
        if (attackCoolTimer > 0f) attackCoolTimer -= DeltaTime;

        float dist = DistanceToPlayer();

        switch (State)
        {
            case EnemyState.Idle:
                StopMoving();
                if (dist < detectRange)
                    SetState(EnemyState.Chase);
                break;

            case EnemyState.Chase:
                if (dist > loseRange)
                {
                    SetState(EnemyState.Idle);
                    break;
                }
                if (!isActing && dist <= attackRange)
                {
                    // 공격 범위 내 → 즉시 정지
                    StopMoving();
                    if (attackCoolTimer <= 0f)
                        StartCoroutine(DoAttack());
                    break;
                }
                if (!isActing) MoveToward(player.position, moveSpeed);
                break;

            case EnemyState.Attack:
            case EnemyState.Hurt:
            case EnemyState.Dead:
                break;
        }
    }

    private IEnumerator DoAttack()
    {
        isActing = true;
        SetState(EnemyState.Attack);
        attackCoolTimer = attackCooldown;
        StopMoving();

        // 윈드업 — Idle 포즈로 잠깐 멈춤 (눈치채는 시간)
        anim?.Play("Idle", 0, 0f);
        yield return new WaitForSeconds(attackWindup);

        // 윈드업 중 피격됐으면 공격 취소
        if (isDead || State == EnemyState.Hurt) { isActing = false; yield break; }

        // 공격 모션 + 히트박스 활성 (제자리)
        anim?.Play("Attack", 0, 0f);

        // 히트박스를 현재 바라보는 방향으로 배치
        if (meleeHitBox != null)
        {
            float dir = player != null && player.position.x > transform.position.x ? 1f : -1f;
            Vector3 hp = meleeHitBox.transform.localPosition;
            meleeHitBox.transform.localPosition = new Vector3(Mathf.Abs(hp.x) * dir, hp.y, hp.z);
        }

        yield return new WaitForSeconds(0.1f);   // 공격 모션 시작 후 0.1초 뒤 판정 활성
        if (isDead) { isActing = false; yield break; }
        meleeHitBox?.Activate(attackDamage);
        yield return new WaitForSeconds(attackActiveTime);
        meleeHitBox?.Deactivate();

        // 공격 후 경직
        yield return new WaitForSeconds(attackRecover);

        isActing = false;
        if (!isDead) SetState(EnemyState.Chase);
    }

    protected override void Die()
    {
        meleeHitBox?.Deactivate();
        base.Die();
    }

    // 패링 확인용 — PlayerStats.TakeDamage에서 ParrySystem 체크 후 처리됨
}
