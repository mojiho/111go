using System;
using System.Collections.Generic;
using UnityEngine;

// 공격 판정 박스. Collider2D는 Inspector에서 연결, 평소엔 비활성
// 빠른 이동 중 터널링 방지를 위해 매 프레임 OverlapBox로 보완
[RequireComponent(typeof(Collider2D))]
public class HitBox : MonoBehaviour
{
    [Header("Anti-Tunneling")]
    public LayerMask enemyLayer;        // Inspector에서 Enemy 레이어 설정
    public bool useOverlapCheck = false; // 대쉬어택 히트박스에만 true 설정

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

    private void Update()
    {
        if (!col.enabled || !useOverlapCheck) return;

        // 트리거 누락 보완 — 매 프레임 박스 영역 안 적 직접 검사
        Bounds b = col.bounds;
        Collider2D[] hits = Physics2D.OverlapBoxAll(b.center, b.size, 0f, enemyLayer);
        foreach (var hit in hits)
        {
            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy == null || hitEnemies.Contains(enemy)) continue;

            hitEnemies.Add(enemy);
            enemy.TakeDamage(damage, knockbackForce);
            onHitCallback?.Invoke(enemy);
        }
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
