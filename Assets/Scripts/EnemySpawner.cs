using System.Collections;           // IEnumerator 사용 (지역 준비를 기다리는 Start)
using System.Collections.Generic;
using TMPro;                     // TMP_FontAsset 사용 (머리 위 이름표 글꼴)
using UnityEngine;
using UnityEngine.AI;            // NavMesh 사용

// 몬스터 무리 배치기 (디아블로 방식)
// 게임이 시작되면 맵(NavMesh) 전체에 몬스터 무리를 무작위 개수로 한 번 배치합니다. (다시 채우지 않음)
// - 일반 무리: 같은 종류의 일반 몬스터 여러 마리
// - 챔피언 무리: 챔피언 2~4마리 (일반의 2배 능력, 파란빛, 이름표 "챔피언 ○○")
// - 유니크 무리: 이름 있는 유니크 1마리 (3배, 금빛) + 하수인 3~4마리 (1.5배)
// 무리마다 귀신 종류(잡귀·강시·아귀·도깨비 — EnemyData 파일)를 출현 가중치 확률로 하나 골라 그 종류로만 채웁니다.
public class EnemySpawner : MonoBehaviour, IEnemyOwner
{
    [Header("몬스터")]
    [Tooltip("생성할 적 프리팹 (Enemy 스크립트와 NavMeshAgent가 붙어 있어야 합니다)")]
    public GameObject enemyPrefab;

    [Tooltip("출현할 귀신 종류 (데이터 파일 Assets/Data/Enemies + 출현 가중치)")]
    [SerializeField] private List<EnemySpawnEntry> enemyTypes = new List<EnemySpawnEntry>();

    [Tooltip("이 배치기가 만드는 몬스터의 레벨 (레벨마다 체력 ×1.12·피해 ×1.09, 플레이어 방어력 계산에도 쓰임)")]
    [Min(1)] [SerializeField] private int monsterLevel = 1;

    [Header("일반 무리")]
    [Tooltip("맵에 배치할 일반 무리 수 (최소 ~ 최대 사이에서 무작위)")]
    [SerializeField] private IntRange normalPackCount = new IntRange(6, 9);

    [Tooltip("일반 무리 하나의 몬스터 수")]
    [SerializeField] private IntRange normalPackSize = new IntRange(3, 5);

    [Header("챔피언 무리")]
    [SerializeField] private IntRange championPackCount = new IntRange(1, 3);

    [Tooltip("챔피언 무리 하나의 챔피언 수")]
    [SerializeField] private IntRange championPackSize = new IntRange(2, 4);

    [Tooltip("일반 몬스터 대비 능력 배율 (체력·공격력·경험치)")]
    [SerializeField] private float championPower = 2f;

    [Tooltip("처치 시 떨어뜨리는 장비 개수")]
    [SerializeField] private int championLootDrops = 2;

    [Tooltip("좋은 등급(매직·레어·유니크)이 나올 확률 배율")]
    [SerializeField] private float championRarityBonus = 2f;

    [Header("유니크 무리")]
    [SerializeField] private IntRange uniquePackCount = new IntRange(1, 2);

    [Tooltip("유니크 하나를 따르는 하수인 수")]
    [SerializeField] private IntRange minionCount = new IntRange(3, 4);

    [Tooltip("유니크의 능력 배율 (체력·공격력·경험치)")]
    [SerializeField] private float uniquePower = 3f;

    [Tooltip("하수인의 능력 배율 (체력·공격력·경험치)")]
    [SerializeField] private float minionPower = 1.5f;

    [SerializeField] private int uniqueLootDrops = 3;
    [SerializeField] private float uniqueRarityBonus = 3f;
    [SerializeField] private int minionLootDrops = 1;
    [SerializeField] private float minionRarityBonus = 1.5f;

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

    [Header("머리 위 이름표 (챔피언·유니크·하수인만)")]
    [Tooltip("비워 두면 Enemy 프리팹의 LootDropper에 지정된 글꼴(NotoSansKR)을 사용")]
    [SerializeField] private TMP_FontAsset labelFont;
    [SerializeField] private float labelFontSize = 3.5f;

    [Tooltip("몸 중심에서 이름표까지의 높이(m), 몸 크기에 맞춰 자동으로 늘어남")]
    [SerializeField] private float labelHeight = 1.7f;

