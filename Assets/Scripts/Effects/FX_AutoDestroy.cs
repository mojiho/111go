using UnityEngine;

// 애니메이션이 끝나면 FXPool로 반납 (풀 없으면 Destroy 폴백)
public class FX_AutoDestroy : MonoBehaviour
{
    private Animator anim;

    private void Awake() => anim = GetComponent<Animator>();

    private void OnEnable()
    {
        // 풀에서 재사용될 때 애니메이터를 처음 상태로 리셋
        if (anim == null) anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.Rebind();
            anim.Update(0f);
        }
    }

    private void Update()
    {
        if (anim == null) return;
        var state = anim.GetCurrentAnimatorStateInfo(0);
        if (state.normalizedTime >= 1f && !anim.IsInTransition(0))
            FXPool.Return(gameObject);
    }
}
