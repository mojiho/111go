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
    public float nextWaveDelay = 4f;        // 웨이브 클리어 후 다음 웨이브까지 대기
}

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Wave Config")]
    public List<WaveData> waves;

    [Header("Spawn Points")]
    public Transform[] spawnPoints;

    [Header("Prefabs")]
    public GameObject meleePrefab;
    public GameObject rangedPrefab;
    public GameObject eliteMeleePrefab;
    public GameObject eliteRangedPrefab;

    public int CurrentWave { get; private set; }
    public int AliveEnemies { get; private set; }
    public bool IsComplete { get; private set; }

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
        for (int i = 0; i < waves.Count; i++)
        {
            CurrentWave = i + 1;
            WaveData wave = waves[i];
            AliveEnemies = 0;

            OnWaveStart?.Invoke(CurrentWave, waves.Count);
            yield return StartCoroutine(SpawnWave(wave));

            // 이번 웨이브 전부 처치 대기
            yield return new WaitUntil(() => AliveEnemies <= 0);

            OnWaveCleared?.Invoke(CurrentWave);

            if (i < waves.Count - 1)
                yield return new WaitForSecondsRealtime(wave.nextWaveDelay);
        }

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

            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            GameObject obj = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

            EnemyBase enemy = obj.GetComponent<EnemyBase>();
            if (enemy != null) enemy.isElite = isElite;

            AliveEnemies++;

            yield return new WaitForSeconds(wave.spawnInterval);
        }
    }

    public void OnEnemyKilled()
    {
        AliveEnemies = Mathf.Max(0, AliveEnemies - 1);
    }
}
