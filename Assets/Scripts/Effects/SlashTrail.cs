using System.Collections;
using UnityEngine;

// 공격 시 검 궤적 이펙트
// TrailRenderer 기반 + 선택적으로 라인 렌더러로 슬래시 호를 그릴 수 있음
[RequireComponent(typeof(TrailRenderer))]
public class SlashTrail : MonoBehaviour
{
    [Header("Trail Settings")]
    public float activeTime = 0.12f;
    public Gradient attackTrailColor;
    public Gradient skill1TrailColor;
    public Gradient skill2TrailColor;

    private TrailRenderer trail;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        trail.emitting = false;
    }

    public void PlayAttack() => StartCoroutine(Emit(activeTime, attackTrailColor));
    public void PlaySkill1() => StartCoroutine(Emit(activeTime * 1.5f, skill1TrailColor));
    public void PlaySkill2() => StartCoroutine(Emit(activeTime * 2f, skill2TrailColor));

    private IEnumerator Emit(float duration, Gradient color)
    {
        if (color != null) trail.colorGradient = color;
        trail.emitting = true;
        yield return new WaitForSecondsRealtime(duration);
        trail.emitting = false;
    }
}
