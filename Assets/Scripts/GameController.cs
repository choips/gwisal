using System.Collections.Generic;
using UnityEngine;

// 게임 전체 단축키 관리자 (씬에 하나 배치)
// 빠른 저장(QuickSave, 기본 S 키): 현재 플레이어 상태와 인벤토리를 저장
// 빠른 불러오기(QuickLoad, 기본 L 키): 저장된 데이터를 불러와 플레이어 상태와 인벤토리를 덮어씀
// 키 연결은 Assets/InputSystem_Actions의 Player 묶음에서 바꿉니다.
public class GameController : MonoBehaviour
{
    // 지금 플레이 중인 캐릭터 칸 번호 (캐릭터 선택 화면은 S7, 그 전까지는 항상 0번)
    private const int CharacterSlot = 0;

    [Tooltip("저장/불러오기할 플레이어 (비워두면 씬에서 자동으로 찾음)")]
    [SerializeField] private PlayerStats playerStats;

    private void Update()
    {
        if (GameInput.QuickSave.WasPressedThisFrame())
        {
            SaveCurrentGame();
        }
        else if (GameInput.QuickLoad.WasPressedThisFrame())
        {
            LoadSavedGame();
        }
    }

    // 현재 상태를 CharacterSaveData에 담아 저장
    private void SaveCurrentGame()
    {
        if (!TryGetReferences(out PlayerStats stats)) return;

        CharacterSaveData data = new CharacterSaveData
        {
            level = stats.level,
            currentXP = stats.currentXP,
            requiredXP = stats.requiredXP,
            currentHP = stats.currentHP,
            currentMP = stats.currentMP,
        };

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null)
        {
            foreach (ItemData item in inventory.inventory)
            {
                SavedItem saved = SavedItem.FromItem(item);
                if (saved != null) data.inventoryItems.Add(saved);
            }
            data.AddEquipment(SavedEquipment.WeaponSlot, inventory.EquippedWeapon);
            data.AddEquipment(SavedEquipment.ArmorSlot, inventory.EquippedArmor);
        }

        if (SaveManager.Instance.SaveCharacter(data, CharacterSlot))
        {
            NotificationUI.Show(Loc.Get("notice.save_done"), NotificationUI.NoticeType.Success);
        }
    }

    // 저장된 데이터를 불러와 현재 상태에 덮어씀
    private void LoadSavedGame()
    {
        if (!TryGetReferences(out PlayerStats stats)) return;

        CharacterSaveData data = SaveManager.Instance.LoadCharacter(CharacterSlot);
        if (data == null)
        {
            NotificationUI.Show(Loc.Get("notice.load_no_save"), NotificationUI.NoticeType.Warning);
            return;
        }

        // 장비를 먼저 복원 → 최대 체력이 장비를 포함해 정해진 뒤 저장된 현재 체력을 넣음
        if (InventoryManager.Instance != null)
        {
            List<ItemData> items = new List<ItemData>();
            foreach (SavedItem saved in data.inventoryItems)
            {
                ItemData item = saved != null ? saved.ToItem() : null;
                if (item != null) items.Add(item);
            }

            InventoryManager.Instance.SetItems(items);
            InventoryManager.Instance.SetEquipment(
                ToItem(data.FindEquipment(SavedEquipment.WeaponSlot)),
                ToItem(data.FindEquipment(SavedEquipment.ArmorSlot)));
        }

        stats.ApplyCharacterData(data);

        NotificationUI.Show(Loc.Get("notice.load_done"), NotificationUI.NoticeType.Success);
    }

    private static ItemData ToItem(SavedItem saved)
    {
        return saved != null ? saved.ToItem() : null;
    }

    // 저장/불러오기에 필요한 SaveManager와 PlayerStats가 준비되어 있는지 확인
    private bool TryGetReferences(out PlayerStats stats)
    {
        // 연결되지 않았거나 씬 전환으로 사라졌다면 씬에서 다시 찾음
        if (playerStats == null)
        {
            playerStats = FindFirstObjectByType<PlayerStats>();
        }
        stats = playerStats;

        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("씬에 SaveManager가 없어 저장/불러오기를 할 수 없습니다.");
            return false;
        }

        if (stats == null)
        {
            Debug.LogWarning("씬에서 PlayerStats를 찾지 못해 저장/불러오기를 할 수 없습니다.");
            return false;
        }

        return true;
    }
}
