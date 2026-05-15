using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.Collections.Generic;

public enum GameState { Playing, GameOver, GameClear }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState State { get; private set; }

    public UnityEvent OnGameOver;
    public UnityEvent OnGameClear;

    [Header("References")]
    public WaveManager waveManager;
    public GameObject gameOverUI;
    public GameObject gameClearUI;
    public List<SpriteRenderer> BG_List = new();

    [Header("BG Darken (웨이브 진행 시)")]
    [Tooltip("웨이브가 진행될 때마다 BG의 RGB에서 차감할 값 (0~255)")]
    public int bgDarkenStepPer255 = 15;
    [Tooltip("최소 밝기 — 이 값 이하로 떨어지지 않음 (0~1)")]
    [Range(0f, 1f)] public float bgMinBrightness = 0.2f;

    [Header("플레이어 시작 위치")]
    public GameObject playerPrefab;
    public GameObject StartSpot;
    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        State = GameState.Playing;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (gameOverUI != null) gameOverUI.SetActive(false);
        if (gameClearUI != null) gameClearUI.SetActive(false);

        // 플레이어를 시작 지점으로 이동
        TeleportPlayerToStart();

        waveManager?.StartWaves();
    }

    public void TriggerGameOver()
    {
        if (State != GameState.Playing) return;
        State = GameState.GameOver;

        Time.timeScale = 0f;
        OnGameOver?.Invoke();

        if (gameOverUI != null) gameOverUI.SetActive(true);
    }

    public void TriggerGameClear()
    {
        if (State != GameState.Playing) return;
        State = GameState.GameClear;

        Time.timeScale = 0f;
        OnGameClear?.Invoke();

        if (gameClearUI != null) gameClearUI.SetActive(true);
    }

    public void RestartGame() => Restart();

    /// <summary>현재 씬을 재로드 — 모든 상태를 초기화하고 게임 다시 시작</summary>
    public void Restart()
    {
        // 시간/Animator/Audio 정상화 (GameOver/Clear에서 timeScale=0 상태 대비)
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // 정적 플래그 초기화 (씬 로드 후에도 살아남을 수 있는 것들)
        EnemyBase.SuppressDamagePopup = false;

        // 일시정지/슬로우모션 끝
        if (HitEffectManager.Instance != null && HitEffectManager.Instance.IsUltimatePlaying)
        {
            // 진행 중인 시네마틱 강제 정리 — IsUltimatePlaying은 자동으로 false 되지만 안전망
            HitEffectManager.Instance.StopAllCoroutines();
        }

        // 씬 재로드 — 모든 Awake/Start가 다시 호출되며 깨끗한 상태로 복원
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void TeleportPlayerToStart()
    {
        if (StartSpot == null)
        {
            Debug.LogWarning("[GameManager] StartSpot 미설정 — 플레이어 스폰 건너뜀");
            return;
        }

        Vector3 target = StartSpot.transform.position;

        // 1. 씬에 이미 있는 Player 찾기
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.gameObject;
        }

        // 2. 없으면 playerPrefab으로 새로 생성
        if (player == null)
        {
            if (playerPrefab == null)
            {
                Debug.LogWarning("[GameManager] 씬에 Player도 없고 playerPrefab도 비어있음");
                return;
            }
            player = Instantiate(playerPrefab, target, Quaternion.identity);
            Debug.Log($"[GameManager] Player 프리팹으로 새로 스폰 @ {target}");
        }
        else
        {
            // 이미 있으면 위치만 이동
            Rigidbody2D prb = player.GetComponent<Rigidbody2D>();
            if (prb != null)
            {
                prb.position = target;
                prb.linearVelocity = Vector2.zero;
            }
            player.transform.position = target;
            Debug.Log($"[GameManager] Player 위치 이동 → {target}");
        }

        // CameraController가 새 player를 따라가도록 target 갱신
        if (CameraController.Instance != null)
            CameraController.Instance.SetTarget(player.transform);
    }

    /// <summary>WaveManager가 매 웨이브 시작 시 호출 — BG 색상을 한 단계 어둡게</summary>
    public void DarkenBG()
    {
        float step = bgDarkenStepPer255 / 255f;
        foreach (var sr in BG_List)
        {
            if (sr == null) continue;
            Color c = sr.color;
            c.r = Mathf.Max(bgMinBrightness, c.r - step);
            c.g = Mathf.Max(bgMinBrightness, c.g - step);
            c.b = Mathf.Max(bgMinBrightness, c.b - step);
            sr.color = c;
        }
    }
}
