using System.Collections.Generic;
using UnityEngine;

// 모든 귀신 종류(EnemyData) 목록 — ID로 귀신 데이터를 찾는 사전
// 파일은 반드시 Assets/Resources/EnemyDatabase.asset 한 개만 둡니다 (개발자 메뉴 소환·나중에 배치기가 Resources에서 이 이름으로 불러올 수 있음).
// 새 귀신 데이터를 만들면 메뉴 [귀살 → 적 데이터 만들기]로 목록에 넣거나, Inspector에서 Enemies 목록에 추가합니다.
[CreateAssetMenu(fileName = "EnemyDatabase", menuName = "귀살/Enemy Database")]
public class EnemyDatabase : ScriptableObject
{
    public const string ResourcePath = "EnemyDatabase"; // Resources 폴더 안의 파일 이름 (확장자 제외)

    [Tooltip("게임에 등장하는 모든 귀신 종류 (메뉴 [귀살 → 적 데이터 만들기]가 자동으로 채움)")]
    [SerializeField] private List<EnemyData> enemies = new List<EnemyData>();

    private Dictionary<string, EnemyData> enemiesById; // ID → 귀신 데이터 (처음 찾을 때 한 번 만듦)

    private static EnemyDatabase instance;

    // 어디서든 EnemyDatabase.Instance로 접근 (처음 한 번만 Resources에서 불러옴)
    public static EnemyDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<EnemyDatabase>(ResourcePath);
                if (instance == null)
                {
                    Debug.LogError($"Assets/Resources/{ResourcePath}.asset 파일이 없습니다. 메뉴 [귀살 → 적 데이터 만들기 (슬라이스)]을 눌러 만들어 주세요.");
                }
            }
            return instance;
        }
    }

    public IReadOnlyList<EnemyData> Enemies => enemies;

    // ID로 귀신 데이터 찾기 (없으면 null)
    public EnemyData Find(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return null;

        BuildLookups();
        enemiesById.TryGetValue(enemyId, out EnemyData found);
        return found;
    }

    private void BuildLookups()
    {
        if (enemiesById != null) return;

        enemiesById = new Dictionary<string, EnemyData>();

        foreach (EnemyData enemy in enemies)
        {
            if (enemy == null || string.IsNullOrEmpty(enemy.enemyId)) continue;

            if (enemiesById.ContainsKey(enemy.enemyId))
            {
                Debug.LogWarning($"EnemyDatabase에 ID \"{enemy.enemyId}\"가 두 번 들어 있습니다. 앞의 것만 사용합니다.", enemy);
                continue;
            }
            enemiesById[enemy.enemyId] = enemy;
        }
    }

    private void OnValidate()
    {
        enemiesById = null;
    }

#if UNITY_EDITOR
    // 에디터 메뉴가 목록에 귀신을 넣을 때 사용 (이미 있으면 false)
    public bool EditorAdd(EnemyData enemy)
    {
        if (enemy == null || enemies.Contains(enemy)) return false;

        enemies.Add(enemy);
        enemiesById = null;
        return true;
    }
#endif
}
