using System.Collections.Generic;
using UnityEngine;

// 아이템 종류
// 세이브 파일에는 순서 번호(0, 1, 2…)로 저장되므로, 새 종류는 반드시 맨 뒤에 추가해야 합니다.
public enum ItemType
{
    Weapon,     // 무기: 공격력 증가
    Armor,      // 방어구: 최대 체력 증가
    ManaPotion  // 기력 물약: 사용하면 기력(MP) 회복 (한 번 쓰면 사라짐)
}

// 아이템 등급 (색은 ItemRarityColors 표에서 가져옴)
public enum ItemRarity
{
    Normal, // 노멀 (한지)
    Magic,  // 매직 (쪽빛)
    Rare,   // 레어 (황토)
    Unique  // 유니크 (주사 붉은색)
}

// 아이템에 붙은 무작위 옵션 하나 (예: 공격 속도 +15%)
// 버전 4부터: 옵션 Id + 굴림 위치(0~1) + 단계로 저장하고, 수치는 S6에서 표로 계산 (지금은 value도 함께 둠)
[System.Serializable]
public class ItemAffix
{
    public string affixId = "";
    public float value;

    // —— 버전 4부터 (맨 뒤에만 추가) ——
    public float rollPosition; // 굴림 위치 0~1 (같은 단계 안에서의 위치)
    public int tier;           // 옵션 단계 1~11

    public ItemAffix() { }

    public ItemAffix(string affixId, float value)
    {
        this.affixId = affixId;
        this.value = value;
    }

    // 복사 (세이브·인벤토리 복제용)
    public ItemAffix(ItemAffix source)
    {
        if (source == null) return;
        affixId = source.affixId;
        value = source.value;
        rollPosition = source.rollPosition;
        tier = source.tier;
    }
}

// 아이템 한 개의 정보 (기본 아이템 ID, 종류, 등급, 숨겨진 아이템 레벨, 능력치 증가량, 옵션 목록)
// 바닥 아이템(Item), 인벤토리, 장비 슬롯에서 이 형식을 사용합니다 (세이브 파일에는 SavedItem으로 바꿔 저장).
// 이름·아이콘은 baseId로 찾은 기본 아이템(ItemBase)에서 가져옵니다.
// MonoBehaviour가 아닌 순수 C# 클래스이며, Inspector 표시와 JSON 저장을 위해 [System.Serializable]을 붙입니다.
[System.Serializable]
public class ItemData
{
    public ItemType itemType = ItemType.Weapon;
    public ItemRarity rarity = ItemRarity.Normal;

    [Tooltip("무기: 공격력 증가량 / 방어구: 최대 체력 증가량 / 기력 물약: 기력 회복량")]
    public float statValue = 5f;

    [Tooltip("기본 아이템 ID (ItemBase의 itemId, 예: weapon_old_dagger)")]
    public string baseId = "";

    [Tooltip("숨겨진 아이템 레벨 (떨어뜨린 귀신의 몬스터 레벨, 플레이어에게 보이지 않음)")]
    public int itemLevel = 1;

    [Tooltip("붙은 무작위 옵션 목록 (옵션 굴리기는 아이템 & 파밍 단계에서, 지금은 항상 비어 있음)")]
    public List<ItemAffix> affixes = new List<ItemAffix>();

    // —— 버전 4부터 ([전달-28] 자리만, 값은 지금은 0) ——
    [Tooltip("벼림(품 올리기) 횟수 — 대장장이는 나중에")]
    public int upgradeCount;
    [Tooltip("품 올리기로 얻은 옵션 단계 보너스 (+0~2)")]
    public int tierBonus;

    // 찾아 둔 기본 아이템 (저장하지 않음)
    [System.NonSerialized] private ItemBase cachedBase;

    public ItemData() { }

    // 기본 아이템으로 새 아이템을 만듦 (드랍할 때)
    public ItemData(ItemBase baseItem, ItemRarity rarity, int itemLevel, float statValue)
    {
        cachedBase = baseItem;
        baseId = baseItem.itemId;
        itemType = baseItem.itemType;
        this.rarity = rarity;
        this.itemLevel = itemLevel;
        this.statValue = statValue;
    }

