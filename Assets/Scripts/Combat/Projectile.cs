using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float damage = 20f;
    public float lifetime = 4f;
    public bool isPlayerProjectile = false;
    public GameObject hitEffectPrefab;

    private Vector2 direction;
    private Rigidbody2D rb;

    private bool _isFrozen;
    private Vector2 _frozenVel;
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
            if (rb != null) rb.linearVelocity = _frozenVel;
            else if (direction != Vector2.zero) { /* rb-less는 Update 멈춰있다가 자연 재개 */ }
        }
    }

    public static void FreezeAll(bool freeze)
    {
        foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            p.SetFrozen(freeze);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.gravityScale = 0f;
    }

    public void Init(Vector2 dir, float dmg = -1f)
    {
        direction = dir.normalized;
        if (dmg >= 0f) damage = dmg;

        if (rb != null)
            rb.linearVelocity = direction * speed;
        else
            transform.right = direction;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (_isFrozen) return;
        if (rb == null)
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isPlayerProjectile)
        {
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, direction.x > 0 ? 3f : -3f);
                HitEffectManager.Instance?.SpawnHitEffect(transform.position);
                Destroy(gameObject);
            }
        }
        else
        {
            PlayerStats player = other.GetComponent<PlayerStats>();
            if (player != null)
            {
                // 발사체 진행 방향으로 셰이크
                player.TakeDamage(damage, direction);
                if (hitEffectPrefab != null)
                    Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
                Destroy(gameObject);
            }
        }

        // 지형에 맞으면 제거
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
            Destroy(gameObject);
    }
}
