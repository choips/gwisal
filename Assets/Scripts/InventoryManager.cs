using System.Collections.Generic;
using TMPro;                     // TMP_Text, TextMeshProUGUI 사용
using UnityEngine;
using UnityEngine.InputSystem;   // 새 Input System (Keyboard.current) 사용
using UnityEngine.UI;            // Button, GridLayoutGroup 사용

// 인벤토리 + 장비 관리자 (싱글톤)
// - 어디서든 InventoryManager.Instance로 접근해서 아이템을 추가할 수 있습니다.
// - 'I' 키로 인벤토리 UI 패널을 열고 닫습니다.
// - 가방은 디아블로식 격자(기본 6×5 = 30칸)로 보여 주고, 칸을 클릭하면 그 아이템을 장착합니다.
// - 가방이 가득 차면 더 이상 아이템을 주울 수 없습니다.
// - 무기·방어구 장비 칸 버튼을 클릭하면 장착 중인 장비를 해제해 가방으로 돌려보냅니다.
// - 기력 물약은 같은 것끼리 한 칸으로 묶어 보여 주고, 클릭하거나 단축키(1)를 누르면 마십니다.
public class InventoryManager : MonoBehaviour
{
    // 씬에 하나만 존재하는 인벤토리 관리자에 접근하기 위한 전역 참조
    public static InventoryManager Instance { get; private set; }

    [Header("인벤토리 데이터")]
    [Tooltip("가방에 들어 있는 아이템 목록 (Play 중에 Inspector에서 확인용)")]
    public List<ItemData> inventory = new List<ItemData>();

    [Header("UI 연결")]
    [Tooltip("'I' 키로 열고 닫을 인벤토리 패널")]
    [SerializeField] private GameObject inventoryPanel;

    [Tooltip("(선택) 장착 중인 장비와 가방 사용 칸 수를 표시할 텍스트 (이 글꼴을 가방 칸 글자에도 사용)")]
    [SerializeField] private TextMeshProUGUI equipmentText;

    [Tooltip("(선택) 아이템 버튼에 마우스를 올리면 띄울 툴팁 창")]
    [SerializeField] private ItemTooltip itemTooltip;

    [Header("장비 칸 (클릭하면 해제)")]
    [Tooltip("장착 중인 무기를 보여 주는 버튼 (자식에 TextMeshPro 텍스트 포함)")]
    [SerializeField] private Button weaponSlotButton;

    [Tooltip("장착 중인 방어구를 보여 주는 버튼 (자식에 TextMeshPro 텍스트 포함)")]
    [SerializeField] private Button armorSlotButton;

    [Tooltip("장비 칸이 비어 있을 때 글자 색")]
    [SerializeField] private Color emptySlotColor = new Color(0.6f, 0.6f, 0.6f); // 회색

    [Header("격자 가방")]
    [Tooltip("가방 칸들이 자동으로 만들어질 부모 (InventoryPanel 안의 빈 오브젝트 BagGrid)")]
    [SerializeField] private RectTransform bagGrid;

    [Tooltip("한 줄에 놓을 칸 수 (가로)")]
    [SerializeField] private int bagColumns = 6;

    [Tooltip("가방 전체 칸 수 (6열이면 30칸 = 5줄)")]
    [SerializeField] private int bagCapacity = 30;

    [Tooltip("칸 하나의 크기 (픽셀)")]
    [SerializeField] private float slotSize = 56f;

    [Tooltip("칸 사이 간격 (픽셀)")]
    [SerializeField] private float slotSpacing = 4f;

    [Tooltip("칸 배경 색")]
    [SerializeField] private Color slotColor = new Color(0.12f, 0.12f, 0.15f, 0.95f);

    [Header("아이템 아이콘")]
    [Tooltip("아이템 전용 아이콘을 찾을 Resources 안 폴더. 아이템 이름과 같은 이름의 그림을 넣으면 됩니다 (예: Resources/ItemIcons/낡은 단검.png)")]
    [SerializeField] private string itemIconFolder = "ItemIcons";

