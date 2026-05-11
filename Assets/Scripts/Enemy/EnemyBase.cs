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

    [Header("Knockback")]
    public float knockbackResistance = 0f;   // 0 = 풀 넉백, 1 = 무효

    public EnemyState State { get; protected set; }

    protected Rigidbody2D rb;
    protected Animator anim;
    protected SpriteRenderer sr;
    protected Transform player;
    protected bool isDead;

    // 슬로우모션 중에도 적은 같이 느려짐 (Time.deltaTime 기반)
    protected float DeltaTime => Time.deltaTime;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        currentHp = maxHp;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        rb.freezeRotation = true;
        rb.gravityScale = 3f;
    }

    protected virtual void Update()
    {
        if (isDead) return;
        UpdateFacing();
        UpdateStateMachine();
    }

    protected abstract void UpdateStateMachine();

    protected float DistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        return Vector2.Distance(transform.position, player.position);
    }

    protected void MoveToward(Vector2 target, float speed)
    {
        float dir = target.x > transform.position.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
    }

    protected void StopMoving()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void UpdateFacing()
    {
        if (player == null) return;
        float dir = player.position.x > transform.position.x ? 1f : -1f;
        transform.localScale = new Vector3(dir, 1f, 1f);
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
