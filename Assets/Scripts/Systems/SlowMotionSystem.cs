using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// 슬로우모션 게이지 — Tab 홀드로 발동, 게이지 소진 시 해제
public class SlowMotionSystem : MonoBehaviour
{
    [Header("Settings")]
    public float maxGauge = 100f;
    public float drainRate = 30f;
    public float slowTimeScale = 0.3f;
    public float transitionSpeed = 8f;

    [Header("Audio")]
    public AudioClip activateClip;
    public AudioClip deactivateClip;

    public float CurrentGauge { get; private set; }
    public bool IsActive { get; private set; }
    public float GaugeRatio => CurrentGauge / maxGauge;

    public UnityEvent<float> OnGaugeChanged;
    public UnityEvent OnActivate;
    public UnityEvent OnDeactivate;

    private AudioSource audioSource;
    private float targetTimeScale = 1f;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        CurrentGauge = 0f;
    }

    private void Update()
    {
        HandleInput();
        HandleDrain();
        SmoothTimeScale();
    }

    private void HandleInput()
    {
        if (Keyboard.current == null) return;

        // Tab 홀드로 슬로우모션
        bool held = Keyboard.current.tabKey.isPressed;

        if (held && CurrentGauge > 1f)
            TryActivate();
        else if (!held)
            Deactivate();
    }

    private void HandleDrain()
    {
        if (!IsActive) return;

        CurrentGauge -= drainRate * Time.unscaledDeltaTime;
        OnGaugeChanged?.Invoke(GaugeRatio);

        if (CurrentGauge <= 0f)
        {
            CurrentGauge = 0f;
            Deactivate();
        }
    }

    private void SmoothTimeScale()
    {
        if (Mathf.Abs(Time.timeScale - targetTimeScale) < 0.01f)
        {
            Time.timeScale = targetTimeScale;
            return;
        }
        Time.timeScale = Mathf.Lerp(Time.timeScale, targetTimeScale, transitionSpeed * Time.unscaledDeltaTime);
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }

    public void TryActivate()
    {
        if (IsActive || CurrentGauge <= 1f) return;

        IsActive = true;
        targetTimeScale = slowTimeScale;
        OnActivate?.Invoke();

        if (audioSource != null && activateClip != null)
            audioSource.PlayOneShot(activateClip);

        CameraController.Instance?.TriggerSlowMoVignette(true);
    }

    public void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;
        targetTimeScale = 1f;
        OnDeactivate?.Invoke();

        if (audioSource != null && deactivateClip != null)
            audioSource.PlayOneShot(deactivateClip);

        CameraController.Instance?.TriggerSlowMoVignette(false);
    }

    public void AddGauge(float amount)
    {
        CurrentGauge = Mathf.Min(maxGauge, CurrentGauge + amount);
        OnGaugeChanged?.Invoke(GaugeRatio);
    }

    public void ConsumeGauge(float amount)
    {
        CurrentGauge = Mathf.Max(0f, CurrentGauge - amount);
        OnGaugeChanged?.Invoke(GaugeRatio);

        if (IsActive && CurrentGauge <= 0f)
            Deactivate();
    }

    private void OnApplicationQuit()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
}
