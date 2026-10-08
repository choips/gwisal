using UnityEngine;

// 아이템 종류
public enum ItemType
{
    Weapon, // 무기: 공격력 증가
    Armor   // 방어구: 최대 체력 증가
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

    [Tooltip("무기: 공격력 증가량 / 방어구: 최대 체력 증가량")]
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

    // 화면 표시용 이름 (예: "[Rare] 낡은 단검")
    public string DisplayName => $"[{rarity}] {itemName}";

    // 종류 이름 (예: "무기")
    public string TypeName => itemType == ItemType.Weapon ? "무기" : "방어구";

    // 이 아이템이 올려 주는 능력치 이름 (예: "공격력")
    public string StatName => itemType == ItemType.Weapon ? "공격력" : "최대 체력";

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
}
