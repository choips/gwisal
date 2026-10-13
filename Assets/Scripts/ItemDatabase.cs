using System.Collections.Generic;
using UnityEngine;

// 모든 기본 아이템(ItemBase) 목록 — ID로 기본 아이템을 찾는 사전
// 파일은 반드시 Assets/Resources/ItemDatabase.asset 한 개만 둡니다 (게임이 Resources에서 이 이름으로 불러옴).
// 세이브 불러오기·드랍·아이콘 찾기가 모두 이 목록을 거치므로, 새 기본 아이템을 만들면 Items 목록에 추가해야 합니다.
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "귀살/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public const string ResourcePath = "ItemDatabase"; // Resources 폴더 안의 파일 이름 (확장자 제외)

    [Tooltip("게임에 등장하는 모든 기본 아이템 (메뉴 [귀살 → 아이템 기본 데이터 만들기]가 자동으로 채움)")]
    [SerializeField] private List<ItemBase> items = new List<ItemBase>();

    private Dictionary<string, ItemBase> itemsById;      // ID → 기본 아이템 (처음 찾을 때 한 번 만듦)
    private Dictionary<string, ItemBase> itemsByOldName; // 예전 이름 → 기본 아이템 (옛 세이브 변환용)

    private static ItemDatabase instance;

    // 어디서든 ItemDatabase.Instance로 접근 (처음 한 번만 Resources에서 불러옴)
    public static ItemDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<ItemDatabase>(ResourcePath);
                if (instance == null)
                {
                    Debug.LogError($"Assets/Resources/{ResourcePath}.asset 파일이 없습니다. 메뉴 [귀살 → 아이템 기본 데이터 만들기]를 눌러 만들어 주세요.");
                }
            }
            return instance;
        }
    }

    public IReadOnlyList<ItemBase> Items => items;

    // ID로 기본 아이템 찾기 (없으면 null)
    public ItemBase Find(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        BuildLookups();
        itemsById.TryGetValue(itemId, out ItemBase found);
        return found;
    }

    // 옛 세이브의 아이템 이름으로 기본 아이템 찾기 (지금 한국어 이름 또는 예전 이름, 없으면 null)
    public ItemBase FindByOldName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return null;

        BuildLookups();
        itemsByOldName.TryGetValue(itemName, out ItemBase found);
        return found;
    }

    private void BuildLookups()
    {
        if (itemsById != null) return;

        itemsById = new Dictionary<string, ItemBase>();
        itemsByOldName = new Dictionary<string, ItemBase>();

        foreach (ItemBase item in items)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) continue;

            if (itemsById.ContainsKey(item.itemId))
            {
                Debug.LogWarning($"ItemDatabase에 ID \"{item.itemId}\"가 두 번 들어 있습니다. 앞의 것만 사용합니다.", item);
                continue;
            }
            itemsById[item.itemId] = item;

            if (!string.IsNullOrEmpty(item.displayName)) itemsByOldName[item.displayName] = item;
            foreach (string oldName in item.oldNames)
            {
                if (!string.IsNullOrEmpty(oldName)) itemsByOldName[oldName] = item;
            }
        }
    }

    // Inspector에서 목록을 고치면 찾기용 사전을 다시 만들도록 비움
    private void OnValidate()
    {
        itemsById = null;
        itemsByOldName = null;
    }

#if UNITY_EDITOR
    // 에디터 메뉴가 목록에 기본 아이템을 넣을 때 사용 (이미 있으면 false)
    public bool EditorAdd(ItemBase item)
    {
        if (item == null || items.Contains(item)) return false;

        items.Add(item);
        itemsById = null;
        itemsByOldName = null;
        return true;
    }
#endif
}