    // 같은 내용의 새 아이템을 만듦 (바닥 아이템이 파괴되어도 인벤토리의 아이템은 따로 유지)
    public ItemData(ItemData source)
    {
        itemType = source.itemType;
        rarity = source.rarity;
        statValue = source.statValue;
        baseId = source.baseId;
        itemLevel = source.itemLevel;
        cachedBase = source.cachedBase;

        upgradeCount = source.upgradeCount;
        tierBonus = source.tierBonus;

        affixes = new List<ItemAffix>();
        if (source.affixes != null)
        {
            foreach (ItemAffix affix in source.affixes)
            {
                if (affix != null) affixes.Add(new ItemAffix(affix));
            }
        }
    }

    // 기본 아이템 ID가 없는 빈 칸인지 확인
    public bool IsEmpty => string.IsNullOrEmpty(baseId);

    // 기본 아이템 (ID로 찾음 — 못 찾으면 null)
    public ItemBase Base
    {
        get
        {
            if (cachedBase == null && !string.IsNullOrEmpty(baseId))
            {
                ItemDatabase database = ItemDatabase.Instance;
                if (database != null) cachedBase = database.Find(baseId);
            }
            return cachedBase;
        }
    }

    // 같은 기본 아이템인지 구분하는 값 (물약 묶음용)
    public string BaseKey => baseId;

    // 화면에 보일 이름 (예: "낡은 단검" — 기본 아이템을 못 찾으면 ID 그대로)
    public string Name => Base != null ? Base.LocalizedName : baseId;

    // 기본 아이템의 아이콘 (없으면 null)
    public Sprite Icon => Base != null ? Base.icon : null;

    // 장착하는 대신 사용하면 사라지는 소모품인지 (물약 등)
    public bool IsConsumable => itemType == ItemType.ManaPotion;

    // 화면 표시용 이름 (예: "[레어] 낡은 단검", 등급이 없는 소모품은 "기력 물약")
    public string DisplayName => IsConsumable ? Name : Loc.Format("item.display_name", RarityName, Name);

    // 종류 이름 (예: "무기") — 화면 글자 표 "UI"에서 가져옴
    public string TypeName => GetTypeName(itemType);

    // 이 아이템이 올려 주는 능력치 이름 (예: "공격력")
    public string StatName
    {
        get
        {
            switch (itemType)
            {
                case ItemType.Armor:      return Loc.Get("item.stat.armor");
                case ItemType.ManaPotion: return Loc.Get("item.stat.mana_potion");
                default:                  return Loc.Get("item.stat.weapon");
            }
        }
    }

    // 등급 이름 (예: "레어")
    public string RarityName
    {
        get
        {
            switch (rarity)
            {
                case ItemRarity.Magic:  return Loc.Get("item.rarity.magic");
                case ItemRarity.Rare:   return Loc.Get("item.rarity.rare");
                case ItemRarity.Unique: return Loc.Get("item.rarity.unique");
                default:                return Loc.Get("item.rarity.normal");
            }
        }
    }

    // 능력치 설명 (예: "공격력 +10")
    public string StatDescription => Loc.Format("item.stat_line", StatName, statValue);

    // 종류 이름 (아이템 없이 종류만으로 — 예: 비어 있는 장비 칸 "무기: 없음")
    public static string GetTypeName(ItemType type)
    {
        switch (type)
        {
            case ItemType.Armor:      return Loc.Get("item.type.armor");
            case ItemType.ManaPotion: return Loc.Get("item.type.mana_potion");
            default:                  return Loc.Get("item.type.weapon");
        }
    }

    // 등급 색 (ItemRarityColors 표에서 가져옴)
    public Color RarityColor => ItemRarityColors.Instance.Get(rarity);

    // 이름·바닥 구슬·인벤토리 칸에 쓰는 색 (장비는 등급 색, 물약은 물약 색)
    public Color NameColor => IsConsumable ? ItemRarityColors.Instance.PotionColor : RarityColor;
}
