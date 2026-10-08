using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;   // 새 Input System (Keyboard.current) 사용

// 게임 전체 단축키 관리자 (씬에 하나 배치)
// S 키: 현재 플레이어 상태와 인벤토리를 저장
// L 키: 저장된 데이터를 불러와 플레이어 상태와 인벤토리를 덮어씀
public class GameController : MonoBehaviour
{
    [Tooltip("저장/불러오기할 플레이어 (비워두면 씬에서 자동으로 찾음)")]
    [SerializeField] private PlayerStats playerStats;

    private void Update()
    {
        // 키보드가 연결되어 있지 않으면 아무것도 하지 않음
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.sKey.wasPressedThisFrame)
        {
            SaveCurrentGame();
        }
        else if (keyboard.lKey.wasPressedThisFrame)
        {
            LoadSavedGame();
        }
    }

    // 현재 상태를 SaveData에 담아 저장
    private void SaveCurrentGame()
    {
        if (!TryGetReferences(out PlayerStats stats)) return;

        SaveData data = new SaveData
        {
            version = SaveData.CurrentVersion,
            level = stats.level,
            currentXP = stats.currentXP,
            requiredXP = stats.requiredXP,
            maxHP = stats.maxHP,
            currentHP = stats.currentHP,
            attackPower = stats.attackPower,
            maxMP = stats.maxMP,
            currentMP = stats.currentMP,
        };

        if (InventoryManager.Instance != null)
        {
            data.inventoryItems = new List<ItemData>(InventoryManager.Instance.inventory);
            data.equippedWeapon = InventoryManager.Instance.EquippedWeapon;
            data.equippedArmor = InventoryManager.Instance.EquippedArmor;
        }

        if (SaveManager.Instance.SaveGame(data))
        {
            NotificationUI.Show("게임 저장 완료!", NotificationUI.NoticeType.Success);
        }
    }

    // 저장된 데이터를 불러와 현재 상태에 덮어씀
    private void LoadSavedGame()
    {
        if (!TryGetReferences(out PlayerStats stats)) return;

        SaveData data = SaveManager.Instance.LoadGame();
        if (data == null)
        {
            NotificationUI.Show("불러올 세이브 파일이 없습니다. 먼저 S 키로 저장해 주세요.", NotificationUI.NoticeType.Warning);
            return;
        }

        stats.ApplySaveData(data);

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.SetItems(data.inventoryItems);
            InventoryManager.Instance.SetEquipment(data.equippedWeapon, data.equippedArmor);
        }

        NotificationUI.Show("게임 불러오기 완료!", NotificationUI.NoticeType.Success);
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
