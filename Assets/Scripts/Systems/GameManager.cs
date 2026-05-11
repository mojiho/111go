using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

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

    public void RestartGame()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