    [Tooltip("전용 아이콘이 없을 때 쓰는 종류별 공용 아이콘 (이것도 비워 두면 검/갑/약 글자로 표시)")]
    [SerializeField] private Sprite weaponIcon;
    [SerializeField] private Sprite armorIcon;
    [SerializeField] private Sprite potionIcon;

    [Header("설정")]
    [Tooltip("게임 시작 시 인벤토리를 열어 둘지 여부")]
    [SerializeField] private bool openOnStart = false;

    [Tooltip("기력 물약(Mana Potion)을 마시는 단축키 (인벤토리를 닫아 두어도 사용 가능)")]
    [SerializeField] private Key potionKey = Key.Digit1;

    // 장착 중인 장비 (없으면 null)
    // Inspector에 노출하면 Unity가 빈 객체를 자동으로 채워 null 검사가 어려워지므로 직렬화하지 않음
    private ItemData equippedWeapon;
    private ItemData equippedArmor;

    private PlayerStats playerStats; // 장착 시 능력치를 바꿀 플레이어

    private readonly List<ItemSlotUI> bagSlots = new List<ItemSlotUI>(); // 코드로 만든 가방 칸들
    private readonly Dictionary<string, Sprite> itemIconCache = new Dictionary<string, Sprite>(); // 아이템 이름 → 전용 아이콘 (없으면 null)

    private const string SlotIconName = "EquipIcon"; // 장비 칸 버튼에 코드로 붙이는 아이콘 오브젝트 이름
    private static readonly Color EmptySlotBorderColor = new Color(0.3f, 0.3f, 0.35f); // 빈 장비 칸 테두리 색

    public ItemData EquippedWeapon => equippedWeapon;
    public ItemData EquippedArmor => equippedArmor;

    // 지금 가방에서 차지하고 있는 칸 수 (같은 물약 묶음은 1칸)
    public int UsedSlotCount => GetBagEntries().Count;
    public int BagCapacity => bagCapacity;

    private void Awake()
    {
        // 이미 다른 InventoryManager가 있다면 중복이므로 자신을 제거
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("InventoryManager가 씬에 2개 이상 있어 중복된 것을 제거합니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        // 씬이 바뀌는 등으로 파괴될 때 전역 참조도 비워 줌
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(openOnStart);
        }

        if (weaponSlotButton != null) weaponSlotButton.onClick.AddListener(UnequipWeapon);
        if (armorSlotButton != null) armorSlotButton.onClick.AddListener(UnequipArmor);

        RefreshUI();
    }

    private void Update()
    {
        // 키보드가 연결되어 있지 않으면 아무것도 하지 않음
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.iKey.wasPressedThisFrame)
        {
            ToggleInventory();
        }

