using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerState { Idle, Run, Jump, Fall, Dash, Attack, Skill1, Skill2, Ultimate, Parry, Dead }

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

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.18f;
    public LayerMask groundLayer;

    public PlayerState State { get; private set; }
    public int FacingDirection { get; private set; } = 1;
    public bool IsGrounded { get; private set; }

    private const string ANIM_IDLE       = "Idle";
    private const string ANIM_RUN        = "Run";
    private const string ANIM_JUMP       = "jump";
    private const string ANIM_FALL       = "Fall";
    private const string ANIM_DASH       = "Dash";
    private const string ANIM_ATTACK     = "Attack";
    private const string ANIM_DASHATTACK = "Dash-Attack";
    private const string ANIM_HURT       = "Hurt";
    private const string ANIM_DEATH      = "Death";

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
        if (Keyboard.current == null) return;
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

        var kb = Keyboard.current;

        // ←→ 방향키 이동
        float left  = kb.leftArrowKey.isPressed  ? -1f : 0f;
        float right = kb.rightArrowKey.isPressed ?  1f : 0f;
        moveInput = left + right;

        if (moveInput > 0.1f)       SetFacing(1);
        else if (moveInput < -0.1f) SetFacing(-1);

        // 점프: ↑ 방향키
        if (kb.upArrowKey.wasPressedThisFrame && jumpCount < maxJumpCount)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpCount++;
        }

        // 대시: Left Ctrl
        if (kb.leftCtrlKey.wasPressedThisFrame && canDash)
            StartCoroutine(DoDash());

        // State 갱신
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

        var kb = Keyboard.current;
        if (kb.zKey.wasPressedThisFrame) combat.TryAttack();
        if (kb.xKey.wasPressedThisFrame) combat.TrySkill1();
        if (kb.cKey.wasPressedThisFrame) combat.TrySkill2();
        if (kb.vKey.wasPressedThisFrame) combat.TryUltimate();
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

    public void PlayAttackAnim()  => anim?.Play(ANIM_ATTACK,     0, 0f);
    public void PlaySkill1Anim()  => anim?.Play(ANIM_DASHATTACK, 0, 0f);
    public void PlaySkill2Anim()  => anim?.Play(ANIM_ATTACK,     0, 0f);
    public void PlayHurtAnim()    => anim?.Play(ANIM_HURT,       0, 0f);

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
