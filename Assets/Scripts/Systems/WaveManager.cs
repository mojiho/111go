using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class WaveData
{
    public int meleeCount = 3;
    public int rangedCount = 1;
    public int eliteCount = 0;
    public float spawnInterval = 0.5f;
}

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Wave Config")]
    public List<WaveData> waves;
    [Tooltip("이 웨이브가 처치되든 안 되든 다음 웨이브 시작까지의 고정 대기 시간")]
    public float waveInterval = 15f;

    [Header("Spawn Points (지상 기반 — 비워두면 공중 영역 사용)")]
    public Transform[] spawnPoints;

    [Header("Aerial Spawn Area (공중에서 떨어지며 등장)")]
    public bool useAerialSpawn = true;
    public Vector2 aerialSpawnMin = new Vector2(-10f, 8f);   // 좌하단
    public Vector2 aerialSpawnMax = new Vector2(20f, 12f);   // 우상단

    [Header("Prefabs")]
    public GameObject meleePrefab;
    public GameObject rangedPrefab;
    public GameObject eliteMeleePrefab;
    public GameObject eliteRangedPrefab;

    public int CurrentWave { get; private set; }
    public int AliveEnemies { get; private set; }
    public bool IsComplete { get; private set; }
    public float NextWaveSeconds { get; private set; }   // 다음 웨이브까지 남은 시간 (0이면 비카운팅)
    public bool IsFinalWaveStarted { get; private set; } // 마지막 웨이브 스폰 시작했는지

    public UnityEvent<int, int> OnWaveStart;    // current, total
    public UnityEvent<int> OnWaveCleared;
    public UnityEvent OnAllWavesCleared;

    private bool isSpawning;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartWaves()
    {
        CurrentWave = 0;
        IsComplete = false;
        StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
        AliveEnemies = 0;
        NextWaveSeconds = 0f;
        IsFinalWaveStarted = false;

        for (int i = 0; i < waves.Count; i++)
        {
            CurrentWave = i + 1;
            WaveData wave = waves[i];

            if (i == waves.Count - 1) IsFinalWaveStarted = true;

            OnWaveStart?.Invoke(CurrentWave, waves.Count);
            GameManager.Instance?.DarkenBG();

            yield return StartCoroutine(SpawnWave(wave));

            // 마지막 웨이브가 아니면 15초 카운트다운 후 다음 웨이브
            if (i < waves.Count - 1)
            {
                NextWaveSeconds = waveInterval;
                while (NextWaveSeconds > 0f)
                {
                    NextWaveSeconds -= Time.unscaledDeltaTime;
                    yield return null;
                }
                NextWaveSeconds = 0f;
            }
        }

        // 모든 웨이브 스폰 완료 후 남은 적 전부 처치 대기
        yield return new WaitUntil(() => AliveEnemies <= 0);

        IsComplete = true;
        OnAllWavesCleared?.Invoke();
        GameManager.Instance?.TriggerGameClear();
    }

    private IEnumerator SpawnWave(WaveData wave)
    {
        List<(GameObject prefab, bool elite)> spawnList = new List<(GameObject, bool)>();

        for (int i = 0; i < wave.meleeCount; i++)   spawnList.Add((meleePrefab, false));
        for (int i = 0; i < wave.rangedCount; i++)   spawnList.Add((rangedPrefab, false));
        for (int i = 0; i < wave.eliteCount; i++)
        {
            // 엘리트는 절반은 근접, 절반은 원거리
            bool useRanged = i % 2 == 1;
            spawnList.Add((useRanged ? eliteRangedPrefab : eliteMeleePrefab, true));
        }

        // 랜덤 순서 셔플
        for (int i = spawnList.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (spawnList[i], spawnList[j]) = (spawnList[j], spawnList[i]);
        }

        foreach (var (prefab, isElite) in spawnList)
        {
            if (prefab == null) continue;

            Vector3 spawnPos;
            if (useAerialSpawn)
            {
                // 공중 영역에서 랜덤 — 중력으로 자연 낙하
                spawnPos = new Vector3(
                    Random.Range(aerialSpawnMin.x, aerialSpawnMax.x),
                    Random.Range(aerialSpawnMin.y, aerialSpawnMax.y),
                    0f);
            }
            else if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
                spawnPos = spawnPoint.position;
            }
            else
            {
                spawnPos = Vector3.zero;
            }

            GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);

            EnemyBase enemy = obj.GetComponent<EnemyBase>();
            if (enemy != null) enemy.isElite = isElite;

            AliveEnemies++;

            yield return new WaitForSecondsRealtime(wave.spawnInterval);
        }
    }

    public void OnEnemyKilled()
    {
        AliveEnemies = Mathf.Max(0, AliveEnemies - 1);
    }
}
