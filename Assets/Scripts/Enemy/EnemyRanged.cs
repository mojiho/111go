using System.Collections;
using UnityEngine;

// 원거리 투사체형 — 거리 유지하면서 투사체 발사
public class EnemyRanged : EnemyBase
{
    [Header("Ranged Attack")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileDamage = 15f;
    public float fireRate = 2.5f;
    public int burstCount = 1;
    public float burstInterval = 0.18f;

    [Header("Positioning")]
    public float preferredDistance = 6f;
    public float tooCloseDistance = 3.5f;

    private float fireCoolTimer;
    private bool isFiring;

    protected override void Awake()
    {
        base.Awake();
        attackRange = preferredDistance + 1f;
    }

    protected override void UpdateStateMachine()
    {
        if (fireCoolTimer > 0f) fireCoolTimer -= DeltaTime;

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

                if (dist < tooCloseDistance)
                    RetreatFromPlayer();
                else if (dist > preferredDistance)
                    MoveToward(player.position, moveSpeed * 0.7f);
                else
                    StopMoving();

                if (dist <= attackRange && fireCoolTimer <= 0f && !isFiring)
                    StartCoroutine(DoFire());
                break;

            case EnemyState.Attack:
            case EnemyState.Hurt:
            case EnemyState.Dead:
                break;
        }
    }

    private void RetreatFromPlayer()
    {
        float dir = player.position.x > transform.position.x ? -1f : 1f;
        rb.linearVelocity = new Vector2(dir * moveSpeed, rb.linearVelocity.y);
    }

    private IEnumerator DoFire()
    {
        isFiring = true;
        fireCoolTimer = fireRate;
        SetState(EnemyState.Attack);

        anim?.Play("Cast", 0, 0f);
        yield return new WaitForSeconds(0.3f);

        for (int i = 0; i < burstCount; i++)
        {
            if (isDead) break;
            SpawnProjectile();
            if (burstCount > 1) yield return new WaitForSeconds(burstInterval);
        }

        yield return new WaitForSeconds(0.2f);
        isFiring = false;
        if (!isDead) SetState(EnemyState.Chase);
    }

    private void SpawnProjectile()
    {
        if (projectilePrefab == null || player == null) return;

        Transform spawnPoint = firePoint != null ? firePoint : transform;
        Vector2 dir = ((Vector2)player.position - (Vector2)spawnPoint.position).normalized;

        GameObject obj = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.identity);
        Projectile proj = obj.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.damage = projectileDamage;
            proj.isPlayerProjectile = false;
            proj.Init(dir);
        }
    }
}
