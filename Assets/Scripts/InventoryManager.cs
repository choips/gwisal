using System.Collections.Generic;
using TMPro;                     // TMP_Text, TextMeshProUGUI 사용
using UnityEngine;
using UnityEngine.InputSystem;   // 새 Input System (Keyboard.current) 사용
using UnityEngine.UI;            // Button 사용

// 인벤토리 + 장비 관리자 (싱글톤)
// - 어디서든 InventoryManager.Instance로 접근해서 아이템을 추가할 수 있습니다.
// - 'I' 키로 인벤토리 UI 패널을 열고 닫습니다.
// - 아이템마다 버튼을 만들어 목록에 나열하고, 버튼을 클릭하면 그 아이템을 장착합니다.
// - 무기·방어구 장비 칸 버튼을 클릭하면 장착 중인 장비를 해제해 가방으로 돌려보냅니다.
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

    [Tooltip("아이템 버튼들이 생성될 부모 (Scroll View의 Content)")]
    [SerializeField] private Transform itemListParent;

    [Tooltip("아이템 한 칸으로 쓸 버튼 프리팹 (자식에 TextMeshPro 텍스트 포함)")]
    [SerializeField] private Button itemButtonPrefab;

    [Tooltip("(선택) 장착 중인 장비와 보유 개수를 표시할 텍스트")]
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

    [Header("설정")]
    [Tooltip("게임 시작 시 인벤토리를 열어 둘지 여부")]
    [SerializeField] private bool openOnStart = false;

    // 장착 중인 장비 (없으면 null)
    // Inspector에 노출하면 Unity가 빈 객체를 자동으로 채워 null 검사가 어려워지므로 직렬화하지 않음
    private ItemData equippedWeapon;
    private ItemData equippedArmor;

    private PlayerStats playerStats; // 장착 시 능력치를 바꿀 플레이어

    public ItemData EquippedWeapon => equippedWeapon;
    public ItemData EquippedArmor => equippedArmor;

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

    // 아이템을 가방에 추가하고 UI 갱신
    public void AddItem(ItemData item)
    {
        if (item == null || item.IsEmpty) return;

        inventory.Add(item);
        RefreshUI();
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

    // 가방에 있는 아이템을 장착 (아이템 버튼을 클릭하면 호출됨)
    public void EquipItem(ItemData item)
    {
        if (item == null || !inventory.Contains(item)) return;

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

        PlayerStats stats = GetUsablePlayerStats();
        if (stats == null) return;

        RemoveWeapon(stats);
        RefreshUI();
    }

    // 장착 중인 방어구를 해제해 가방으로 (방어구 칸 버튼을 클릭하면 호출됨)
    public void UnequipArmor()
    {
        if (equippedArmor == null) return;

        PlayerStats stats = GetUsablePlayerStats();
        if (stats == null) return;

        RemoveArmor(stats);
        RefreshUI();
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

    // 장비를 바꿀 수 있는 상태의 플레이어 능력치 (찾지 못했거나 사망 중이면 null)
    private PlayerStats GetUsablePlayerStats()
    {
        PlayerStats stats = GetPlayerStats();
        if (stats == null)
        {
            Debug.LogWarning("씬에서 PlayerStats를 찾지 못해 장비를 바꿀 수 없습니다.");
            return null;
        }

        if (stats.IsDead)
        {
            Debug.Log("사망한 상태에서는 장비를 바꿀 수 없습니다.");
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

    // 아이템 버튼 목록과 장비 텍스트를 현재 데이터에 맞게 다시 만듦
    private void RefreshUI()
    {
        RefreshEquipmentText();
        RefreshSlotButton(weaponSlotButton, equippedWeapon, "무기");
        RefreshSlotButton(armorSlotButton, equippedArmor, "방어구");

        if (itemListParent == null || itemButtonPrefab == null) return;

        // 기존 버튼 제거 (Destroy는 프레임 끝에 처리되므로, 그 전까지 목록 배치에서 빠지도록 먼저 비활성화)
        for (int i = itemListParent.childCount - 1; i >= 0; i--)
        {
            GameObject oldButton = itemListParent.GetChild(i).gameObject;
            oldButton.SetActive(false);
            Destroy(oldButton);
        }

        // 아이템마다 버튼을 새로 생성
        foreach (ItemData item in inventory)
        {
            Button button = Instantiate(itemButtonPrefab, itemListParent);

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = $"{item.DisplayName}  ({item.StatDescription})";
                label.color = item.RarityColor;
            }

            // 이 버튼을 누르면 "이 버튼의 아이템"을 장착하도록 클릭 이벤트 연결
            button.onClick.AddListener(() => EquipItem(item));

            // 마우스를 올리면 이 아이템의 툴팁이 뜨도록 연결
            if (itemTooltip != null)
            {
                button.gameObject.AddComponent<ItemTooltipTrigger>().Setup(item, itemTooltip);
            }
        }
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
            label.color = equipped != null ? equipped.RarityColor : emptySlotColor;
        }

        if (itemTooltip != null)
        {
            if (!slotButton.TryGetComponent(out ItemTooltipTrigger trigger))
            {
                trigger = slotButton.gameObject.AddComponent<ItemTooltipTrigger>();
            }
            trigger.Setup(equipped, itemTooltip, true);
        }
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

        text += inventory.Count > 0
            ? $"보유 아이템: {inventory.Count}개 (클릭하여 장착)"
            : "(가방이 비어 있음)";

        equipmentText.text = text;
    }
}
