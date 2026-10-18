using System.Collections;           // IEnumerator 사용 (지역 준비를 기다리는 Start)
using System.Collections.Generic;
using TMPro;                     // TMP_FontAsset 사용 (머리 위 이름표 글꼴)
using UnityEngine;
using UnityEngine.AI;            // NavMesh 사용

// 몬스터 무리 무작위 배치기 — 지금은 귀문 균열용으로 보관 (스토리 지역은 고정 배치 무리 EnemyPack)
// 게임이 시작되면 맵(NavMesh) 전체에 몬스터 무리를 무작위 개수로 한 번 배치합니다. (다시 채우지 않음)
// - 일반 무리: 같은 종류의 일반 몬스터 여러 마리
// - 정예 무리: 정예 1마리 + 일반 여러 마리 (이름표 "정예 ○○")
// - 이름 있는 정예는 무작위로 만들지 않음 — 정해진 자리의 EnemyPack만 사용 (📬 전달-26)
// 무리마다 귀신 종류(EnemyData)를 출현 가중치 확률로 하나 골라 그 종류로만 채웁니다.
// 몬스터 만들기(등급 배율·색·크기·이름표)는 MonsterFactory, 등급 배율 숫자는 BalanceConfig에서 가져옵니다.
// 한 마리가 깨어나면 같은 무리가 함께 깨어납니다.
public class EnemySpawner : MonoBehaviour, IEnemyOwner
{
    [Header("몬스터")]
    [Tooltip("생성할 적 프리팹 (Enemy 스크립트와 NavMeshAgent가 붙어 있어야 합니다)")]
    public GameObject enemyPrefab;

    [Tooltip("출현할 귀신 종류 (데이터 파일 Assets/Data/Enemies + 출현 가중치)")]
    [SerializeField] private List<EnemySpawnEntry> enemyTypes = new List<EnemySpawnEntry>();

    [Tooltip("이 배치기가 만드는 몬스터의 레벨 (체력·피해 배율, 떨어지는 아이템 레벨)")]
    [Min(1)] [SerializeField] private int monsterLevel = 1;

    [Header("일반 무리")]
    [Tooltip("맵에 배치할 일반 무리 수 (최소 ~ 최대 사이에서 무작위)")]
    [SerializeField] private IntRange normalPackCount = new IntRange(6, 9);

    [Tooltip("일반 무리 하나의 몬스터 수")]
    [SerializeField] private IntRange normalPackSize = new IntRange(3, 5);

    [Header("정예 무리")]
    [SerializeField] private IntRange elitePackCount = new IntRange(1, 3);

    [Tooltip("정예 하나에 따라다니는 일반 수")]
    [SerializeField] private IntRange eliteEscortCount = new IntRange(2, 3);

    [Header("배치")]
    [Tooltip("플레이어 시작 위치와 이 거리(m)보다 가까운 곳에는 무리를 두지 않음")]
    public float minDistanceFromPlayer = 18f;

    [Tooltip("무리와 무리 사이의 최소 거리(m)")]
    [SerializeField] private float packSpacing = 14f;

    [Tooltip("무리 중심에서 몬스터들이 흩어지는 반경(m)")]
    [SerializeField] private float packSpread = 2.5f;

    [Tooltip("맵 가장자리에서 이만큼(m) 안쪽에만 배치")]
    [SerializeField] private float mapEdgeMargin = 3f;

    [Tooltip("무리 하나의 자리를 찾을 때 무작위 위치를 뽑아 보는 최대 횟수")]
    [SerializeField] private int maxPlacementAttempts = 40;

    [Header("머리 위 이름표 (정예·하수인만)")]
    [Tooltip("비워 두면 Enemy 프리팹의 LootDropper에 지정된 글꼴(NotoSansKR)을 사용")]
    [SerializeField] private TMP_FontAsset labelFont;

    [Header("상태 (Play 중 확인용)")]
    [Tooltip("이 배치기가 만든 몬스터 중 살아 있는 수")]
    [SerializeField] private int currentEnemyCount;

    public int CurrentEnemyCount => currentEnemyCount;

    private readonly List<Vector3> packCenters = new List<Vector3>(); // 이미 배치한 무리 중심들
    private readonly List<Enemy> spawnedEnemies = new List<Enemy>();  // 이 배치기가 꺼낸 살아 있는 적 (지역을 떠날 때 창고로 돌려보냄)
    private readonly Dictionary<Enemy, List<Enemy>> packByEnemy = new Dictionary<Enemy, List<Enemy>>(); // 적 → 그 적이 속한 무리 (함께 깨우기용)
    private readonly List<EnemySpawnEntry> activeTypes = new List<EnemySpawnEntry>(); // 실제로 뽑을 귀신 종류 (빈 칸 제외)
    private Bounds mapBounds;     // NavMesh 전체를 감싸는 범위
    private Transform player;
    private BalanceConfig config; // 레벨 성장·등급 배율 (플레이어가 가진 밸런스 숫자 모음)
    private bool hasPopulated;    // 배치를 마쳤는지 (마지막 한 마리 처치 알림용)

