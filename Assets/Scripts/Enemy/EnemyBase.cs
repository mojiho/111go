using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("HP Bar")]
    public Slider hpSlider;                 // 자식 Canvas 아래 Slider 드래그
    public bool hideHpBarWhenFull = true;   // 풀피일 땐 숨김
    public CanvasGroup hpBarCanvasGroup;    // (선택) 페이드용
    public Transform hpBarFollow;           // 위치를 따라갈 Transform — 보통 Canvas (또는 Slider) 드래그
    public Vector2 hpBarOffset = new Vector2(0f, 1.2f);  // centerPoint 기준 오프셋

    [Header("Animation State Names")]
    public string animIdle   = "Idle";
    public string animWalk   = "Walk";
    public string animAttack = "Attack";
    public string animHurt   = "Hurt";
    public string animDeath  = "Death";

    [Header("Freeze Pose (필살기 연출용)")]
    [Tooltip("Freeze 시 재생할 Animator State 이름 (비워두면 animHurt 사용)")]
    public string frozenHurtStateName = "";
    [Range(0f, 1f)] public float frozenHurtNormalizedTime = 0.3f;

    [Header("Hit Stun")]
    [Tooltip("피격 후 짧게 아무것도 못하는 시간 (행동 불가)")]
    public float postHitStunDuration = 0.05f;
    private float _hitStunTimer;
    public bool IsHitStunned => _hitStunTimer > 0f;

    public EnemyState State { get; protected set; }

    // 필살기 연출 — freeze 상태
    private bool _isFrozen;
    private Vector2 _frozenVel;
    private float _frozenAnimSpeed = 1f;
    private float _frozenGravity;
    public bool IsFrozen => _isFrozen;

    public void SetFrozen(bool freeze)
    {
        if (_isFrozen == freeze) return;
        _isFrozen = freeze;
        if (freeze)
        {
            _frozenVel = rb.linearVelocity;
            _frozenAnimSpeed = anim != null ? anim.speed : 1f;
            _frozenGravity = rb.gravityScale;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                string freezeState = string.IsNullOrEmpty(frozenHurtStateName)
                    ? animHurt : frozenHurtStateName;
                anim.Play(freezeState, 0, frozenHurtNormalizedTime);
                anim.Update(0f);
                anim.speed = 0f;
            }
        }
        else
        {
            rb.linearVelocity = _frozenVel;
            rb.gravityScale = _frozenGravity;
            if (anim != null) anim.speed = _frozenAnimSpeed;
        }
    }

    // 모든 적 freeze (필살기 연출용)
    public static void FreezeAll(bool freeze)
    {
        foreach (var e in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            e.SetFrozen(freeze);
    }

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

        // 씬에 미리 배치된 적의 초기 스프라이트 방향을 lastFaceDir에 동기화
        // → 첫 이동 시 ApplyFacing이 동일 방향 판단으로 skip되는 현상 방지
        if (sr != null)
            lastFaceDir = spriteDefaultRight ? (sr.flipX ? -1f : 1f)
                                             : (sr.flipX ?  1f : -1f);

        // HP 슬라이더 초기화
        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = 1f;
            hpSlider.wholeNumbers = false;
            hpSlider.interactable = false;
            hpSlider.value = 1f;
        }
        UpdateHpBarVisibility();

        // 적끼리는 레이어 단위로 무시해도 OK (적-적 트리거 없음)
        int enemyLayer  = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);

        // ⚠️ 플레이어-적 레이어 무시는 사용하지 않음
        //   → EnemyHitBox(트리거)의 OnTriggerEnter2D 까지 막혀서 적이 데미지를 못 줌
        //   → 대신 본체 콜라이더(non-trigger)끼리만 IgnoreCollision으로 통과 처리
        if (mainCol != null && playerObj != null)
        {
            foreach (var pc in playerObj.GetComponentsInChildren<Collider2D>())
            {
                if (pc.isTrigger) continue;   // 플레이어쪽 트리거(있다면)는 건드리지 않음
                Physics2D.IgnoreCollision(mainCol, pc, true);
            }
        }
    }

    protected virtual void Update()
    {
        if (isDead) return;
        if (_isFrozen) return;   // 필살기 연출 중 정지

        if (_hitStunTimer > 0f)
        {
            _hitStunTimer -= Time.deltaTime;
            // Stun 중엔 AI/이동 결정만 skip — velocity는 그대로 두어 넉백 유지
            return;
        }

        UpdateFacing();
        UpdateStateMachine();
    }

    protected virtual void LateUpdate()
    {
        // HP바를 centerPoint(또는 root) + offset 위치에 강제 정렬
        // — 부모 자식 관계와 무관하게 절대 좌표로 위치 고정 → flip 시 따라 흔들리지 않음
        if (hpBarFollow != null)
        {
            Vector3 basePos = centerPoint != null ? centerPoint.position : transform.position;
            hpBarFollow.position = new Vector3(basePos.x + hpBarOffset.x, basePos.y + hpBarOffset.y, basePos.z);
        }
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
        ApplyFacing(dir);   // 이동 시작 즉시 스프라이트 방향 반영 (velocity 가속 대기 불필요)
        float targetX = dir * speed;
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
        if (sr == null) return;
        // Chase·Attack 상태에서만 매 프레임 플레이어 방향으로 갱신
        // Idle 이동 방향은 MoveToward() 호출 시점에 ApplyFacing()으로 즉시 처리
        if ((State == EnemyState.Chase || State == EnemyState.Attack) && player != null)
        {
            float xDelta = player.position.x - BodyPosition.x;
            if (Mathf.Abs(xDelta) >= 0.1f)
                ApplyFacing(xDelta > 0f ? 1f : -1f);
        }
    }

    // 방향 적용 공통 메서드 — MoveToward / UpdateFacing 양쪽에서 호출
    protected void ApplyFacing(float dir)
    {
        if (sr == null) return;
        if (centerPoint != null && !centerInit)
        {
            centerBaseX = Mathf.Abs(centerPoint.localPosition.x);
            centerInit = true;
        }

        if (dir == lastFaceDir) return;
        lastFaceDir = dir;

        bool flipped = spriteDefaultRight ? (dir < 0f) : (dir > 0f);

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

        // 3) 콜라이더 오프셋도 부호 반전
        if (mainCol != null)
        {
            mainCol.offset = new Vector2(
                flipped ? -Mathf.Abs(colBaseOffset.x) : Mathf.Abs(colBaseOffset.x),
                colBaseOffset.y
            );
        }

        // 4) 루트 위치 보정 → Center 월드 위치를 flip 전과 동일하게 유지
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
        UpdateHpBar();
        _hitStunTimer = postHitStunDuration;   // 매 피격마다 짧은 stun

        // 데미지 팝업 — 넉백 방향을 direction으로 사용
        Vector3 popupDir = new Vector3(knockbackX, 0f, 0f);
        DamagePopupManager.Instance?.ShowPopup(Mathf.RoundToInt(damage), BodyPosition + Vector2.up * 0.5f, popupDir);

        float actualKnockback = knockbackX * (1f - knockbackResistance);
        rb.linearVelocity = new Vector2(actualKnockback, rb.linearVelocity.y + 3f);

        if (currentHp <= 0f)
            Die();
        else
        {
            StopCoroutine(nameof(HurtRoutine));   // 중첩 방지
            StartCoroutine(nameof(HurtRoutine));
        }
    }

    protected virtual IEnumerator HurtRoutine()
    {
        State = EnemyState.Hurt;
        anim?.Play(animHurt, 0, 0f);

        yield return new WaitForSeconds(0.25f);

        if (!isDead) SetState(EnemyState.Chase);
    }

    protected virtual void Die()
    {
        isDead = true;
        State = EnemyState.Dead;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;       // 그 자리에서 멈춤
        rb.bodyType = RigidbodyType2D.Kinematic;  // 외부 힘도 무시

        anim?.Play(animDeath, 0, 0f);
        if (mainCol != null) mainCol.enabled = false;

        if (hpSlider != null) hpSlider.gameObject.SetActive(false);

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
            case EnemyState.Idle:   anim?.Play(animIdle,   0, 0f); break;
            case EnemyState.Chase:  anim?.Play(animWalk,   0, 0f); break;
            case EnemyState.Attack: anim?.Play(animAttack, 0, 0f); break;
        }
    }

    protected void UpdateHpBar()
    {
        if (hpSlider != null && maxHp > 0f)
            hpSlider.value = Mathf.Clamp01(currentHp / maxHp);
        UpdateHpBarVisibility();
    }

    protected void UpdateHpBarVisibility()
    {
        if (hpSlider == null) return;
        bool full = currentHp >= maxHp - 0.01f;
        bool show = !(hideHpBarWhenFull && full);
        if (hpBarCanvasGroup != null)
        {
            hpBarCanvasGroup.alpha = show ? 1f : 0f;
        }
        else
        {
            hpSlider.gameObject.SetActive(show);
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