    [SerializeField] private Color championColor = new Color(0.4f, 0.6f, 1f);   // 파랑
    [SerializeField] private Color uniqueColor = new Color(1f, 0.8f, 0.2f);     // 금색
    [SerializeField] private Color minionColor = new Color(0.85f, 0.85f, 0.85f); // 밝은 회색

    [Header("상태 (Play 중 확인용)")]
    [Tooltip("이 배치기가 만든 몬스터 중 살아 있는 수")]
    [SerializeField] private int currentEnemyCount;

    public int CurrentEnemyCount => currentEnemyCount;

    // 유니크 이름을 만들 때 쓰는 앞말·뒷말 (예: "천년 묵은 강시 왕")
    // 화면 글자 표의 enemy.unique_prefix.0~5, enemy.unique_suffix.0~2 (표에 줄을 늘리면 이 숫자도 함께 늘림)
    private const int UniquePrefixCount = 6;
    private const int UniqueSuffixCount = 3;

    private readonly List<Vector3> packCenters = new List<Vector3>(); // 이미 배치한 무리 중심들
    private readonly List<Enemy> spawnedEnemies = new List<Enemy>();  // 이 배치기가 꺼낸 살아 있는 적 (지역을 떠날 때 창고로 돌려보냄)
    private readonly List<EnemySpawnEntry> activeTypes = new List<EnemySpawnEntry>(); // 실제로 뽑을 귀신 종류 (빈 칸 제외)
    private Bounds mapBounds;     // NavMesh 전체를 감싸는 범위
    private Transform player;
    private bool hasPopulated;    // 배치를 마쳤는지 (마지막 한 마리 처치 알림용)
    private float levelHealthMultiplier = 1f; // 몬스터 레벨에 따른 체력 배율
    private float levelDamageMultiplier = 1f; // 몬스터 레벨에 따른 피해 배율

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
        if (stats != null) player = stats.transform;

        BuildActiveTypes();
        if (activeTypes.Count == 0)
        {
            Debug.LogWarning($"{gameObject.name}: 출현할 귀신 종류가 없어 몬스터를 배치하지 않습니다. Enemy Types에 데이터 파일을 넣어 주세요.");
            yield break;
        }

