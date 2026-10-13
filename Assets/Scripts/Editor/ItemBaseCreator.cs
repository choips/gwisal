using UnityEditor;
using UnityEngine;

// 에디터 전용 도구 (게임 빌드에는 포함되지 않음)
// 메뉴 [귀살 → 아이템 기본 데이터 만들기 (기존 9종)]을 누르면
// 1) 지금 드랍 표의 장비 8종 + 기력 물약을 ItemBase 파일로 Assets/Data/Items에 만들고 (아이콘은 Resources/ItemIcons의 그림을 연결)
// 2) Assets/Resources/ItemDatabase.asset을 만들어 Assets/Data/Items 안의 모든 ItemBase를 목록에 넣습니다.
// 이미 있는 파일은 건드리지 않습니다 (직접 고친 숫자를 덮어쓰지 않기 위함). 목록에 빠진 아이템만 추가합니다.
public static class ItemBaseCreator
{
    private const string ItemFolder = "Assets/Data/Items";
    private const string IconFolder = "Assets/Resources/ItemIcons";
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
        created += Create("weapon_jujube_sword",    "대추나무 검",    ItemType.Weapon,     15f, "벼락 맞은 대추나무 검", "벼락 맞은 대추나무 검");
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

    // 메뉴 [귀살 → 드랍 표를 기본 아이템에 연결]: 프리팹들의 LootDropper 드랍 표에서
    // Base Item이 비어 있는 줄을 옛 이름(Item → Item Name)으로 찾아 채우고, Potion Item이 비어 있으면 기력 물약을 넣습니다.
    // 이미 채워진 칸은 건드리지 않습니다.
    [MenuItem("귀살/드랍 표를 기본 아이템에 연결")]
    private static void LinkDropTables()
    {
        ItemDatabase database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
        if (database == null)
        {
            Debug.LogError($"{DatabasePath}이 없습니다. 메뉴 [귀살 → 아이템 기본 데이터 만들기 (기존 9종)]을 먼저 눌러 주세요.");
            return;
        }

        int linked = 0;
        int notFound = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            LootDropper[] droppers = prefab.GetComponentsInChildren<LootDropper>(true);
            if (droppers.Length == 0) continue;

            foreach (LootDropper dropper in droppers)
            {
                SerializedObject serialized = new SerializedObject(dropper);

                SerializedProperty table = serialized.FindProperty("dropTable");
                for (int i = 0; i < table.arraySize; i++)
                {
                    SerializedProperty entry = table.GetArrayElementAtIndex(i);
                    SerializedProperty baseItem = entry.FindPropertyRelative("baseItem");
                    if (baseItem.objectReferenceValue != null) continue;

                    string oldName = entry.FindPropertyRelative("item.itemName").stringValue;
                    ItemBase found = database.FindByOldName(oldName);
                    if (found == null)
                    {
                        Debug.LogWarning($"{path}: 드랍 표 {i}번 줄 \"{oldName}\"에 맞는 기본 아이템이 없습니다. Inspector에서 Base Item을 직접 넣어 주세요.", prefab);
                        notFound++;
                        continue;
                    }

                    baseItem.objectReferenceValue = found;
                    Debug.Log($"{path}: 드랍 표 {i}번 줄 \"{oldName}\" → {found.itemId}", prefab);
                    linked++;
                }

                SerializedProperty potionItem = serialized.FindProperty("potionItem");
                if (potionItem.objectReferenceValue == null)
                {
                    ItemBase potion = database.Find("potion_mana");
                    if (potion != null)
                    {
                        potionItem.objectReferenceValue = potion;
                        Debug.Log($"{path}: Potion Item → {potion.itemId}", prefab);
                        linked++;
                    }
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SavePrefabAsset(prefab);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"드랍 표 연결 끝: 새로 연결 {linked}칸, 찾지 못함 {notFound}칸");
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
