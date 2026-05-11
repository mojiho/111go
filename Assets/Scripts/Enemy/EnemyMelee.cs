using System.Collections;
using UnityEngine;

// 근접 돌진형 적 — Idle → 감지 → 추격 → 돌진 공격
public class EnemyMelee : EnemyBase
{
    [Header("Melee Attack")]
    public float attackDamage = 20f;
    public float rushSpeed = 14f;
    public float rushDuration = 0.25f;
    public float attackCooldown = 2f;
    public float attackWindup = 0.4f;       // 공격 전 멈추는 시간

    [Header("Hitbox")]
    public HitBox meleeHitBox;

    private float attackCoolTimer;
    private bool isRushing;

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
                if (dist <= attackRange && attackCoolTimer <= 0f)
                {
                    StartCoroutine(DoRushAttack());
                    break;
                }
                MoveToward(player.position, moveSpeed);
                break;

            case EnemyState.Attack:
            case EnemyState.Hurt:
            case EnemyState.Dead:
                break;
        }
    }

    private IEnumerator DoRushAttack()
    {
        SetState(EnemyState.Attack);
        attackCoolTimer = attackCooldown;
        StopMoving();

        // 윈드업 (플레이어가 눈치채는 시간)
        anim?.SetTrigger("WindUp");
        yield return new WaitForSeconds(attackWindup);

        if (isDead) yield break;

        // 돌진
        isRushing = true;
        float dir = player.position.x > transform.position.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * rushSpeed, 0f);

        meleeHitBox?.Activate(attackDamage, dir * 6f);

        yield return new WaitForSeconds(rushDuration);

        isRushing = false;
        meleeHitBox?.Deactivate();
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.2f, rb.linearVelocity.y);

        if (!isDead) SetState(EnemyState.Chase);
    }

    protected override void Die()
    {
        meleeHitBox?.Deactivate();
        base.Die();
    }
}
