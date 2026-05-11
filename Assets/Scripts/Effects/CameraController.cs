using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("Follow")]
    public Transform target;
    public float smoothSpeed = 8f;
    public Vector2 offset = new Vector2(0f, 1.5f);
    public Vector2 deadZone = new Vector2(0.5f, 0.3f);

    [Header("Bounds (선택)")]
    public bool useBounds = false;
    public Vector2 minBounds;
    public Vector2 maxBounds;

    [Header("Shake")]
    public float shakeDecay = 5f;

    [Header("Slow-Mo Vignette")]
    public UnityEngine.UI.Image vignetteImage;
    public Color vignetteColor = new Color(0f, 0.5f, 1f, 0.18f);
    public float vignetteFadeSpeed = 3f;

    private Vector3 shakeOffset;
    private float shakeTimer;
    private float shakeMagnitude;
    private Camera cam;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        cam = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        FollowTarget();
        ApplyShake();
    }

    private void FollowTarget()
    {
        Vector3 desired = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z
        );

        // 데드존 적용
        Vector3 delta = desired - transform.position;
        if (Mathf.Abs(delta.x) < deadZone.x) delta.x = 0f;
        if (Mathf.Abs(delta.y) < deadZone.y) delta.y = 0f;

        Vector3 next = transform.position + delta;

        if (useBounds)
        {
            next.x = Mathf.Clamp(next.x, minBounds.x, maxBounds.x);
            next.y = Mathf.Clamp(next.y, minBounds.y, maxBounds.y);
        }

        transform.position = Vector3.Lerp(transform.position, next, smoothSpeed * Time.unscaledDeltaTime);
    }

    private void ApplyShake()
    {
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            float magnitude = shakeMagnitude * (shakeTimer / shakeMagnitude);
            shakeOffset = Random.insideUnitSphere * magnitude;
            shakeOffset.z = 0f;
        }
        else
        {
            shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, shakeDecay * Time.unscaledDeltaTime);
        }

        transform.position += shakeOffset;
    }

    public void TriggerShake(float duration, float magnitude)
    {
        // 현재 셰이크보다 강하면 덮어씀
        if (magnitude > shakeMagnitude || shakeTimer <= 0f)
        {
            shakeTimer = duration;
            shakeMagnitude = magnitude;
        }
    }

    public void TriggerSlowMoVignette(bool active)
    {
        if (vignetteImage == null) return;
        StopCoroutine(nameof(VignetteRoutine));
        StartCoroutine(VignetteRoutine(active));
    }

    private IEnumerator VignetteRoutine(bool fadeIn)
    {
        Color targetColor = fadeIn ? vignetteColor : new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, 0f);
        Color startColor = vignetteImage.color;
        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            vignetteImage.color = Color.Lerp(startColor, targetColor, elapsed / duration);
            yield return null;
        }
        vignetteImage.color = targetColor;
    }
}
