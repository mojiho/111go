using UnityEngine;
using UnityEngine.UI;
using TMPro;

// HP바, 슬로우게이지, 웨이브 표시, 스킬 쿨다운 UI
public class UIManager : MonoBehaviour
{
    [Header("Player HP")]
    public Slider hpSlider;
    public TextMeshProUGUI hpText;

    [Header("Slow / Ultimate Gauge (공유)")]
    public Slider slowGaugeSlider;
    public Image slowGaugeFill;
    public Color gaugeEmptyColor   = new Color(0.4f, 0.4f, 0.4f);
    public Color gaugeNormalColor  = new Color(0.2f, 0.8f, 1f);
    public Color gaugeUltReadyColor = new Color(1f, 0.8f, 0.1f);  // 필살기 가능: 금색

    [Header("Skill Cards")]
    public SkillCard skill1Card;   // X키 — 돌진 참격
    public SkillCard skill2Card;   // C키 — 회전베기
    public SkillCard parryCard;    // Shift 키 — 패링
    public SkillCard deshCard;     // ctrl 키 - 대쉬
    public SkillCard ultimateCard; // V키 — 필살기 (게이지가 곧 쿨다운 역할)

    //[Header("Ultimate Ready (legacy — ultimateCard 사용 시 비워둬도 됨)")]
    //public GameObject ultimateReadyIndicator;   // 필살기 준비됐을 때 활성화

    [Header("Wave Info")]
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI enemyCountText;
    public TextMeshProUGUI waveSecText;

    private PlayerStats playerStats;
    private PlayerCombat playerCombat;
    private PlayerController playerController;
    private ParrySystem parrySystem;
    private SlowMotionSystem slowMo;
    private WaveManager waveManager;

    private void Start() => Init();

    private void Init()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        playerStats      = player.GetComponent<PlayerStats>();
        playerCombat     = player.GetComponent<PlayerCombat>();
        playerController = player.GetComponent<PlayerController>();
        parrySystem      = player.GetComponent<ParrySystem>();

        if (playerStats == null || playerCombat == null) return;  // 아직 준비 안 됨

