using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed     = 10f;
    public float damage    = 20f;
    public float lifetime  = 4f;
    public float knockback = 4f;
    public bool  isPlayerProjectile = false;

    [Header("Hit Animation")]
    public string hitStateName = "Hit";   // 비어있으면 즉시 파괴
    public float  hitAnimLife  = 0.35f;   // hit anim 재생 후 파괴까지 시간

    private Vector2     direction;
    private Rigidbody2D rb;
    private Animator    anim;
    private Collider2D  col;
    private SpriteRenderer sr;

    private Vector3 _spawnPos;     // 발사 시점 위치 (Animator가 덮어써도 무시하기 위해)
    private float   _elapsed;      // 발사 후 경과 시간
    private bool _isHit;
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

    private void Awake()
    {
        rb   = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        col  = GetComponent<Collider2D>();
        sr   = GetComponent<SpriteRenderer>();
        if (rb != null) rb.gravityScale = 0f;
    }

    public void Init(Vector2 dir, float dmg = -1f)
    {
        _isHit    = false;
        direction = dir.normalized;
        if (dmg >= 0f) damage = dmg;

        // Animator 클립이 position을 덮어쓰는 환경에서도 발사 위치/이동을 보존하기 위해
        // 스폰 위치를 기억하고, LateUpdate에서 (스폰 + 경과시간*방향*속도)로 절대값 셋
        _spawnPos = transform.position;
        _elapsed  = 0f;

        if (rb != null) rb.linearVelocity = Vector2.zero;

        CancelInvoke(nameof(ForceDestroy));
        Invoke(nameof(ForceDestroy), lifetime);
    }

    // Animator가 transform.position을 덮어쓴 뒤 마지막에 직선 이동값으로 절대 셋
    private void LateUpdate()
    {
        if (_isFrozen || _isHit) return;

        Vector3 prevPos = transform.position;
        _elapsed += Time.deltaTime;
        Vector3 newPos = _spawnPos + (Vector3)(direction * speed * _elapsed);
        transform.position = newPos;

        // sprite 중심에 collider 위치 자동 동기화 — 프레임마다 pivot이 달라도 따라감
        SyncColliderToSprite();

        // ── 터널링 방지 — 두 프레임 사이 직선 구간을 Linecast로 검사 ──
        SweepCheck(prevPos, newPos);
    }

    // sprite renderer의 bounds 중심에 collider offset을 맞춤 — 프레임마다 pivot 달라도 추적
    private void SyncColliderToSprite()
    {
        if (col == null || sr == null || sr.sprite == null) return;
        // sprite의 월드 중심 - transform 위치 = collider offset이 가져야 할 값 (local 기준)
        Vector3 spriteCenterWorld = sr.bounds.center;
        Vector3 localOffset = transform.InverseTransformPoint(spriteCenterWorld);
        col.offset = new Vector2(localOffset.x, localOffset.y);
    }

    private void SweepCheck(Vector3 from, Vector3 to)
    {
        if (_isHit) return;
        // 자기 자신 콜라이더가 결과에 포함되지 않도록 잠시 비활성
        bool wasEnabled = col != null && col.enabled;
        if (col != null) col.enabled = false;

        RaycastHit2D[] hits = Physics2D.LinecastAll(from, to);

        if (col != null) col.enabled = wasEnabled;

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            HandleHit(hit.collider);
            if (_isHit) return;
        }
    }

    private void HandleHit(Collider2D other)
    {
        if (_isHit) return;

        // 플레이어가 지상 대시 중이면 이 프레임 어떤 충돌도 무시
        // (Linecast가 player를 통과한 뒤 ground와 동시 hit하는 케이스 방지)
        if (!isPlayerProjectile && PlayerController.Instance != null && PlayerController.Instance.IsGroundDashing)
            return;

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
            PlayerStats player = other.GetComponentInParent<PlayerStats>();
            if (player != null)
            {
                // 지상 대시 중인 플레이어는 투사체가 통과 (피격/폭발 X)
                PlayerController pc = player.GetComponent<PlayerController>();
                if (pc != null && pc.IsGroundDashing) return;

                player.TakeDamage(damage, direction);
                shouldHit = true;
            }
        }

        if (!shouldHit && other.gameObject.layer == LayerMask.NameToLayer("Ground"))
            shouldHit = true;

        if (shouldHit) PlayHitAndDestroy();
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleHit(other);

    private void PlayHitAndDestroy()
    {
        if (_isHit) return;
        _isHit = true;

        if (rb  != null) rb.linearVelocity = Vector2.zero;
        if (col != null) col.enabled = false;

        if (anim != null && !string.IsNullOrEmpty(hitStateName))
        {
            anim.Play(hitStateName, 0, 0f);
            CancelInvoke(nameof(ForceDestroy));
            Invoke(nameof(ForceDestroy), hitAnimLife);
        }
        else
        {
            ForceDestroy();
        }
    }

    private void ForceDestroy() => Destroy(gameObject);
}
