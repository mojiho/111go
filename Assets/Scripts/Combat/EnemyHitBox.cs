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
            ps.TakeDamage(damage);
    }
}
