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

        // 도깨비불 불덩이 프리팹이 없으면 먼저 만듦
        GameObject fireball = EnemyFireballPrefabCreator.EnsurePrefab();

        int created = 0;
        //                  파일 이름            ID            이름       몸 색                                    크기                          체력  공격 쿨타임 속도  경험치
        created += Create("Enemy_Mangja",     "mangja",     "망자",   new Color(0.55f, 0.55f, 0.58f), Vector3.one,                      30f, 5f, 1.5f, 3.5f, 50f);
        created += Create("Enemy_Changgwi",   "changgwi",   "창귀",   new Color(0.95f, 0.55f, 0.25f), new Vector3(0.65f, 0.8f, 0.65f), 18f, 4f, 1f,   5.5f, 35f);
        created += Create("Enemy_Dokkaebibul","dokkaebibul","도깨비불", new Color(0.35f, 0.85f, 1f),    new Vector3(0.7f, 0.7f, 0.7f),   20f, 6f, 2f,   4f,   45f);
        // 산군: 체력 700, 피해 18, 간격 1.8초, 속도 5, 경험 2000 (엑셀 sangun) — 크기·색은 보스답게 큼직하게
        created += Create("Enemy_Sangun",     "sangun",     "산군",   new Color(0.55f, 0.25f, 0.15f), new Vector3(1.8f, 1.8f, 1.8f), 700f, 18f, 1.8f, 5f, 2000f);

        // 창귀 호출 / 도깨비불 원거리 / 산군 보스 칸 (이미 있던 파일도 비어 있으면 채움 — 숫자는 덮지 않음)
        ApplyChanggwiCall();
        ApplyDokkaebibulRanged(fireball);
        ApplySangunBoss();

        int added = FillDatabase();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"적 데이터: 새로 만든 파일 {created}개 ({FolderPath}), EnemyDatabase 목록에 새로 넣은 귀신 {added}개 ({DatabasePath}). 이미 있던 파일은 그대로 두었습니다.");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(FolderPath);
    }

    private static void ApplyChanggwiCall()
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>($"{FolderPath}/Enemy_Changgwi.asset");
        if (data == null) return;
        if (data.callRadius > 0f) return; // 이미 설정됨

        data.callRadius = 12f;
        data.callDuration = 0.5f;
        EditorUtility.SetDirty(data);
    }

    private static void ApplyDokkaebibulRanged(GameObject fireball)
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>($"{FolderPath}/Enemy_Dokkaebibul.asset");
        if (data == null) return;

        // 프리팹만 비어 있으면 연결, 거리 값은 0일 때만 기획 기본값으로 채움
        if (data.projectilePrefab == null) data.projectilePrefab = fireball;
        if (data.hoverHeight <= 0f) data.hoverHeight = 1.5f;
        if (data.preferredRangeMin <= 0f) data.preferredRangeMin = 6f;
        if (data.preferredRangeMax <= 0f) data.preferredRangeMax = 9f;
        if (data.fleeRange <= 0f) data.fleeRange = 4f;
        if (data.fleeDistance <= 0f) data.fleeDistance = 3f;
        if (data.fleeCooldown <= 0f) data.fleeCooldown = 5f;
        if (data.projectileSpeed <= 0f) data.projectileSpeed = 8f;
        if (data.aggroRange < data.preferredRangeMax) data.aggroRange = 12f;
        data.attackRange = data.preferredRangeMax; // 기즈모·참고용

        EditorUtility.SetDirty(data);
    }

    // 산군 보스 플래그·패턴 간격·소환 창귀 연결 (이미 isBoss면 숫자 덮지 않음)
    private static void ApplySangunBoss()
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>($"{FolderPath}/Enemy_Sangun.asset");
        if (data == null) return;

        EnemyData changgwi = AssetDatabase.LoadAssetAtPath<EnemyData>($"{FolderPath}/Enemy_Changgwi.asset");

        if (!data.isBoss)
        {
            data.isBoss = true;
            data.clawTelegraph = 0.5f;
            data.clawRange = 3f;
            data.clawArcDegrees = 120f;
            data.pounceTelegraph = 1f;
            data.pounceRadius = 3f;
            data.pounceCooldown = 8f;
            data.pounceDamageMult = 2f;
            data.summonCount = 4;
            data.summonIntervalPhase1 = 20f;
            data.summonIntervalPhase2 = 15f;
            data.summonMaxAlive = 6;
            data.phase2HealthRatio = 0.5f;
            data.swallowHealPercent = 0.05f;
            data.swallowDamageBonus = 0.1f;
            data.swallowMaxStacks = 3;
            data.swallowRange = 2.5f;
            data.aggroRange = 20f;
            data.attackRange = data.clawRange;
        }

        if (data.summonEnemyData == null && changgwi != null)
            data.summonEnemyData = changgwi;

        EditorUtility.SetDirty(data);
    }

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
