using System;
using System.IO;                 // File, Path 사용
using UnityEngine;

// 세이브 파일 저장/불러오기 담당 (싱글톤 + 씬이 바뀌어도 유지)
// 저장 위치: Application.persistentDataPath 폴더
//   Account.json      — 계정 공용 (모든 캐릭터가 함께 쓰는 것)
//   Character_0.json  — 캐릭터별 (0번 칸 캐릭터의 레벨·가방·장비 등)
//   SaveData.json     — 옛 세이브 (버전 1·2). 캐릭터 파일이 없을 때만 읽어서 버전 3으로 변환 (파일은 지우지 않음)
// 안전한 저장: 같은 폴더의 "파일이름.tmp"에 끝까지 쓴 뒤 진짜 파일과 바꿔치기하고, 바로 전 파일은 "파일이름.bak"에 보관
// (저장하는 순간 게임이 꺼져도 진짜 파일은 이전 내용 그대로 남음)
public class SaveManager : MonoBehaviour
{
    private const string AccountFileName = "Account.json";
    private const string CharacterFilePrefix = "Character_";
    private const string LegacySaveFileName = "SaveData.json";
    private const string TempSuffix = ".tmp";   // 쓰는 중인 임시 파일
    private const string BackupSuffix = ".bak"; // 바로 전 저장 보관

    // 어디서든 SaveManager.Instance로 접근하기 위한 전역 참조
    public static SaveManager Instance { get; private set; }

    public string AccountPath => Path.Combine(Application.persistentDataPath, AccountFileName);
    public string LegacySavePath => Path.Combine(Application.persistentDataPath, LegacySaveFileName);

