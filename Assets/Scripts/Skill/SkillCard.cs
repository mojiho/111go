using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 *  스킬 카드 UI — 아이콘, 키 표시, 쿨타임 오버레이
 *  UIManager가 매 프레임 SetCooldown(ratio)을 호출해 갱신
 */
public class SkillCard : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI keyText;

    [Header("Cooldown UI")]
    [SerializeField] private Image coolTimeImage;           // Filled 타입 Image
    [SerializeField] private TextMeshProUGUI coolTimeText;  // 남은 초 표시

    private float _maxCooldown;

    /// <summary>카드 초기 설정 — UIManager의 Start에서 호출</summary>
    public void Setup(Sprite icon, string keyString, float maxCooldown)
    {
        if (iconImage != null)
        {
            iconImage.sprite  = icon;
            iconImage.enabled = icon != null;
        }

        if (keyText != null)
            keyText.text = keyString;

        _maxCooldown = maxCooldown;
        SetCooldown(0f);
    }

    /// <summary>쿨타임 갱신 — ratio 0 = 사용 가능, 1 = 풀쿨</summary>
    public void SetCooldown(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);

        if (coolTimeImage != null)
            coolTimeImage.fillAmount = ratio;

        if (coolTimeText != null)
        {
            if (ratio > 0f)
            {
                coolTimeText.text = Mathf.CeilToInt(ratio * _maxCooldown).ToString();
                coolTimeText.gameObject.SetActive(true);
            }
            else
            {
                coolTimeText.text = "";
                coolTimeText.gameObject.SetActive(false);
            }
        }
    }
}
