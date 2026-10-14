using System.Diagnostics;
using UnityEngine;

// 게임 기록(Console 로그) 모음 — Debug.Log 대신 이것을 씁니다.
// - 에디터와 개발 빌드(Development Build)에서만 동작하고, 정식 출시 빌드에서는 호출 자체가 사라집니다
//   ([Conditional] 덕분에 메시지 문자열도 만들지 않음 → 귀신 떼 전투에서도 성능 부담 없음).
// - 종류별로 켜고 끕니다. 기본값은 전투만 꺼짐 (F1 개발자 메뉴의 "전투 로그 표시"로 켬).
// - Console 검색창에 "[전투]"처럼 머리말을 넣으면 그 종류만 볼 수 있습니다.
// - 경고·오류는 이것을 쓰지 않고 그대로 Debug.LogWarning·Debug.LogError를 씁니다 (출시 빌드에서도 남아야 함).
public static class GameLog
{
    public enum Category
    {
        Combat, // 전투: 공격·피격·남은 체력·사망·경험치 (자주 찍힘)
        Item,   // 아이템: 드랍·획득·장착
        Region, // 지역: 도착·귀신 배치
        Save,   // 저장: 세이브 위치·변환
    }

    // 종류별 켜짐 여부 (Category 순서와 같음)
    private static readonly bool[] DefaultEnabled = { false, true, true, true };
    private static readonly bool[] enabled = (bool[])DefaultEnabled.Clone();
    private static readonly string[] Prefixes = { "[전투] ", "[아이템] ", "[지역] ", "[저장] " };

    // 전투 로그 켜기/끄기 (F1 개발자 메뉴에서 사용)
    public static bool CombatEnabled
    {
        get => enabled[(int)Category.Combat];
        set => enabled[(int)Category.Combat] = value;
    }

    public static bool IsEnabled(Category category) => enabled[(int)category];

    public static void SetEnabled(Category category, bool isOn) => enabled[(int)category] = isOn;

    // 이 프로젝트는 Play를 누를 때 스크립트를 다시 불러오지 않으므로(Enter Play Mode Options), static 값을 직접 기본값으로 되돌림
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        DefaultEnabled.CopyTo(enabled, 0);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Combat(string message, Object context = null) => Write(Category.Combat, message, context);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Item(string message, Object context = null) => Write(Category.Item, message, context);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Region(string message, Object context = null) => Write(Category.Region, message, context);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Save(string message, Object context = null) => Write(Category.Save, message, context);

    private static void Write(Category category, string message, Object context)
    {
        if (!enabled[(int)category]) return;

        UnityEngine.Debug.Log(Prefixes[(int)category] + message, context);
    }
}