        // HP 슬라이더 초기값
        playerStats.OnHpChanged.RemoveListener(UpdateHP);
        playerStats.OnHpChanged.AddListener(UpdateHP);
        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = 1f;
            hpSlider.wholeNumbers = false;
            hpSlider.interactable = false;   // 마우스로 안 만져지게
        }
        UpdateHP(playerStats.currentHp, playerStats.maxHp);

        // SkillCard 초기화 — 키 매핑 변경됨
        skill1Card?.Setup("Z",   playerCombat.skill1Cooldown);   // 대쉬어택
        skill2Card?.Setup("X",   playerCombat.skill2Cooldown);   // 강공격
        deshCard?.Setup("Shift", playerController != null ? playerController.dashCooldown : 0.7f);
        ultimateCard?.Setup("C", playerCombat.ultimateCooldown);
        // 패링 — 일단 비활성, 카드 숨김
        if (parryCard != null) parryCard.gameObject.SetActive(false);

        // 게이지 슬라이더
        slowMo = FindFirstObjectByType<SlowMotionSystem>();
        if (slowMo != null)
        {
            if (slowGaugeSlider != null)
            {
                slowGaugeSlider.minValue = 0f;
                slowGaugeSlider.maxValue = 1f;
                slowGaugeSlider.wholeNumbers = false;
                slowGaugeSlider.interactable = false;
            }
            slowMo.OnGaugeChanged.RemoveListener(UpdateSlowGauge);
            slowMo.OnGaugeChanged.AddListener(UpdateSlowGauge);
            UpdateSlowGauge(slowMo.GaugeRatio);
        }

        waveManager = WaveManager.Instance;
        if (waveManager != null)
            waveManager.OnWaveStart.AddListener((cur, total) => UpdateWaveText(cur, total));

        _initialized = true;
    }

    private bool _initialized = false;

    private void Update()
    {
        // Awake 순서 문제로 Start에서 초기화 실패했을 때 첫 프레임에 재시도
        if (!_initialized) Init();

        UpdateSkillCooldowns();
        UpdateEnemyCount();
        UpdateGaugeEveryFrame();
        UpdateWaveSecText();
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
        {
            // 필살기 발동 가능이면 금색, 아니면 파란색
            Color targetColor = (playerCombat != null && playerCombat.UltimateReady)
                ? gaugeUltReadyColor
                : Color.Lerp(gaugeEmptyColor, gaugeNormalColor, ratio);
            slowGaugeFill.color = Color.Lerp(slowGaugeFill.color, targetColor, Time.unscaledDeltaTime * 8f);
        }
    }

    private void UpdateSkillCooldowns()
    {
        if (playerCombat == null) return;

        //// ultimateCard 사용 시 indicator는 숨김 (둘 다 켜져있을 필요 X)
        //if (ultimateReadyIndicator != null)
        //    ultimateReadyIndicator.SetActive(ultimateCard == null && playerCombat.UltimateReady);

        // SkillCard 쿨타임 갱신
        skill1Card?.SetCooldown(playerCombat.Skill1CooldownRatio);
        skill2Card?.SetCooldown(playerCombat.Skill2CooldownRatio);
        parryCard?.SetCooldown(parrySystem      != null ? parrySystem.CooldownRatio           : 0f);
        deshCard?.SetCooldown(playerController  != null ? playerController.DashCooldownRatio  : 0f);
        // Ultimate — 시간 기반 쿨타임만 표시 (게이지는 Slider_Gauge에서 별도 표시)
        ultimateCard?.SetCooldown(playerCombat.UltimateCooldownRatio);
        // 필살기 사용 가능 여부에 따라 카드 아이콘 색조 변경
        ultimateCard?.SetAvailableTint(playerCombat.UltimateReady);
    }

    // 게이지/HP 모두 이벤트 외에 매 프레임 직접 갱신 (이벤트 누락 방지)
    private void UpdateGaugeEveryFrame()
    {
        if (!_initialized) return;

        // HP — slider + text 강제 갱신
        if (playerStats != null && hpSlider != null && playerStats.maxHp > 0f)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = 1f;
            hpSlider.wholeNumbers = false;
            hpSlider.value = playerStats.currentHp / playerStats.maxHp;
            if (hpText != null)
                hpText.text = $"{Mathf.CeilToInt(playerStats.currentHp)} / {Mathf.CeilToInt(playerStats.maxHp)}";
        }

        // SlowMo Gauge
        if (slowMo != null && slowGaugeSlider != null)
        {
            slowGaugeSlider.minValue = 0f;
            slowGaugeSlider.maxValue = 1f;
            slowGaugeSlider.wholeNumbers = false;
            slowGaugeSlider.value = slowMo.GaugeRatio;
        }
    }

    private void UpdateWaveText(int current, int total)
    {
        if (waveText != null)
            waveText.text = $"Wave {current}/{total}";
    }

    private void UpdateEnemyCount()
    {
        if (waveManager == null) return;

        // Wave 텍스트도 매 프레임 동기화 (이벤트 누락 대비)
        if (waveText != null && waveManager.CurrentWave > 0)
            waveText.text = $"Wave {waveManager.CurrentWave}/{waveManager.waves.Count}";

        if (enemyCountText != null)
            enemyCountText.text = $"남은 적: {waveManager.AliveEnemies}";
    }

    private void UpdateWaveSecText()
    {
        if (waveManager == null || waveSecText == null) return;

        if (waveManager.IsComplete)
        {
            waveSecText.text = "CLEAR!";
        }
        else if (waveManager.NextWaveSeconds > 0f)
        {
            int secs = Mathf.CeilToInt(waveManager.NextWaveSeconds);
            waveSecText.text = $"Next Wave\n{secs}";
        }
        else if (waveManager.IsFinalWaveStarted)
        {
            waveSecText.text = "FINAL";
        }
        else
        {
            waveSecText.text = "";
        }
    }
}
