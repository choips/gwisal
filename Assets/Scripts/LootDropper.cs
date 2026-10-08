using System.Collections.Generic;
using TMPro;                     // TMP_FontAsset 사용 (바닥 이름표 글꼴)
using UnityEngine;

// 드랍 테이블의 한 줄: 어떤 아이템이 얼마나 자주 떨어지는지
[System.Serializable]
public class LootEntry
{
    [Tooltip("떨어질 아이템 정보 (등급은 드랍할 때 랜덤으로 정해지므로 여기 등급은 무시됨)")]
    public ItemData item = new ItemData();

    [Tooltip("나올 확률 가중치 (클수록 자주 나옴, 0이면 안 나옴)")]
    public float weight = 10f; // 가중치

    public LootEntry() { }

    public LootEntry(string itemName, ItemType itemType, float statValue, float weight)
    {
        item = new ItemData { itemName = itemName, itemType = itemType, statValue = statValue };
        this.weight = weight;
    }
}

// 디아블로 스타일 아이템 드랍 스크립트
// 드랍 테이블에서 아이템 종류를 무작위로 고르고(가중치 확률), 등급도 무작위로 정한 뒤
// 지정한 위치에 생성해서 위로 튀어 오르게 하고 등급에 따라 색을 바꿉니다.
public class LootDropper : MonoBehaviour
{
    [Header("드랍 아이템")]
    [Tooltip("바닥에 떨어질 아이템 모양 프리팹 (Item 스크립트, Rigidbody, Collider가 붙어 있어야 합니다)")]
    public GameObject itemPrefab;

    [Tooltip("떨어질 수 있는 아이템 목록 (비워 두면 Item Prefab에 설정된 아이템이 그대로 떨어짐)\n"
           + "가중치 예: 30 / 20 / 10이면 각각 50% / 33% / 17% 확률")]
    [SerializeField] private List<LootEntry> dropTable = new List<LootEntry>
    {
        // 무기 (공격력)
        new LootEntry("낡은 단검", ItemType.Weapon, 5f, 30f),
        new LootEntry("복숭아나무 검", ItemType.Weapon, 8f, 20f),
        new LootEntry("도사의 지팡이", ItemType.Weapon, 11f, 10f),
        new LootEntry("벼락 맞은 대추나무 검", ItemType.Weapon, 15f, 5f),

        // 방어구 (최대 체력)
        new LootEntry("무명 도포", ItemType.Armor, 15f, 30f),
        new LootEntry("가죽 갑옷", ItemType.Armor, 20f, 20f),
        new LootEntry("비단 도포", ItemType.Armor, 30f, 10f),
        new LootEntry("사슬 갑옷", ItemType.Armor, 40f, 5f),
    };

    [Header("튀어 오르는 힘")]
    [Tooltip("위쪽으로 가하는 힘")]
    [SerializeField] private float upwardForce = 5f;

    [Tooltip("옆으로 퍼지는 힘 (랜덤 방향)")]
    [SerializeField] private float sideForce = 2f;

    [Tooltip("아이템이 빙글빙글 도는 힘")]
    [SerializeField] private float spinForce = 3f;

    [Tooltip("바닥에 파묻혀 생성되지 않도록 dropPosition보다 위로 띄울 높이")]
    [SerializeField] private float spawnHeightOffset = 0.5f;

    [Header("바닥 이름표")]
    [Tooltip("아이템 위에 띄울 이름표의 글꼴 (한글이 보이도록 NotoSansKR-Regular SDF 지정, 비워 두면 이름표 없음)")]
    [SerializeField] private TMP_FontAsset labelFont;

    [Tooltip("이름표 글자 크기 (3이면 글자 높이 약 0.3m)")]
    [SerializeField] private float labelFontSize = 3f;

    [Tooltip("아이템 중심에서 이름표까지의 높이(m)")]
    [SerializeField] private float labelHeight = 0.5f;

