using System.Collections.Generic;
using TMPro;                     // TMP_FontAsset 사용 (바닥 이름표 글꼴)
using UnityEngine;
using UnityEngine.AI;            // NavMesh (아이템을 놓을 바닥 찾기)

// 드랍 테이블의 한 줄: 어떤 아이템이 얼마나 자주 떨어지는지
[System.Serializable]
public class LootEntry
{
    [Tooltip("나올 확률 가중치 (클수록 자주 나옴, 0이면 안 나옴)")]
    public float weight = 10f; // 가중치

    [Tooltip("떨어질 기본 아이템 (Assets/Data/Items의 파일, 능력치는 이 파일의 Base Stat Value × 등급 배율)")]
    public ItemBase baseItem;
}

// 디아블로 스타일 아이템 드랍 스크립트
// 드랍 테이블에서 아이템 종류를 무작위로 고르고(가중치 확률), 등급도 무작위로 정한 뒤
// 죽은 자리 주변의 빈자리(다른 아이템과 떨어진 NavMesh 위)를 골라 포물선으로 떨어뜨리고 등급에 따라 색을 바꿉니다.
// 등급 색은 Assets/Resources/ItemRarityColors(등급 색 표)에서 가져옵니다.
public class LootDropper : MonoBehaviour
{
    // 빈자리 찾기: 한 번에 뽑아 보는 후보 수, 자리가 없을 때 반경을 넓히는 최대 횟수 (넓힐 때마다 최소 간격만큼)
    private const int CandidatesPerTry = 16;
    private const int MaxRadiusExpansions = 4;

    // BalanceConfig가 없을 때 쓰는 기본값 (엑셀 LootScatterRadius·LootMinSpacing)
    private const float DefaultScatterRadius = 2f;
    private const float DefaultMinSpacing = 0.8f;

    [Header("드랍 아이템")]
    [Tooltip("바닥에 떨어질 아이템 모양 프리팹 (Item 스크립트와 클릭용 Collider가 붙어 있어야 합니다. Rigidbody는 있어도 자동으로 고정됨)")]
    public GameObject itemPrefab;

    [Tooltip("떨어질 수 있는 기본 아이템 목록 (Base Item이 비어 있거나 가중치가 0인 줄은 무시)\n"
           + "가중치 예: 30 / 20 / 10이면 각각 50% / 33% / 17% 확률")]
    [SerializeField] private List<LootEntry> dropTable = new List<LootEntry>();

    [Header("기력 물약(Mana Potion)")]
    [Tooltip("장비와 별도로 기력 물약이 하나 더 떨어질 확률 (0.3 = 30%)")]
    [Range(0f, 1f)] [SerializeField] private float potionDropChance = 0.3f;

    [Tooltip("물약 기본 아이템 (Assets/Data/Items/Item_potion_mana, 회복량은 이 파일의 Base Stat Value, 비워 두면 물약이 안 떨어짐)")]
    [SerializeField] private ItemBase potionItem;

    [Tooltip("장비 구슬 대비 물약 구슬 크기 (0.7 = 70%, 장비와 구별하기 쉽게)")]
    [SerializeField] private float potionScale = 0.7f;

    [Header("떨어지는 모양 (흩어지는 반경·간격은 BalanceConfig)")]
    [Tooltip("죽은 자리에서 착지할 자리까지 날아가는 시간(초)")]
    [Min(0f)] [SerializeField] private float flightTime = 0.4f;

    [Tooltip("포물선 꼭대기 높이(m) — 출발 높이와 착지 높이를 잇는 선보다 이만큼 위로 솟음")]
    [Min(0f)] [SerializeField] private float arcHeight = 1.2f;

    [Tooltip("공중에서 도는 바퀴 수 (0이면 돌지 않음)")]
    [Min(0)] [SerializeField] private int flipCount = 1;

