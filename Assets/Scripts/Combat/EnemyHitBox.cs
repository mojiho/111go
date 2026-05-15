using UnityEngine;

// 적의 근접 공격 판정박스. Collider2D는 Inspector에서 연결.
// HitBox(플레이어→적)와 구분하여 EnemyHitBox(적→플레이어)로 분리.
[RequireComponent(typeof(Collider2D))]
public class EnemyHitBox : MonoBehaviour
{
    private float damage;
    private Collider2D col;
    private bool activated;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        col.enabled = false;
    }

    public void Activate(float dmg)
    {
        damage = dmg;
        activated = true;
        col.enabled = true;
    }

    public void Deactivate()
    {
        activated = false;
        col.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!activated) return;

        PlayerStats ps = other.GetComponent<PlayerStats>();
        if (ps != null)
        {
            // 적 본체 위치(이 히트박스의 부모 EnemyBase) → 플레이어 방향
            // hitbox 자체 위치를 쓰면 hitbox가 플레이어를 지나쳐있을 때 부호 반전 → 팝업/넉백 방향 뒤집힘
            EnemyBase enemy = GetComponentInParent<EnemyBase>();
            Vector2 attackerPos = enemy != null
                ? enemy.BodyPosition
                : (Vector2)transform.position;
            Vector2 hitDir = (Vector2)other.transform.position - attackerPos;
            ps.TakeDamage(damage, hitDir);
        }
    }
}
