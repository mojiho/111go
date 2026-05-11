using System.Collections;
using UnityEngine;

// 슬래시 이펙트 — 라인 렌더러로 짧은 호/직선을 그리고 페이드 아웃
[RequireComponent(typeof(LineRenderer))]
public class SlashEffect : MonoBehaviour
{
    private LineRenderer lr;
    private float lifetime = 0.15f;
    private float startWidth = 0.25f;
    private Color startColor = Color.white;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
    }

    // 정적 헬퍼 — 한 줄로 호출 가능
    public static void Spawn(Vector3 worldPos, float angleDeg, float length,
                             Color color, float life = 0.15f, float width = 0.25f)
    {
        var go = new GameObject("SlashEffect");
        go.transform.position = worldPos;
        var fx = go.AddComponent<SlashEffect>();
        fx.startColor = color;
        fx.lifetime = life;
        fx.startWidth = width;
        fx.Init(angleDeg, length);
    }

    private void Init(float angleDeg, float length)
    {
        // 라인 양 끝 좌표 (각도 기준 — 0=가로, 45=/, -45=\)
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
        Vector3 center = transform.position;
        center.z = 0f;   // 2D 카메라 앞에 확실히 보이게
        Vector3 a = center - dir * (length * 0.5f);
        Vector3 b = center + dir * (length * 0.5f);

        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);

        // LineRenderer 머티리얼 — 알파블렌딩이 되는 셰이더가 필요
        // Sprites/Default는 URP에서도 호환 모드로 동작하지만 안전을 위해 fallback 체인
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(sh);
        // 1x1 흰색 텍스처 할당 — 일부 셰이더(Sprites/Default)는 텍스처 없으면 마젠타
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        if (mat.HasProperty("_MainTex"))  mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseMap"))  mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", startColor);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", startColor);
        lr.material = mat;

        lr.startWidth = startWidth;
        lr.endWidth = startWidth * 0.3f;   // 끝쪽이 가늘게
        lr.startColor = startColor;
        lr.endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;

        // sortingLayer "Character"가 없으면 기본 레이어 사용 — 안전 처리
        if (SortingLayer.NameToID("Character") != 0)
            lr.sortingLayerName = "Character";
        lr.sortingOrder = 100;

        StartCoroutine(FadeAndDestroy());
    }

    private IEnumerator FadeAndDestroy()
    {
        float t = 0f;
        Color from = lr.startColor;
        while (t < lifetime)
        {
            t += Time.unscaledDeltaTime;
            float alpha = 1f - (t / lifetime);
            lr.startColor = new Color(from.r, from.g, from.b, alpha);
            lr.endColor   = new Color(from.r, from.g, from.b, 0f);
            float w = Mathf.Lerp(startWidth, 0f, t / lifetime);
            lr.startWidth = w;
            lr.endWidth = w * 0.3f;
            yield return null;
        }
        Destroy(gameObject);
    }
}
