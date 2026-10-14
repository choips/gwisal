using System;
using System.IO;                 // File, Path 사용
using UnityEngine;

// 세이브 파일 저장/불러오기 담당 (싱글톤 + 씬이 바뀌어도 유지)
// 저장 위치: Application.persistentDataPath 폴더
//   Account.json      — 계정 공용 (모든 캐릭터가 함께 쓰는 것)
//   Character_0.json  — 캐릭터별 (0번 칸 캐릭터의 레벨·가방·장비 등)
//   SaveData.json     — 옛 세이브 (버전 1·2). 캐릭터 파일이 없을 때만 읽어서 버전 3으로 변환 (파일은 지우지 않음)
public class SaveManager : MonoBehaviour
{
    private const string AccountFileName = "Account.json";
    private const string CharacterFilePrefix = "Character_";
    private const string LegacySaveFileName = "SaveData.json";

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
    // 0번 칸에 캐릭터 파일이 없고 옛 세이브(SaveData.json)가 있으면 그것을 버전 3으로 변환해서 돌려줌
    public CharacterSaveData LoadCharacter(int slot)
    {
        string path = GetCharacterPath(slot);
        if (File.Exists(path))
        {
            CharacterSaveData data = ReadJson<CharacterSaveData>(path);
            if (data != null && data.version > CharacterSaveData.CurrentVersion)
            {
                Debug.LogError($"세이브 파일이 더 새로운 형식(버전 {data.version})이라 불러오지 않습니다. 게임을 최신 버전으로 실행해 주세요.");
                return null;
            }
            return data;
        }

        if (slot == 0 && File.Exists(LegacySavePath))
        {
            SaveData legacy = ReadJson<SaveData>(LegacySavePath);
            return legacy != null ? legacy.ToCharacterSaveData() : null;
        }

        return null;
    }

    // 계정 공용 세이브 불러오기 (없거나 읽기에 실패하면 null)
    public AccountSaveData LoadAccount()
    {
        return File.Exists(AccountPath) ? ReadJson<AccountSaveData>(AccountPath) : null;
    }

    // 불러올 세이브가 있는지 (캐릭터 파일 또는 0번 칸이면 옛 세이브)
    public bool HasSaveFile(int slot = 0)
    {
        return File.Exists(GetCharacterPath(slot)) || (slot == 0 && File.Exists(LegacySavePath));
    }

    // 데이터를 JSON 파일로 저장 (성공 시 true)
    private static bool WriteJson(string path, object data)
    {
        try
        {
            // 두 번째 인자 true: 사람이 읽기 쉽게 줄바꿈/들여쓰기 적용
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"게임 저장 실패 ({path}): {e.Message}");
            return false;
        }
    }

    // JSON 파일을 읽어 T로 반환 (읽기에 실패하면 null)
    private static T ReadJson<T>(string path) where T : class
    {
        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"게임 불러오기 실패 ({path}): {e.Message}");
            return null;
        }
    }
}
