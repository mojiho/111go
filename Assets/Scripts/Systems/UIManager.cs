using UnityEngine;
using UnityEngine.UI;
using TMPro;

// HP바, 슬로우게이지, 웨이브 표시, 스킬 쿨다운 UI
public class UIManager : MonoBehaviour
{
    [Header("Player HP")]
    public Slider hpSlider;
    public TextMeshProUGUI hpText;

    [Header("Slow Gauge")]
    public Slider slowGaugeSlider;
    public Image slowGaugeFill;
    public Color gaugeFullColor = new Color(0.2f, 0.8f, 1f);
    public Color gaugeEmptyColor = new Color(0.4f, 0.4f, 0.4f);

    [Header("Skill Cooldown")]
    public Image skill1CooldownOverlay;
    public Image skill2CooldownOverlay;
    public Image parryCooldownOverlay;

    [Header("Ultimate Ready")]
    public GameObject ultimateReadyIndicator;   // 필살기 준비됐을 때 활성화

    [Header("Wave Info")]
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI enemyCountText;

    private PlayerStats playerStats;
    private PlayerCombat playerCombat;
    private ParrySystem parrySystem;
    private SlowMotionSystem slowMo;
    private WaveManager waveManager;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
            playerCombat = player.GetComponent<PlayerCombat>();
            parrySystem  = player.GetComponent<ParrySystem>();

            if (playerStats != null)
                playerStats.OnHpChanged.AddListener(UpdateHP);
        }

        slowMo = FindFirstObjectByType<SlowMotionSystem>();
        if (slowMo != null)
            slowMo.OnGaugeChanged.AddListener(UpdateSlowGauge);

        waveManager = WaveManager.Instance;
        if (waveManager != null)
        {
            waveManager.OnWaveStart.AddListener((cur, total) => UpdateWaveText(cur, total));
        }
    }

    private void Update()
    {
        UpdateSkillCooldowns();
        UpdateEnemyCount();
    }

    private void UpdateHP(float current, float max)
    {
        if (hpSlider != null) hpSlider.value = current / max;
        if (hpText != null) hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void UpdateSlowGauge(float ratio)
    {
        if (slowGaugeSlider != null) slowGaugeSlider.value = ratio;
        if (slowGaugeFill != null)
            slowGaugeFill.color = Color.Lerp(gaugeEmptyColor, gaugeFullColor, ratio);
    }

    private void UpdateSkillCooldowns()
    {
        if (playerCombat == null) return;

        if (skill1CooldownOverlay != null)
            skill1CooldownOverlay.fillAmount = playerCombat.Skill1CooldownRatio;

        if (skill2CooldownOverlay != null)
            skill2CooldownOverlay.fillAmount = playerCombat.Skill2CooldownRatio;

        if (parryCooldownOverlay != null && parrySystem != null)
            parryCooldownOverlay.fillAmount = parrySystem.CooldownRatio;

        if (ultimateReadyIndicator != null)
            ultimateReadyIndicator.SetActive(playerCombat.UltimateReady);
    }

    private void UpdateWaveText(int current, int total)
    {
        if (waveText != null)
            waveText.text = $"WAVE  {current} / {total}";
    }

    private void UpdateEnemyCount()
    {
        if (waveManager == null || enemyCountText == null) return;
        enemyCountText.text = $"남은 적: {waveManager.AliveEnemies}";
    }
}
