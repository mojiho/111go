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
    private float shakeDuration;     // 시작 시 지정한 전체 지속시간 (감쇠 보정용)
    private float shakeMagnitude;
    private Vector2 shakeDirection;  // 셰이크 편향 방향 (0벡터면 등방성 랜덤)
    private float shakeDirBias;      // 0=완전 랜덤, 1=완전 방향성

    private Rigidbody2D targetRb;
    private PlayerController playerCtrl;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        SnapToTarget();
    }

    /// <summary>외부에서 target 변경 후 호출 — 새 타겟으로 즉시 카메라 스냅</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        SnapToTarget();
    }

    private void SnapToTarget()
    {
        if (target == null) return;
        targetRb   = target.GetComponent<Rigidbody2D>();
        playerCtrl = target.GetComponent<PlayerController>();

        // 시작 위치 즉시 스냅
        currentLookX = 0f;
        targetLookX  = 0f;
        smoothVelocity = Vector3.zero;
        basePosition = DesiredPosition();
        transform.position = basePosition;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        UpdateLookAhead();
        UpdateShake();       // 셰이크 먼저 — FollowTarget에서 최신 offset 사용
        FollowTarget();
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
            // ★ HitStop(Time.timeScale=0) 중에는 타이머를 멈춰서 셰이크가 묻히지 않게 함
            if (Time.timeScale > 0.01f)
                shakeTimer -= Time.unscaledDeltaTime;

            float t01 = (shakeDuration > 0f)
                ? 1f - Mathf.Clamp01(shakeTimer / shakeDuration)   // 0→1 진행도
                : 1f;
            // 감쇠 — 시작은 강하게, 끝으로 갈수록 약하게
            float decayAmp = Mathf.Pow(1f - t01, 1.4f) * shakeMagnitude;

            Vector2 offset;
            if (shakeDirection.sqrMagnitude > 0.0001f)
            {
                // 방향성: 초기 확 밀림(kick) + 천천히 진동하며 복귀
                // 진동수 18Hz — 눈으로 명확히 보이는 속도
                float elapsed = shakeDuration - shakeTimer;
                float phase = elapsed * 18f;
                // cos 사용 → t=0일 때 1 (즉시 최대 변위), 점차 진동하며 감쇠
                float along = Mathf.Cos(phase);
                float side  = Mathf.Sin(phase * 1.7f) * 0.2f;
                Vector2 perp = new Vector2(-shakeDirection.y, shakeDirection.x);
                offset = shakeDirection * along + perp * side;
            }
            else
            {
                // 등방성: 매 프레임 랜덤 방향
                offset = Random.insideUnitCircle;
            }

            shakeOffset = (Vector3)offset * decayAmp;
        }
        else if (shakeOffset.sqrMagnitude > 0.0001f)
        {
            shakeOffset = Vector3.MoveTowards(shakeOffset, Vector3.zero, shakeDecay * Time.unscaledDeltaTime);
        }
    }

    public void TriggerShake(float duration, float magnitude)
    {
        // 항상 덮어쓰기 — 새 셰이크가 더 약해도 새로 시작 (연타 시 묻히지 않게)
        shakeTimer     = duration;
        shakeDuration  = duration;
        shakeMagnitude = magnitude;
        shakeDirection = Vector2.zero;
        shakeDirBias   = 0f;
    }

    // 방향성 셰이크 — direction 방향으로 편향된 흔들림
    // bias: 0=완전 랜덤, 1=완전 방향성 (권장 0.7)
    public void TriggerDirectionalShake(float duration, float magnitude, Vector2 direction, float bias = 0.7f)
    {
        shakeTimer     = duration;
        shakeDuration  = duration;
        shakeMagnitude = magnitude;
        shakeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
        shakeDirBias   = Mathf.Clamp01(bias);
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