        if (keyboard[potionKey].wasPressedThisFrame)
        {
            DrinkFirstPotion();
        }
    }

    // 인벤토리 패널 열기/닫기 전환 (닫기 버튼의 OnClick에도 연결 가능)
    public void ToggleInventory()
    {
        if (inventoryPanel == null)
        {
            Debug.LogWarning("InventoryManager에 Inventory Panel이 연결되지 않았습니다.");
            return;
        }

        inventoryPanel.SetActive(!inventoryPanel.activeSelf);
    }

    // 아이템을 가방에 추가하고 UI 갱신 (가방이 가득 차서 못 넣으면 false)
    public bool AddItem(ItemData item)
    {
        if (item == null || item.IsEmpty) return false;

        if (!HasRoomFor(item))
        {
            NotificationUI.Show($"가방이 가득 찼습니다! ({bagCapacity}칸)", NotificationUI.NoticeType.Warning);
            return false;
        }

        inventory.Add(item);
        RefreshUI();
        return true;
    }

    // 이 아이템을 가방에 넣을 자리가 있는지 (같은 이름 물약 묶음이 이미 있으면 칸이 늘지 않으므로 항상 가능)
    public bool HasRoomFor(ItemData item)
    {
        if (item.IsConsumable && inventory.Exists(other => other.IsConsumable && other.itemName == item.itemName))
        {
            return true;
        }
        return UsedSlotCount < bagCapacity;
    }

    // 가방 전체를 주어진 목록으로 교체하고 UI 갱신 (세이브 불러오기용)
    public void SetItems(List<ItemData> items)
    {
        inventory.Clear();
        if (items != null)
        {
            foreach (ItemData item in items)
            {
                if (item != null && !item.IsEmpty)
                {
                    inventory.Add(item);
                }
            }
        }

        // 저장된 아이템을 버리지 않도록 그대로 불러오되, 칸이 모자라면 넘친 아이템은 칸에 보이지 않음
        if (UsedSlotCount > bagCapacity)
        {
            Debug.LogWarning($"불러온 아이템이 가방 칸 수({bagCapacity})보다 많아 일부가 보이지 않습니다.");
        }
        RefreshUI();
    }

    // 장비 슬롯을 그대로 복원 (세이브 불러오기용)
    // 저장된 공격력/체력에는 이미 장비 수치가 포함되어 있으므로 능력치는 건드리지 않음
    public void SetEquipment(ItemData weapon, ItemData armor)
    {
        equippedWeapon = weapon == null || weapon.IsEmpty ? null : weapon;
        equippedArmor = armor == null || armor.IsEmpty ? null : armor;
        RefreshUI();
    }

    // 가방의 아이템 버튼을 클릭했을 때: 소모품이면 사용, 장비면 장착
    public void UseItem(ItemData item)
    {
        if (item == null) return;

        if (item.IsConsumable)
        {
            DrinkPotion(item);
        }
        else
        {
            EquipItem(item);
        }
    }

    // 가방에 든 특정 종류 아이템의 개수 (물약 칸 UI가 남은 물약 수를 표시할 때 사용)
    public int CountItems(ItemType type)
    {
        int count = 0;
        foreach (ItemData item in inventory)
        {
            if (item.itemType == type) count++;
        }
        return count;
    }

    // 단축키: 가방에서 처음 찾은 기력 물약을 마심
    public void DrinkFirstPotion()
    {
        ItemData potion = inventory.Find(item => item.itemType == ItemType.ManaPotion);
        if (potion == null)
        {
            NotificationUI.Show("기력 물약이 없습니다.", NotificationUI.NoticeType.Warning);
            return;
        }

        DrinkPotion(potion);
    }

    // 기력 물약을 마셔 기력 회복 (기력이 가득 차 있으면 물약을 쓰지 않음)
    private void DrinkPotion(ItemData potion)
    {
        if (!inventory.Contains(potion)) return;

        PlayerStats stats = GetUsablePlayerStats();
        if (stats == null) return;

        if (!stats.RestoreMP(potion.statValue)) return;

        inventory.Remove(potion);
        NotificationUI.Show($"{potion.itemName} 사용! 기력 +{potion.statValue}", NotificationUI.NoticeType.Success);
        RefreshUI();
    }

    // 가방에 있는 장비를 장착
    public void EquipItem(ItemData item)
    {
        if (item == null || item.IsConsumable || !inventory.Contains(item)) return;

        PlayerStats stats = GetUsablePlayerStats();
        if (stats == null) return;

        inventory.Remove(item);

        switch (item.itemType)
        {
            case ItemType.Weapon:
                EquipWeapon(item, stats);
                break;
            case ItemType.Armor:
                EquipArmor(item, stats);
                break;
        }

        RefreshUI();
    }

    // 장착 중인 무기를 해제해 가방으로 (무기 칸 버튼을 클릭하면 호출됨)
    public void UnequipWeapon()
    {
        if (equippedWeapon == null) return;
        if (!CheckRoomToUnequip(equippedWeapon)) return;

        PlayerStats stats = GetUsablePlayerStats();
        if (stats == null) return;

        RemoveWeapon(stats);
        RefreshUI();
    }

    // 장착 중인 방어구를 해제해 가방으로 (방어구 칸 버튼을 클릭하면 호출됨)
    public void UnequipArmor()
    {
        if (equippedArmor == null) return;
        if (!CheckRoomToUnequip(equippedArmor)) return;

        PlayerStats stats = GetUsablePlayerStats();
        if (stats == null) return;

        RemoveArmor(stats);
        RefreshUI();
    }

    // 장비를 벗어 가방에 넣을 자리가 있는지 확인 (없으면 안내 후 false)
    private bool CheckRoomToUnequip(ItemData equipped)
    {
        if (HasRoomFor(equipped)) return true;

        NotificationUI.Show("가방이 가득 차서 장비를 벗을 수 없습니다.", NotificationUI.NoticeType.Warning);
        return false;
    }

    // 무기 장착: 기존 무기가 있으면 먼저 해제(가방으로)한 뒤 새 무기 장착
    private void EquipWeapon(ItemData weapon, PlayerStats stats)
    {
        if (equippedWeapon != null)
        {
            RemoveWeapon(stats);
        }

        equippedWeapon = weapon;
        stats.attackPower += weapon.statValue;
        Debug.Log($"무기 장착: 공격력이 {weapon.statValue} 증가했습니다!");
    }

    // 방어구 장착: 기존 방어구가 있으면 먼저 해제(가방으로)한 뒤 새 방어구 장착
    private void EquipArmor(ItemData armor, PlayerStats stats)
    {
        if (equippedArmor != null)
        {
            RemoveArmor(stats);
        }

        equippedArmor = armor;
        stats.maxHP += armor.statValue;
        stats.currentHP += armor.statValue;
        Debug.Log($"방어구 장착: 최대 체력이 {armor.statValue} 증가했습니다!");
    }

    // 장착 중인 무기를 빼서 가방에 넣고 공격력을 되돌림
    private void RemoveWeapon(PlayerStats stats)
    {
        stats.attackPower -= equippedWeapon.statValue;
        inventory.Add(equippedWeapon);
        Debug.Log($"무기 해제: {equippedWeapon.DisplayName} (공격력 -{equippedWeapon.statValue})");
        equippedWeapon = null;
    }

    // 장착 중인 방어구를 빼서 가방에 넣고 최대 체력을 되돌림
    private void RemoveArmor(PlayerStats stats)
    {
        stats.maxHP -= equippedArmor.statValue;

        // 장착할 때 늘어난 만큼 현재 체력도 줄임 (장착·해제를 반복해 체력을 채우는 것 방지)
        // 단, 장비를 벗다가 죽지는 않도록 최소 1은 남김
        stats.currentHP = Mathf.Clamp(stats.currentHP - equippedArmor.statValue, 1f, stats.maxHP);

        inventory.Add(equippedArmor);
        Debug.Log($"방어구 해제: {equippedArmor.DisplayName} (최대 체력 -{equippedArmor.statValue})");
        equippedArmor = null;
    }

    // 장비를 바꾸거나 물약을 쓸 수 있는 상태의 플레이어 능력치 (찾지 못했거나 사망 중이면 null)
    private PlayerStats GetUsablePlayerStats()
    {
        PlayerStats stats = GetPlayerStats();
        if (stats == null)
        {
            Debug.LogWarning("씬에서 PlayerStats를 찾지 못해 아이템을 쓸 수 없습니다.");
            return null;
        }

        if (stats.IsDead)
        {
            NotificationUI.Show("사망한 상태에서는 아이템을 쓸 수 없습니다.", NotificationUI.NoticeType.Warning);
            return null;
        }

        return stats;
    }

    private PlayerStats GetPlayerStats()
    {
        if (playerStats == null)
        {
            playerStats = FindFirstObjectByType<PlayerStats>();
        }
        return playerStats;
    }

    // 가방 칸과 장비 표시를 현재 데이터에 맞게 갱신
    private void RefreshUI()
    {
        RefreshEquipmentText();
        RefreshSlotButton(weaponSlotButton, equippedWeapon, "무기");
        RefreshSlotButton(armorSlotButton, equippedArmor, "방어구");

        if (bagGrid == null) return;

        EnsureBagSlots();

        // 앞 칸부터 차례로 채우고, 남은 칸은 비움
        List<BagEntry> entries = GetBagEntries();
        for (int i = 0; i < bagSlots.Count; i++)
        {
            if (i < entries.Count)
            {
                bagSlots[i].Show(entries[i].item, entries[i].count, GetIcon(entries[i].item));
            }
            else
            {
                bagSlots[i].Clear();
            }
        }
    }

    // 가방 칸 하나에 들어갈 내용 (물약은 같은 이름끼리 묶어 count에 개수)
    private struct BagEntry
    {
        public ItemData item;
        public int count;
    }

    // 가방 목록을 칸 단위로 정리 (장비는 1개당 1칸, 같은 이름 물약은 묶어서 1칸)
    private List<BagEntry> GetBagEntries()
    {
        List<BagEntry> entries = new List<BagEntry>();
        Dictionary<string, int> consumableIndex = new Dictionary<string, int>(); // 물약 이름 → entries 위치

        foreach (ItemData item in inventory)
        {
            if (item.IsConsumable && consumableIndex.TryGetValue(item.itemName, out int index))
            {
                BagEntry stack = entries[index];
                stack.count++;
                entries[index] = stack;
                continue;
            }

            if (item.IsConsumable)
            {
                consumableIndex[item.itemName] = entries.Count;
            }
            entries.Add(new BagEntry { item = item, count = 1 });
        }

        return entries;
    }

    // 가방 칸이 아직 없으면 bagCapacity개만큼 한 번만 만들어 둠 (이후에는 내용만 바꿔 끼움)
    private void EnsureBagSlots()
    {
        if (bagSlots.Count > 0) return;

        // 칸을 격자 모양으로 자동 배치해 주는 Grid Layout Group 설정
        if (!bagGrid.TryGetComponent(out GridLayoutGroup grid))
        {
            grid = bagGrid.gameObject.AddComponent<GridLayoutGroup>();
        }
        grid.cellSize = new Vector2(slotSize, slotSize);
        grid.spacing = new Vector2(slotSpacing, slotSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; // 한 줄에 bagColumns칸 고정
        grid.constraintCount = Mathf.Max(1, bagColumns);
        grid.childAlignment = TextAnchor.UpperCenter;

        TMP_FontAsset font = equipmentText != null ? equipmentText.font : null;
        for (int i = 0; i < bagCapacity; i++)
        {
            bagSlots.Add(ItemSlotUI.Create(bagGrid, font, itemTooltip, UseItem, slotColor));
        }
    }

    // 아이템 아이콘: 이름과 같은 전용 아이콘이 있으면 그것, 없으면 종류별 공용 아이콘
    private Sprite GetIcon(ItemData item)
    {
        Sprite itemIcon = GetItemIcon(item.itemName);
        if (itemIcon != null) return itemIcon;

        switch (item.itemType)
        {
            case ItemType.Weapon: return weaponIcon;
            case ItemType.Armor: return armorIcon;
            case ItemType.ManaPotion: return potionIcon;
            default: return null;
        }
    }

    // Resources/ItemIcons 폴더에서 아이템 이름과 같은 그림을 찾음 (찾은 결과는 기억해 두고 재사용)
    private Sprite GetItemIcon(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return null;

        if (!itemIconCache.TryGetValue(itemName, out Sprite sprite))
        {
            sprite = Resources.Load<Sprite>($"{itemIconFolder}/{itemName}");
            itemIconCache[itemName] = sprite;
        }
        return sprite;
    }

    // 장비 칸 버튼에 장착 중인 장비를 표시 (비어 있으면 회색 "없음", 클릭 불가)
    private void RefreshSlotButton(Button slotButton, ItemData equipped, string slotName)
    {
        if (slotButton == null) return;

        slotButton.interactable = equipped != null;

        TMP_Text label = slotButton.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = equipped != null
                ? $"{slotName}: {equipped.DisplayName} ({equipped.StatDescription})"
                : $"{slotName}: 없음";
            label.color = equipped != null ? equipped.NameColor : emptySlotColor;
        }

        // 왼쪽 아이콘 (그림이 연결되지 않았거나 비어 있으면 숨김)
        Image slotIcon = GetOrCreateSlotIcon(slotButton, label);
        Sprite sprite = equipped != null ? GetIcon(equipped) : null;
        slotIcon.sprite = sprite;
        slotIcon.enabled = sprite != null;

        // 가방 칸처럼 등급 색 테두리
        if (!slotButton.TryGetComponent(out Outline border))
        {
            border = slotButton.gameObject.AddComponent<Outline>();
            border.effectDistance = new Vector2(2f, -2f);
        }
        border.effectColor = equipped != null ? equipped.NameColor : EmptySlotBorderColor;

        if (itemTooltip != null)
        {
            if (!slotButton.TryGetComponent(out ItemTooltipTrigger trigger))
            {
                trigger = slotButton.gameObject.AddComponent<ItemTooltipTrigger>();
            }
            trigger.Setup(equipped, itemTooltip, true);
        }
    }

    // 장비 칸 버튼 왼쪽의 아이콘 이미지 (처음 한 번만 코드로 만들고, 이후에는 찾아서 재사용)
    private Image GetOrCreateSlotIcon(Button slotButton, TMP_Text label)
    {
        Transform existing = slotButton.transform.Find(SlotIconName);
        if (existing != null) return existing.GetComponent<Image>();

        // 버튼 높이에 맞춘 정사각형 (위아래 3픽셀씩 여백)
        float iconSize = ((RectTransform)slotButton.transform).rect.height - 6f;
        if (iconSize <= 0f) iconSize = 30f;

        GameObject iconObject = new GameObject(SlotIconName, typeof(RectTransform));
        iconObject.transform.SetParent(slotButton.transform, false);

        // 버튼 왼쪽 가운데에 붙임
        RectTransform rect = (RectTransform)iconObject.transform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(4f, 0f);
        rect.sizeDelta = new Vector2(iconSize, iconSize);

        Image image = iconObject.AddComponent<Image>();
        image.preserveAspect = true;  // 그림 비율 유지
        image.raycastTarget = false;  // 클릭은 버튼이 받도록

        // 글자가 아이콘과 겹치지 않도록 글자 왼쪽 여백을 아이콘 너비만큼 늘림
        if (label != null)
        {
            Vector4 margin = label.margin; // x = 왼쪽 여백
            margin.x += iconSize + 6f;
            label.margin = margin;
        }

        return image;
    }

    // 장착 중인 장비와 보유 개수 표시
    // 장비 칸 버튼이 연결되어 있으면 그 장비 줄은 버튼이 대신 보여 주므로 생략
    private void RefreshEquipmentText()
    {
        if (equipmentText == null) return;

        string text = "";

        if (weaponSlotButton == null)
        {
            string weaponLine = equippedWeapon != null
                ? $"{equippedWeapon.DisplayName} ({equippedWeapon.StatDescription})"
                : "없음";
            text += $"무기: {weaponLine}\n";
        }

        if (armorSlotButton == null)
        {
            string armorLine = equippedArmor != null
                ? $"{equippedArmor.DisplayName} ({equippedArmor.StatDescription})"
                : "없음";
            text += $"방어구: {armorLine}\n";
        }

        int usedSlots = UsedSlotCount;
        text += usedSlots > 0
            ? $"가방 {usedSlots} / {bagCapacity}칸 (클릭: 장착/사용)"
            : $"가방 0 / {bagCapacity}칸 (비어 있음)";

        equipmentText.text = text;
    }
}