    [Tooltip("날아오르기 시작하는 높이 (죽은 자리 발밑보다 이만큼 위에서 출발)")]
    [SerializeField] private float spawnHeightOffset = 0.5f;

    [Header("바닥 이름표 (글자·박스 크기는 BalanceConfig)")]
    [Tooltip("이름표 글꼴 (한글이 보이도록 NotoSansKR-Regular SDF 지정, 비워 두면 한글이 □로 깨짐) — 귀신 이름표도 이 글꼴을 빌려 씀")]
    [SerializeField] private TMP_FontAsset labelFont;

    [Tooltip("아이템 중심에서 이름표까지의 높이(m)")]
    [SerializeField] private float labelHeight = 0.5f;

    [Header("등급별 능력치 배율")]
    [Tooltip("아이템 프리팹에 설정된 능력치에 곱해지는 값 (등급이 높을수록 강해짐)")]
    [SerializeField] private float magicStatMultiplier = 1.5f;
    [SerializeField] private float rareStatMultiplier = 2f;
    [SerializeField] private float uniqueStatMultiplier = 3f;

    private float scatterRadius = DefaultScatterRadius; // 이번 드랍의 흩어지는 반경
    private float minSpacing = DefaultMinSpacing;       // 이번 드랍의 아이템 간 최소 간격

    // 몬스터 배치기가 이름표 글꼴을 빌려 쓰기 위한 통로
    public TMP_FontAsset LabelFont => labelFont;

    // dropPosition 위치에 장비를 equipmentCount개 떨어뜨리고, 장비마다 확률에 따라 기력 물약도 하나씩 더 떨어뜨림
    // rarityBonus: 좋은 등급(매직·레어·유니크) 확률 배율 (챔피언 2, 유니크 3 등)
    // itemLevel: 숨겨진 아이템 레벨 (떨어뜨린 귀신의 몬스터 레벨)
    // config: 흩어지는 반경·최소 간격 (null이면 기본값 2m·0.8m)
    public void DropItem(Vector3 dropPosition, int equipmentCount = 1, float rarityBonus = 1f, int itemLevel = 1,
                         BalanceConfig config = null)
    {
        if (itemPrefab == null)
        {
            Debug.LogWarning($"{gameObject.name}의 LootDropper에 Item Prefab이 등록되지 않았습니다.");
            return;
        }

        scatterRadius = config != null ? config.lootScatterRadius : DefaultScatterRadius;
        minSpacing = config != null ? config.lootMinSpacing : DefaultMinSpacing;

        for (int i = 0; i < equipmentCount; i++)
        {
            DropEquipment(dropPosition, rarityBonus, itemLevel);

            if (Random.value < potionDropChance)
            {
                DropPotion(dropPosition, itemLevel);
            }
        }
    }

    // 드랍 테이블에서 고른 장비를 무작위 등급으로 떨어뜨림 (등급에 따라 능력치 강화)
    private void DropEquipment(Vector3 dropPosition, float rarityBonus, int itemLevel)
    {
        LootEntry entry = PickEntry();
        if (entry == null)
        {
            Debug.LogWarning($"{gameObject.name}의 LootDropper 드랍 표에 Base Item이 들어간 줄이 없어 장비를 떨어뜨리지 않습니다.");
            return;
        }

        DropEquipment(dropPosition, entry, RollRarity(rarityBonus), itemLevel);
    }

    // 정해진 기본 아이템을 정해진 등급으로 떨어뜨림
    private void DropEquipment(Vector3 dropPosition, LootEntry entry, ItemRarity rarity, int itemLevel)
    {
        float statValue = Mathf.Round(entry.baseItem.baseStatValue * GetStatMultiplier(rarity));
        ItemData data = new ItemData(entry.baseItem, rarity, itemLevel, statValue);

        SpawnItem(dropPosition, data);
    }

