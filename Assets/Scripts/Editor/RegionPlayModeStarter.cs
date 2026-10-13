using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 에디터 전용 도구 (게임 빌드에는 포함되지 않음)
// 지역 씬(RegionEntry가 있는 씬)을 열어 둔 채 Play를 누르면, Core 씬으로 시작한 뒤 그 지역을 불러옵니다.
// → 지역을 꾸미다가 바로 Play 해도 플레이어·UI·관리자가 갖춰진 상태로 테스트할 수 있음
// 메뉴 [귀살 → 지역 씬에서 Play 하면 Core로 시작]으로 켜고 끌 수 있습니다 (기본: 켜짐).
// 지역 씬이 아닌 씬(Core 혼자, 옛 SampleScene 등)을 열고 Play 하면 평소대로 그 씬으로 시작합니다.
[InitializeOnLoad]
public static class RegionPlayModeStarter
{
    private const string CoreScenePath = "Assets/Scenes/Core.unity";
    private const string MenuPath = "귀살/지역 씬에서 Play 하면 Core로 시작";
    private const string EnabledPrefKey = "Gwisal.RegionPlayModeStarter.Enabled";

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(EnabledPrefKey, true);
        set => EditorPrefs.SetBool(EnabledPrefKey, value);
    }

    static RegionPlayModeStarter()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem(MenuPath)]
    private static void ToggleEnabled()
    {
        Enabled = !Enabled;
        Debug.Log(Enabled ? "지역 씬에서 Play 하면 Core 씬으로 시작합니다." : "지역 씬에서 Play 하면 그 씬만으로 시작합니다.");
    }

    // 메뉴 옆 체크 표시를 지금 상태에 맞춤
    [MenuItem(MenuPath, true)]
    private static bool ToggleEnabledValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;

        // 지난번 설정이 남아 있지 않도록 매번 초기화
        EditorSceneManager.playModeStartScene = null;
        SessionState.EraseString(RegionManager.EditorStartRegionKey);

        if (!Enabled) return;

        string regionScene = FindOpenRegionScene();
        if (regionScene == null) return;

        SceneAsset coreScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(CoreScenePath);
        if (coreScene == null)
        {
            Debug.LogWarning($"RegionPlayModeStarter: '{CoreScenePath}'를 찾지 못해 열린 씬 그대로 시작합니다.");
            return;
        }

        // Play는 디스크에 저장된 씬을 불러오므로, 저장 안 한 변경이 있으면 먼저 저장할지 물어봄
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorApplication.isPlaying = false; // "취소"를 누르면 Play도 취소
            return;
        }

        EditorSceneManager.playModeStartScene = coreScene;
        SessionState.SetString(RegionManager.EditorStartRegionKey, regionScene);
        Debug.Log($"'{regionScene}' 지역 씬이 열려 있어 Core 씬으로 시작한 뒤 이 지역을 불러옵니다. (끄기: 메뉴 {MenuPath})");
    }

    // 열려 있는 씬 중 RegionEntry가 있는 첫 지역 씬의 이름 (없으면 null)
    private static string FindOpenRegionScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || scene.path == CoreScenePath) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<RegionEntry>(true) != null) return scene.name;
            }
        }
        return null;
    }
}
