using System.Collections.Generic;
using UnityEngine;

// 캐릭터 한 명의 세이브 (세이브 형식 버전 3부터, 파일: Character_칸번호.json)
// 레벨·경험치·현재 체력/기력·가방·장비를 저장합니다.
// 최대 체력·최대 기력·무기 피해는 레벨과 장비로 다시 계산하므로 저장하지 않습니다.
// JsonUtility는 public 필드만 저장하므로 모든 변수를 public으로 선언합니다. 새 칸은 맨 뒤에 추가합니다.
[System.Serializable]
public class CharacterSaveData
{
    // 저장 형식 버전 (1: 기력 없음, 2: 기력 추가, 3: 아이템을 ID로 + 계정 공용 / 캐릭터별 파일 분리)
    public const int CurrentVersion = 3;

    public int version;

    // 플레이어 상태
    public int level = 1;
    public float currentXP;
    public float requiredXP;   // 다음 레벨까지 필요한 경험치
    public float currentHP;
    public float currentMP;

    // 가방
    public List<SavedItem> inventoryItems = new List<SavedItem>();

    // 장착 중인 장비 (칸 이름 + 아이템, 빈 칸은 저장하지 않음)
    public List<SavedEquipment> equipment = new List<SavedEquipment>();

    // 장비 칸 이름의 아이템을 찾음 (없으면 null)
    public SavedItem FindEquipment(string slotId)
    {
        foreach (SavedEquipment entry in equipment)
        {
            if (entry != null && entry.slotId == slotId) return entry.item;
        }
        return null;
    }

    // 장비를 칸 이름과 함께 추가 (비어 있거나 기본 아이템을 못 찾으면 넣지 않음)
    public void AddEquipment(string slotId, ItemData item)
    {
        SavedItem saved = SavedItem.FromItem(item);
        if (saved != null)
        {
            equipment.Add(new SavedEquipment { slotId = slotId, item = saved });
        }
    }
}

// 장비 칸 하나 (칸 이름은 번호가 아닌 글자로 저장 — 장비 칸이 10칸으로 늘어도 옛 세이브가 안전)
[System.Serializable]
public class SavedEquipment
{
    public const string WeaponSlot = "weapon"; // 무기 칸
    public const string ArmorSlot = "armor";   // 방어구 칸

    public string slotId = "";
    public SavedItem item;
}

// 세이브 파일에 저장하는 아이템 한 개 (이름·종류·아이콘은 기본 아이템 ID로 다시 찾으므로 저장하지 않음)
[System.Serializable]
public class SavedItem
{
    public string baseId = "";
    public ItemRarity rarity;
    public int itemLevel = 1;
    public float statValue;
    public List<ItemAffix> affixes = new List<ItemAffix>();

    // 게임 속 아이템 → 저장용 (비어 있거나 기본 아이템을 못 찾으면 null)
    public static SavedItem FromItem(ItemData item)
    {
        if (item == null || item.IsEmpty) return null;

        ItemBase baseItem = item.Base;
        if (baseItem == null)
        {
            Debug.LogWarning($"기본 아이템을 찾지 못해 저장하지 않습니다: ID \"{item.baseId}\" (ItemDatabase 목록 확인)");
            return null;
        }

        SavedItem saved = new SavedItem
        {
            baseId = baseItem.itemId,
            rarity = item.rarity,
            itemLevel = item.itemLevel,
            statValue = item.statValue,
        };
        if (item.affixes != null)
        {
            foreach (ItemAffix affix in item.affixes)
            {
                if (affix != null) saved.affixes.Add(new ItemAffix(affix.affixId, affix.value));
            }
        }
        return saved;
    }

    // 저장용 → 게임 속 아이템 (ID에 맞는 기본 아이템이 없으면 null — 아이템을 지운 경우 등)
    public ItemData ToItem()
    {
        if (string.IsNullOrEmpty(baseId)) return null;

        ItemDatabase database = ItemDatabase.Instance;
        ItemBase baseItem = database != null ? database.Find(baseId) : null;
        if (baseItem == null)
        {
            Debug.LogWarning($"세이브의 아이템 ID \"{baseId}\"에 맞는 기본 아이템이 없어 건너뜁니다. (ItemDatabase 목록 확인)");
            return null;
        }

        ItemData item = new ItemData(baseItem, rarity, Mathf.Max(1, itemLevel), statValue);
        if (affixes != null)
        {
            foreach (ItemAffix affix in affixes)
            {
                if (affix != null) item.affixes.Add(new ItemAffix(affix.affixId, affix.value));
            }
        }
        return item;
    }
}
