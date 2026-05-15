using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillCard : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI keyText;

    [Header("Cooldown UI")]
    [SerializeField] private Image coolTimeImage;
    [SerializeField] private TextMeshProUGUI coolTimeText;

    private float _maxCooldown;

    /// <summary>카드 초기 설정 — 아이콘은 Inspector에서 직접 또는 SetIcon()으로 별도 관리</summary>
    public void Setup(string keyString, float maxCooldown)
    {
        if (keyText != null)
            keyText.text = keyString;

        _maxCooldown = maxCooldown;
        SetCooldown(0f);
    }

    /// <summary>아이콘 스프라이트 설정</summary>
    public void SetIcon(Sprite icon)
    {
        if (iconImage == null) return;
        iconImage.sprite = icon;
    }

    /// <summary>아이콘 활성/비활성</summary>
    public void SetIconActive(bool active)
    {
        if (iconImage == null) return;
        iconImage.enabled = active;
    }

    /// <summary>사용 가능 여부에 따라 카드 배경(부모 Image) 색상 변경 (false = 100/100/100 회색, true = 0/0/0 검정)</summary>
    public void SetAvailableTint(bool available)
    {
        // 카드 루트의 Image 컴포넌트 (배경) — 자식 Image_Icon은 건드리지 않음
        Image bg = GetComponent<Image>();
        if (bg == null) return;
        Color c = available
            ? new Color(255f, 255f, 255f, bg.color.a)
            : new Color(100f / 255f, 100f / 255f, 100f / 255f, bg.color.a);
        bg.color = c;
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
