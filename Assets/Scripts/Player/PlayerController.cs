using System.Collections;
using UnityEngine;

public enum PlayerState { Idle, Run, Jump, Fall, Dash, Attack, Skill1, Skill2, Ultimate, Parry, Dead }

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  조작 요약
//  이동       A·D
//  점프       Space (2단 점프)
//  대시       Shift
//  공격       J (3콤보)
//  스킬1      K (돌진 참격)
//  스킬2      L (범위 회전베기)
//  필살기     R (게이지 80% 필요)
//  패링       Q
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

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.18f;
    public LayerMask groundLayer;

    public PlayerState State { get; private set; }
    public int FacingDirection { get; private set; } = 1;
    public bool IsGrounded { get; private set; }

    // ─── Warrior 애니메이션 State 이름 ───
    // Warrior.controller에 파라미터가 없으므로 Play() 직접 사용
    private const string ANIM_IDLE    = "Idle";
    private const string ANIM_RUN     = "Run";
    private const string ANIM_JUMP    = "jump";
    private const string ANIM_FALL    = "Fall";
    private const string ANIM_DASH    = "Dash";
    private const string ANIM_ATTACK  = "Attack";
    private const string ANIM_DASHATTACK = "Dash-Attack";   // Skill1 돌진 참격
    private const string ANIM_HURT    = "Hurt";
    private const string ANIM_DEATH   = "Death";

    private Rigidbody2D rb;
    private Animator anim;
    private PlayerCombat combat;
    private PlayerStats stats;

    private float moveInput;
    private int jumpCount;
    private bool isDashing;
    private bool canDash = true;
    private PlayerState prevAnimState;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        combat = GetComponent<PlayerCombat>();
        stats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        if (State == PlayerState.Dead) return;

        CheckGround();
        HandleMovementInput();
        HandleCombatInput();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (State == PlayerState.Dead || isDashing) return;
        if (combat.IsLocked) return;

        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
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
        if (isDashing || combat.IsLocked) return;

        moveInput = Input.GetAxisRaw("Horizontal");

        if (moveInput > 0.1f) SetFacing(1);
        else if (moveInput < -0.1f) SetFacing(-1);

        // 점프 (2단)
        if (Input.GetButtonDown("Jump") && jumpCount < maxJumpCount)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpCount++;
        }

        // 대시
        if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)
             || Input.GetKeyDown(KeyCode.JoystickButton1)) && canDash)
        {
            StartCoroutine(DoDash());
        }

        // 이동 State 갱신
        if (!isDashing && !combat.IsLocked)
        {
            if (!IsGrounded)
                SetState(rb.linearVelocity.y > 0.1f ? PlayerState.Jump : PlayerState.Fall);
            else if (Mathf.Abs(moveInput) > 0.05f)
                SetState(PlayerState.Run);
            else
                SetState(PlayerState.Idle);
        }
    }

    private void HandleCombatInput()
    {
        if (isDashing) return;

        if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.JoystickButton2))
            combat.TryAttack();

        if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.JoystickButton3))
            combat.TrySkill1();

        if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.JoystickButton5))
            combat.TrySkill2();

        if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.JoystickButton8))
            combat.TryUltimate();
    }

    private IEnumerator DoDash()
    {
        canDash = false;
        isDashing = true;
        SetState(PlayerState.Dash);

        float dir = moveInput != 0 ? Mathf.Sign(moveInput) : FacingDirection;
        SetFacing((int)dir);
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(dir * dashSpeed, 0f);

        stats.SetInvincible(dashDuration + 0.05f);
        HitEffectManager.Instance?.TriggerDashEffect(transform.position, dir);

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = 3f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.2f, 0f);
        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private void SetFacing(int dir)
    {
        if (FacingDirection == dir) return;
        FacingDirection = dir;
        transform.localScale = new Vector3(dir, 1f, 1f);
    }

    public void SetState(PlayerState newState)
    {
        if (State == newState) return;
        State = newState;
    }

    // ── 외부(Combat)에서 직접 애니메이션 재생 요청 ──
    public void PlayAnim(string stateName, float normalizedTime = 0f)
    {
        anim?.Play(stateName, 0, normalizedTime);
    }

    public void PlayAttackAnim()  => anim?.Play(ANIM_ATTACK, 0, 0f);
    public void PlaySkill1Anim()  => anim?.Play(ANIM_DASHATTACK, 0, 0f);
    public void PlaySkill2Anim()  => anim?.Play(ANIM_ATTACK, 0, 0f);
    public void PlayHurtAnim()    => anim?.Play(ANIM_HURT, 0, 0f);

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

    // ── 매 프레임 이동 State에 맞게 애니메이션 갱신 ──
    private void UpdateAnimator()
    {
        if (anim == null) return;
        // 전투 중(IsLocked)이면 Combat이 직접 애니메이션 제어
        if (combat.IsLocked) return;

        string target = State switch
        {
            PlayerState.Idle => ANIM_IDLE,
            PlayerState.Run  => ANIM_RUN,
            PlayerState.Jump => ANIM_JUMP,
            PlayerState.Fall => ANIM_FALL,
            PlayerState.Dash => ANIM_DASH,
            _                => ANIM_IDLE,
        };

        // 같은 State 반복 Play 방지
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