    [Header("등급별 색상")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color magicColor = Color.blue;
    [SerializeField] private Color rareColor = Color.yellow;
    [SerializeField] private Color uniqueColor = new Color(1f, 0.5f, 0f); // 주황색

    [Header("등급별 능력치 배율")]
    [Tooltip("아이템 프리팹에 설정된 능력치에 곱해지는 값 (등급이 높을수록 강해짐)")]
    [SerializeField] private float magicStatMultiplier = 1.5f;
    [SerializeField] private float rareStatMultiplier = 2f;
    [SerializeField] private float uniqueStatMultiplier = 3f;

    // dropPosition 위치에 아이템을 생성하고 튀어 오르게 함
    public void DropItem(Vector3 dropPosition)
    {
        if (itemPrefab == null)
        {
            Debug.LogWarning($"{gameObject.name}의 LootDropper에 Item Prefab이 등록되지 않았습니다.");
            return;
        }

        Vector3 spawnPosition = dropPosition + Vector3.up * spawnHeightOffset;
        GameObject item = Instantiate(itemPrefab, spawnPosition, Random.rotation);

        ItemRarity rarity = RollRarity();
        ApplyRarityColor(item, rarity);
        item.name = $"{itemPrefab.name} ({rarity})";

        // 드랍 테이블에서 고른 아이템으로 내용을 바꾸고, 등급을 기록한 뒤 등급에 따라 능력치를 강화
        if (item.TryGetComponent(out Item itemComponent))
        {
            LootEntry entry = PickEntry();
            if (entry != null)
            {
                // 프리팹 원본이나 드랍 테이블이 바뀌지 않도록 복사본을 넣음
                itemComponent.data = new ItemData(entry.item);
            }

            itemComponent.data.rarity = rarity;
            itemComponent.data.statValue = Mathf.Round(itemComponent.data.statValue * GetStatMultiplier(rarity));
            item.name = $"{itemComponent.data.itemName} ({rarity})";
            Debug.Log($"아이템 드랍! {itemComponent.data.DisplayName} ({itemComponent.data.StatDescription})");

            // 아이템 위에 이름을 등급 색으로 표시 (글꼴이 없으면 한글이 □로 깨지므로 생략)
            if (labelFont != null)
            {
                item.AddComponent<ItemNameLabel>().Setup(
                    itemComponent.data.itemName, itemComponent.data.RarityColor, labelFont, labelFontSize, labelHeight);
            }
        }

        // 프리팹에 DroppedItem이 없어도 착지 후 고정되도록 자동으로 붙여 줌
        if (!item.TryGetComponent(out DroppedItem _))
        {
            item.AddComponent<DroppedItem>();
        }

        Launch(item);
    }

    // Rigidbody에 힘을 가해 아이템을 위로 통 튀어 오르게 함
    private void Launch(GameObject item)
    {
        if (!item.TryGetComponent(out Rigidbody rb))
        {
            Debug.LogWarning($"{item.name}에 Rigidbody가 없어 튀어 오르지 않습니다. 아이템 프리팹에 Rigidbody를 추가해 주세요.");
            return;
        }

        // 랜덤한 방향 중 수평 성분만 사용 (위아래 성분은 upwardForce가 담당)
        Vector3 sideDirection = Random.insideUnitSphere;
        sideDirection.y = 0f;

        Vector3 force = Vector3.up * upwardForce + sideDirection * sideForce;
        rb.AddForce(force, ForceMode.Impulse);

        // 공중에서 회전하며 떨어지도록 랜덤 회전력 추가
        rb.AddTorque(Random.insideUnitSphere * spinForce, ForceMode.Impulse);
    }

    // 드랍 테이블에서 가중치 확률로 아이템 하나 고르기 (고를 수 있는 것이 없으면 null)
    // 예: 가중치 30, 20, 10 → 0~60 사이 숫자를 뽑아 0~30이면 첫째, 30~50이면 둘째, 50~60이면 셋째
    private LootEntry PickEntry()
    {
        float totalWeight = 0f;
        foreach (LootEntry entry in dropTable)
        {
            if (IsValidEntry(entry)) totalWeight += entry.weight;
        }
        if (totalWeight <= 0f) return null;

        float roll = Random.value * totalWeight;
        LootEntry lastValid = null; // 계산 오차로 끝까지 못 고른 경우를 대비한 마지막 후보
        foreach (LootEntry entry in dropTable)
        {
            if (!IsValidEntry(entry)) continue;

            lastValid = entry;
            roll -= entry.weight;
            if (roll < 0f) return entry;
        }
        return lastValid;
    }

    // 이름이 있고 가중치가 0보다 큰 줄만 드랍 후보로 인정
    private static bool IsValidEntry(LootEntry entry)
    {
        return entry != null && entry.item != null && !entry.item.IsEmpty && entry.weight > 0f;
    }

    // 확률에 따라 아이템 등급 결정
    // 노멀 70%, 매직 20%, 레어 9%, 유니크 1%
    private ItemRarity RollRarity()
    {
        float roll = Random.value; // 0 ~ 1 사이의 랜덤 값

        if (roll < 0.01f) return ItemRarity.Unique; // 0.00 ~ 0.01 : 1%
        if (roll < 0.10f) return ItemRarity.Rare;   // 0.01 ~ 0.10 : 9%
        if (roll < 0.30f) return ItemRarity.Magic;  // 0.10 ~ 0.30 : 20%
        return ItemRarity.Normal;                   // 0.30 ~ 1.00 : 70%
    }

    // 등급에 맞는 색상을 아이템의 재질(Material)에 적용
    private void ApplyRarityColor(GameObject item, ItemRarity rarity)
    {
        Renderer itemRenderer = item.GetComponentInChildren<Renderer>();
        if (itemRenderer == null) return;

        // .material은 이 아이템 전용 재질 복사본을 만들므로 다른 아이템 색에 영향을 주지 않음
        itemRenderer.material.color = GetRarityColor(rarity);
    }

    private float GetStatMultiplier(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Magic:  return magicStatMultiplier;
            case ItemRarity.Rare:   return rareStatMultiplier;
            case ItemRarity.Unique: return uniqueStatMultiplier;
            default:                return 1f;
        }
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Magic:  return magicColor;
            case ItemRarity.Rare:   return rareColor;
            case ItemRarity.Unique: return uniqueColor;
            default:                return normalColor;
        }
    }
}
