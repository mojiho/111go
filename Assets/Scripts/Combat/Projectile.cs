using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed  = 10f;
    public float damage = 20f;
    public float lifetime = 4f;
    public float knockback = 4f;
    public bool isPlayerProjectile = false;

    [Header("Hit Animation")]
    public string hitStateName = "Hit";   // Animator State 이름

    private Vector2   direction;
    private Rigidbody2D rb;
    private Animator  anim;
    private Collider2D col;

    private bool _isHit;     // 충돌 처리 완료 — 중복 방지
    private bool _isFrozen;
    private Vector2 _frozenVel;

    // ─── Freeze (필살기 연출) ───
    public void SetFrozen(bool freeze)
    {
        if (_isFrozen == freeze) return;
        _isFrozen = freeze;
        if (freeze)
        {
            _frozenVel = rb != null ? rb.linearVelocity : Vector2.zero;
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
        else
        {
            if (rb != null && !_isHit) rb.linearVelocity = _frozenVel;
        }
    }

    public static void FreezeAll(bool freeze)
    {
        foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            p.SetFrozen(freeze);
    }

    // ─── 초기화 ───
    private void Awake()
    {
        rb   = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        col  = GetComponent<Collider2D>();
        if (rb != null) rb.gravityScale = 0f;
    }

    public void Init(Vector2 dir, float dmg = -1f)
    {
        _isHit    = false;
        direction = dir.normalized;
        if (dmg >= 0f) damage = dmg;

        if (rb != null)
            rb.linearVelocity = direction * speed;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 애니메이터 재시작 (풀 재사용 대비)
        anim?.Play("Projectile", 0, 0f);

        CancelInvoke(nameof(ForceDestroy));
        Invoke(nameof(ForceDestroy), lifetime);
    }

    // ─── 이동 ───
    private void Update()
    {
        if (_isFrozen || _isHit) return;
        if (rb == null)
            transform.position += (Vector3)(direction * speed * Time.deltaTime);

        // Hit 애니메이션 재생 중 — 끝나면 파괴
        if (_isHit && anim != null)
        {
            var state = anim.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(hitStateName) && state.normalizedTime >= 1f)
                ForceDestroy();
        }
    }

    // ─── 충돌 ───
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isHit) return;

        bool shouldHit = false;

        if (isPlayerProjectile)
        {
            EnemyBase enemy = other.GetComponentInParent<EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, direction.x > 0f ? knockback : -knockback);
                HitEffectManager.Instance?.SpawnHitEffect(transform.position);
                shouldHit = true;
            }
        }
        else
        {
            PlayerStats player = other.GetComponent<PlayerStats>();
            if (player != null)
            {
                player.TakeDamage(damage, direction);
                shouldHit = true;
            }
        }

        // 지형
        if (!shouldHit && other.gameObject.layer == LayerMask.NameToLayer("Ground"))
            shouldHit = true;

        if (shouldHit) PlayHitAndDestroy();
    }

    // ─── Hit 연출 후 파괴 ───
    private void PlayHitAndDestroy()
    {
        if (_isHit) return;
        _isHit = true;

        // 이동 정지
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (col != null) col.enabled = false;

        // Hit 애니메이터 State 재생
        if (anim != null && !string.IsNullOrEmpty(hitStateName))
        {
            anim.Play(hitStateName, 0, 0f);
            // Update()에서 종료 감지 → ForceDestroy 호출
        }
        else
        {
            ForceDestroy();
        }
    }

    private void ForceDestroy() => Destroy(gameObject);
}