    // 등급 색 확인용 (개발자 메뉴): 노멀·매직·레어·유니크 장비를 하나씩, 물약을 하나 떨어뜨림
    public void DropColorPreview(Vector3 dropPosition)
    {
        if (itemPrefab == null) return;

        scatterRadius = DefaultScatterRadius;
        minSpacing = DefaultMinSpacing;

        LootEntry entry = PickEntry();
        if (entry != null)
        {
            DropEquipment(dropPosition, entry, ItemRarity.Normal, 1);
            DropEquipment(dropPosition, entry, ItemRarity.Magic, 1);
            DropEquipment(dropPosition, entry, ItemRarity.Rare, 1);
            DropEquipment(dropPosition, entry, ItemRarity.Unique, 1);
        }
        DropPotion(dropPosition, 1);
    }

    // 기력 물약을 떨어뜨림 (물약은 등급이 없음)
    private void DropPotion(Vector3 dropPosition, int itemLevel)
    {
        if (potionItem == null)
        {
            Debug.LogWarning($"{gameObject.name}의 LootDropper에 Potion Item이 비어 있어 물약을 떨어뜨리지 않습니다.");
            return;
        }

        ItemData data = new ItemData(potionItem, ItemRarity.Normal, itemLevel, potionItem.baseStatValue);
        SpawnItem(dropPosition, data, potionScale);
    }

    // 아이템 모양 프리팹을 만들어 내용·색·이름표를 정하고 빈자리로 떨어뜨림
    // scale: 프리팹 크기에 곱할 배율 (물약은 장비보다 작게)
    private void SpawnItem(Vector3 dropPosition, ItemData data, float scale = 1f)
    {
        // 착지할 자리를 먼저 골라야, 같은 순간에 떨어지는 아이템끼리 같은 자리를 고르지 않음
        Vector3 groundPoint = FindLandingSpot(dropPosition);

        Vector3 spawnPosition = dropPosition + Vector3.up * spawnHeightOffset;
        Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); // 똑바로 선 채 방향만 무작위
        GameObject item = Instantiate(itemPrefab, spawnPosition, spawnRotation);
        item.transform.localScale *= scale;
        string itemName = data.Name;
        item.name = data.IsConsumable ? itemName : $"{itemName} ({data.rarity})";
        Item.ApplyObjectColor(item, data.NameColor);

        if (item.TryGetComponent(out Item itemComponent))
        {
            itemComponent.data = data;
            GameLog.Item($"아이템 드랍! {data.DisplayName} ({data.StatDescription}, 아이템 레벨 {data.itemLevel})");

            // 아이템 위에 이름 박스를 등급 색으로 표시 (화면 UI, 겹치면 위로 쌓임, 클릭하면 주움)
            ItemLabelManager.Register(itemComponent, labelFont, labelHeight);
        }

