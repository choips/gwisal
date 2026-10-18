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
        { "notice.save_restored",     "세이브 파일이 손상되어 이전 저장으로 복구했습니다" },
        { "notice.mana_not_enough",   "기력이 부족합니다!" },
        { "notice.mana_full",         "기력이 이미 가득 차 있습니다." },
        { "notice.level_up",          "레벨 업! (레벨 {0})" },
        { "notice.bag_full",          "가방이 가득 찼습니다! ({0}칸)" },
        { "notice.bag_full_unequip",  "가방이 가득 차서 장비를 벗을 수 없습니다." },
        { "notice.no_mana_potion",    "기력 물약이 없습니다." },
        { "notice.potion_used",       "{0} 사용! 기력 +{1}" },
        { "notice.dead_cannot_use",   "사망한 상태에서는 아이템을 쓸 수 없습니다." },
        { "notice.area_threats",      "이 지역에 정예 무리 {0}개와 이름 있는 정예 {1}마리가 도사리고 있습니다" },
        { "notice.area_cleared",      "이 지역의 모든 요괴를 처치했습니다!" },
        { "notice.boss_cleared",      "보스를 처치했습니다!" },
        { "boss.phase2",              "2페이즈 (광폭화)" },
        { "boss.swallow",             "창귀 삼킴! ({0}/{1})" },

        // 화면 위 정보 (HUD)
        { "hud.level",                "Lv. {0}" },

        // 아이템 종류·능력치·등급 (ItemData)
        { "item.type.weapon",         "무기" },
        { "item.type.armor",          "방어구" },
        { "item.type.mana_potion",    "물약" },
        { "item.stat.weapon",         "공격력" },
        { "item.stat.armor",          "최대 체력" },
        { "item.stat.mana_potion",    "기력 회복" },
        { "item.stat_line",           "{0} +{1}" },
        { "item.rarity.normal",       "노멀" },
        { "item.rarity.magic",        "매직" },
        { "item.rarity.rare",         "레어" },
        { "item.rarity.unique",       "유니크" },
        { "item.letter.weapon",       "검" },
        { "item.letter.armor",        "갑" },
        { "item.letter.mana_potion",  "약" },
        { "item.display_name",        "[{0}] {1}" },

        // 인벤토리 (InventoryManager)
        { "inventory.slot_equipped",  "{0}: {1} ({2})" },
        { "inventory.slot_empty",     "{0}: 없음" },
        { "inventory.bag_count",      "가방 {0} / {1}칸 (클릭: 장착/사용)" },
        { "inventory.bag_empty",      "가방 0 / {0}칸 (비어 있음)" },

        // 아이템 툴팁 (ItemTooltip)
        { "tooltip.hint_use",         "클릭 또는 1 키로 사용" },
        { "tooltip.hint_equip",       "클릭하여 장착" },
        { "tooltip.hint_unequip",     "클릭하여 해제" },
        { "tooltip.potion_full",      "기력이 가득 차 있어 마실 필요가 없습니다" },
        { "tooltip.potion_gain",      "▲ 기력 +{0}" },
        { "tooltip.potion_change",    "기력 {0} → {1}" },
        { "tooltip.equipped",         "장착 중:" },
        { "tooltip.nothing_equipped", "장착 중인 {0} 없음" },
        { "tooltip.if_unequipped",    "해제하면" },
        { "tooltip.no_change",        "변화 없음" },
        { "tooltip.stat_up",          "▲ {0} +{1}" },
        { "tooltip.stat_down",        "▼ {0} {1}" },
        { "tooltip.stat_change",      "{0} {1} → {2}" },

        // 씬에 직접 적힌 글자 (메뉴 [귀살 → 씬 글자를 표에 연결]로 연결)
        { "hud.potion_slot",                   "기력 물약" },
        { "hud.skill_slot.amulet_projectile",  "부적" },
        { "hud.skill_slot.amulet_explosion",   "폭발" },
        { "key.right_click",                   "우클릭" },
        { "death.title",                       "YOU DIED" },
        { "death.revive",                      "부활하기" },

        // 귀신 이름 (EnemyData의 ID 기준: enemy.ID)
        { "enemy.mangja",             "망자" },
        { "enemy.changgwi",           "창귀" },
        { "enemy.dokkaebibul",        "도깨비불" },
        { "enemy.sangun",             "산군" },

        // 정예·하수인·이름 있는 정예 이름표 (MonsterFactory) — 📬 전달-26
        { "enemy.elite_name",         "정예 {0}" },
        { "enemy.minion_name",        "{0} 하수인" },
        { "enemy.named_elite_name",   "{0} {1} {2}" },
        { "enemy.named_elite_prefix.0", "피에 굶주린" },
        { "enemy.named_elite_prefix.1", "저주받은" },
        { "enemy.named_elite_prefix.2", "흉포한" },
        { "enemy.named_elite_prefix.3", "썩어 가는" },
        { "enemy.named_elite_prefix.4", "천년 묵은" },
        { "enemy.named_elite_prefix.5", "원한 서린" },
        { "enemy.named_elite_suffix.0", "왕" },
        { "enemy.named_elite_suffix.1", "우두머리" },
        { "enemy.named_elite_suffix.2", "대장" },

        // 기본 아이템 이름 (ItemBase의 ID 기준: item.name.ID)
        { "item.name.weapon_old_dagger",      "낡은 단검" },
        { "item.name.weapon_peachwood_sword", "복숭아나무 검" },
        { "item.name.weapon_taoist_staff",    "도사의 지팡이" },
        { "item.name.weapon_jujube_sword",    "대추나무 검" },
        { "item.name.armor_cotton_robe",      "무명 도포" },
        { "item.name.armor_leather_armor",    "가죽 갑옷" },
        { "item.name.armor_silk_robe",        "비단 도포" },
        { "item.name.armor_chainmail",        "사슬 갑옷" },
        { "item.name.potion_mana",            "기력 물약" },

        // 지역 이름 (RegionManager, 키 = region.name.씬이름) · 출구 이름표 (RegionExit)
        { "region.name.Region_01",            "백팔 계단" },
        { "region.name.Region_02",            "안개 산길" },
        { "region.exit_label",                "{0} 가는 길" },
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