    // 적은 풀(DontDestroyOnLoad의 [ObjectPool]) 아래에 있어 지역 씬을 내려도 함께 사라지지 않으므로,
    // 배치기가 사라질 때(= 지역을 떠날 때) 남은 적을 직접 창고로 돌려보냄
    private void OnDestroy()
    {
        if (ObjectPoolManager.IsShuttingDown) return;

        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (spawnedEnemies[i] != null) spawnedEnemies[i].Despawn();
        }
        spawnedEnemies.Clear();
        packByEnemy.Clear();
    }

    // 지역 씬이 막 불러와진 순간에는 플레이어가 아직 입구로 옮겨지기 전이므로,
    // RegionManager가 지역 준비(활성 씬 지정·플레이어 배치)를 마칠 때까지 기다렸다가 배치함
    private IEnumerator Start()
    {
        while (RegionManager.IsBusy) yield return null;

        if (enemyPrefab == null)
        {
            Debug.LogWarning($"{gameObject.name}: Enemy Prefab이 연결되지 않아 몬스터를 배치하지 않습니다.");
            yield break;
        }

        if (labelFont == null && enemyPrefab.TryGetComponent(out LootDropper lootDropper))
        {
            labelFont = lootDropper.LabelFont;
        }

        PlayerStats stats = FindFirstObjectByType<PlayerStats>();
        if (stats != null)
        {
            player = stats.transform;
            if (stats.Stats != null) config = stats.Stats.Config;
        }

        BuildActiveTypes();
        if (activeTypes.Count == 0)
        {
            Debug.LogWarning($"{gameObject.name}: 출현할 귀신 종류가 없어 몬스터를 배치하지 않습니다. Enemy Types에 데이터 파일을 넣어 주세요.");
            yield break;
        }

        PopulateMap();
    }

    // 뽑을 귀신 종류 목록 만들기 (데이터 파일이 비었거나 가중치가 0인 칸은 뺌)
    private void BuildActiveTypes()
    {
        activeTypes.Clear();
        foreach (EnemySpawnEntry entry in enemyTypes)
        {
            if (entry != null && entry.data != null && entry.spawnWeight > 0f) activeTypes.Add(entry);
        }
    }

    // 맵 전체에 무리 배치 (강한 무리가 좋은 자리를 먼저 차지하도록 정예 → 일반 순서)
    private void PopulateMap()
    {
        if (!TryCalculateMapBounds())
        {
            Debug.LogWarning($"{gameObject.name}: 구워진 NavMesh가 없어 몬스터를 배치할 수 없습니다. Plane의 NavMesh Surface에서 Bake를 눌러 주세요.");
            return;
        }

        int elitePacks = PlacePacks(elitePackCount.Roll(), SpawnElitePack);
        int normalPacks = PlacePacks(normalPackCount.Roll(), SpawnNormalPack);

        hasPopulated = true;
        GameLog.Region($"몬스터 배치 완료 (레벨 {monsterLevel}): 일반 무리 {normalPacks}개, 정예 무리 {elitePacks}개 (총 {currentEnemyCount}마리)");
        // 이름 있는 정예는 무작위 배치하지 않으므로 두 번째 인자는 0
        NotificationUI.Show(Loc.Format("notice.area_threats", elitePacks, 0));
    }

    // count개의 무리 자리를 찾아 spawnPack으로 채움 (실제로 배치한 무리 수를 돌려줌)
    private int PlacePacks(int count, System.Action<Vector3> spawnPack)
    {
        int placed = 0;
        for (int i = 0; i < count; i++)
        {
            if (!TryFindPackCenter(out Vector3 center))
            {
                Debug.LogWarning($"{gameObject.name}: 무리를 놓을 빈자리가 부족합니다. 맵을 키우거나 Pack Spacing을 줄여 주세요.");
                break;
            }

            packCenters.Add(center);
            spawnPack(center);
            placed++;
        }
        return placed;
    }

    private void SpawnNormalPack(Vector3 center)
    {
        EnemyData data = PickEnemyType();
        List<Enemy> pack = new List<Enemy>();
        int size = normalPackSize.Roll();
        for (int i = 0; i < size; i++)
        {
            SpawnMonster(GetMemberPosition(center), data, MonsterRank.Normal, null, pack);
        }
    }

    private void SpawnElitePack(Vector3 center)
    {
        EnemyData data = PickEnemyType();
        List<Enemy> pack = new List<Enemy>();
        string eliteName = MonsterFactory.MakeEliteName(data);
        SpawnMonster(center, data, MonsterRank.Elite, eliteName, pack);

        int escorts = eliteEscortCount.Roll();
        for (int i = 0; i < escorts; i++)
        {
            SpawnMonster(GetMemberPosition(center), data, MonsterRank.Normal, null, pack);
        }
    }

    // 몬스터 한 마리 생성 (MonsterFactory) → 이 배치기와 무리에 등록
    private void SpawnMonster(Vector3 position, EnemyData data, MonsterRank rank, string labelText, List<Enemy> pack)
    {
        Enemy enemy = MonsterFactory.Spawn(enemyPrefab, data, monsterLevel, rank, labelText, position, config, labelFont);
        if (enemy == null) return;

        enemy.SetOwner(this);
        spawnedEnemies.Add(enemy);
        pack.Add(enemy);
        packByEnemy[enemy] = pack;
        currentEnemyCount++;
    }

    // 이 배치기가 만든 적이 죽었을 때 Enemy가 호출
    public void OnEnemyDied(Enemy enemy)
    {
        spawnedEnemies.Remove(enemy);
        if (packByEnemy.TryGetValue(enemy, out List<Enemy> pack))
        {
            pack.Remove(enemy);
            packByEnemy.Remove(enemy);
        }

        currentEnemyCount = Mathf.Max(currentEnemyCount - 1, 0);

        if (hasPopulated && currentEnemyCount == 0)
        {
            NotificationUI.Show(Loc.Get("notice.area_cleared"), NotificationUI.NoticeType.Success);
        }
    }

    // 한 마리가 깨어나면 같은 무리를 모두 깨움
    public void OnEnemyAlerted(Enemy enemy)
    {
        if (!packByEnemy.TryGetValue(enemy, out List<Enemy> pack)) return;

        foreach (Enemy member in pack)
        {
            if (member != null && member != enemy) member.WakeUp();
        }
    }

    // 구워진 NavMesh의 모든 꼭짓점을 감싸는 범위 = 몬스터를 둘 수 있는 맵 범위
    private bool TryCalculateMapBounds()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.vertices == null || triangulation.vertices.Length == 0) return false;

        mapBounds = new Bounds(triangulation.vertices[0], Vector3.zero);
        foreach (Vector3 vertex in triangulation.vertices)
        {
            mapBounds.Encapsulate(vertex);
        }
        return true;
    }

    // 맵 안에서 플레이어·다른 무리와 충분히 떨어진 NavMesh 위 지점을 찾음
    private bool TryFindPackCenter(out Vector3 center)
    {
        float minX = mapBounds.min.x + mapEdgeMargin;
        float maxX = mapBounds.max.x - mapEdgeMargin;
        float minZ = mapBounds.min.z + mapEdgeMargin;
        float maxZ = mapBounds.max.z - mapEdgeMargin;

        for (int i = 0; i < maxPlacementAttempts; i++)
        {
            Vector3 candidate = new Vector3(Random.Range(minX, maxX), mapBounds.center.y, Random.Range(minZ, maxZ));
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;

            if (player != null && GetFlatDistance(hit.position, player.position) < minDistanceFromPlayer) continue;
            if (IsTooCloseToOtherPacks(hit.position)) continue;

            center = hit.position;
            return true;
        }

        center = Vector3.zero;
        return false;
    }

    private bool IsTooCloseToOtherPacks(Vector3 position)
    {
        foreach (Vector3 other in packCenters)
        {
            if (GetFlatDistance(position, other) < packSpacing) return true;
        }
        return false;
    }

    // 무리 중심 주변의 NavMesh 위 무작위 위치 (못 찾으면 중심)
    private Vector3 GetMemberPosition(Vector3 center)
    {
        Vector2 offset = Random.insideUnitCircle * packSpread;
        Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
        return NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, NavMesh.AllAreas) ? hit.position : center;
    }

    // 출현 가중치 확률로 귀신 종류 하나 고르기 (activeTypes에는 빈 칸·가중치 0이 없음 — Start에서 걸러 둠)
    private EnemyData PickEnemyType()
    {
        float totalWeight = 0f;
        foreach (EnemySpawnEntry entry in activeTypes)
        {
            totalWeight += entry.spawnWeight;
        }

        float roll = Random.value * totalWeight;
        foreach (EnemySpawnEntry entry in activeTypes)
        {
            roll -= entry.spawnWeight;
            if (roll < 0f) return entry.data;
        }
        return activeTypes[activeTypes.Count - 1].data; // 계산 오차로 끝까지 못 고른 경우 마지막 종류
    }

    // 높이 차이는 무시한 바닥 기준 거리
    private static float GetFlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // Play 중 Scene 뷰에 무리 위치(주황 원)와 플레이어 주변 "배치 금지" 범위(노란 원)를 표시
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = new Color(1f, 0.5f, 0f);
        foreach (Vector3 center in packCenters)
        {
            DrawCircle(center, packSpread + 0.5f, 24);
        }

        if (player != null)
        {
            Gizmos.color = Color.yellow;
            DrawCircle(player.position, minDistanceFromPlayer, 48);
        }
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

// 배치기의 출현 목록 한 칸: 어떤 귀신이(데이터 파일) 얼마나 자주(가중치) 나오는지
// 같은 귀신이라도 지역마다 출현 빈도가 다를 수 있어 가중치는 데이터 파일이 아니라 여기에 둠
[System.Serializable]
public class EnemySpawnEntry
{
    [Tooltip("귀신 데이터 파일 (Assets/Data/Enemies)")]
    public EnemyData data;

    [Tooltip("출현 가중치 (클수록 자주 나옴, 0이면 안 나옴)")]
    [Min(0f)] public float spawnWeight = 10f;
}
