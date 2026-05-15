using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 히트스탑, 스크린 셰이크, 피격 플래시, 대시 이펙트, 필살기 시네마틱 전담 싱글턴
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
    public GameObject hitEffectPrefab;          // 적 피격 이펙트
    public GameObject playerHitEffectPrefab;    // 플레이어 피격 이펙트 (Hit 프리팹)
    public GameObject dashAfterimagePool;       // 선택: 오브젝트 풀

    [Header("Slash Trail")]
    public GameObject slashTrailPrefab;

    // ───────── 필살기 시네마틱 ─────────
    [Header("Ultimate Cinematic — UI")]
    public Image ultPortraitImage;        // Image_Portrate
    public Image ultBlackOutImage;        // Image_BlackOut
    public RectTransform ultSlashContainer; // (선택) 슬래시 부모. 비어있으면 BlackOut 자식으로 자동 생성
    public Sprite ultSlashSprite;         // (선택) 슬래시 모양 스프라이트. 비어있으면 흰 사각형

    [Header("Ultimate Cinematic — World Effect")]
    public GameObject ultImpactPrefab;    // CFXR Impact Glowing HDR (Blue)
    public Vector2 ultImpactOffset = new Vector2(0f, 1.2f);

    [Header("Ultimate Cinematic — Timing")]
    public float ultImpactToPortraitDelay = 0.18f;
    public float ultBlackoutFadeIn = 0.15f;
    public float ultBlackoutHoldBeforeSlash = 0.05f;
    public float ultPerSlashInterval = 0.07f;
    public float ultBlackoutFadeOut = 0.18f;
    public float ultPostDamageHold = 0.1f;
    [Tooltip("연출 종료 후 적에게 순차로 들어가는 타격 간격")]
    public float ultSequentialHitInterval = 0.1f;

    [Header("Ultimate Cinematic — BlackOut Color (완전 검정)")]
    public Color ultBlackOutColor = new Color(0f, 0f, 0f, 1f);

    [Header("Ultimate Cinematic — World Slash (적/투사체 위치)")]
    [Tooltip("적 위치 슬래시 — 길이 / 두께 / 수명")]
    public float ultEnemySlashLength = 3.5f;
    public float ultEnemySlashThickness = 0.3f;
    public float ultEnemySlashLife = 0.18f;
    [Tooltip("투사체 위치 슬래시 — 길이 / 두께 / 수명")]
    public float ultProjectileSlashLength = 2.5f;
    public float ultProjectileSlashThickness = 0.25f;
    public float ultProjectileSlashLife = 0.18f;

    [Header("Ultimate Cinematic — Slash Visual (UI)")]
    public Color[] ultSlashColors = new[]
    {
        new Color(0.75f, 0.35f, 1.00f, 1f),   // 라벤더
        new Color(0.55f, 0.20f, 0.95f, 1f),   // 보라
        new Color(0.90f, 0.55f, 1.00f, 1f),   // 연보라
    };
    public Vector2 ultSlashLengthRange = new Vector2(400f, 900f);   // UI pixel
    public Vector2 ultSlashThicknessRange = new Vector2(10f, 22f);
    public float ultSlashLife = 0.22f;
    [Tooltip("BlackOut 영역 안 슬래시 스폰 padding (0~1)")]
    public Vector2 ultSlashAreaPadding = new Vector2(0.12f, 0.18f);

    // 양 끝이 뾰족한 슬래시용 sprite (런타임 생성)
    private Sprite _taperedSlashSprite;

    public bool IsUltimatePlaying { get; private set; }

    private bool isHitStopped;
    private readonly List<GameObject> _activeUltSlashes = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // 시네마틱 UI 초기 상태
        if (ultPortraitImage != null) ultPortraitImage.gameObject.SetActive(false);
        if (ultBlackOutImage != null)
        {
            var c = ultBlackOutColor; c.a = 0f;
            ultBlackOutImage.color = c;
            ultBlackOutImage.gameObject.SetActive(false);
        }
    }

    // ───── 히트스탑 ─────
    public void TriggerHitStop(float duration = -1f)
    {
        if (isHitStopped) return;
        float d = duration < 0f ? defaultHitStopDuration : duration;
        StartCoroutine(HitStopRoutine(d));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        isHitStopped = true;
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f;

        yield return new WaitForSecondsRealtime(duration);

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

    public void TriggerDirectionalShake(float duration, float magnitude, Vector2 direction, float bias = 0.7f)
    {
        CameraController.Instance?.TriggerDirectionalShake(duration, magnitude, direction, bias);
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

    // ───── 히트 이펙트 스폰 (적 피격) ─────
    public void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab == null) return;
        // FX_AutoDestroy가 붙어있으면 자동 반납, 없으면 1초 후 반납
        GameObject fx = FXPool.Spawn(hitEffectPrefab, position + Vector3.up * 0.5f, Quaternion.identity);
        if (fx != null && fx.GetComponent<FX_AutoDestroy>() == null)
            FXPool.ReturnDelayed(fx, 1f);
    }

    // ───── 플레이어 피격 이펙트 스폰 ─────
    public void SpawnPlayerHitEffect(Vector3 position)
    {
        if (playerHitEffectPrefab == null) return;
        GameObject fx = FXPool.Spawn(playerHitEffectPrefab, position, Quaternion.identity);
        if (fx != null && fx.GetComponent<FX_AutoDestroy>() == null)
            FXPool.ReturnDelayed(fx, 1f);
    }

    // ───── 대시 이펙트 ─────
    public void TriggerDashEffect(Vector3 position, float direction)
    {
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

    // ───────── 필살기 시네마틱 ─────────

    /// <summary>필살기 시네마틱 재생.
    /// targets: UltimateHitBox 안에 잡힌 적 (이들만 freeze 후 누적 데미지).
    /// 화면 내 모든 투사체는 freeze 후 연출 종료 시 제거.</summary>
    public IEnumerator PlayUltimate(Vector3 playerWorldPos, List<EnemyBase> targets,
                                    int hitCount, float perHitDamage, float knockback,
                                    Bounds? hitboxBounds = null, float stunPerHit = 0.05f)
    {
        IsUltimatePlaying = true;

        // ── 1. 타겟 적 Freeze ──
        foreach (var t in targets)
            if (t != null) t.SetFrozen(true);

        // 투사체 — hitboxBounds가 주어졌으면 그 안에 있는 것만, 아니면 전부
        List<Projectile> frozenProjectiles = new List<Projectile>();
        foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
        {
            if (p == null) continue;
            if (hitboxBounds.HasValue && !hitboxBounds.Value.Contains(p.transform.position))
                continue;
            p.SetFrozen(true);
            frozenProjectiles.Add(p);
        }

        // ── 시네마틱 동안 hitbox 안으로 들어오는 적/투사체 추가 감시 시작 ──
        Coroutine monitor = null;
        if (hitboxBounds.HasValue)
            monitor = StartCoroutine(MonitorNewEntries(hitboxBounds.Value, targets, frozenProjectiles));

        // ── 2. Impact 이펙트 ──
        if (ultImpactPrefab != null)
        {
            Vector3 impactPos = playerWorldPos + (Vector3)ultImpactOffset;
            GameObject fx = FXPool.Spawn(ultImpactPrefab, impactPos, Quaternion.identity);
            if (fx != null && fx.GetComponent<FX_AutoDestroy>() == null)
                FXPool.ReturnDelayed(fx, 2.5f);
        }

        yield return new WaitForSecondsRealtime(ultImpactToPortraitDelay);

        // ── 3. Portrait 활성 ──
        if (ultPortraitImage != null)
            ultPortraitImage.gameObject.SetActive(true);

        // ── 4. BlackOut 페이드 인 ──
        if (ultBlackOutImage != null)
        {
            ultBlackOutImage.gameObject.SetActive(true);
            yield return StartCoroutine(FadeBlackOut(0f, ultBlackOutColor.a, ultBlackoutFadeIn));
        }

        yield return new WaitForSecondsRealtime(ultBlackoutHoldBeforeSlash);

        // ── 5. 슬래시 펑펑 ── (시작과 동시에 Portrait 숨김)
        if (ultPortraitImage != null) ultPortraitImage.gameObject.SetActive(false);
        for (int i = 0; i < hitCount; i++)
        {
            SpawnUltUISlash(i);
            yield return new WaitForSecondsRealtime(ultPerSlashInterval);
        }

        // ── 6. BlackOut 페이드 아웃 + UI 정리 (적 타격이 시각적으로 보이도록 먼저 치움) ──
        if (ultBlackOutImage != null)
            yield return StartCoroutine(FadeBlackOut(ultBlackOutColor.a, 0f, ultBlackoutFadeOut));

        if (ultPortraitImage != null) ultPortraitImage.gameObject.SetActive(false);
        if (ultBlackOutImage != null) ultBlackOutImage.gameObject.SetActive(false);
        ClearUltSlashes();

        // 감시 중단 — 이후 순차 타격 동안 새로 추가되지 않게
        if (monitor != null) StopCoroutine(monitor);

        // 적 unfreeze — 이제부터 Hurt 애니메이션/넉백이 정상 동작해야 함
        foreach (var t in targets)
            if (t != null) t.SetFrozen(false);

        Debug.Log($"[Ultimate Cinematic] 순차타격 시작 — targets={targets.Count} hitCount={hitCount} dmgPerHit={perHitDamage}");

        // EnemyBase 자체 데미지 팝업 억제 — 시네마틱이 방향 강제해서 직접 띄움
        EnemyBase.SuppressDamagePopup = true;

        // ── 7. 순차 다타격 — 타수에 맞춰 0.1초 간격으로 1타씩 ──
        for (int i = 0; i < hitCount; i++)
        {
            float slashBaseAngle = (i % 2 == 0) ? -45f : 45f;
            int hitsApplied = 0;
            foreach (var t in targets)
            {
                if (t == null) continue;
                float kb = (t.transform.position.x > playerWorldPos.x ? 1f : -1f) * knockback;

                // 데미지 팝업 방향 — 플레이어 기준 적이 있는 쪽 (오른쪽 적 → 오른쪽으로 튕김)
                float popupSignX = (t.transform.position.x > playerWorldPos.x) ? 1f : -1f;
                Vector3 popupDir = new Vector3(popupSignX, 0f, 0f);

                // 적이 죽었어도 데미지 팝업/슬래시 이펙트는 끝까지 표시
                bool wasAlive = t.currentHp > 0f;
                if (wasAlive)
                {
                    t.TakeDamage(perHitDamage, kb);
                    t.ApplyHitStun(stunPerHit);
                }
                // 항상 직접 띄움 — facing 반대 방향 보장 (살았든 죽었든)
                DamagePopupManager.Instance?.ShowPopup(
                    Mathf.RoundToInt(perHitDamage),
                    t.BodyPosition + Vector2.up * 0.5f,
                    popupDir);
                hitsApplied++;

                Vector3 sp = (Vector3)t.BodyPosition;
                sp.z = 0f;
                Color slashCol = ultSlashColors[i % ultSlashColors.Length];
                SlashEffect.Spawn(sp, slashBaseAngle + Random.Range(-10f, 10f),
                                  ultEnemySlashLength, slashCol, ultEnemySlashLife, ultEnemySlashThickness);
                SpawnHitEffect((Vector3)t.BodyPosition);
            }

            // 투사체에도 슬래시 연출 — 데미지는 없지만 시각적으로 같이 베이는 느낌
            Color projSlashCol = ultSlashColors[i % ultSlashColors.Length];
            foreach (var p in frozenProjectiles)
            {
                if (p == null) continue;
                Vector3 pp = p.transform.position;
                pp.z = 0f;
                SlashEffect.Spawn(pp, slashBaseAngle + Random.Range(-10f, 10f),
                                  ultProjectileSlashLength, projSlashCol, ultProjectileSlashLife, ultProjectileSlashThickness);
                SpawnHitEffect(pp);
            }

            Debug.Log($"[Ultimate Cinematic] hit#{i+1}/{hitCount} 적용={hitsApplied}");

            TriggerHitStop(0.03f);
            TriggerScreenShake(0.05f, 0.12f);

            yield return new WaitForSecondsRealtime(ultSequentialHitInterval);
        }

        yield return new WaitForSecondsRealtime(ultPostDamageHold);

        // 자동 팝업 억제 플래그 복원
        EnemyBase.SuppressDamagePopup = false;

        // ── 8. 투사체 정리 ──
        foreach (var p in frozenProjectiles)
            if (p != null) Destroy(p.gameObject);

        IsUltimatePlaying = false;
    }

    // 시네마틱 동안 hitbox 영역에 새로 진입한 적/투사체를 감지해서 freeze 리스트에 추가
    private IEnumerator MonitorNewEntries(Bounds bounds, List<EnemyBase> targets, List<Projectile> frozenProjectiles)
    {
        const float scanInterval = 0.05f;
        while (true)
        {
            // 새 적 검출
            Collider2D[] hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                EnemyBase e = h.GetComponentInParent<EnemyBase>();
                if (e != null && !targets.Contains(e))
                {
                    e.SetFrozen(true);
                    targets.Add(e);
                }
            }

            // 새 투사체 검출
            foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                if (p == null) continue;
                if (frozenProjectiles.Contains(p)) continue;
                if (!bounds.Contains(p.transform.position)) continue;
                p.SetFrozen(true);
                frozenProjectiles.Add(p);
            }

            yield return new WaitForSecondsRealtime(scanInterval);
        }
    }

    private IEnumerator FadeBlackOut(float fromA, float toA, float duration)
    {
        float t = 0f;
        Color c = ultBlackOutImage.color;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            c.r = ultBlackOutColor.r; c.g = ultBlackOutColor.g; c.b = ultBlackOutColor.b;
            c.a = Mathf.Lerp(fromA, toA, k);
            ultBlackOutImage.color = c;
            yield return null;
        }
        c.a = toA;
        ultBlackOutImage.color = c;
    }

    // BlackOut 위(같은 Canvas 자식)에 UI Image로 칼질 그림 — Overlay Canvas에서도 동작
    private void SpawnUltUISlash(int index)
    {
        // 슬래시 부모 — slashContainer 없으면 BlackOut의 자식으로 자동 생성
        RectTransform parent = ultSlashContainer;
        if (parent == null && ultBlackOutImage != null)
            parent = EnsureSlashContainer();
        if (parent == null) return;

        // Image GameObject 동적 생성
        GameObject go = new GameObject($"UltSlash_{index}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        Image img = go.GetComponent<Image>();
        // 사용자가 별도 sprite 안 줬으면 양 끝 뾰족 텍스처 자동 생성/재사용
        img.sprite = ultSlashSprite != null ? ultSlashSprite : GetTaperedSlashSprite();
        img.raycastTarget = false;

        // 부모(BlackOut) 영역 안 랜덤 위치
        Rect bounds = parent.rect;
        float padX = bounds.width * ultSlashAreaPadding.x;
        float padY = bounds.height * ultSlashAreaPadding.y;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(
            Random.Range(-bounds.width  * 0.5f + padX,  bounds.width  * 0.5f - padX),
            Random.Range(-bounds.height * 0.5f + padY,  bounds.height * 0.5f - padY));

        // 크기 + 각도
        float length = Random.Range(ultSlashLengthRange.x, ultSlashLengthRange.y);
        float thick  = Random.Range(ultSlashThicknessRange.x, ultSlashThicknessRange.y);
        rt.sizeDelta = new Vector2(length, thick);

        float baseAngle = (index % 2 == 0) ? -45f : 45f;
        float angle = baseAngle + Random.Range(-10f, 10f);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        rt.localScale = Vector3.one;

        Color color = ultSlashColors[index % ultSlashColors.Length];
        color.a = 0f;
        img.color = color;

        _activeUltSlashes.Add(go);
        StartCoroutine(FadeAndDestroyUltSlash(img, ultSlashLife));
    }

    // 양 끝이 sin 곡선으로 뾰족해지는 가로 그라데이션 텍스처 → 스프라이트로 캐싱
    private Sprite GetTaperedSlashSprite()
    {
        if (_taperedSlashSprite != null) return _taperedSlashSprite;

        const int W = 256, H = 8;
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[W * H];
        for (int x = 0; x < W; x++)
        {
            float u = (float)x / (W - 1);
            // 양 끝이 매우 뾰족하게 — sin^3 곡선 (가운데 폭 좁고 양쪽으로 길게 좁아짐)
            float a = Mathf.Sin(u * Mathf.PI);
            a = Mathf.Pow(a, 3.0f);
            for (int y = 0; y < H; y++)
            {
                float vy = (float)y / (H - 1);
                // Y는 sin^2 (수직도 뾰족)
                float yFade = Mathf.Pow(Mathf.Sin(vy * Mathf.PI), 2.0f);
                pixels[y * W + x] = new Color(1f, 1f, 1f, a * yFade);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        _taperedSlashSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        _taperedSlashSprite.name = "TaperedSlash_Runtime";
        return _taperedSlashSprite;
    }

    private RectTransform EnsureSlashContainer()
    {
        if (ultSlashContainer != null) return ultSlashContainer;
        if (ultBlackOutImage == null) return null;

        GameObject go = new GameObject("SlashContainer", typeof(RectTransform));
        ultSlashContainer = go.GetComponent<RectTransform>();
        ultSlashContainer.SetParent(ultBlackOutImage.transform, false);
        ultSlashContainer.anchorMin = Vector2.zero;
        ultSlashContainer.anchorMax = Vector2.one;
        ultSlashContainer.offsetMin = Vector2.zero;
        ultSlashContainer.offsetMax = Vector2.zero;
        ultSlashContainer.SetAsLastSibling();  // BlackOut의 최상단 자식 → 가장 앞
        return ultSlashContainer;
    }

    private IEnumerator FadeAndDestroyUltSlash(Image slash, float life)
    {
        float t = 0f;
        Color baseColor = slash.color;
        baseColor.a = 1f;
        while (t < life)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / life);
            // 0~0.15: 강한 페이드 인, 0.15~1: 천천히 페이드 아웃
            float a = (k < 0.15f) ? Mathf.Lerp(0f, 1f, k / 0.15f) : Mathf.Lerp(1f, 0f, (k - 0.15f) / 0.85f);
            Color c = baseColor; c.a = a;
            if (slash != null) slash.color = c;
            yield return null;
        }
        if (slash != null) Destroy(slash.gameObject);
    }

    private void ClearUltSlashes()
    {
        foreach (var g in _activeUltSlashes)
            if (g != null) Destroy(g);
        _activeUltSlashes.Clear();
    }
}
