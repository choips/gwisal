using System.Collections.Generic;
using UnityEngine;

// 게임 전체 저장·불러오기 관리자 (씬에 하나 배치)
// 빠른 저장(QuickSave, 기본 S 키): 현재 플레이어 상태·인벤토리·마지막 지역/입구를 저장
// 빠른 불러오기(QuickLoad, 기본 L 키): 저장된 데이터를 불러와 덮어쓰고, 저장한 지역 입구로 이동
// 자동 저장 (알림 없음): 새 지역에 도착한 직후 + 게임을 끌 때 + 일정 시간마다
// 이어하기: 게임을 시작할 때 세이브가 있으면 그 지역 입구에서 그 상태로 시작 ("새로 시작"을 체크하면 처음부터)
// 키 연결은 Assets/InputSystem_Actions의 Player 묶음에서 바꿉니다.
// RegionManager.Start보다 먼저 Start가 돌아야 이어하기 지역을 알려 줄 수 있으므로 실행 순서를 앞당김
[DefaultExecutionOrder(-10)]
public class GameController : MonoBehaviour
{
    // 지금 플레이 중인 캐릭터 칸 번호 (캐릭터 선택 화면은 S7, 그 전까지는 항상 0번)
    private const int CharacterSlot = 0;

    [Tooltip("저장/불러오기할 플레이어 (비워두면 씬에서 자동으로 찾음)")]
    [SerializeField] private PlayerStats playerStats;

    [Header("이어하기 / 자동 저장")]
    [Tooltip("체크하면 세이브가 있어도 이어하지 않고 처음부터 시작 (테스트용). 자동 저장이 돌면 기존 세이브는 새 내용으로 덮어씌워집니다.")]
    [SerializeField] private bool startNewGame = false;

    [Tooltip("자동 저장 간격 (초). 0이면 시간마다 자동 저장하지 않음 (지역 도착·게임 끌 때는 그대로 저장)")]
    [Min(0f)] [SerializeField] private float autoSaveInterval = 300f;

    // 산 아래 마을 탈환 여부 ([전달-19] — 아직 게임플레이 없음, 세이브만 주고받음)
    private bool villageLiberated;

    private CharacterSaveData pendingContinueData; // 이어하기: 첫 지역에 도착하면 적용할 세이브
    private bool autoSaveReady;                    // 첫 지역 도착(이어하기 적용) 전에는 자동 저장하지 않음 (빈 캐릭터로 덮어쓰지 않도록)
    private bool autoSaveBlocked;                  // 세이브가 있는데 불러오지 못했으면 이번 실행 동안 자동 저장 안 함 (그 파일을 덮어쓰지 않도록)
    private float autoSaveTimer;                   // 마지막 저장 뒤 지난 시간 (초)

    private void Start()
    {
        if (RegionManager.Instance != null)
        {
            RegionManager.Instance.RegionLoaded += OnRegionLoaded;
        }
        else
        {
            // 지역 관리자가 없는 씬(테스트 씬 등): 이어하기 없이 바로 자동 저장 시작
            autoSaveReady = true;
        }

        PrepareContinue();
    }

    private void OnDestroy()
    {
        if (RegionManager.Instance != null)
        {
            RegionManager.Instance.RegionLoaded -= OnRegionLoaded;
        }
    }

    private void Update()
    {
        if (GameInput.QuickSave.WasPressedThisFrame())
        {
            SaveCurrentGame(true);
        }
        else if (GameInput.QuickLoad.WasPressedThisFrame())
        {
            LoadSavedGame();
        }

        UpdateAutoSaveTimer();
    }

    // 게임을 끌 때 자동 저장 (에디터에서는 Play를 멈출 때)
    private void OnApplicationQuit()
    {
        AutoSave("게임 종료");
    }

    // 이어하기 준비: 세이브가 있으면 읽어 두고, RegionManager에게 그 지역·입구부터 불러오라고 알림
    // (능력치·가방은 첫 지역에 도착한 뒤 적용 — PlayerStats.Start가 체력을 가득 채우는 것보다 뒤여야 함)
    private void PrepareContinue()
    {
        if (SaveManager.Instance == null || RegionManager.Instance == null) return;

        if (startNewGame)
        {
            GameLog.Save("\"새로 시작\"이 체크되어 있어 세이브를 불러오지 않고 처음부터 시작합니다.");
            return;
        }

        if (!SaveManager.Instance.HasSaveFile(CharacterSlot)) return;

        CharacterSaveData data = SaveManager.Instance.LoadCharacter(CharacterSlot);
        if (data == null)
        {
            autoSaveBlocked = true;
            Debug.LogWarning("세이브 파일을 불러오지 못해 처음부터 시작합니다. 그 파일을 덮어쓰지 않도록 이번 실행에서는 자동 저장을 하지 않습니다 (S 키 저장은 됨).");
            return;
        }

        pendingContinueData = data;
        RegionManager.Instance.SetContinueLocation(data.regionSceneName, data.entryId);
        GameLog.Save($"이어하기: '{data.regionSceneName}' 입구 '{data.entryId}'에서 시작합니다.");
    }

    // 새 지역에 도착하면: 첫 도착이면 이어하기 적용, 그 뒤로는 자동 저장
    private void OnRegionLoaded(string sceneName)
    {
        if (!autoSaveReady)
        {
            autoSaveReady = true;
            if (pendingContinueData != null && TryGetReferences(out PlayerStats stats))
            {
                ApplySaveData(pendingContinueData, stats);
            }
            pendingContinueData = null;
            autoSaveTimer = 0f;
            return; // 막 불러온 내용이라 바로 다시 저장할 필요 없음
        }

        AutoSave("지역 도착");
    }

    // 일정 시간마다 자동 저장
    private void UpdateAutoSaveTimer()
    {
        if (autoSaveInterval <= 0f || !autoSaveReady) return;

        autoSaveTimer += Time.unscaledDeltaTime;
        if (autoSaveTimer >= autoSaveInterval)
        {
            AutoSave("시간마다");
        }
    }

    // 알림 없이 저장 (지역을 불러오는 중이면 지역·입구가 비어 있으므로 건너뜀)
    private void AutoSave(string reason)
    {
        autoSaveTimer = 0f;
        if (!autoSaveReady || autoSaveBlocked) return;
        if (RegionManager.Instance != null && RegionManager.Instance.IsLoading) return;

        if (SaveCurrentGame(false))
        {
            GameLog.Save($"자동 저장 ({reason})");
        }
    }

    // 현재 상태를 CharacterSaveData에 담아 저장 (showNotice: S 키 저장만 화면 알림)
    private bool SaveCurrentGame(bool showNotice)
    {
        if (!TryGetReferences(out PlayerStats stats)) return false;

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

        if (!SaveManager.Instance.SaveCharacter(data, CharacterSlot)) return false;

        autoSaveTimer = 0f;
        if (showNotice)
        {
            NotificationUI.Show(Loc.Get("notice.save_done"), NotificationUI.NoticeType.Success);
        }
        return true;
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

        ApplySaveData(data, stats);

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

    // 세이브 내용을 가방·장비·능력치에 덮어씀 (지역 이동은 하지 않음)
    private void ApplySaveData(CharacterSaveData data, PlayerStats stats)
    {
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