        // 프리팹에 DroppedItem이 없어도 포물선 착지·빈자리 목록에 오르도록 자동으로 붙여 줌
        if (!item.TryGetComponent(out DroppedItem droppedItem))
        {
            droppedItem = item.AddComponent<DroppedItem>();
        }
        droppedItem.Launch(groundPoint, flightTime, arcHeight, flipCount);
    }

    // 죽은 자리 주변(scatterRadius 안)에서 다른 바닥 아이템과 minSpacing 이상 떨어진 NavMesh 위 빈자리를 고름
    // 빈자리 후보 중 죽은 자리에 가장 가까운 곳을 골라 아이템이 한데 모이게 함 (디아블로 2 방식)
    // 자리가 없으면 반경을 minSpacing씩 넓히고, 끝까지 없으면 다른 아이템과 가장 먼 후보를 씀
    private Vector3 FindLandingSpot(Vector3 dropPosition)
    {
        // 죽은 자리를 NavMesh 위로 맞춤 (NavMesh가 근처에 없으면 그 자리에 그대로 떨어뜨림)
        if (!NavMesh.SamplePosition(dropPosition, out NavMeshHit centerHit, Mathf.Max(scatterRadius, 1f), NavMesh.AllAreas))
        {
            return dropPosition;
        }
        Vector3 center = centerHit.position;

        float minSpacingSqr = minSpacing * minSpacing;
        Vector3 fallbackSpot = center;                       // 빈자리가 끝내 없을 때 쓸 후보
        float fallbackClearanceSqr = GetClearanceSqr(center); // 그 후보에서 가장 가까운 아이템까지 거리²
        float radius = scatterRadius;

        for (int expansion = 0; expansion <= MaxRadiusExpansions; expansion++)
        {
            bool found = false;
            Vector3 nearestSpot = center;            // 빈자리 중 죽은 자리에 가장 가까운 곳
            float nearestDistanceSqr = float.MaxValue;

            for (int i = 0; i < CandidatesPerTry; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);

                // NavMesh 위가 아니면 제외
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, minSpacing * 0.5f, NavMesh.AllAreas)) continue;

                // 벽·낭떠러지 너머(죽은 자리에서 NavMesh를 따라 곧게 갈 수 없는 곳)는 제외
                if (NavMesh.Raycast(center, hit.position, out _, NavMesh.AllAreas)) continue;

                float clearanceSqr = GetClearanceSqr(hit.position);
                if (clearanceSqr >= minSpacingSqr)
                {
                    found = true;
                    float distanceSqr = (hit.position - center).sqrMagnitude;
                    if (distanceSqr < nearestDistanceSqr)
                    {
                        nearestDistanceSqr = distanceSqr;
                        nearestSpot = hit.position;
                    }
                }
                else if (clearanceSqr > fallbackClearanceSqr)
                {
                    fallbackClearanceSqr = clearanceSqr;
                    fallbackSpot = hit.position;
                }
            }

            if (found) return nearestSpot;
            radius += minSpacing;
        }

        return fallbackSpot;
    }

    // position에서 가장 가까운 바닥 아이템(날아가는 중 포함)까지의 수평 거리² (아이템이 없으면 아주 큰 수)
    private static float GetClearanceSqr(Vector3 position)
    {
        float minDistanceSqr = float.MaxValue;
        IReadOnlyList<DroppedItem> items = DroppedItem.Active;
        for (int i = 0; i < items.Count; i++)
        {
            Vector3 offset = items[i].LandingPosition - position;
            offset.y = 0f;
            minDistanceSqr = Mathf.Min(minDistanceSqr, offset.sqrMagnitude);
        }
        return minDistanceSqr;
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

    // 기본 아이템이 있고 가중치가 0보다 큰 줄만 드랍 후보로 인정
    private static bool IsValidEntry(LootEntry entry)
    {
        return entry != null && entry.baseItem != null && entry.weight > 0f;
    }

    // 확률에 따라 아이템 등급 결정
    // 기본(rarityBonus 1): 노멀 70%, 매직 20%, 레어 9%, 유니크 1%
    // rarityBonus만큼 매직·레어·유니크 확률이 늘어남 (예: 3이면 매직 60%, 레어 27%, 유니크 3%, 노멀 10%)
    private ItemRarity RollRarity(float rarityBonus = 1f)
    {
        float uniqueChance = 0.01f * rarityBonus;
        float rareChance = 0.09f * rarityBonus;
        float magicChance = 0.20f * rarityBonus;

        // 세 확률의 합이 100%를 넘지 않도록 비율을 유지한 채 줄임
        float total = uniqueChance + rareChance + magicChance;
        if (total > 1f)
        {
            uniqueChance /= total;
            rareChance /= total;
            magicChance /= total;
        }

        float roll = Random.value; // 0 ~ 1 사이의 랜덤 값

        if (roll < uniqueChance) return ItemRarity.Unique;
        if (roll < uniqueChance + rareChance) return ItemRarity.Rare;
        if (roll < uniqueChance + rareChance + magicChance) return ItemRarity.Magic;
        return ItemRarity.Normal;
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
}
