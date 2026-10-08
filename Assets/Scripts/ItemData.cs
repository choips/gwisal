using UnityEngine;

// 아이템 종류
// 세이브 파일에는 순서 번호(0, 1, 2…)로 저장되므로, 새 종류는 반드시 맨 뒤에 추가해야 합니다.
public enum ItemType
{
    Weapon,     // 무기: 공격력 증가
    Armor,      // 방어구: 최대 체력 증가
    ManaPotion  // 기력 물약: 사용하면 기력(MP) 회복 (한 번 쓰면 사라짐)
}

// 아이템 등급
public enum ItemRarity
{
    Normal, // 노멀 (흰색)
    Magic,  // 매직 (파란색)
    Rare,   // 레어 (노란색)
    Unique  // 유니크 (주황색)
}

// 아이템 한 개의 정보 (이름, 종류, 등급, 능력치 증가량)
// 바닥 아이템(Item), 인벤토리, 장비 슬롯, 세이브 파일에서 모두 이 형식을 사용합니다.
// MonoBehaviour가 아닌 순수 C# 클래스이며, Inspector 표시와 JSON 저장을 위해 [System.Serializable]을 붙입니다.
[System.Serializable]
public class ItemData
{
    public string itemName = "알 수 없는 아이템";
    public ItemType itemType = ItemType.Weapon;
    public ItemRarity rarity = ItemRarity.Normal;

    [Tooltip("무기: 공격력 증가량 / 방어구: 최대 체력 증가량 / 기력 물약: 기력 회복량")]
    public float statValue = 5f;

    public ItemData() { }

    // 같은 내용의 새 아이템을 만듦 (바닥 아이템이 파괴되어도 인벤토리의 아이템은 따로 유지)
    public ItemData(ItemData source)
    {
        itemName = source.itemName;
        itemType = source.itemType;
        rarity = source.rarity;
        statValue = source.statValue;
    }

    // JSON에서 불러온 빈 칸(이름 없음)인지 확인
    public bool IsEmpty => string.IsNullOrEmpty(itemName);

    // 장착하는 대신 사용하면 사라지는 소모품인지 (물약 등)
    public bool IsConsumable => itemType == ItemType.ManaPotion;

    // 화면 표시용 이름 (예: "[Rare] 낡은 단검", 등급이 없는 소모품은 "기력 물약")
    public string DisplayName => IsConsumable ? itemName : $"[{rarity}] {itemName}";

    // 종류 이름 (예: "무기")
    public string TypeName
    {
        get
        {
            switch (itemType)
            {
                case ItemType.Armor:      return "방어구";
                case ItemType.ManaPotion: return "물약";
                default:                  return "무기";
            }
        }
    }

    // 이 아이템이 올려 주는 능력치 이름 (예: "공격력")
    public string StatName
    {
        get
        {
            switch (itemType)
            {
                case ItemType.Armor:      return "최대 체력";
                case ItemType.ManaPotion: return "기력 회복";
                default:                  return "공격력";
            }
        }
    }

    // 등급 한글 이름 (예: "레어")
    public string RarityName
    {
        get
        {
            switch (rarity)
            {
                case ItemRarity.Magic:  return "매직";
                case ItemRarity.Rare:   return "레어";
                case ItemRarity.Unique: return "유니크";
                default:                return "노멀";
            }
        }
    }

    // 능력치 설명 (예: "공격력 +10")
    public string StatDescription => $"{StatName} +{statValue}";

    // 어두운 UI 배경에서 잘 보이는 등급별 글자 색
    public Color RarityColor
    {
        get
        {
            switch (rarity)
            {
                case ItemRarity.Magic:  return new Color(0.4f, 0.6f, 1f);   // 밝은 파랑
                case ItemRarity.Rare:   return new Color(1f, 0.9f, 0.2f);   // 노랑
                case ItemRarity.Unique: return new Color(1f, 0.55f, 0.1f);  // 주황
                default:                return Color.white;
            }
        }
    }

    // 이름을 표시할 때 쓰는 색 (장비는 등급 색, 기력 물약은 하늘색)
    public Color NameColor => IsConsumable ? new Color(0.35f, 0.85f, 1f) : RarityColor;
}
