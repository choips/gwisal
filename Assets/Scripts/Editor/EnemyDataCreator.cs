using UnityEditor;
using UnityEngine;

// 에디터 전용 도구 (게임 빌드에는 포함되지 않음 — Editor 폴더 안에 있기 때문)
// 메뉴 [귀살 → 적 데이터 만들기 (기존 4종)]을 누르면 처음 4종(잡귀·강시·아귀·도깨비)의 EnemyData 파일을
// Assets/Data/Enemies에 만듭니다. 파일을 실수로 지웠을 때 처음 값으로 다시 만드는 용도입니다.
// 값 = 옛 배치기의 기본값(체력 30, 공격력 5, 쿨타임 1.5초, 속도 3.5, 경험치 50) × 종류별 배율 (2026-10-13 결정: 공격력 기준 5)
// 같은 이름의 파일이 이미 있으면 건드리지 않습니다 (직접 고친 숫자를 덮어쓰지 않기 위함).
public static class EnemyDataCreator
{
    private const string FolderParent = "Assets/Data";
    private const string FolderName = "Enemies";
    private const string FolderPath = FolderParent + "/" + FolderName;

    [MenuItem("귀살/적 데이터 만들기 (기존 4종)")]
    private static void CreateDefaultEnemyData()
    {
        if (!AssetDatabase.IsValidFolder(FolderParent)) AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(FolderPath)) AssetDatabase.CreateFolder(FolderParent, FolderName);

        int created = 0;
        //                  파일 이름        ID          이름      몸 색                              크기(너비, 키, 두께)            체력  공격 쿨타임 속도  경험치
        created += Create("Enemy_Japgwi",   "japgwi",   "잡귀",   new Color(0.45f, 0.55f, 0.45f), new Vector3(1f, 1f, 1f),       30f, 5f, 1.5f,  3.5f,  50f);
        created += Create("Enemy_Gangsi",   "gangsi",   "강시",   new Color(0.55f, 0.6f, 0.8f),   new Vector3(0.8f, 1.4f, 0.8f), 54f, 6f, 1.95f, 2.1f,  70f);
        created += Create("Enemy_Agwi",     "agwi",     "아귀",   new Color(0.75f, 0.75f, 0.3f),  new Vector3(0.7f, 0.7f, 0.7f), 18f, 4f, 0.9f,  5.6f,  45f);
        created += Create("Enemy_Dokkaebi", "dokkaebi", "도깨비", new Color(0.8f, 0.2f, 0.15f),   new Vector3(1.4f, 1.4f, 1.4f), 75f, 9f, 1.8f,  3.15f, 100f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"적 데이터 {created}개를 새로 만들었습니다 ({FolderPath}). 이미 있던 파일은 그대로 두었습니다.");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(FolderPath);
    }

    // 파일 하나 만들기 (이미 있으면 0, 새로 만들면 1을 돌려줌)
    private static int Create(string fileName, string id, string displayName, Color color, Vector3 scale,
                              float health, float damage, float cooldown, float speed, float experience)
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

        AssetDatabase.CreateAsset(data, path);
        return 1;
    }
}
