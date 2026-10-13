using UnityEngine;

// 오브젝트 풀에서 만든 오브젝트라는 표시 (ObjectPoolManager가 자동으로 붙임 — 직접 붙이지 않음)
// 어느 프리팹의 창고로 돌아갈지 기억하고, "몇 초 뒤 반납" 시간을 잽니다.
[DisallowMultipleComponent]
public class PooledObject : MonoBehaviour
{
    public GameObject SourcePrefab { get; internal set; } // 이 오브젝트를 만든 원본 프리팹
    public bool IsInPool { get; private set; }            // 지금 창고에서 쉬고 있는지

    private float returnTime = -1f; // 반납할 시각 (-1 = 예약 없음)

    // 창고에서 꺼낼 때 (OnEnable보다 먼저 호출됨)
    internal void MarkSpawned()
    {
        IsInPool = false;
        returnTime = -1f;
    }

    // seconds초 뒤 반납 예약 (다시 호출하면 마지막 예약으로 바뀜)
    public void ReturnAfter(float seconds)
    {
        if (IsInPool) return;
        returnTime = Time.time + Mathf.Max(0f, seconds);
    }

    // 지금 바로 반납 (이미 창고에 있으면 무시)
    public void ReturnToPool()
    {
        if (IsInPool) return;

        IsInPool = true;
        returnTime = -1f;
        ObjectPoolManager.Store(this);
    }

    private void Update()
    {
        if (returnTime >= 0f && Time.time >= returnTime)
        {
            ReturnToPool();
        }
    }
}
