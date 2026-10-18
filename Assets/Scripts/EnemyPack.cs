using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;            // NavMesh 위 위치 찾기

// 고정 배치 귀신 무리 (스토리 지역 씬에 직접 놓는 빈 오브젝트)
// 이 오브젝트 위치를 중심으로, 지역 준비가 끝나면 정해 둔 귀신 무리를 풀에서 꺼내 세웁니다.
// 지역 레벨·귀신 프리팹은 같은 씬의 RegionSettings에서 가져오고, 등급 배율은 BalanceConfig에서 가져옵니다.
// Scene 뷰에는 무리 범위가 원(일반 흰색 / 정예 파랑 / 이름 있는 정예 금색)과 이름으로 표시됩니다.
public class EnemyPack : MonoBehaviour, IEnemyOwner
{
    // 무리 종류 (씬 파일에 번호로 저장되므로 값은 바꾸지 않음: 0=일반, 1=정예, 2=이름 있는 정예)
    // 예전 Champion·Unique는 Elite·NamedElite로만 바꿈 (📬 전달-26)
    public enum PackKind { Normal, Elite, NamedElite }

    [Tooltip("이 무리의 귀신 종류 (Assets/Data/Enemies의 데이터 파일)")]
    [SerializeField] private EnemyData enemyData;

    [Tooltip("일반: 일반 몬스터 무리 / 정예: 정예 1 + 일반들 / 이름 있는 정예: 이름 있는 정예 1 + 하수인")]
    [SerializeField] private PackKind packKind = PackKind.Normal;

    [Tooltip("일반: 몬스터 수 / 정예: 따라다니는 일반 수 (정예는 1마리 고정) / 이름 있는 정예: 하수인 수")]
    [SerializeField] private IntRange memberCount = new IntRange(3, 5);

    [Tooltip("무리 중심에서 몬스터들이 흩어지는 반경(m)")]
    [Min(0f)] [SerializeField] private float spread = 2.5f;

    [Header("상태 (Play 중 확인용)")]
    [Tooltip("이 무리에서 살아 있는 몬스터 수")]
    [SerializeField] private int aliveCount;

    private readonly List<Enemy> members = new List<Enemy>(); // 살아 있는 무리 구성원 (지역을 떠날 때 창고로 돌려보냄)
    private RegionSettings region;

    public PackKind Kind => packKind;
    public int AliveCount => aliveCount;

    // 무리를 채움 (지역 준비가 끝나면 RegionSettings가 한 번 호출). 실제로 세운 몬스터 수를 돌려줌
    public int Populate(RegionSettings owner)
    {
        region = owner;

        if (enemyData == null)
        {
            Debug.LogWarning($"{name}: Enemy Data(귀신 종류)가 비어 있어 무리를 세우지 않습니다.");
            return 0;
        }

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"{name}: 근처(3m)에 NavMesh가 없어 무리를 세우지 않습니다. 무리를 땅 위로 옮겨 주세요.");
            return 0;
        }
        Vector3 center = hit.position;

        int count = memberCount.Roll();
        switch (packKind)
        {
            case PackKind.Elite:
                // 정예 1마리 + 나머지는 일반 (기획: 정예 무리의 다른 귀신은 일반 그대로)
                SpawnMember(MonsterRank.Elite, MonsterFactory.MakeEliteName(enemyData), center);
                for (int i = 0; i < count; i++) SpawnMember(MonsterRank.Normal, null, GetMemberPosition(center));
                break;

            case PackKind.NamedElite:
                // 이름 있는 정예는 정해진 자리에만 (무작위 배치기는 만들지 않음 — 📬 전달-26)
                SpawnMember(MonsterRank.NamedElite, MonsterFactory.MakeNamedEliteName(enemyData), center);
                string minionName = MonsterFactory.MakeMinionName(enemyData);
                for (int i = 0; i < count; i++) SpawnMember(MonsterRank.Minion, minionName, GetMemberPosition(center));
                break;

            default:
                for (int i = 0; i < count; i++) SpawnMember(MonsterRank.Normal, null, GetMemberPosition(center));
                break;
        }

        return aliveCount;
    }

    private void SpawnMember(MonsterRank rank, string labelText, Vector3 position)
    {
        Enemy enemy = region.SpawnMonster(enemyData, rank, labelText, position);
        if (enemy == null) return;

        enemy.SetOwner(this);
        members.Add(enemy);
        aliveCount++;
    }

    // 무리 구성원이 죽었을 때 Enemy가 호출
    public void OnEnemyDied(Enemy enemy)
    {
        members.Remove(enemy);
        aliveCount = Mathf.Max(aliveCount - 1, 0);
        if (region != null) region.OnPackMemberDied();
    }

    // 구성원 한 마리가 깨어나면(플레이어가 다가오거나 맞음) 무리 전체를 깨움
    public void OnEnemyAlerted(Enemy enemy)
    {
        foreach (Enemy member in members)
        {
            if (member != null && member != enemy) member.WakeUp();
        }
    }

    // 적은 풀(DontDestroyOnLoad) 아래에 있어 지역 씬과 함께 사라지지 않으므로, 지역을 떠날 때 남은 구성원을 창고로 돌려보냄
    private void OnDestroy()
    {
        if (ObjectPoolManager.IsShuttingDown) return;

        for (int i = members.Count - 1; i >= 0; i--)
        {
            if (members[i] != null) members[i].Despawn();
        }
        members.Clear();
    }

    // 무리 중심 주변의 NavMesh 위 무작위 위치 (못 찾으면 중심)
    private Vector3 GetMemberPosition(Vector3 center)
    {
        Vector2 offset = Random.insideUnitCircle * spread;
        Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
        return NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, NavMesh.AllAreas) ? hit.position : center;
    }

    // Scene 뷰에 무리 범위(원)와 이름 표시 — 일반 흰색 / 정예 파랑 / 이름 있는 정예 금색
    private void OnDrawGizmos()
    {
        Color color = packKind == PackKind.Elite ? MonsterFactory.EliteColor
                    : packKind == PackKind.NamedElite ? MonsterFactory.NamedEliteColor
                    : Color.white;
        Gizmos.color = color;
        DrawCircle(transform.position, spread + 0.5f, 32);

#if UNITY_EDITOR
        string typeName = enemyData != null ? enemyData.displayName : "(귀신 종류 없음)";
        string kindName = packKind == PackKind.Elite ? "정예"
                        : packKind == PackKind.NamedElite ? "이름 있는 정예" : "일반";
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"{kindName} {typeName} {memberCount.min}~{memberCount.max}");
#endif
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
