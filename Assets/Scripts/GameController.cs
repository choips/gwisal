using System.Collections.Generic;
using UnityEngine;

// 게임 전체 단축키 관리자 (씬에 하나 배치)
// 빠른 저장(QuickSave, 기본 S 키): 현재 플레이어 상태·인벤토리·마지막 지역/입구를 저장
// 빠른 불러오기(QuickLoad, 기본 L 키): 저장된 데이터를 불러와 덮어쓰고, 저장한 지역 입구로 이동
// 키 연결은 Assets/InputSystem_Actions의 Player 묶음에서 바꿉니다.
public class GameController : MonoBehaviour
{
    // 지금 플레이 중인 캐릭터 칸 번호 (캐릭터 선택 화면은 S7, 그 전까지는 항상 0번)
    private const int CharacterSlot = 0;

    [Tooltip("저장/불러오기할 플레이어 (비워두면 씬에서 자동으로 찾음)")]
    [SerializeField] private PlayerStats playerStats;

    // 산 아래 마을 탈환 여부 ([전달-19] — 아직 게임플레이 없음, 세이브만 주고받음)
    private bool villageLiberated;

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
            villageLiberated = villageLiberated,
        };

        // 지금 지역·입구 (RegionManager가 없으면 시작 지역으로)
        FillLocationFromRegionManager(data);

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

    // 저장된 데이터를 불러와 현재 상태에 덮어쓰고, 저장한 지역 입구로 이동
    private void LoadSavedGame()
    {
        if (!TryGetReferences(out PlayerStats stats)) return;

        if (RegionManager.Instance != null && RegionManager.Instance.IsLoading)
        {
            Debug.LogWarning("지역을 불러오는 중이라 세이브를 불러오지 않습니다. 잠시 후 다시 L 키를 눌러 주세요.");
            return;
        }

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
        villageLiberated = data.villageLiberated;

        // 저장한 지역 입구로 이동 (좌표가 아니라 입구 기준 — 무작위 지역에도 안전)
        if (RegionManager.Instance != null && !string.IsNullOrEmpty(data.regionSceneName))
        {
            string entry = string.IsNullOrEmpty(data.entryId)
                ? RegionManager.Instance.StartEntryId
                : data.entryId;
            RegionManager.Instance.TravelTo(data.regionSceneName, entry);
        }

        NotificationUI.Show(Loc.Get("notice.load_done"), NotificationUI.NoticeType.Success);
    }

    // RegionManager의 지금 지역·입구를 세이브에 넣고, 없으면 시작 지역으로 채움
    private static void FillLocationFromRegionManager(CharacterSaveData data)
    {
        if (RegionManager.Instance != null)
        {
            string region = RegionManager.Instance.CurrentRegionScene;
            string entry = RegionManager.Instance.CurrentEntryId;
            data.regionSceneName = string.IsNullOrEmpty(region)
                ? RegionManager.Instance.StartRegionScene
                : region;
            data.entryId = string.IsNullOrEmpty(entry)
                ? RegionManager.Instance.StartEntryId
                : entry;
        }
        else
        {
            data.FillLocationDefaults("Region_01", "start");
        }
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
