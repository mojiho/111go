using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerState { Idle, Run, Jump, Fall, Dash, Attack, Skill1, Skill2, Ultimate, Parry, Crouch, Hurt, Dead }

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  조작키 (New Input System)
//  이동       ←→ 방향키
//  점프       ↑ 방향키
//  대시       Left Ctrl
//  공격       Z
//  스킬1      X
//  스킬2      C
//  필살기     V
//  패링       Left/Right Shift
//  슬로우모션  Tab (홀드)
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
[RequireComponent(typeof(Rigidbody2D), typeof(PlayerCombat), typeof(PlayerStats))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 9f;
    public float jumpForce = 18f;
    public int maxJumpCount = 2;

    [Header("Dash")]
    public float dashSpeed = 22f;
    public float dashDuration = 0.14f;
    public float dashCooldown = 0.7f;

    [Header("ShockWave")]
    public GameObject shockWavePrefab;
    public float shockWaveBehindOffset = 0.4f;   // 대쉬/대쉬어택 — 뒤쪽 X 오프셋
    public float shockWaveBelowOffset  = 0.3f;   // 점프 — 아래쪽 Y 오프셋

    [Header("Jump Effect")]
    public GameObject jumpEffectPrefab;           // 지상 점프 시 발 아래 이펙트
    public float jumpEffectYOffset = -0.2f;       // 발 기준 Y 오프셋

    [Header("Jump Feel")]
    public float fallMultiplier = 2.5f;      // 떨어질 때 중력 배수
    public float lowJumpMultiplier = 2f;     // 점프키 짧게 눌렀을 때 상승 컷
    public float maxFallSpeed = 25f;         // 종단속도 (너무 빨리 안 떨어지게)

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.18f;
    public LayerMask groundLayer;

    [Header("Crouch")]
    public float crouchColliderScaleY = 0.5f;   // 콜라이더 Y 사이즈 배수 (작을수록 더 낮음)
    public float crouchMoveSpeedMul = 0.4f;     // 웅크린 상태 이동속도 배수

    public PlayerState State { get; private set; }
    public int FacingDirection { get; private set; } = 1;
    public bool IsGrounded { get; private set; }

    private const string ANIM_IDLE       = "Idle";
    private const string ANIM_RUN        = "Run";
    private const string ANIM_JUMP       = "jump";
    private const string ANIM_FALL       = "Fall";
    private const string ANIM_DASH       = "Dash";
    private const string ANIM_ATTACK     = "Attack";
    private const string ANIM_ATTACK2    = "Attack2";
    private const string ANIM_DASHATTACK = "Dash-Attack";
    private const string ANIM_HURT       = "Hurt";
    private const string ANIM_DEATH      = "Death";
    private const string ANIM_CROUCH     = "Croush";
    private const string ANIM_SLIDE      = "Slide";

    private Rigidbody2D rb;
    private Animator anim;
    private PlayerCombat combat;
    private PlayerStats stats;
    private CapsuleCollider2D capsule;

    private float moveInput;
    private int jumpCount;
    private bool isDashing;
    private bool canDash = true;
    private float dashCoolTimer = 0f;
    public float DashCooldownRatio => dashCooldown > 0f ? Mathf.Clamp01(dashCoolTimer / dashCooldown) : 0f;
    private bool isCrouching;
    private Vector2 capsuleOriginalSize;
    private Vector2 capsuleOriginalOffset;
    private PlayerState prevAnimState;
    public bool IsCrouching => isCrouching;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        combat = GetComponent<PlayerCombat>();
        stats = GetComponent<PlayerStats>();
        capsule = GetComponent<CapsuleCollider2D>();
        if (capsule != null)
        {
            capsuleOriginalSize = capsule.size;
            capsuleOriginalOffset = capsule.offset;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (State == PlayerState.Dead) return;

        CheckGround();
        HandleMovementInput();
        HandleCombatInput();
        UpdateAnimator();
        UpdateDashCooldown();
    }

    private void FixedUpdate()
    {
        if (State == PlayerState.Dead || isDashing) return;
        if (combat.IsLocked) return;

        float speed = isCrouching ? moveSpeed * crouchMoveSpeedMul : moveSpeed;
        rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);

        ApplyBetterJumpGravity();
    }

    private void ApplyBetterJumpGravity()
    {
        // 낙하 중: 중력 강화
        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
        // 상승 중인데 점프키 안 누르고 있으면 짧은 점프
        else if (rb.linearVelocity.y > 0f && !Keyboard.current.upArrowKey.isPressed)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }

        // 종단속도 클램프
        if (rb.linearVelocity.y < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
    }

    private void CheckGround()
    {
        bool wasGrounded = IsGrounded;
        IsGrounded = groundCheck != null
            && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (IsGrounded && !wasGrounded)
            jumpCount = 0;
    }

    private void HandleMovementInput()
    {
        if (isDashing || combat.IsLocked)
        {
            moveInput = 0f;
            return;
        }

        var kb = Keyboard.current;

        // ←→ 방향키 이동
        float left  = kb.leftArrowKey.isPressed  ? -1f : 0f;
        float right = kb.rightArrowKey.isPressed ?  1f : 0f;
        moveInput = left + right;

        if (moveInput > 0.1f)       SetFacing(1);
        else if (moveInput < -0.1f) SetFacing(-1);

        // 웅크리기: ↓ 방향키 (홀드, 땅 위에서만)
        bool wantCrouch = kb.downArrowKey.isPressed && IsGrounded;
        if (wantCrouch && !isCrouching) SetCrouch(true);
        else if (!wantCrouch && isCrouching) SetCrouch(false);

        // 점프: ↑ 방향키 (웅크린 상태에선 점프 불가)
        if (!isCrouching && kb.upArrowKey.wasPressedThisFrame && jumpCount < maxJumpCount)
        {
            bool isGroundJump = jumpCount == 0;   // 0 = 지상 첫 점프, 1 = 이단점프
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpCount++;
            if (isGroundJump)
            {
                SpawnJumpEffect();    // 지상 점프: Jump 이펙트만
            }
            else
            {
                SpawnShockWaveBelow();  // 이단점프: ShockWave만
            }
        }

        // 대시: Left Ctrl (웅크려도 대시는 가능 — 대시 시 자동 해제)
        if (kb.leftCtrlKey.wasPressedThisFrame && canDash)
        {
            if (isCrouching) SetCrouch(false);
            StartCoroutine(DoDash());
        }

        // State 갱신 — Hurt 중엔 HurtRecoverRoutine이 복귀 담당
        if (!isDashing && !combat.IsLocked && State != PlayerState.Hurt)
        {
            if (!IsGrounded)
                SetState(rb.linearVelocity.y > 0.1f ? PlayerState.Jump : PlayerState.Fall);
            else if (isCrouching)
                SetState(PlayerState.Crouch);
            else if (Mathf.Abs(moveInput) > 0.05f)
                SetState(PlayerState.Run);
            else
                SetState(PlayerState.Idle);
        }
    }

    private void SetCrouch(bool on)
    {
        if (isCrouching == on) return;
        isCrouching = on;

        if (capsule == null) return;

        if (on)
        {
            // 발 위치(bottom) 유지하면서 위쪽만 줄임
            float newSizeY = capsuleOriginalSize.y * crouchColliderScaleY;
            float bottom = capsuleOriginalOffset.y - capsuleOriginalSize.y * 0.5f;
            float newOffsetY = bottom + newSizeY * 0.5f;
            capsule.size = new Vector2(capsuleOriginalSize.x, newSizeY);
            capsule.offset = new Vector2(capsuleOriginalOffset.x, newOffsetY);
        }
        else
        {
            capsule.size = capsuleOriginalSize;
            capsule.offset = capsuleOriginalOffset;
        }
    }

    private void HandleCombatInput()
    {
        if (isDashing) return;

        var kb = Keyboard.current;
        bool attackPressed = kb.zKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame
                          || kb.cKey.wasPressedThisFrame || kb.vKey.wasPressedThisFrame;
        if (attackPressed && isCrouching) SetCrouch(false);

        if (kb.zKey.wasPressedThisFrame) combat.TryAttack();
        if (kb.xKey.wasPressedThisFrame) combat.TrySkill1();
        if (kb.cKey.wasPressedThisFrame) combat.TrySkill2();
        if (kb.vKey.wasPressedThisFrame) combat.TryUltimate();
    }

    private IEnumerator DoDash()
    {
        canDash = false;
        isDashing = true;
        dashCoolTimer = dashCooldown;   // 사용 즉시 쿨타임 시작
        bool dashGrounded = IsGrounded;
        SetState(PlayerState.Dash);

        float dir = moveInput != 0 ? Mathf.Sign(moveInput) : FacingDirection;
        SetFacing((int)dir);
        rb.gravityScale = 0f;

        // 지상 = Slide, 공중 = Dash 애니메이션 직접 재생
        anim?.Play(dashGrounded ? ANIM_SLIDE : ANIM_DASH, 0, 0f);
        prevAnimState = PlayerState.Dash;

        stats.SetInvincible(dashDuration + 0.05f);
        HitEffectManager.Instance?.TriggerDashEffect(transform.position, dir);
        SpawnShockWaveBehind();

        // 진행 중 매 프레임 Y 강제 — 단차/경사면에 의한 떠오름 방지
        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            if (dashGrounded)
                rb.linearVelocity = new Vector2(dir * dashSpeed, -2f);  // 지상은 강하게 누름
            else
                rb.linearVelocity = new Vector2(dir * dashSpeed, rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.gravityScale = 3f;
        // 종료 시 Y=0이면 중력 가속까지 한 프레임 떠 보임 → 즉시 음수 Y 부여
        float endY = dashGrounded ? -5f : rb.linearVelocity.y;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.2f, endY);
        isDashing = false;
        SetState(PlayerState.Idle);   // 대시 끝난 직후 명시적으로 Idle 세팅 → Run이 덮지 못하게

        // 쿨타임은 Update에서 감소 (대쉬 종료 후 자동으로 canDash 복구)
    }

    private void UpdateDashCooldown()
    {
        if (dashCoolTimer <= 0f) return;
        dashCoolTimer -= Time.unscaledDeltaTime;
        if (dashCoolTimer <= 0f)
        {
            dashCoolTimer = 0f;
            canDash = true;
        }
    }

    // ───── ShockWave 스폰 ─────

    // 대쉬 / 대쉬어택 — 진행 방향 반대쪽 (뒤)
    public void SpawnShockWaveBehind()
    {
        if (shockWavePrefab == null) return;
        Vector3 pos = transform.position
            + new Vector3(-FacingDirection * shockWaveBehindOffset, 0f, 0f);
        GameObject fx = FXPool.Spawn(shockWavePrefab, pos, Quaternion.identity);
        if (fx != null)
        {
            SpriteRenderer sr = fx.GetComponent<SpriteRenderer>();
            if (sr != null) sr.flipX = FacingDirection > 0;
        }
    }

    // 지상 점프 이펙트 — 발 아래 정방향 스폰
    private void SpawnJumpEffect()
    {
        if (jumpEffectPrefab == null) return;
        Vector3 pos = transform.position + new Vector3(0f, jumpEffectYOffset, 0f);
        FXPool.Spawn(jumpEffectPrefab, pos, Quaternion.identity);
    }

    // 점프 / 이단점프 — 발 아래 (90도 회전)
    public void SpawnShockWaveBelow()
    {
        if (shockWavePrefab == null) return;
        Vector3 pos = transform.position
            + new Vector3(0f, -shockWaveBelowOffset, 0f);
        GameObject fx = FXPool.Spawn(shockWavePrefab, pos, Quaternion.Euler(0f, 0f, -90f));
        if (fx != null)
        {
            SpriteRenderer sr = fx.GetComponent<SpriteRenderer>();
            if (sr != null) sr.flipY = FacingDirection < 0;
        }
    }

    public void SetFacing(int dir)
    {
        if (dir == 0) return;
        if (FacingDirection == dir) return;
        FacingDirection = dir;
        // 클립에 X=5 구워넣었으므로 반전은 -5로
        Vector3 s = transform.localScale;
        transform.localScale = new Vector3(dir * Mathf.Abs(s.x), s.y, s.z);
    }

    public void SetState(PlayerState newState)
    {
        if (State == newState) return;
        State = newState;
    }

    // comboIndex 짝수(0,2..) → Attack, 홀수(1) → Attack2 — 두 모션 번갈아 사용
    public void PlayAttackAnim(int comboIndex = 0)
    {
        string clip = (comboIndex % 2 == 0) ? ANIM_ATTACK : ANIM_ATTACK2;
        anim?.Play(clip, 0, 0f);
        prevAnimState = PlayerState.Attack;   // 종료 후 Idle 재생을 위해
    }
    public void PlaySkill1Anim()
    {
        anim?.Play(ANIM_DASHATTACK, 0, 0f);
        prevAnimState = PlayerState.Skill1;
    }

    // 필살기 연출용 — Dash-Attack 포즈 고정 / 해제
    public void FreezeAtDashAttackPose(float normalizedTime = 0.35f)
    {
        if (anim == null) return;
        anim.Play(ANIM_DASHATTACK, 0, normalizedTime);
        anim.Update(0f);
        anim.speed = 0f;
        prevAnimState = PlayerState.Skill1;
    }

    public void UnfreezeAnimator()
    {
        if (anim != null) anim.speed = 1f;
    }
    public void PlaySkill2Anim()
    {
        anim?.Play(ANIM_ATTACK2, 0, 0f);   // 스킬2는 Attack2로 차별화
        prevAnimState = PlayerState.Skill2;
    }
    private bool hurtPending;

    public void PlayHurtAnim()
    {
        // 대시/스킬/공격 중이더라도 피격이 우위 — 강제 중단
        isDashing = false;
        combat.ForceUnlock();
        rb.gravityScale = 3f;

        hurtPending = true;
        SetState(PlayerState.Hurt);
        StopCoroutine(nameof(HurtRecoverRoutine));
        StartCoroutine(nameof(HurtRecoverRoutine));
    }

    private IEnumerator HurtRecoverRoutine()
    {
        yield return new WaitForSeconds(0.3f);
        if (State == PlayerState.Hurt)
            SetState(PlayerState.Idle);
    }

    public void Die()
    {
        if (State == PlayerState.Dead) return;
        SetState(PlayerState.Dead);
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 1f;
        anim?.Play(ANIM_DEATH, 0, 0f);
        StartCoroutine(DelayGameOver());
    }

    private IEnumerator DelayGameOver()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        GameManager.Instance?.TriggerGameOver();
    }

    private void UpdateAnimator()
    {
        if (anim == null) return;

        // Hurt — 플래그 방식으로 Update 내에서 즉시 재생 (FixedUpdate에서 호출 시 딜레이 방지)
        if (hurtPending)
        {
            hurtPending = false;
            prevAnimState = PlayerState.Hurt;
            anim.Play(ANIM_HURT, 0, 0f);
            return;
        }

        if (State == PlayerState.Hurt) return;

        if (combat.IsLocked) return;

        string target = State switch
        {
            PlayerState.Idle   => ANIM_IDLE,
            PlayerState.Run    => ANIM_RUN,
            PlayerState.Jump   => ANIM_JUMP,
            PlayerState.Fall   => ANIM_FALL,
            PlayerState.Dash   => ANIM_SLIDE,
            PlayerState.Crouch => ANIM_CROUCH,
            _                  => ANIM_IDLE,
        };

        if (prevAnimState != State)
        {
            prevAnimState = State;
            anim.Play(target, 0, 0f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
