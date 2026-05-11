using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("Follow")]
    public Transform target;
    public Vector2 offset = new Vector2(0f, 1.5f);
    public float smoothTime = 0.2f;   // 클수록 느긋하게 따라옴
    public float maxSpeed  = 25f;

    [Header("Look Ahead")]
    public float lookAheadDist   = 2.5f;  // 진행 방향 앞쪽 최대 오프셋
    public float lookAheadSmooth = 0.6f;  // 클수록 천천히 이동 (0.5~1.0 권장)

    [Header("Bounds (선택)")]
    public bool useBounds = false;
    public Vector2 minBounds;
    public Vector2 maxBounds;

    [Header("Shake")]
    public float shakeDecay = 8f;

    [Header("Slow-Mo Vignette")]
    public UnityEngine.UI.Image vignetteImage;
    public Color vignetteColor = new Color(0f, 0.5f, 1f, 0.18f);

    // ── 내부 상태 ──
    private Vector3 basePosition;     // 셰이크 제외 순수 카메라 위치
    private Vector3 smoothVelocity;   // SmoothDamp 내부 속도

    private float targetLookX;        // 목표 룩어헤드 X
    private float currentLookX;       // 현재 보간된 룩어헤드 X

    private Vector3 shakeOffset;
    private float shakeTimer;
    private float shakeMagnitude;

    private Rigidbody2D targetRb;
    private PlayerController playerCtrl;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (target == null) return;
        targetRb    = target.GetComponent<Rigidbody2D>();
        playerCtrl  = target.GetComponent<PlayerController>();

        // 시작 위치 즉시 스냅
        currentLookX = 0f;
        targetLookX  = 0f;
        basePosition = DesiredPosition();
        transform.position = basePosition;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        UpdateLookAhead();
        FollowTarget();
        UpdateShake();
    }

    private void UpdateLookAhead()
    {
        // 실제 이동 속도 기준 룩어헤드 — 서 있으면 0, 달리면 최대
        float vx = targetRb != null ? targetRb.linearVelocity.x : 0f;
        float speedRatio = Mathf.Clamp(vx / 9f, -1f, 1f); // 9f = moveSpeed 기준 정규화

        targetLookX = speedRatio * lookAheadDist;

        // 부드럽게 접근 (Lerp — 튀지 않음)
        currentLookX = Mathf.Lerp(currentLookX, targetLookX, Time.unscaledDeltaTime / lookAheadSmooth);
    }

    private Vector3 DesiredPosition()
    {
        return new Vector3(
            target.position.x + offset.x + currentLookX,
            target.position.y + offset.y,
            transform.position.z
        );
    }

    private void FollowTarget()
    {
        Vector3 desired = DesiredPosition();

        basePosition = Vector3.SmoothDamp(
            basePosition,
            desired,
            ref smoothVelocity,
            smoothTime,
            maxSpeed,
            Time.unscaledDeltaTime
        );

        if (useBounds)
        {
            basePosition.x = Mathf.Clamp(basePosition.x, minBounds.x, maxBounds.x);
            basePosition.y = Mathf.Clamp(basePosition.y, minBounds.y, maxBounds.y);
        }

        transform.position = basePosition + shakeOffset;
    }

    private void UpdateShake()
    {
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(shakeTimer / shakeMagnitude);
            shakeOffset = (Vector3)Random.insideUnitCircle * (shakeMagnitude * t);
        }
        else
        {
            shakeOffset = Vector3.MoveTowards(shakeOffset, Vector3.zero, shakeDecay * Time.unscaledDeltaTime);
        }
    }

    public void TriggerShake(float duration, float magnitude)
    {
        if (magnitude > shakeMagnitude || shakeTimer <= 0f)
        {
            shakeTimer   = duration;
            shakeMagnitude = magnitude;
        }
    }

    public void TriggerScreenShake(float duration, float magnitude) => TriggerShake(duration, magnitude);

    public void TriggerSlowMoVignette(bool active)
    {
        if (vignetteImage == null) return;
        StopAllCoroutines();
        StartCoroutine(VignetteRoutine(active));
    }

    private IEnumerator VignetteRoutine(bool fadeIn)
    {
        Color to   = fadeIn ? vignetteColor : new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, 0f);
        Color from = vignetteImage.color;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 4f;
            vignetteImage.color = Color.Lerp(from, to, t);
            yield return null;
        }
        vignetteImage.color = to;
    }
}
