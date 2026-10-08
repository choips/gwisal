using System;
using System.IO;                 // File, Path 사용
using UnityEngine;

// 세이브 파일 저장/불러오기 담당 (싱글톤 + 씬이 바뀌어도 유지)
// 저장 위치: Application.persistentDataPath/SaveData.json
public class SaveManager : MonoBehaviour
{
    private const string SaveFileName = "SaveData.json";

    // 어디서든 SaveManager.Instance로 접근하기 위한 전역 참조
    public static SaveManager Instance { get; private set; }

    // 세이브 파일의 전체 경로
    public string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

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

    // 데이터를 JSON 파일로 저장 (성공 시 true)
    public bool SaveGame(SaveData data)
    {
        if (data == null) return false;

        try
        {
            // 두 번째 인자 true: 사람이 읽기 쉽게 줄바꿈/들여쓰기 적용
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"세이브 파일 위치: {SavePath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"게임 저장 실패: {e.Message}");
            return false;
        }
    }

    // JSON 파일을 읽어 SaveData로 반환 (파일이 없거나 읽기에 실패하면 null)
    public SaveData LoadGame()
    {
        if (!File.Exists(SavePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"게임 불러오기 실패: {e.Message}");
            return null;
        }
    }

    // 세이브 파일이 존재하는지 확인
    public bool HasSaveFile()
    {
        return File.Exists(SavePath);
    }
}
