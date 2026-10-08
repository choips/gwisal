using System.Collections;
using UnityEngine;
using UnityEngine.AI;            // NavMesh.SamplePosition 사용

// 몬스터 스포너
// 스포너 위치를 중심으로 spawnRadius 안의 NavMesh 위 랜덤 위치에
// spawnInterval마다 적을 생성하며, 동시에 maxEnemies마리까지만 유지합니다.
public class EnemySpawner : MonoBehaviour
{
    [Header("스폰 설정")]
    [Tooltip("생성할 적 프리팹 (Enemy 스크립트와 NavMeshAgent가 붙어 있어야 합니다)")]
    public GameObject enemyPrefab;

    [Tooltip("스포너를 중심으로 적이 생성될 수 있는 반경")]
    public float spawnRadius = 10f; // 스폰 반경

    [Tooltip("적을 생성하는 주기(초)")]
    public float spawnInterval = 3f; // 스폰 간격

    [Tooltip("이 스포너가 동시에 유지할 최대 적 수")]
    public int maxEnemies = 5; // 최대 몬스터 수

    [Header("NavMesh 위치 탐색")]
    [Tooltip("랜덤 위치에서 이 거리 안의 NavMesh 지점을 찾음")]
    [SerializeField] private float navMeshSampleDistance = 2f;

    [Tooltip("NavMesh 위 위치를 찾기 위해 랜덤 위치를 다시 뽑는 최대 횟수")]
    [SerializeField] private int maxSpawnAttempts = 10;

    [Header("상태 (Play 중 확인용)")]
    [Tooltip("이 스포너가 생성해서 현재 살아 있는 적의 수")]
    [SerializeField] private int currentEnemyCount;

    public int CurrentEnemyCount => currentEnemyCount;

    private void Start()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning($"{gameObject.name}: Enemy Prefab이 연결되지 않아 스폰하지 않습니다.");
            return;
        }

        StartCoroutine(SpawnRoutine());
    }

    // spawnInterval마다 적 수를 확인하고, 부족하면 한 마리씩 생성
    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            if (currentEnemyCount < maxEnemies)
            {
                TrySpawnEnemy();
            }

            // 반복할 때마다 새로 만들어서, Play 중에 Inspector에서 간격을 바꿔도 바로 반영
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    // NavMesh 위의 랜덤 위치를 찾아 적 1마리 생성
    private void TrySpawnEnemy()
    {
        if (!TryGetRandomNavMeshPosition(out Vector3 spawnPosition))
        {
            Debug.LogWarning($"{gameObject.name}: 스폰 반경 안에서 NavMesh 위치를 찾지 못했습니다. 스포너가 바닥 근처에 있는지 확인해 주세요.");
            return;
        }

        // 바라보는 방향도 랜덤으로 (Y축 회전만)
        Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject enemyObject = Instantiate(enemyPrefab, spawnPosition, spawnRotation);

        // 적에게 "너를 만든 스포너는 나"라고 알려 줘서, 죽을 때 이 스포너에게 연락하게 함
        if (enemyObject.TryGetComponent(out Enemy enemy))
        {
            enemy.SetSpawner(this);
        }
        else
        {
            Debug.LogWarning($"{enemyPrefab.name} 프리팹에 Enemy 스크립트가 없어 적 수를 추적할 수 없습니다.");
        }

        currentEnemyCount++;
    }

    // 스폰 반경 안의 랜덤 위치 중 NavMesh 위에 있는 지점을 찾음
    private bool TryGetRandomNavMeshPosition(out Vector3 result)
    {
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            // 구 안의 랜덤 위치에서 높이 성분은 버리고 스포너와 같은 높이의 평면 위 점으로 사용
            Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
            randomOffset.y = 0f;
            Vector3 candidate = transform.position + randomOffset;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        result = Vector3.zero;
        return false;
    }

    // 이 스포너가 만든 적이 죽었을 때 Enemy가 호출
    public void OnEnemyDied(Enemy enemy)
    {
        currentEnemyCount = Mathf.Max(currentEnemyCount - 1, 0);
    }

    // Scene 뷰에 스폰 범위를 원으로 표시 (선택하지 않아도 항상 보임)
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f); // 붉은색
        DrawCircle(transform.position, spawnRadius, 64);

        // 스포너 중심 위치 표시
        Gizmos.DrawSphere(transform.position, 0.3f);
    }

    // 선택했을 때는 반투명한 구로 범위를 한 번 더 강조
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.15f);
        Gizmos.DrawSphere(transform.position, spawnRadius);
    }

    // 바닥과 평행한(XZ 평면) 원을 선분 여러 개로 그림
    private static void DrawCircle(Vector3 center, float radius, int segments)
    {
        Vector3 previousPoint = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }
}
