using System.Collections.Generic;
using UnityEngine;

// 옛 세이브 형식 (버전 1·2, 파일: SaveData.json) — 옛 파일을 읽어 버전 3으로 바꿀 때만 사용합니다.
// 버전 3부터는 캐릭터별 CharacterSaveData(Character_칸번호.json) + 계정 공용 AccountSaveData(Account.json)에 저장합니다.
// JsonUtility는 public 필드만 읽으므로 모든 변수를 public으로 선언합니다. (칸 이름을 바꾸면 옛 파일을 못 읽음)
[System.Serializable]
public class SaveData
{
    // 이 파일이 어떤 형식으로 저장되었는지 (버전 1 파일에는 값이 없어서 0으로 읽힘)
    public int version;

    // 플레이어 상태
    public int level;
    public float currentXP;
    public float requiredXP;   // 다음 레벨까지 필요한 경험치 (레벨마다 달라지므로 함께 저장)
    public float maxHP;
    public float currentHP;
    public float attackPower;
    public float maxMP;        // 최대 기력 (버전 2부터)
    public float currentMP;    // 현재 기력 (버전 2부터)

    // 인벤토리 (가방) — 아이템을 이름으로 저장
    public List<LegacyItem> inventoryItems = new List<LegacyItem>();

    // 장착 중인 장비 (장착하지 않았다면 이름이 빈 아이템으로 저장됨)
    public LegacyItem equippedWeapon;
    public LegacyItem equippedArmor;

    // 버전 3 캐릭터 세이브로 변환 (아이템은 이름으로 기본 아이템을 찾고, 못 찾으면 건너뜀)
    public CharacterSaveData ToCharacterSaveData()
    {
        CharacterSaveData data = new CharacterSaveData
        {
            version = CharacterSaveData.CurrentVersion,
            level = level,
            currentXP = currentXP,
            requiredXP = requiredXP,
            currentHP = currentHP,
            // 기력은 버전 2부터 저장됨. 버전 1 파일이면 가득 채움 (불러올 때 최대 기력으로 줄어듦)
            currentMP = version >= 2 ? currentMP : float.MaxValue,
        };

        int skipped = 0;
        if (inventoryItems != null)
        {
            foreach (LegacyItem item in inventoryItems)
            {
                if (item == null || item.IsEmpty) continue;

                SavedItem saved = item.ToSavedItem();
                if (saved != null) data.inventoryItems.Add(saved);
                else skipped++;
            }
        }

        skipped += AddEquipment(data, SavedEquipment.WeaponSlot, equippedWeapon);
        skipped += AddEquipment(data, SavedEquipment.ArmorSlot, equippedArmor);

        Debug.Log($"옛 세이브(버전 {version})를 버전 {CharacterSaveData.CurrentVersion}으로 변환했습니다. 가방 {data.inventoryItems.Count}개, 장비 {data.equipment.Count}개, 찾지 못해 건너뛴 아이템 {skipped}개");
        return data;
    }

    // 옛 장비 하나를 칸 이름과 함께 추가 (비어 있으면 0, 기본 아이템을 못 찾아 건너뛰면 1을 돌려줌)
    private static int AddEquipment(CharacterSaveData data, string slotId, LegacyItem item)
    {
        if (item == null || item.IsEmpty) return 0;

        SavedItem saved = item.ToSavedItem();
        if (saved == null) return 1;

        data.equipment.Add(new SavedEquipment { slotId = slotId, item = saved });
        return 0;
    }
}

// 옛 세이브(버전 1·2)의 아이템 한 개 — 이름으로 저장되어 있음 (칸 이름은 옛 ItemData와 같아야 읽힘)
[System.Serializable]
public class LegacyItem
{
    public string itemName;
    public ItemType itemType;
    public ItemRarity rarity;
    public float statValue;

    public bool IsEmpty => string.IsNullOrEmpty(itemName);

    // 이름(지금 이름 또는 ItemBase의 예전 이름)으로 기본 아이템을 찾아 버전 3 아이템으로 (못 찾으면 null)
    public SavedItem ToSavedItem()
    {
        ItemDatabase database = ItemDatabase.Instance;
        ItemBase baseItem = database != null ? database.FindByOldName(itemName) : null;
        if (baseItem == null)
        {
            Debug.LogWarning($"옛 세이브의 아이템 \"{itemName}\"에 맞는 기본 아이템이 없어 건너뜁니다. (ItemBase의 Display Name·Old Names 확인)");
            return null;
        }

        return new SavedItem
        {
            baseId = baseItem.itemId,
            rarity = rarity,
            itemLevel = 1,
            statValue = statValue,
        };
    }
}
