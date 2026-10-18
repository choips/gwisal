using UnityEditor;
using UnityEngine;

// 에디터 전용 도구 (게임 빌드에는 포함되지 않음 — Editor 폴더 안에 있기 때문)
// 메뉴 [귀살 → 적 데이터 만들기 (슬라이스)]을 누르면 슬라이스 귀신 EnemyData를
// Assets/Data/Enemies에 만들고, Assets/Resources/EnemyDatabase.asset 목록에도 넣습니다.
// 같은 이름의 파일이 이미 있으면 건드리지 않습니다 (직접 고친 숫자를 덮어쓰지 않기 위함).
// 수치 = 밸런스 엑셀 "귀신" 시트
public static class EnemyDataCreator
{
    private const string FolderParent = "Assets/Data";
    private const string FolderName = "Enemies";
    private const string FolderPath = FolderParent + "/" + FolderName;
    private const string DatabasePath = "Assets/Resources/" + EnemyDatabase.ResourcePath + ".asset";

    [MenuItem("귀살/적 데이터 만들기 (슬라이스)")]
    private static void CreateSliceEnemyData()
    {
        if (!AssetDatabase.IsValidFolder(FolderParent)) AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(FolderPath)) AssetDatabase.CreateFolder(FolderParent, FolderName);
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        int created = 0;
        //                  파일 이름            ID          이름    몸 색                                    크기                          체력  공격 쿨타임 속도  경험치 호출반경 울기시간
        created += Create("Enemy_Mangja",   "mangja",   "망자", new Color(0.55f, 0.55f, 0.58f), Vector3.one,                      30f, 5f, 1.5f, 3.5f, 50f, 0f,  0f);
        // 창귀: 마른 체형·흐린 주황빛, 부르는 소리 반경 12m·0.5초 (기획 04 S2, 엑셀 changgwi)
        created += Create("Enemy_Changgwi", "changgwi", "창귀", new Color(0.95f, 0.55f, 0.25f), new Vector3(0.65f, 0.8f, 0.65f), 18f, 4f, 1f,   5.5f, 35f, 12f, 0.5f);
        // 도깨비불·산군은 각 S2 항목에서 추가

        int added = FillDatabase();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"적 데이터: 새로 만든 파일 {created}개 ({FolderPath}), EnemyDatabase 목록에 새로 넣은 귀신 {added}개 ({DatabasePath}). 이미 있던 파일은 그대로 두었습니다.");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(FolderPath);
    }

    // EnemyDatabase가 없으면 만들고, Assets/Data/Enemies 안의 모든 EnemyData 중 빠진 것을 목록에 넣음
    private static int FillDatabase()
    {
        EnemyDatabase database = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<EnemyDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:EnemyData", new[] { FolderPath }))
        {
            EnemyData enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
            if (database.EditorAdd(enemy)) added++;
        }

        EditorUtility.SetDirty(database);
        return added;
    }

    // 파일 하나 만들기 (이미 있으면 0, 새로 만들면 1을 돌려줌)
    private static int Create(string fileName, string id, string displayName, Color color, Vector3 scale,
                              float health, float damage, float cooldown, float speed, float experience,
                              float callRadius = 0f, float callDuration = 0f)
    {
        string path = $"{FolderPath}/{fileName}.asset";
        if (AssetDatabase.LoadAssetAtPath<EnemyData>(path) != null) return 0;

        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        data.enemyId = id;
        data.displayName = displayName;
        data.bodyColor = color;
        data.bodyScale = scale;
        data.maxHealth = health;
        data.attackDamage = damage;
        data.attackCooldown = cooldown;
        data.moveSpeed = speed;
        data.experienceReward = experience;
        data.callRadius = callRadius;
        data.callDuration = callDuration;

        AssetDatabase.CreateAsset(data, path);
        return 1;
    }
}
