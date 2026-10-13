using UnityEngine.Localization.Settings;

// 화면 글자를 Localization 표(String Table "UI")에서 꺼내는 도우미
// 사용 예: NotificationUI.Show(Loc.Get("notice.save_done"));
//          NotificationUI.Show(Loc.Format("notice.level_up", level)); — 표의 문구 안 {0}, {1} 자리에 값이 들어감
// 표에 없는 키는 "No translation found for ..." 문구가 그대로 화면에 보이므로 빠진 키를 바로 알 수 있습니다.
// 새 문구를 추가할 때는 Editor/LocalizationTableFiller.cs 목록에 키와 한국어를 적고 메뉴 [귀살 → 화면 글자 표 채우기]를 누릅니다.
// 표를 찾는 비용이 있으므로 매 프레임(Update)이 아니라 알림·툴팁처럼 필요할 때만 부릅니다.
public static class Loc
{
    public const string UiTable = "UI"; // 화면 글자 표 이름

    // 키에 해당하는 현재 언어의 문구
    public static string Get(string key)
    {
        return LocalizationSettings.StringDatabase.GetLocalizedString(UiTable, key);
    }

    // 키에 해당하는 문구의 {0}, {1} … 자리에 값을 넣은 문구
    public static string Format(string key, params object[] args)
    {
        return string.Format(Get(key), args);
    }
}
