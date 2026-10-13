using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

// 에디터 전용 도구 (게임 빌드에는 포함되지 않음)
// 메뉴 [귀살 → 씬 글자를 표에 연결]을 누르면 지금 열린 씬에서 아래 목록의 글자가 적힌 TextMeshPro를 찾아
// Localize String Event 부품을 붙이고 화면 글자 표 "UI"의 키에 연결합니다.
// (TextMeshPro 부품 제목을 우클릭 → Localize 한 뒤 키를 고르는 것과 같은 결과)
// 이미 Localize String Event가 붙은 글자는 건너뜁니다. 실행한 뒤에는 Ctrl+S로 씬을 저장해야 합니다.
public static class SceneTextLocalizer
{
    // 씬에 적힌 글자, 연결할 키
    private static readonly string[,] SceneTextKeys =
    {
        { "기력 물약", "hud.potion_slot" },
        { "부적",     "hud.skill_slot.amulet_projectile" },
        { "폭발",     "hud.skill_slot.amulet_explosion" },
        { "우클릭",   "key.right_click" },
        { "YOU DIED", "death.title" },
        { "부활하기", "death.revive" },
    };

    [MenuItem("귀살/씬 글자를 표에 연결")]
    private static void LinkSceneTexts()
    {
        // 꺼져 있는 오브젝트(사망 화면 등)도 포함해서 찾음
        TextMeshProUGUI[] texts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int linked = 0;
        int skipped = 0;
        foreach (TextMeshProUGUI text in texts)
        {
            string key = FindKey(text.text);
            if (key == null)
            {
                continue;
            }

            if (text.GetComponent<LocalizeStringEvent>() != null)
            {
                skipped++;
                continue;
            }

            Link(text, key);
            Debug.Log($"씬 글자 연결: {text.gameObject.name} \"{text.text}\" → {key}", text);
            linked++;
        }

        Debug.Log($"씬 글자 연결 끝: 새로 연결 {linked}개, 이미 연결되어 있어서 건너뜀 {skipped}개. Ctrl+S로 씬을 저장해 주세요.");
    }

    private static string FindKey(string sceneText)
    {
        if (string.IsNullOrEmpty(sceneText))
        {
            return null;
        }

        string trimmed = sceneText.Trim();
        for (int i = 0; i < SceneTextKeys.GetLength(0); i++)
        {
            if (SceneTextKeys[i, 0] == trimmed)
            {
                return SceneTextKeys[i, 1];
            }
        }
        return null;
    }

    private static void Link(TextMeshProUGUI text, string key)
    {
        // Ctrl+Z로 되돌릴 수 있게 Undo를 거쳐 부품 추가
        LocalizeStringEvent localizer = Undo.AddComponent<LocalizeStringEvent>(text.gameObject);
        localizer.StringReference = new LocalizedString(Loc.UiTable, key);

        // 표에서 글자를 받으면 TextMeshPro의 text에 넣도록 연결 (에디터에서도 미리보기로 바뀜)
        UnityAction<string> setText = (UnityAction<string>)System.Delegate.CreateDelegate(
            typeof(UnityAction<string>), text, typeof(TextMeshProUGUI).GetProperty("text").GetSetMethod());
        UnityEventTools.AddPersistentListener(localizer.OnUpdateString, setText);
        localizer.OnUpdateString.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);

        EditorUtility.SetDirty(localizer);
        EditorSceneManager.MarkSceneDirty(text.gameObject.scene);
    }
}
