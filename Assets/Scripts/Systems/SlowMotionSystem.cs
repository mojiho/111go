using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// 슬로우모션 게이지 — Tab 홀드 / LB 홀드로 발동, 게이지 소진 시 해제
public class SlowMotionSystem : MonoBehaviour
{
    [Header("Settings")]
    public float maxGauge = 100f;
    public float drainRate = 30f;           // 초당 소모량
    public float slowTimeScale = 0.3f;
    public float transitionSpeed = 8f;      // timeScale 전환 속도

    [Header("Audio")]
    public AudioClip activateClip;
    public AudioClip deactivateClip;

    public float CurrentGauge { get; private set; }
    public bool IsActive { get; private set; }
    public float GaugeRatio => CurrentGauge / maxGauge;

    public UnityEvent<float> OnGaugeChanged;    // 0~1
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
        // Tab (홀드) / LB = JoystickButton4
        bool held = Input.GetKey(KeyCode.Tab) || Input.GetKey(KeyCode.JoystickButton4);

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

    private void OnApplicationQuit()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
}
