using System.Collections;
using UnityEngine;

// 히트스탑, 스크린 셰이크, 피격 플래시, 대시 이펙트 전담 싱글턴
public class HitEffectManager : MonoBehaviour
{
    public static HitEffectManager Instance { get; private set; }

    [Header("Hit Stop")]
    public float defaultHitStopDuration = 0.06f;

    [Header("Screen Shake")]
    // CameraController에 위임

    [Header("Hit Flash")]
    public Color hitFlashColor = Color.white;
    public float hitFlashDuration = 0.08f;

    [Header("Prefabs")]
    public GameObject hitEffectPrefab;          // 파티클 or 스프라이트
    public GameObject dashAfterimagePool;       // 선택: 오브젝트 풀

    [Header("Slash Trail")]
    public GameObject slashTrailPrefab;

    private bool isHitStopped;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ───── 히트스탑 ─────
    // timeScale 건드리는 대신 Time.timeScale을 직접 0으로 내리고 realtime으로 복구
    public void TriggerHitStop(float duration = -1f)
    {
        if (isHitStopped) return;
        float d = duration < 0f ? defaultHitStopDuration : duration;
        StartCoroutine(HitStopRoutine(d));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        isHitStopped = true;
        float prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f;

        yield return new WaitForSecondsRealtime(duration);

        // 슬로우모션 중이면 slowTimeScale로 복귀
        SlowMotionSystem slowMo = FindFirstObjectByType<SlowMotionSystem>();
        float targetScale = (slowMo != null && slowMo.IsActive) ? slowMo.slowTimeScale : 1f;
        Time.timeScale = targetScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        isHitStopped = false;
    }

    // ───── 스크린 셰이크 ─────
    public void TriggerScreenShake(float duration, float magnitude)
    {
        CameraController.Instance?.TriggerShake(duration, magnitude);
    }

    // ───── 피격 플래시 ─────
    public void TriggerHitFlash(SpriteRenderer sr)
    {
        if (sr == null) return;
        StartCoroutine(FlashRoutine(sr));
    }

    private IEnumerator FlashRoutine(SpriteRenderer sr)
    {
        Color original = sr.color;
        sr.color = hitFlashColor;
        yield return new WaitForSecondsRealtime(hitFlashDuration);
        if (sr != null) sr.color = original;
    }

    // ───── 히트 이펙트 스폰 ─────
    public void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab == null) return;
        GameObject fx = Instantiate(hitEffectPrefab, position, Quaternion.identity);
        Destroy(fx, 1f);
    }

    // ───── 대시 이펙트 ─────
    public void TriggerDashEffect(Vector3 position, float direction)
    {
        // 간단한 잔상: 현재 위치에 반투명 스프라이트 복사
        // 실제 구현 시 오브젝트 풀 사용 권장
        if (dashAfterimagePool != null)
            dashAfterimagePool.SendMessage("SpawnAfterimage", position, SendMessageOptions.DontRequireReceiver);
    }

    // ───── 슬래시 트레일 ─────
    public void SpawnSlashTrail(Vector3 position, Quaternion rotation)
    {
        if (slashTrailPrefab == null) return;
        GameObject trail = Instantiate(slashTrailPrefab, position, rotation);
        Destroy(trail, 0.3f);
    }
}
