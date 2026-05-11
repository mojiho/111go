using System.Collections;
using UnityEngine;

public enum EnemyState { Idle, Chase, Attack, Hurt, Dead }

[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Stats")]
    public float maxHp = 60f;
    public float currentHp { get; protected set; }
    public bool isElite = false;

    [Header("Detection")]
    public float detectRange = 8f;
    public float attackRange = 1.5f;
    public float loseRange = 14f;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float acceleration = 30f;   // 초당 속도 변화량 (클수록 즉시 반응)

    [Header("Sprite")]
    public bool spriteDefaultRight = false;  // 스프라이트 기본 방향이 오른쪽이면 true
    public Transform centerPoint;            // 캐릭터의 실제 몸 중심 — 거리/공격 판정 기준

    [Header("Knockback")]
    public float knockbackResistance = 0f;   // 0 = 풀 넉백, 1 = 무효

    public EnemyState State { get; protected set; }

    protected Rigidbody2D rb;
    protected Animator anim;
    protected SpriteRenderer sr;
    protected Collider2D mainCol;
    protected Vector2 colBaseOffset;
    protected Transform player;
    protected bool isDead;

    // 슬로우모션 중에도 적은 같이 느려짐 (Time.deltaTime 기반)
    protected float DeltaTime => Time.deltaTime;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        // 본체 콜라이더(트리거 아님)만 캐싱 — 자식 EnemyHitBox는 제외
        foreach (var c in GetComponents<Collider2D>())
        {
            if (!c.isTrigger) { mainCol = c; break; }
        }
        if (mainCol != null) colBaseOffset = mainCol.offset;
        currentHp = maxHp;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        rb.freezeRotation = true;
        rb.gravityScale = 3f;

        // 적끼리 + 플레이어와 물리 충돌 무시 (통과)
        int enemyLayer  = LayerMask.NameToLayer("Enemy");
        int playerLayer = LayerMask.NameToLayer("Player");
        if (enemyLayer >= 0)
            Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);
        if (enemyLayer >= 0 && playerLayer >= 0)
            Physics2D.IgnoreLayerCollision(enemyLayer, playerLayer, true);
    }

    protected virtual void Update()
    {
        if (isDead) return;
        UpdateFacing();
        UpdateStateMachine();
    }

    protected abstract void UpdateStateMachine();

    // 거리/공격 판정용 실제 몸 중심 — centerPoint 자식이 있으면 그 위치, 없으면 루트
    public Vector2 BodyPosition =>
        centerPoint != null ? (Vector2)centerPoint.position : (Vector2)transform.position;

    protected float DistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        return Vector2.Distance(BodyPosition, player.position);
    }

    private float moveVelX;   // 미사용 — StopMoving 호환용으로 남김
    protected void MoveToward(Vector2 target, float speed)
    {
        float dir = target.x > BodyPosition.x ? 1f : -1f;
        float targetX = dir * speed;
        // 일정 가속도로 목표속도까지 접근 → 누적 폭주 없음
        float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetX, acceleration * Time.deltaTime);
        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
    }

    protected void StopMoving()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        moveVelX = 0f;   // SmoothDamp 누적 속도 리셋
    }

    private float lastFaceDir = 1f;
    private bool centerInit;
    private float centerBaseX;
    private void UpdateFacing()
    {
        if (player == null || sr == null) return;
        if (centerPoint != null && !centerInit)
        {
            centerBaseX = Mathf.Abs(centerPoint.localPosition.x);
            centerInit = true;
        }

        float xDelta = player.position.x - BodyPosition.x;
        if (Mathf.Abs(xDelta) < 0.2f) return;
        float faceDir = xDelta > 0f ? 1f : -1f;
        if (faceDir == lastFaceDir) return;
        lastFaceDir = faceDir;

        bool flipped = spriteDefaultRight ? (faceDir < 0f) : (faceDir > 0f);

        // 1) Flip 전 Center의 월드 위치 기억
        Vector3 centerWorldBefore = centerPoint != null ? centerPoint.position : transform.position;

        sr.flipX = flipped;

        // 2) Center의 localPosition X 부호 반전
        if (centerPoint != null)
        {
            Vector3 c = centerPoint.localPosition;
            c.x = flipped ? -centerBaseX : centerBaseX;
            centerPoint.localPosition = c;
        }

        // 3) 콜라이더 오프셋도 부호 반전 (시각과 함께 이동)
        if (mainCol != null)
        {
            mainCol.offset = new Vector2(
                flipped ? -Mathf.Abs(colBaseOffset.x) : Mathf.Abs(colBaseOffset.x),
                colBaseOffset.y
            );
        }

        // 4) 루트 위치를 보정 → Center의 월드 위치를 flip 전과 동일하게 유지
        if (centerPoint != null)
        {
            Vector3 centerWorldAfter = centerPoint.position;
            Vector3 shift = centerWorldBefore - centerWorldAfter;
            transform.position += shift;
        }
    }

    public virtual void TakeDamage(float damage, float knockbackX)
    {
        if (isDead) return;

        currentHp -= damage;
        HitEffectManager.Instance?.TriggerHitFlash(sr);

        float actualKnockback = knockbackX * (1f - knockbackResistance);
        rb.linearVelocity = new Vector2(actualKnockback, rb.linearVelocity.y + 3f);

        if (currentHp <= 0f)
            Die();
        else
            StartCoroutine(HurtRoutine());
    }

    protected virtual IEnumerator HurtRoutine()
    {
        EnemyState prev = State;
        State = EnemyState.Hurt;
        anim?.Play("Hurt", 0, 0f);

        yield return new WaitForSeconds(0.15f);

        if (!isDead) State = prev == EnemyState.Hurt ? EnemyState.Chase : prev;
    }

    protected virtual void Die()
    {
        isDead = true;
        State = EnemyState.Dead;
        rb.linearVelocity = Vector2.zero;

        anim?.Play("Death", 0, 0f);
        GetComponent<Collider2D>().enabled = false;

        // 슬로우모션 게이지 보상
        FindFirstObjectByType<SlowMotionSystem>()?.AddGauge(isElite ? 30f : 10f);

        WaveManager.Instance?.OnEnemyKilled();

        Destroy(gameObject, 1.5f);
    }

    protected void SetState(EnemyState newState)
    {
        if (State == newState) return;
        State = newState;
        // Bringer of Death 컨트롤러도 파라미터 없으므로 Play() 사용
        switch (newState)
        {
            case EnemyState.Idle:   anim?.Play("Idle",   0, 0f); break;
            case EnemyState.Chase:  anim?.Play("Walk",   0, 0f); break;
            case EnemyState.Attack: anim?.Play("Attack", 0, 0f); break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = centerPoint != null ? centerPoint.position : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, attackRange);
    }
}
