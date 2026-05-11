using System;
using System.Collections.Generic;
using UnityEngine;

// 공격 판정 박스. Collider2D는 Inspector에서 연결, 평소엔 비활성
[RequireComponent(typeof(Collider2D))]
public class HitBox : MonoBehaviour
{
    private float damage;
    private float knockbackForce;
    private Action<EnemyBase> onHitCallback;
    private HashSet<EnemyBase> hitEnemies = new HashSet<EnemyBase>();
    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        col.enabled = false;
    }

    public void Activate(float dmg, float knockback, Action<EnemyBase> callback = null)
    {
        damage = dmg;
        knockbackForce = knockback;
        onHitCallback = callback;
        hitEnemies.Clear();
        col.enabled = true;
    }

    public void Deactivate()
    {
        col.enabled = false;
        hitEnemies.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!col.enabled) return;

        EnemyBase enemy = other.GetComponent<EnemyBase>();
        if (enemy == null || hitEnemies.Contains(enemy)) return;

        hitEnemies.Add(enemy);
        enemy.TakeDamage(damage, knockbackForce);
        onHitCallback?.Invoke(enemy);
    }
}
