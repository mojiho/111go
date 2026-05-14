using System.Collections;
using UnityEngine;

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  원거리 투사체형 적
//  · 감지 → 거리 유지 (너무 가까우면 후퇴)
//  · 발사 전 스프라이트 플래시로 예고
//  · 연사(burst) 지원
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
public class EnemyRanged : EnemyBase
{
    [Header("Animation — Ranged")]
    public string animCast  = "Cast";   // 발사 모션 State 이름

    [Header("Ranged Attack")]
    public GameObject projectilePrefab;
    public Transform firePoint;              // 없으면 transform 기준
    public float projectileDamage   = 15f;
    public float projectileKnockback = 4f;  // 플레이어에게 가하는 넉백
    public float projectileSpeed    = 12f;  // Projectile.speed 오버라이드
    public float fireRate           = 2.5f; // 발사 쿨다운(초)
    public float windupDuration     = 0.45f;// 발사 전 대기(예고) 시간
    public int   burstCount         = 1;    // 연사 수
    public float burstInterval      = 0.18f;
    public float spreadAngle        = 0f;   // 연사 시 탄 퍼짐 각도 (0 = 직선)

    [Header("Windup Flash (발사 예고)")]
    public Color windupFlashColor   = new Color(1f, 0.6f, 0.1f, 1f);
    public float windupFlashSpeed   = 12f;  // 원래 색 ↔ 플래시 색 전환 속도

    [Header("Positioning")]
    public float preferredDistance  = 6f;   // 유지하려는 거리
    public float tooCloseDistance   = 3.5f; // 이 거리 이하면 후퇴

    private float fireCoolTimer;
    private bool  isFiring;
    private Color _baseColor;

    protected override void Awake()
    {
        base.Awake();
        attackRange    = preferredDistance + 1f;
        _baseColor     = sr != null ? sr.color : Color.white;
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
                if (dist > loseRange) { SetState(EnemyState.Idle); break; }

                if (!isFiring)
                {
                    if (dist < tooCloseDistance)
                        Retreat();
                    else if (dist > preferredDistance)
                        MoveToward(player.position, moveSpeed);
                    else
                        StopMoving();

                    if (dist <= attackRange && fireCoolTimer <= 0f)
                        StartCoroutine(DoFire());
                }
                break;

            case EnemyState.Attack:
            case EnemyState.Hurt:
            case EnemyState.Dead:
                break;
        }
    }

    // ───── 후퇴 — 플레이어 방향을 바라보면서 반대로 이동 ─────
    private void Retreat()
    {
        if (player == null) return;
        float moveDir = player.position.x > transform.position.x ? -1f : 1f;
        rb.linearVelocity = new Vector2(moveDir * moveSpeed, rb.linearVelocity.y);
        // UpdateFacing(Chase)에서 플레이어 방향으로 sprite는 자동 유지됨
    }

    // ───── 발사 루틴 ─────
    private IEnumerator DoFire()
    {
        isFiring       = true;
        fireCoolTimer  = fireRate;
        SetState(EnemyState.Attack);
        StopMoving();

        // 발사 예고 애니메이션 + 스프라이트 플래시
        anim?.Play(animCast, 0, 0f);
        yield return StartCoroutine(WindupFlash(windupDuration));

        if (isDead) { isFiring = false; yield break; }

        // 연사
        for (int i = 0; i < burstCount; i++)
        {
            if (isDead) break;
            SpawnProjectile(i);
            if (i < burstCount - 1)
                yield return new WaitForSeconds(burstInterval);
        }

        yield return new WaitForSeconds(0.2f);

        isFiring = false;
        if (!isDead) SetState(EnemyState.Chase);
    }

    // 윈드업 동안 스프라이트를 플래시 색 ↔ 원래 색으로 깜빡임
    private IEnumerator WindupFlash(float duration)
    {
        if (sr == null) { yield return new WaitForSeconds(duration); yield break; }

        float t = 0f;
        while (t < duration)
        {
            if (isDead) break;
            float k = Mathf.PingPong(t * windupFlashSpeed, 1f);
            sr.color = Color.Lerp(_baseColor, windupFlashColor, k);
            t += Time.deltaTime;
            yield return null;
        }
        sr.color = _baseColor;
    }

    // ───── 투사체 스폰 ─────
    private void SpawnProjectile(int burstIndex)
    {
        if (projectilePrefab == null || player == null) return;

        Transform spawnPt = firePoint != null ? firePoint : transform;
        Vector2 baseDir   = ((Vector2)player.position - (Vector2)spawnPt.position).normalized;

        // 연사 퍼짐 — burstIndex 기준 균등 분배
        float angle = 0f;
        if (spreadAngle > 0f && burstCount > 1)
        {
            float half = spreadAngle * 0.5f;
            angle = Mathf.Lerp(-half, half, (float)burstIndex / (burstCount - 1));
        }

        Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDir;

        GameObject obj = Instantiate(projectilePrefab, spawnPt.position, Quaternion.identity);
        if (obj.TryGetComponent(out Projectile proj))
        {
            proj.speed             = projectileSpeed;
            proj.damage            = projectileDamage;
            proj.isPlayerProjectile = false;
            proj.knockback         = projectileKnockback;
            proj.Init(dir);
        }
    }

    protected override void Die()
    {
        StopAllCoroutines();
        if (sr != null) sr.color = _baseColor;  // 플래시 중 사망 시 색상 복구
        base.Die();
    }
}
