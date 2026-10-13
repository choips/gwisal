using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

// 에디터 전용 도구 (게임 빌드에는 포함되지 않음)
// 메뉴 [귀살 → 화면 글자 표 채우기]를 누르면 아래 목록의 키와 한국어 문구를 String Table "UI"의 Korean (ko) 표에 넣습니다.
// 이미 있는 키는 건드리지 않습니다 (Localization Tables 창에서 직접 고친 문구를 덮어쓰지 않기 위함).
// 새 문구가 필요하면 이 목록 맨 아래에 한 줄 추가하고 메뉴를 다시 누르면 빠진 키만 들어갑니다.
// {0}, {1}은 코드에서 Loc.Format으로 넣는 값의 자리입니다.
public static class LocalizationTableFiller
{
    private const string TableName = Loc.UiTable;
    private const string KoreanCode = "ko";

    // 키, 한국어 문구
    private static readonly string[,] KoreanEntries =
    {
        // 알림 (NotificationUI)
        { "notice.save_done",         "게임 저장 완료!" },
        { "notice.load_done",         "게임 불러오기 완료!" },
        { "notice.load_no_save",      "불러올 세이브 파일이 없습니다. 먼저 S 키로 저장해 주세요." },
        { "notice.mana_not_enough",   "기력이 부족합니다!" },
        { "notice.mana_full",         "기력이 이미 가득 차 있습니다." },
        { "notice.level_up",          "레벨 업! (레벨 {0})" },
        { "notice.bag_full",          "가방이 가득 찼습니다! ({0}칸)" },
        { "notice.bag_full_unequip",  "가방이 가득 차서 장비를 벗을 수 없습니다." },
        { "notice.no_mana_potion",    "기력 물약이 없습니다." },
        { "notice.potion_used",       "{0} 사용! 기력 +{1}" },
        { "notice.dead_cannot_use",   "사망한 상태에서는 아이템을 쓸 수 없습니다." },
        { "notice.area_threats",      "이 지역에 챔피언 무리 {0}개와 유니크 몬스터 {1}마리가 도사리고 있습니다" },
        { "notice.area_cleared",      "이 지역의 모든 요괴를 처치했습니다!" },
    };

    [MenuItem("귀살/화면 글자 표 채우기")]
    private static void FillTable()
    {
        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (collection == null)
        {
            Debug.LogError($"String Table Collection \"{TableName}\"을 찾지 못했습니다. Window → Asset Management → Localization Tables에서 \"{TableName}\" 표를 먼저 만들어 주세요.");
            return;
        }

        StringTable koreanTable = collection.GetTable(KoreanCode) as StringTable;
        if (koreanTable == null)
        {
            Debug.LogError($"\"{TableName}\" 표에 Korean ({KoreanCode}) 언어가 없습니다. Localization Tables 창에서 Korean을 추가해 주세요.");
            return;
        }

        int added = 0;
        int kept = 0;
        for (int i = 0; i < KoreanEntries.GetLength(0); i++)
        {
            string key = KoreanEntries[i, 0];
            string korean = KoreanEntries[i, 1];

            if (koreanTable.GetEntry(key) != null)
            {
                kept++;
                continue;
            }

            koreanTable.AddEntry(key, korean);
            added++;
        }

        // 키 목록은 공용 데이터(UI Shared Data)에, 문구는 언어별 표(UI_ko)에 저장되므로 둘 다 저장 표시
        EditorUtility.SetDirty(koreanTable);
        EditorUtility.SetDirty(collection.SharedData);
        AssetDatabase.SaveAssets();

        Debug.Log($"화면 글자 표 \"{TableName}\": 새로 넣은 문구 {added}개, 이미 있어서 그대로 둔 문구 {kept}개");
    }
}
