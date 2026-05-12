using UnityEngine;
using TMPro;
using System.Collections;

/*
 *  데미지 팝업 — 적/플레이어가 피격될 때 떠오르며 페이드 아웃
 *  슬로우모션 영향 없도록 unscaledDeltaTime 사용
 */
public class DamagePopup : MonoBehaviour
{
    [SerializeField] private TextMeshPro textMesh;

    [Header("Tuning")]
    [SerializeField] private float lifeTime = 0.9f;
    [SerializeField] private float gravity = 14f;
    [SerializeField] private float initialJumpVelocity = 4f;
    [SerializeField] private float xForceMin = 1.5f;
    [SerializeField] private float xForceMax = 3.5f;
    [SerializeField] private float popScale = 1.4f;
    [SerializeField] private Color baseColor = new Color(1f, 0.95f, 0.3f, 1f); // 노란색
    [SerializeField] private string sortingLayerName = "Effect";   // 배경 위로 끌어올리는 레이어
    [SerializeField] private int sortingOrder = 1000;

    private Vector3 _moveVector;
    private Color _textColor;

    private void Awake()
    {
        if (textMesh == null) textMesh = GetComponentInChildren<TextMeshPro>();
        if (textMesh != null)
        {
            // 항상 카메라 앞에서 보이도록 — 배경/타일맵에 가려지지 않게 sortingLayer + order 둘 다 설정
            var renderer = textMesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (!string.IsNullOrEmpty(sortingLayerName) &&
                    SortingLayer.NameToID(sortingLayerName) != 0)
                {
                    renderer.sortingLayerName = sortingLayerName;
                }
                renderer.sortingOrder = sortingOrder;
            }
        }
    }

    public void Setup(int damageAmount, Vector3 direction)
    {
        if (textMesh == null) return;

        textMesh.SetText(damageAmount.ToString());

        // 알파 풀 리셋 — 풀에서 재활용 시 알파가 0인 상태로 시작하지 않도록
        _textColor = baseColor;
        _textColor.a = 1f;
        textMesh.color = _textColor;

        float pushDir = (direction.x >= 0f) ? 1f : -1f;
        float xForce = pushDir * Random.Range(xForceMin, xForceMax);
        _moveVector = new Vector3(xForce, initialJumpVelocity, 0f);

        // 위치 z=0 보장 (카메라 평면에 정렬)
        Vector3 p = transform.position;
        p.z = 0f;
        transform.position = p;

        transform.localScale = Vector3.one;

        StopAllCoroutines();
        StartCoroutine(AnimationRoutine());
    }

    private IEnumerator AnimationRoutine()
    {
        float timer = 0f;

        while (timer < lifeTime)
        {
            float dt = Time.unscaledDeltaTime;   // 슬로우모션 영향 X
            timer += dt;

            // 위로 솟구쳤다가 중력으로 떨어짐
            _moveVector.y -= gravity * dt;
            transform.position += _moveVector * dt;

            // 초반엔 살짝 커졌다가 원래 크기 — 강조 효과
            float t01 = timer / lifeTime;
            if (t01 < 0.25f)
            {
                float k = t01 / 0.25f;
                float s = Mathf.Lerp(1f, popScale, Mathf.Sin(k * Mathf.PI));
                transform.localScale = Vector3.one * s;
            }
            else
            {
                transform.localScale = Vector3.one;
            }

            // 후반 50%에서 알파 페이드
            if (t01 > 0.5f)
            {
                _textColor.a = Mathf.Lerp(1f, 0f, (t01 - 0.5f) / 0.5f);
                textMesh.color = _textColor;
            }

            yield return null;
        }

        if (DamagePopupManager.Instance != null)
            DamagePopupManager.Instance.ReturnPopup(this);
        else
            Destroy(gameObject);
    }
}