        // 몬스터 레벨 배율은 플레이어가 가진 밸런스 숫자 모음(BalanceConfig)에서 계산
        BalanceConfig config = stats != null && stats.Stats != null ? stats.Stats.Config : null;
        if (config != null)
        {
            levelHealthMultiplier = config.GetEnemyHealthMultiplier(monsterLevel);
            levelDamageMultiplier = config.GetEnemyDamageMultiplier(monsterLevel);
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

    // 맵 전체에 무리 배치 (강한 무리가 좋은 자리를 먼저 차지하도록 유니크 → 챔피언 → 일반 순서)
    private void PopulateMap()
    {
        if (!TryCalculateMapBounds())
        {
            Debug.LogWarning($"{gameObject.name}: 구워진 NavMesh가 없어 몬스터를 배치할 수 없습니다. Plane의 NavMesh Surface에서 Bake를 눌러 주세요.");
            return;
        }

        int uniquePacks = PlacePacks(uniquePackCount.Roll(), SpawnUniquePack);
        int championPacks = PlacePacks(championPackCount.Roll(), SpawnChampionPack);
        int normalPacks = PlacePacks(normalPackCount.Roll(), SpawnNormalPack);

        hasPopulated = true;
        GameLog.Region($"몬스터 배치 완료 (레벨 {monsterLevel}): 일반 무리 {normalPacks}개, 챔피언 무리 {championPacks}개, 유니크 무리 {uniquePacks}개 (총 {currentEnemyCount}마리)");
        NotificationUI.Show(Loc.Format("notice.area_threats", championPacks, uniquePacks));
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
        int size = normalPackSize.Roll();
        for (int i = 0; i < size; i++)
        {
            SpawnMonster(GetMemberPosition(center), data, MonsterRank.Normal, null, Color.white);
        }
    }

    private void SpawnChampionPack(Vector3 center)
    {
        EnemyData data = PickEnemyType();
        string championName = Loc.Format("enemy.champion_name", data.LocalizedName);
        int size = championPackSize.Roll();
        for (int i = 0; i < size; i++)
        {
            SpawnMonster(GetMemberPosition(center), data, MonsterRank.Champion, championName, championColor);
        }
    }

    private void SpawnUniquePack(Vector3 center)
    {
        EnemyData data = PickEnemyType();
        string uniqueName = MakeUniqueName(data);

        SpawnMonster(center, data, MonsterRank.Unique, uniqueName, uniqueColor);

        string minionName = Loc.Format("enemy.minion_name", data.LocalizedName);
        int minions = minionCount.Roll();
        for (int i = 0; i < minions; i++)
        {
            SpawnMonster(GetMemberPosition(center), data, MonsterRank.Minion, minionName, minionColor);
        }
    }

    // 몬스터 한 마리 생성 → 데이터·레벨 적용 → 등급 적용 → 모양·이름표
    private void SpawnMonster(Vector3 position, EnemyData data, MonsterRank rank, string labelText, Color labelColor)
    {
        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject monsterObject = ObjectPoolManager.Spawn(enemyPrefab, position, rotation);
        if (monsterObject == null) return;

        if (!monsterObject.TryGetComponent(out Enemy enemy))
        {
            Debug.LogWarning($"{enemyPrefab.name} 프리팹에 Enemy 스크립트가 없어 몬스터로 쓸 수 없습니다.");
            ObjectPoolManager.Release(monsterObject);
            return;
        }

        string displayName = string.IsNullOrEmpty(labelText) ? data.LocalizedName : labelText;
        monsterObject.name = displayName;

        enemy.SetOwner(this);
        enemy.ApplyData(data, monsterLevel, levelHealthMultiplier, levelDamageMultiplier);
        enemy.ApplyRank(rank, GetPower(rank), GetLootDrops(rank), GetRarityBonus(rank));
        enemy.SetBodyColor(GetBodyColor(data.bodyColor, rank));

        float rankScale = GetRankScale(rank);
        Vector3 bodyScale = Vector3.Scale(monsterObject.transform.localScale, data.bodyScale) * rankScale;
        monsterObject.transform.localScale = bodyScale;

        if (!string.IsNullOrEmpty(labelText) && labelFont != null)
        {
            monsterObject.AddComponent<ItemNameLabel>().Setup(
                labelText, labelColor, labelFont, labelFontSize, labelHeight * bodyScale.y);
        }

        spawnedEnemies.Add(enemy);
        currentEnemyCount++;
    }

    // 이 배치기가 만든 적이 죽었을 때 Enemy가 호출
    public void OnEnemyDied(Enemy enemy)
    {
        spawnedEnemies.Remove(enemy);
        currentEnemyCount = Mathf.Max(currentEnemyCount - 1, 0);

        if (hasPopulated && currentEnemyCount == 0)
        {
            NotificationUI.Show(Loc.Get("notice.area_cleared"), NotificationUI.NoticeType.Success);
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

    private static string MakeUniqueName(EnemyData data)
    {
        string prefix = Loc.Get("enemy.unique_prefix." + Random.Range(0, UniquePrefixCount));
        string suffix = Loc.Get("enemy.unique_suffix." + Random.Range(0, UniqueSuffixCount));
        return Loc.Format("enemy.unique_name", prefix, data.LocalizedName, suffix);
    }

    private float GetPower(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Champion: return championPower;
            case MonsterRank.Unique: return uniquePower;
            case MonsterRank.Minion: return minionPower;
            default: return 1f;
        }
    }

    private int GetLootDrops(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Champion: return championLootDrops;
            case MonsterRank.Unique: return uniqueLootDrops;
            case MonsterRank.Minion: return minionLootDrops;
            default: return 1;
        }
    }

    private float GetRarityBonus(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Champion: return championRarityBonus;
            case MonsterRank.Unique: return uniqueRarityBonus;
            case MonsterRank.Minion: return minionRarityBonus;
            default: return 1f;
        }
    }

    // 챔피언은 파란빛, 유니크는 금빛이 돌게 몸 색을 섞음
    private Color GetBodyColor(Color typeColor, MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Champion: return Color.Lerp(typeColor, championColor, 0.5f);
            case MonsterRank.Unique: return Color.Lerp(typeColor, uniqueColor, 0.5f);
            default: return typeColor;
        }
    }

    // 강한 등급일수록 조금 더 크게
    private static float GetRankScale(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Champion: return 1.15f;
            case MonsterRank.Unique: return 1.35f;
            default: return 1f;
        }
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
