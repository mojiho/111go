using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FX_AutoDestroy 프리팹 전용 오브젝트 풀 싱글턴.
/// Instantiate / Destroy 대신 FXPool.Spawn / FXPool.Return 사용.
/// </summary>
public class FXPool : MonoBehaviour
{
    public static FXPool Instance { get; private set; }

    [System.Serializable]
    public struct PrewarmEntry
    {
        public GameObject prefab;
        [Min(0)] public int count;
    }

    [Header("Prewarm (선택 — 씬 시작 시 미리 생성)")]
    public PrewarmEntry[] prewarmEntries;

    // prefab → 대기 중인 인스턴스 큐
    private readonly Dictionary<GameObject, Queue<GameObject>> _pools  = new();
    // instanceID → 원본 prefab
    private readonly Dictionary<int, GameObject>               _origin = new();
    // 현재 풀에 반납된(비활성) 인스턴스 ID — 이중 반납 방지
    private readonly HashSet<int>                              _inPool = new();

    // ─── 유니티 생명주기 ───────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        foreach (var e in prewarmEntries)
        {
            if (e.prefab == null || e.count <= 0) continue;
            for (int i = 0; i < e.count; i++)
            {
                var go = CreateNew(e.prefab);
                go.SetActive(false);
                _inPool.Add(go.GetInstanceID());
                GetQueue(e.prefab).Enqueue(go);
            }
        }
    }

    // ─── 정적 API (호출부에서 Instance 널체크 불필요) ────────────────────

    /// <summary>풀에서 꺼내거나 새로 생성해 지정 위치·회전으로 활성화</summary>
    public static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (Instance != null) return Instance.SpawnInternal(prefab, pos, rot);
        // 풀 없으면 일반 인스턴스화 폴백
        return prefab != null ? Instantiate(prefab, pos, rot) : null;
    }

    /// <summary>인스턴스를 즉시 풀로 반납 (비활성화)</summary>
    public static void Return(GameObject instance)
    {
        if (Instance != null) Instance.ReturnInternal(instance);
        else                  Destroy(instance);
    }

    /// <summary>delay 초 후 반납 — 파티클 등 FX_AutoDestroy 없는 오브젝트용</summary>
    public static void ReturnDelayed(GameObject instance, float delay)
    {
        if (instance == null) return;
        if (Instance != null) Instance.StartCoroutine(Instance.ReturnAfter(instance, delay));
        else                  Destroy(instance, delay);
    }

    // ─── 내부 구현 ────────────────────────────────────────────────────

    private GameObject SpawnInternal(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return null;

        var queue = GetQueue(prefab);
        GameObject go = null;

        // 큐에서 유효한 인스턴스 꺼내기 (씬 전환 등으로 null 된 항목 건너뜀)
        while (queue.Count > 0 && go == null)
            go = queue.Dequeue();

        if (go == null)
            go = CreateNew(prefab);

        _inPool.Remove(go.GetInstanceID());
        go.transform.SetPositionAndRotation(pos, rot);
        go.SetActive(true);
        return go;
    }

    private void ReturnInternal(GameObject instance)
    {
        if (instance == null) return;

        int id = instance.GetInstanceID();
        if (_inPool.Contains(id)) return;   // 이미 반납됨 — 이중 반납 방지

        if (!_origin.TryGetValue(id, out var prefab))
        {
            // 풀 밖에서 생성된 오브젝트 → 그냥 파괴
            Destroy(instance);
            return;
        }

        instance.SetActive(false);
        _inPool.Add(id);
        GetQueue(prefab).Enqueue(instance);
    }

    private IEnumerator ReturnAfter(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnInternal(go);
    }

    private Queue<GameObject> GetQueue(GameObject prefab)
    {
        if (!_pools.TryGetValue(prefab, out var q))
        {
            q = new Queue<GameObject>();
            _pools[prefab] = q;
        }
        return q;
    }

    private GameObject CreateNew(GameObject prefab)
    {
        var go = Instantiate(prefab);
        go.name = prefab.name;
        _origin[go.GetInstanceID()] = prefab;
        return go;
    }
}
