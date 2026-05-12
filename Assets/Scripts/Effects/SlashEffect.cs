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
    private bool useGradient = true;   // 노랑→주황→빨강 그라데이션 사용 여부

    // 그라데이션 색상 (양 끝 = 노랑, 중간 = 주황, 가운데 = 빨강)
    private static readonly Color GRAD_YELLOW = new Color(1f, 0.95f, 0.2f, 1f);
    private static readonly Color GRAD_ORANGE = new Color(1f, 0.55f, 0.05f, 1f);
    private static readonly Color GRAD_RED    = new Color(1f, 0.15f, 0.05f, 1f);

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
    }

    // 정적 헬퍼 — 한 줄로 호출 가능
    public static void Spawn(Vector3 worldPos, float angleDeg, float length,
                             Color color, float life = 0.15f, float width = 0.25f,
                             bool gradient = true)
    {
        var go = new GameObject("SlashEffect");
        Vector3 p = worldPos;
        p.z = 0f;
        go.transform.position = p;
        var fx = go.AddComponent<SlashEffect>();
        fx.startColor = color;
        fx.useGradient = gradient;
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
        Vector3 a = center - dir * (length * 0.5f);
        Vector3 b = center + dir * (length * 0.5f);

        // 위치 3개 (시작-중앙-끝) — 가운데가 두껍고 양끝이 뾰족하게 보간되도록
        lr.useWorldSpace = true;
        lr.positionCount = 3;
        lr.SetPosition(0, a);
        lr.SetPosition(1, center);
        lr.SetPosition(2, b);

        lr.material = new Material(Shader.Find("Sprites/Default"));

        // widthCurve — 양 끝 0, 중앙 1 → 양쪽 끝이 날카로운 마름모/잎사귀 형태
        lr.widthMultiplier = startWidth;
        lr.widthCurve = new AnimationCurve(
            new Keyframe(0f,   0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f,   0f)
        );

        if (useGradient)
        {
            // 노랑(끝) → 주황 → 빨강(중앙) → 주황 → 노랑(끝) 그라데이션
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(GRAD_YELLOW, 0f),
                    new GradientColorKey(GRAD_ORANGE, 0.25f),
                    new GradientColorKey(GRAD_RED,    0.5f),
                    new GradientColorKey(GRAD_ORANGE, 0.75f),
                    new GradientColorKey(GRAD_YELLOW, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
            lr.colorGradient = grad;
        }
        else
        {
            lr.startColor = startColor;
            lr.endColor   = startColor;
        }
        lr.numCapVertices = 0;        // 끝을 둥글게 만드는 캡 제거 — 뾰족하게
        lr.numCornerVertices = 4;     // 가운데 꺾임은 부드럽게

        lr.sortingLayerName = "Effect";
        lr.sortingOrder = 100;

        StartCoroutine(FadeAndDestroy());
    }

    private IEnumerator FadeAndDestroy()
    {
        float t = 0f;
        Color from = startColor;
        Gradient baseGrad = useGradient ? lr.colorGradient : null;
        while (t < lifetime)
        {
            t += Time.unscaledDeltaTime;
            float k = t / lifetime;
            float alpha = 1f - k;

            if (useGradient)
            {
                // 그라데이션의 모든 알파키를 동일하게 페이드
                var g = new Gradient();
                g.SetKeys(
                    baseGrad.colorKeys,
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(alpha, 0f),
                        new GradientAlphaKey(alpha, 1f)
                    }
                );
                lr.colorGradient = g;
            }
            else
            {
                Color c = new Color(from.r, from.g, from.b, alpha);
                lr.startColor = c;
                lr.endColor   = c;
            }

            lr.widthMultiplier = Mathf.Lerp(startWidth, 0f, k);
            yield return null;
        }
        Destroy(gameObject);
    }
}
