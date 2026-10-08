using UnityEngine;

// 디아블로 스타일 아이템 드랍 스크립트
// 지정한 위치에 아이템을 생성하고, 위로 튀어 오르게 한 뒤 등급에 따라 색을 바꿉니다.
public class LootDropper : MonoBehaviour
{
    [Header("드랍 아이템")]
    [Tooltip("드랍할 아이템 프리팹 (Rigidbody와 Collider가 붙어 있어야 합니다)")]
    public GameObject itemPrefab;

    [Header("튀어 오르는 힘")]
    [Tooltip("위쪽으로 가하는 힘")]
    [SerializeField] private float upwardForce = 5f;

    [Tooltip("옆으로 퍼지는 힘 (랜덤 방향)")]
    [SerializeField] private float sideForce = 2f;

    [Tooltip("아이템이 빙글빙글 도는 힘")]
    [SerializeField] private float spinForce = 3f;

    [Tooltip("바닥에 파묻혀 생성되지 않도록 dropPosition보다 위로 띄울 높이")]
    [SerializeField] private float spawnHeightOffset = 0.5f;

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
        Debug.Log($"아이템 드랍! 등급: {rarity}");

        // 아이템 데이터에 등급을 기록하고, 등급에 따라 능력치를 강화
        if (item.TryGetComponent(out Item itemComponent))
        {
            itemComponent.data.rarity = rarity;
            itemComponent.data.statValue = Mathf.Round(itemComponent.data.statValue * GetStatMultiplier(rarity));
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
