using UnityEngine;

// 애니메이션 끝나면 자동 삭제
public class FX_AutoDestroy : MonoBehaviour
{
    private Animator anim;

    private void Awake() => anim = GetComponent<Animator>();

    private void Update()
    {
        if (anim == null) return;
        var state = anim.GetCurrentAnimatorStateInfo(0);
        if (state.normalizedTime >= 1f && !anim.IsInTransition(0))
            Destroy(gameObject);
    }
}