    // 캐릭터 칸 번호에 해당하는 세이브 파일 경로 (예: Character_0.json)
    public string GetCharacterPath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, $"{CharacterFilePrefix}{slot}.json");
    }

    private void Awake()
    {
        // 씬을 다시 불러오면 SaveManager가 또 생기므로, 이미 있다면 새로 생긴 쪽을 제거
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad는 Hierarchy 최상위 오브젝트에만 동작
        DontDestroyOnLoad(gameObject);
    }

    // 캐릭터를 저장하고, 계정 파일에 마지막으로 플레이한 칸 번호를 기록 (성공 시 true)
    public bool SaveCharacter(CharacterSaveData data, int slot)
    {
        if (data == null) return false;

        data.version = CharacterSaveData.CurrentVersion;
        if (!WriteJson(GetCharacterPath(slot), data)) return false;

        AccountSaveData account = LoadAccount() ?? new AccountSaveData();
        account.version = AccountSaveData.CurrentVersion;
        account.lastCharacterSlot = slot;
        WriteJson(AccountPath, account);

        GameLog.Save($"세이브 파일 위치: {GetCharacterPath(slot)}");
        return true;
    }

    // 캐릭터 세이브 불러오기 (없거나 읽기에 실패하면 null)
    // 파일이 깨져 있으면 .bak(바로 전 저장)으로 복구
    // 0번 칸에 캐릭터 파일이 없고 옛 세이브(SaveData.json)가 있으면 그것을 변환해서 돌려줌
    // 버전 3 이하는 시작 지역·시작 입구로 채움 (버전 4)
    public CharacterSaveData LoadCharacter(int slot)
    {
        string path = GetCharacterPath(slot);
        if (File.Exists(path) || File.Exists(path + BackupSuffix))
        {
            CharacterSaveData data = ReadWithBackup<CharacterSaveData>(path, d => d.version > 0);
            if (data != null && data.version > CharacterSaveData.CurrentVersion)
            {
                Debug.LogError($"세이브 파일이 더 새로운 형식(버전 {data.version})이라 불러오지 않습니다. 게임을 최신 버전으로 실행해 주세요.");
                return null;
            }
            if (data != null) ApplyLocationDefaults(data);
            return data;
        }

        if (slot == 0 && File.Exists(LegacySavePath))
        {
            SaveData legacy = ReadJson<SaveData>(LegacySavePath);
            if (legacy == null) return null;
            CharacterSaveData data = legacy.ToCharacterSaveData();
            ApplyLocationDefaults(data);
            return data;
        }

        return null;
    }

    // 지역·입구가 비어 있으면 RegionManager 시작 값(없으면 Region_01 / start)으로 채움
    private static void ApplyLocationDefaults(CharacterSaveData data)
    {
        string defaultRegion = "Region_01";
        string defaultEntry = "start";
        if (RegionManager.Instance != null)
        {
            defaultRegion = RegionManager.Instance.StartRegionScene;
            defaultEntry = RegionManager.Instance.StartEntryId;
        }
        data.FillLocationDefaults(defaultRegion, defaultEntry);
    }

    // 계정 공용 세이브 불러오기 (없거나 읽기에 실패하면 null, 깨져 있으면 .bak으로 복구)
    public AccountSaveData LoadAccount()
    {
        if (!File.Exists(AccountPath) && !File.Exists(AccountPath + BackupSuffix)) return null;
        return ReadWithBackup<AccountSaveData>(AccountPath, d => d.version > 0);
    }

    // 불러올 세이브가 있는지 (캐릭터 파일·그 백업 또는 0번 칸이면 옛 세이브)
    public bool HasSaveFile(int slot = 0)
    {
        string path = GetCharacterPath(slot);
        return File.Exists(path) || File.Exists(path + BackupSuffix) || (slot == 0 && File.Exists(LegacySavePath));
    }

    // 세이브 파일을 읽고, 읽기에 실패하거나 내용이 비어 있으면 .bak으로 다시 시도
    // isValid: 읽은 내용이 쓸 만한지 확인 (예: 버전 번호가 들어 있는지)
    // .bak으로 복구하면 진짜 파일도 .bak 내용으로 되돌리고 화면에 알림
    private T ReadWithBackup<T>(string path, Func<T, bool> isValid) where T : class
    {
        T data = File.Exists(path) ? ReadJson<T>(path) : null;
        if (data != null && isValid(data)) return data;

        string backupPath = path + BackupSuffix;
        T backup = File.Exists(backupPath) ? ReadJson<T>(backupPath) : null;
        if (backup == null || !isValid(backup))
        {
            Debug.LogError($"세이브 파일이 손상되었고 쓸 수 있는 백업(.bak)도 없어 불러오지 않습니다: {path} — 새로 시작할 수 있습니다.");
            return null;
        }

        Debug.LogWarning($"세이브 파일이 손상되어 바로 전 저장(.bak)으로 복구했습니다: {path}");
        try
        {
            File.Copy(backupPath, path, true);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"복구한 내용을 세이브 파일에 되돌려 쓰지 못했습니다 (다음 저장 때 다시 씀): {e.Message}");
        }
        NotificationUI.Show(Loc.Get("notice.save_restored"), NotificationUI.NoticeType.Warning);
        return backup;
    }

    // 데이터를 JSON 파일로 안전하게 저장 (성공 시 true, 실패해도 진짜 파일은 그대로)
    private static bool WriteJson(string path, object data)
    {
        string tempPath = path + TempSuffix;
        string backupPath = path + BackupSuffix;
        try
        {
            // 두 번째 인자 true: 사람이 읽기 쉽게 줄바꿈/들여쓰기 적용
            string json = JsonUtility.ToJson(data, true);

            // 1) 지난번 저장에서 남은 임시 파일을 지우고, 임시 파일에 끝까지 씀
            if (File.Exists(tempPath)) File.Delete(tempPath);
            File.WriteAllText(tempPath, json);

            // 2) 바꿔치기 (진짜 파일이 있으면 그 내용은 .bak으로)
            if (File.Exists(path))
            {
                ReplaceWithBackup(tempPath, path, backupPath);
            }
            else
            {
                File.Move(tempPath, path);
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"게임 저장 실패 ({path}): {e.Message}");
            return false;
        }
    }

    // 임시 파일로 진짜 파일을 바꾸고, 원래 진짜 파일은 백업 파일로 남김
    private static void ReplaceWithBackup(string tempPath, string path, string backupPath)
    {
        try
        {
            File.Replace(tempPath, path, backupPath);
        }
        catch (Exception e) when (File.Exists(tempPath) && File.Exists(path))
        {
            // File.Replace를 지원하지 않는 환경(일부 모바일 등)이면 직접 복사 → 바꿔치기
            GameLog.Save($"File.Replace 대신 직접 바꿔치기합니다 ({e.GetType().Name})");
            File.Copy(path, backupPath, true);
            File.Delete(path);
            File.Move(tempPath, path);
        }
    }

    // JSON 파일을 읽어 T로 반환 (읽기에 실패하면 null — 백업으로 다시 시도할 수 있으므로 경고만 남김)
    private static T ReadJson<T>(string path) where T : class
    {
        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"세이브 파일 읽기 실패 ({path}): {e.Message}");
            return null;
        }
    }
}
