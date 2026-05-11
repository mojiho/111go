using System.Collections;
using UnityEngine;

public enum PlayerState { Idle, Run, Jump, Fall, Dash, Attack, Skill1, Skill2, Dead }

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  조작 요약 (키보드 / Xbox 패드)
//  이동       A·D / Left Stick
//  점프       Space / A버튼
//  대시       Shift / B버튼
//  공격       J / X버튼
//  스킬1      K / Y버튼
//  스킬2      L / RB
//  슬로우모션  Tab (홀드) / LB (홀드)
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

    private Rigidbody2D rb;
    private Animator anim;
    private PlayerCombat combat;
    private PlayerStats stats;

    private float moveInput;
    private int jumpCount;
    private bool isDashing;
    private bool canDash = true;

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
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (IsGrounded && !wasGrounded)
            jumpCount = 0;
    }

    private void HandleMovementInput()
    {
        if (isDashing || combat.IsLocked) return;

        // 키보드 + Left Stick 동시 지원
        moveInput = Input.GetAxisRaw("Horizontal");

        // 이동 방향으로 페이싱 (마지막 방향 유지)
        if (moveInput > 0.1f) SetFacing(1);
        else if (moveInput < -0.1f) SetFacing(-1);

        // 점프: Space / Gamepad "Jump" (Input Manager에서 A버튼 매핑)
        if (Input.GetButtonDown("Jump") && jumpCount < maxJumpCount)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpCount++;
            anim?.SetTrigger("Jump");
        }

        // 대시: Shift / Gamepad Button 1 (B버튼) — Input Manager "Dash" 축 추가 권장
        if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)
             || Input.GetKeyDown(KeyCode.JoystickButton1)) && canDash)
        {
            StartCoroutine(DoDash());
        }

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

        // 공격: J / Gamepad Button 2 (X버튼)
        if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.JoystickButton2))
            combat.TryAttack();

        // 스킬1: K / Gamepad Button 3 (Y버튼)
        if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.JoystickButton3))
            combat.TrySkill1();

        // 스킬2: L / Gamepad Button 5 (RB)
        if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.JoystickButton5))
            combat.TrySkill2();
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

    public void Die()
    {
        if (State == PlayerState.Dead) return;
        SetState(PlayerState.Dead);
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 1f;
        anim?.SetTrigger("Die");
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
        anim.SetFloat("MoveSpeed", Mathf.Abs(rb.linearVelocity.x));
        anim.SetBool("IsGrounded", IsGrounded);
        anim.SetFloat("VelocityY", rb.linearVelocity.y);
        anim.SetBool("IsDashing", isDashing);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
