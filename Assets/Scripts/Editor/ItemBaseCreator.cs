using UnityEditor;
using UnityEngine;

// 에디터 전용 도구 (게임 빌드에는 포함되지 않음)
// 메뉴 [귀살 → 아이템 기본 데이터 만들기 (기존 9종)]을 누르면
// 1) 처음 드랍 표의 장비 8종 + 기력 물약을 ItemBase 파일로 Assets/Data/Items에 만들고 (아이콘은 Assets/Icons/Items의 그림을 연결)
// 2) Assets/Resources/ItemDatabase.asset을 만들어 Assets/Data/Items 안의 모든 ItemBase를 목록에 넣습니다.
// 이미 있는 파일은 건드리지 않습니다 (직접 고친 숫자를 덮어쓰지 않기 위함). 목록에 빠진 아이템만 추가합니다.
public static class ItemBaseCreator
{
    private const string ItemFolder = "Assets/Data/Items";
    private const string IconFolder = "Assets/Icons/Items";
    private const string DatabasePath = "Assets/Resources/" + ItemDatabase.ResourcePath + ".asset";

    [MenuItem("귀살/아이템 기본 데이터 만들기 (기존 9종)")]
    private static void CreateDefaultItems()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(ItemFolder)) AssetDatabase.CreateFolder("Assets/Data", "Items");
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        int created = 0;
        //                  ID                        한국어 이름        종류                 능력치 아이콘 파일 이름         예전 이름
        created += Create("weapon_old_dagger",      "낡은 단검",      ItemType.Weapon,     5f,  "낡은 단검");
        created += Create("weapon_peachwood_sword", "복숭아나무 검",  ItemType.Weapon,     8f,  "복숭아나무 검");
        created += Create("weapon_taoist_staff",    "도사의 지팡이",  ItemType.Weapon,     11f, "도사의 지팡이");
        created += Create("weapon_jujube_sword",    "대추나무 검",    ItemType.Weapon,     15f, "대추나무 검",   "벼락 맞은 대추나무 검");
        created += Create("armor_cotton_robe",      "무명 도포",      ItemType.Armor,      15f, "무명 도포");
        created += Create("armor_leather_armor",    "가죽 갑옷",      ItemType.Armor,      20f, "가죽 갑옷");
        created += Create("armor_silk_robe",        "비단 도포",      ItemType.Armor,      30f, "비단 도포");
        created += Create("armor_chainmail",        "사슬 갑옷",      ItemType.Armor,      40f, "사슬 갑옷");
        created += Create("potion_mana",            "기력 물약",      ItemType.ManaPotion, 50f, null);

        int added = FillDatabase();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"아이템 기본 데이터: 새로 만든 파일 {created}개 ({ItemFolder}), ItemDatabase 목록에 새로 넣은 아이템 {added}개 ({DatabasePath}). 이미 있던 파일은 그대로 두었습니다.");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(ItemFolder);
    }

    // 파일 하나 만들기 (이미 있으면 0, 새로 만들면 1을 돌려줌)
    private static int Create(string id, string displayName, ItemType type, float statValue, string iconFileName, params string[] oldNames)
    {
        string path = $"{ItemFolder}/Item_{id}.asset";
        if (AssetDatabase.LoadAssetAtPath<ItemBase>(path) != null) return 0;

        ItemBase item = ScriptableObject.CreateInstance<ItemBase>();
        item.itemId = id;
        item.displayName = displayName;
        item.oldNames = oldNames;
        item.itemType = type;
        item.baseStatValue = statValue;

        if (!string.IsNullOrEmpty(iconFileName))
        {
            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconFolder}/{iconFileName}.png");
            if (item.icon == null)
            {
                Debug.LogWarning($"아이콘을 찾지 못했습니다: {IconFolder}/{iconFileName}.png ({displayName}은 공용 아이콘으로 보입니다)");
            }
        }

        AssetDatabase.CreateAsset(item, path);
        return 1;
    }

    // ItemDatabase가 없으면 만들고, Assets/Data/Items 안의 모든 ItemBase 중 빠진 것을 목록에 넣음
    private static int FillDatabase()
    {
        ItemDatabase database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<ItemDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ItemBase", new[] { ItemFolder }))
        {
            ItemBase item = AssetDatabase.LoadAssetAtPath<ItemBase>(AssetDatabase.GUIDToAssetPath(guid));
            if (database.EditorAdd(item)) added++;
        }

        EditorUtility.SetDirty(database);
        return added;
    }
}
