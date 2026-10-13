using System.Collections.Generic;
using UnityEngine;

// 오브젝트 풀(재사용 창고) 관리자 — 씬에 붙이지 않는 정적 클래스
// 자주 생겼다 사라지는 오브젝트(데미지 숫자, 부적, 이펙트 등)를 파괴하지 않고 창고에 꺼 두었다가 다시 씁니다.
// → 매번 새로 만들고 지울 때 생기는 메모리 청소(GC) 끊김을 줄임 (모바일·귀신 떼 대비)
// 사용법:
//   생성: ObjectPoolManager.Spawn(프리팹, 위치, 회전)   ← Instantiate 대신
//   반납: ObjectPoolManager.Release(오브젝트)           ← Destroy(오브젝트) 대신
//         ObjectPoolManager.Release(오브젝트, 초)       ← Destroy(오브젝트, 초) 대신
// 다시 꺼낼 때마다 OnEnable이 다시 실행되므로, 풀에 넣는 스크립트는 상태 초기화를 Awake/Start가 아니라 OnEnable에서 합니다.
// 풀 오브젝트는 씬이 바뀌어도 사라지지 않도록 DontDestroyOnLoad의 [ObjectPool] 아래에 모입니다 (쉬는 것은 [ObjectPool]/Storage).
public static class ObjectPoolManager
{
    private const int MaxStoredPerPrefab = 200; // 프리팹마다 창고에 보관하는 최대 개수 (넘치면 그냥 파괴)

    private static readonly Dictionary<GameObject, Stack<PooledObject>> storedByPrefab = new Dictionary<GameObject, Stack<PooledObject>>();
    private static Transform activeRoot;  // 사용 중인 오브젝트를 모아 두는 곳 ([ObjectPool])
    private static Transform storageRoot; // 쉬는 오브젝트를 모아 두는 꺼진 창고 ([ObjectPool]/Storage)

    // 플레이를 시작할 때마다 이전 플레이의 기록을 비움 (에디터 설정에 따라 정적 변수가 남아 있을 수 있음)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        storedByPrefab.Clear();
        activeRoot = null;
        storageRoot = null;
    }

    // 프리팹의 복제본을 꺼냄 (창고에 쉬는 것이 있으면 재사용, 없으면 새로 만듦)
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogWarning("ObjectPoolManager: 생성할 프리팹이 비어 있습니다.");
            return null;
        }

        EnsureRoots();

        Stack<PooledObject> stored = GetStack(prefab);
        PooledObject pooled = null;
        while (pooled == null && stored.Count > 0)
        {
            pooled = stored.Pop(); // 실수로 파괴된 것은 null이므로 건너뜀
        }

        if (pooled == null)
        {
            // 꺼진 창고 안에서 만들면 Awake·OnEnable이 아직 실행되지 않음 → 표시(PooledObject)를 먼저 붙인 뒤 깨움
            GameObject created = Object.Instantiate(prefab, position, rotation, storageRoot);
            if (!created.TryGetComponent(out pooled))
            {
                pooled = created.AddComponent<PooledObject>();
            }
            pooled.SourcePrefab = prefab;
        }
        else
        {
            pooled.transform.SetPositionAndRotation(position, rotation);
        }

        pooled.MarkSpawned();
        pooled.transform.SetParent(activeRoot, true); // 켜진 곳으로 옮기는 순간 OnEnable 실행 (처음이면 Awake도)
        return pooled.gameObject;
    }

    // 바로 창고로 돌려보냄 (풀로 만들지 않은 오브젝트는 그냥 파괴)
    public static void Release(GameObject instance)
    {
        if (instance == null) return;

        if (instance.TryGetComponent(out PooledObject pooled) && pooled.SourcePrefab != null)
        {
            pooled.ReturnToPool();
        }
        else
        {
            Object.Destroy(instance);
        }
    }

    // delay초 뒤에 창고로 돌려보냄 (풀로 만들지 않은 오브젝트는 delay초 뒤 파괴)
    public static void Release(GameObject instance, float delay)
    {
        if (instance == null) return;

        if (instance.TryGetComponent(out PooledObject pooled) && pooled.SourcePrefab != null)
        {
            pooled.ReturnAfter(delay);
        }
        else
        {
            Object.Destroy(instance, delay);
        }
    }

    // PooledObject가 반납될 때 호출: 꺼진 창고로 옮기고(OnDisable 실행) 다음에 다시 쓰도록 보관
    internal static void Store(PooledObject pooled)
    {
        if (pooled == null) return;

        Stack<PooledObject> stored = GetStack(pooled.SourcePrefab);
        if (storageRoot == null || stored.Count >= MaxStoredPerPrefab)
        {
            Object.Destroy(pooled.gameObject);
            return;
        }

        pooled.transform.SetParent(storageRoot, true);
        stored.Push(pooled);
    }

    private static Stack<PooledObject> GetStack(GameObject prefab)
    {
        if (!storedByPrefab.TryGetValue(prefab, out Stack<PooledObject> stored))
        {
            stored = new Stack<PooledObject>();
            storedByPrefab.Add(prefab, stored);
        }
        return stored;
    }

    // 처음 쓸 때 [ObjectPool]과 꺼진 창고 Storage를 만듦
    private static void EnsureRoots()
    {
        if (activeRoot != null && storageRoot != null) return;

        GameObject root = new GameObject("[ObjectPool]");
        Object.DontDestroyOnLoad(root);
        activeRoot = root.transform;

        GameObject storage = new GameObject("Storage");
        storage.transform.SetParent(activeRoot, false);
        storage.SetActive(false);
        storageRoot = storage.transform;
    }
}
