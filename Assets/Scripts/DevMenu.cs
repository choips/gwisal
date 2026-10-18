#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 개발자 메뉴 (F1로 열고 닫기) — 에디터와 개발 빌드(Development Build)에만 포함되고, 정식 출시 빌드에서는 통째로 빠집니다.
// 씬에 붙이지 않아도 플레이를 시작하면 스스로 만들어집니다 (Hierarchy의 DontDestroyOnLoad → [DevMenu]).
// 화면 왼쪽 위 창에서 무적·기력 무한·쿨타임 없음·전투 로그(GameLog)를 켜고 끄고(켠 값은 DevCheats·GameLog에 저장), 레벨을 바로 올리고,
// 아이템을 등급·아이템 레벨을 정해 만들고, 귀신을 종류·레벨·등급으로 소환하고, 지역을 바로 옮기고,
// 적 체력·피해·드랍 배율을 조절하고, 등급별 아이템을 떨어뜨려 색을 보고,
// 적에게 준 피해(DPS 측정, DamageMeter)와 능력치를 보고, 테스트용 능력치 수정치를 켜고 끕니다.
// Unity 기본 즉석 UI(IMGUI, OnGUI)로 그려서 Canvas·프리팹 설정이 필요 없습니다. 마우스로 조작합니다.
// 내용이 화면보다 길면 창 높이를 화면에 맞추고 안쪽을 스크롤합니다.
// 열린 창 목록(UIWindowStack)에 등록되어 ESC로도 닫힙니다.
public class DevMenu : MonoBehaviour, IUIWindow
{
    private const float ReferenceScreenHeight = 1080f; // 이 화면 높이에서 창이 아래 크기 그대로 보임 (화면이 크면 함께 커짐)
    private const float MeterRefreshInterval = 0.25f;  // DPS 글자를 새로 만드는 간격(초) (매 프레임 문자열을 만들지 않기 위함)
    private const int WindowId = 9001;                 // IMGUI 창 구분 번호 (다른 창과 겹치지 않게 아무 숫자)
    private const float WindowWidth = 420f;            // 기준 화면에서 창 너비 (스크롤바 자리 포함)
    private const float ScreenEdgeMargin = 20f;        // 화면 가장자리와 창 사이 여백 (기준 좌표)
    private const float MinWindowHeight = 240f;        // 창이 너무 작아지지 않게
    private const float TitleBarDragHeight = 20f;      // 제목 줄을 드래그해서 창을 옮기는 높이

    private bool isOpen;     // 메뉴가 열려 있는지
    private float guiScale = 1f; // 화면 크기에 맞춘 창 확대 배율
    private Rect windowRect = new Rect(10f, 10f, WindowWidth, MinWindowHeight); // 기준 화면에서 창의 위치·크기
    private Vector2 scrollPosition; // 창 안쪽 스크롤 위치

    // OnGUI에서 매번 새로 만들지 않도록 미리 만들어 두는 것들 (Awake에서 만듦)
    private GUI.WindowFunction drawWindowFunction;     // 창 안쪽을 그리는 함수
    private GUILayoutOption[] toggleLayoutOptions;     // 테스트용 수정치 스위치 너비 (두 개가 한 줄에 들어가게)

    private PlayerStats playerStats;   // 레벨을 올릴 플레이어 (메뉴를 열 때 찾아 둠)
    private int shownLevel = -1;       // 창에 표시 중인 레벨 (바뀔 때만 글자를 새로 만듦)
    private string levelLabel = "레벨: -"; // 창에 표시할 레벨 글자

    private float nextMeterRefreshTime;          // 다음에 DPS 글자를 새로 만들 시각
    private string meterDamageLabel = "누적 피해: 0"; // 누적 피해·적중 횟수·최대 한 방
    private string meterDpsLabel = "DPS: 0";         // 초당 피해·측정 시간

    private int shownStatsVersion = -1;              // 표시 중인 새 능력치의 계산 번호 (바뀔 때만 글자를 새로 만듦)
    private string statsDefenseLabel = "체력 - / 기력 -"; // 새 능력치: 체력·기력
    private string statsAttackLabel = "무기 피해 -";      // 새 능력치: 무기 피해·치명타
    private string statsArmorLabel = "방어력 -";          // 새 능력치: 방어력·피해 감소
    private string statsUtilityLabel = "쿨타임 -";        // 새 능력치: 쿨타임 감소·기력 소모 감소·공격 속도·이동 속도

    // 테스트용 능력치 수정치 하나 (켜면 플레이어에게 붙고 끄면 떨어짐, 이 객체 자체가 수정치의 출처)
    private sealed class TestBonus
    {
        public readonly string Label;  // 창에 보일 이름
        public readonly StatType Stat; // 바꿀 능력치
        public readonly float Value;   // 더할 값 (% 능력치는 0.4 = 40%)
        public bool IsOn;              // 켜져 있는지

        public TestBonus(string label, StatType stat, float value)
        {
            Label = label;
            Stat = stat;
            Value = value;
        }
    }

    // 상한까지 올려 보는 값들 (쿨타임 감소·기력 소모 감소 40%, 공격 속도 +100%, 이동 속도 +50%가 상한)
    private readonly TestBonus[] testBonuses =
    {
        new TestBonus(" 방어력 +100", StatType.Armor, 100f),
        new TestBonus(" 피해 감소 +20%", StatType.DamageReduction, 0.2f),
        new TestBonus(" 쿨타임 감소 +40%", StatType.CooldownReduction, 0.4f),
        new TestBonus(" 기력 소모 감소 +40%", StatType.ManaCostReduction, 0.4f),
        new TestBonus(" 공격 속도 +100%", StatType.AttackSpeed, 1f),
        new TestBonus(" 이동 속도 +50%", StatType.MoveSpeed, 0.5f),
    };

    // —— 아이템 만들기 (S1-13 1단계) ——
    private static readonly string[] RarityLabels = { "노멀", "매직", "레어", "유니크" };
    private ItemRarity spawnRarity = ItemRarity.Magic; // 만들 때 쓸 등급
    private int spawnItemLevel = 1;                     // 만들 때 쓸 아이템 레벨
    private int spawnItemIndex;                         // ItemDatabase 목록에서의 번호
    private string spawnItemLabel = "종류: -";          // 창에 보일 종류 이름
    private LootDropper cachedDropper;                  // 아이템 프리팹·드랍을 빌려 쓸 귀신의 LootDropper

    // —— 귀신 소환 (S1-13 2단계) ——
    private static readonly string[] RankLabels = { "일반", "정예", "이름있는정예", "하수인" };
    private const float SpawnDistance = 3f;             // 일반 귀신: 플레이어 앞쪽으로 소환할 거리(m)
    private const float SleepingSpawnMargin = 3f;       // 호출형(창귀): 인식 거리 + 이만큼 밖에 소환 (다가가서 테스트)
    private const float CallPackSpread = 2f;            // 호출형 여러 마리 소환 시 서로 흩어지는 반경(m)
    private MonsterRank spawnRank = MonsterRank.Normal; // 소환할 등급
    private int spawnEnemyLevel = 1;                    // 소환할 몬스터 레벨
    private int spawnEnemyIndex;                        // EnemyDatabase 목록에서의 번호
    private string spawnEnemyLabel = "귀신: -";         // 창에 보일 귀신 이름
    private readonly List<Enemy> devSpawnedEnemies = new List<Enemy>(); // 개발자 메뉴로 소환한 귀신 (지역 이동 때 정리)
    private bool wasRegionBusy;                         // 직전 프레임에 지역을 불러오는 중이었는지

    // —— 지역 이동 (S1-13 3단계) ——
    private static readonly string[] TravelRegionScenes = { "Region_01", "Region_02" };
    private static readonly string[] TravelRegionLabels = { "백팔 계단", "안개 산길" };
    private const string TravelEntryId = "start"; // 개발자 이동은 항상 그 지역의 start 입구로
    private string currentRegionLabel = "지역: -";

    // —— 배율 슬라이더 (S1-13 4단계) ——
    private const float MultMin = 0.25f;
    private const float MultMax = 5f;

    // 첫 씬이 열린 직후 메뉴 오브젝트를 한 번 만듦 (씬이 바뀌어도 유지)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnStart()
    {
        GameObject menuObject = new GameObject("[DevMenu]");
        DontDestroyOnLoad(menuObject);
        menuObject.AddComponent<DevMenu>();
    }

    private void Awake()
    {
        drawWindowFunction = DrawWindow;
        toggleLayoutOptions = new[] { GUILayout.Width(WindowWidth * 0.5f - 20f) };
    }

    private void Update()
    {
        if (GameInput.DevMenu.WasPressedThisFrame())
        {
            SetOpen(!isOpen);
        }

        // 지역이 바뀌기 시작하면 개발자 소환 귀신을 창고로 돌려보냄 (풀 적이라 씬과 함께 안 사라짐)
        bool busy = RegionManager.IsBusy;
        if (busy && !wasRegionBusy) ClearDevSpawnedEnemies();
        wasRegionBusy = busy;

        // 메뉴 창 영역을 알려 둠 → 메뉴를 클릭할 때 뒤쪽 바닥으로 이동하거나 스킬이 나가지 않음
        if (isOpen)
        {
            UpdateLevelLabel();
            UpdateMeterLabels();
            UpdateStatsLabels();
            UpdateCurrentRegionLabel();

            guiScale = Mathf.Max(1f, Screen.height / ReferenceScreenHeight);
            DevCheats.MenuScreenRect = new Rect(windowRect.x * guiScale, windowRect.y * guiScale,
                windowRect.width * guiScale, windowRect.height * guiScale);
        }
        else
        {
            DevCheats.MenuScreenRect = Rect.zero;
        }
    }

    private void OnDisable()
    {
        DevCheats.MenuScreenRect = Rect.zero;
        UIWindowStack.WindowClosed(this);
    }

    // ESC로 닫을 때 (UIWindowStack이 호출)
    public void Close()
    {
        SetOpen(false);
    }

    // 메뉴 열기/닫기 + 열린 창 목록에 넣고 빼기
    private void SetOpen(bool open)
    {
        isOpen = open;

        if (isOpen)
        {
            UIWindowStack.WindowOpened(this);

            // 매 프레임 찾지 않도록 메뉴를 열 때만 플레이어를 찾음
            if (playerStats == null)
            {
                playerStats = FindFirstObjectByType<PlayerStats>();
            }
        }
        else
        {
            UIWindowStack.WindowClosed(this);
        }
    }

    // 화면에 메뉴 창을 그림 (Unity가 화면을 그릴 때마다 호출)
    private void OnGUI()
    {
        if (!isOpen) return;

        guiScale = Mathf.Max(1f, Screen.height / ReferenceScreenHeight);
        GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));

        // 기준 좌표에서 화면 아래 여백을 남긴 최대 높이 (내용이 길어도 창이 화면 밖으로 안 나감)
        float maxHeight = (Screen.height / guiScale) - windowRect.y - ScreenEdgeMargin;
        maxHeight = Mathf.Max(MinWindowHeight, maxHeight);
        windowRect.width = WindowWidth;
        windowRect.height = maxHeight;

        windowRect = GUILayout.Window(WindowId, windowRect, drawWindowFunction, "개발자 메뉴 (F1) — 스크롤",
            GUILayout.Width(WindowWidth), GUILayout.Height(maxHeight));
    }

    // 창 안쪽 내용 (스크롤 영역 + 제목 줄 드래그)
    private void DrawWindow(int windowId)
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("마우스 휠로 아래 항목을 볼 수 있습니다.");

        DevCheats.GodMode = GUILayout.Toggle(DevCheats.GodMode, " 무적 (피해를 받지 않음)");
        DevCheats.InfiniteMana = GUILayout.Toggle(DevCheats.InfiniteMana, " 기력 무한");
        DevCheats.NoCooldown = GUILayout.Toggle(DevCheats.NoCooldown, " 쿨타임 없음");
        GameLog.CombatEnabled = GUILayout.Toggle(GameLog.CombatEnabled, " 전투 로그 표시 (Console)");

        GUILayout.Space(8f);
        GUILayout.Label(levelLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("레벨 +1")) LevelUp(1);
        if (GUILayout.Button("레벨 +5")) LevelUp(5);
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        DrawItemSpawnSection();

        GUILayout.Space(8f);
        DrawEnemySpawnSection();

        GUILayout.Space(8f);
        DrawRegionTravelSection();

        GUILayout.Space(8f);
        DrawMultiplierSection();

        GUILayout.Space(8f);
        GUILayout.Label(meterDamageLabel);
        GUILayout.Label(meterDpsLabel);
        if (GUILayout.Button("DPS 측정 초기화"))
        {
            DamageMeter.Reset();
            nextMeterRefreshTime = 0f; // 바로 0으로 보이게 함
        }

        GUILayout.Space(8f);
        GUILayout.Label("능력치 (계산값)");
        GUILayout.Label(statsDefenseLabel);
        GUILayout.Label(statsAttackLabel);
        GUILayout.Label(statsArmorLabel);
        GUILayout.Label(statsUtilityLabel);

        GUILayout.Space(8f);
        GUILayout.Label("테스트용 수정치");
        for (int i = 0; i < testBonuses.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            DrawTestBonusToggle(testBonuses[i]);
            if (i + 1 < testBonuses.Length) DrawTestBonusToggle(testBonuses[i + 1]);
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        // 제목 줄을 드래그하면 창 위치를 옮김
        GUI.DragWindow(new Rect(0f, 0f, 10000f, TitleBarDragHeight));
    }

    // 테스트용 수정치 스위치 하나 (바뀌었을 때만 플레이어에게 붙이거나 뗌)
    private void DrawTestBonusToggle(TestBonus bonus)
    {
        bool isOn = GUILayout.Toggle(bonus.IsOn, bonus.Label, toggleLayoutOptions);
        if (isOn == bonus.IsOn) return;

        bonus.IsOn = isOn;
        SetTestBonus(bonus);
    }

    // 테스트용 수정치를 플레이어에게 붙이거나 뗌
    private void SetTestBonus(TestBonus bonus)
    {
        if (playerStats == null)
        {
            playerStats = FindFirstObjectByType<PlayerStats>();
            if (playerStats == null) return;
        }

        if (bonus.IsOn)
        {
            playerStats.AddStatBonus(bonus, bonus.Stat, StatModifierType.Flat, bonus.Value);
        }
        else
        {
            playerStats.RemoveStatBonus(bonus);
        }
    }

    // 플레이어 레벨을 count만큼 바로 올림 (플레이어가 없으면 다시 찾아봄)
    private void LevelUp(int count)
    {
        if (playerStats == null)
        {
            playerStats = FindFirstObjectByType<PlayerStats>();
            if (playerStats == null) return;
        }

        playerStats.DevLevelUp(count);
    }

    // 아이템 만들기 구역: 종류·등급·아이템 레벨을 고른 뒤 발밑에 한 개 떨어뜨림
    private void DrawItemSpawnSection()
    {
        GUILayout.Label("아이템 만들기");

        // 종류: ◀ / 이름 / ▶
        EnsureSpawnItemLabel();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(28f))) CycleSpawnItem(-1);
        GUILayout.Label(spawnItemLabel, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("▶", GUILayout.Width(28f))) CycleSpawnItem(1);
        GUILayout.EndHorizontal();

        // 등급 네 칸
        GUILayout.BeginHorizontal();
        for (int i = 0; i < RarityLabels.Length; i++)
        {
            ItemRarity rarity = (ItemRarity)i;
            bool selected = spawnRarity == rarity;
            if (GUILayout.Toggle(selected, RarityLabels[i], GUI.skin.button) && !selected)
            {
                spawnRarity = rarity;
            }
        }
        GUILayout.EndHorizontal();

        // 아이템 레벨
        GUILayout.BeginHorizontal();
        GUILayout.Label("아이템 레벨", GUILayout.Width(80f));
        if (GUILayout.Button("-", GUILayout.Width(28f))) spawnItemLevel = Mathf.Max(1, spawnItemLevel - 1);
        GUILayout.Label(spawnItemLevel.ToString(), GUILayout.Width(36f));
        if (GUILayout.Button("+", GUILayout.Width(28f))) spawnItemLevel = Mathf.Min(99, spawnItemLevel + 1);
        if (GUILayout.Button("+5", GUILayout.Width(36f))) spawnItemLevel = Mathf.Min(99, spawnItemLevel + 5);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("아이템 만들기 (발밑에)")) SpawnDevItem();
        if (GUILayout.Button("등급 색 보기 (등급별 아이템 떨어뜨리기)")) DropRarityColorPreview();
    }

    // ItemDatabase 목록이 바뀌었을 수 있으니, 창을 그릴 때 표시 글자를 맞춤
    private void EnsureSpawnItemLabel()
    {
        ItemBase item = GetSpawnItemBase();
        if (item == null)
        {
            spawnItemLabel = "종류: (목록 없음)";
            return;
        }

        string name = string.IsNullOrEmpty(item.displayName) ? item.itemId : item.displayName;
        spawnItemLabel = $"종류: {name}";
    }

    // ItemDatabase에서 지금 고른 기본 아이템 (없으면 null)
    private ItemBase GetSpawnItemBase()
    {
        ItemDatabase db = ItemDatabase.Instance;
        if (db == null || db.Items == null || db.Items.Count == 0) return null;

        if (spawnItemIndex < 0) spawnItemIndex = 0;
        if (spawnItemIndex >= db.Items.Count) spawnItemIndex = db.Items.Count - 1;
        return db.Items[spawnItemIndex];
    }

    // 기본 아이템 목록을 앞뒤로 한 칸 이동
    private void CycleSpawnItem(int direction)
    {
        ItemDatabase db = ItemDatabase.Instance;
        if (db == null || db.Items == null || db.Items.Count == 0) return;

        int count = db.Items.Count;
        spawnItemIndex = (spawnItemIndex + direction + count) % count;
        EnsureSpawnItemLabel();
    }

    // 고른 종류·등급·아이템 레벨로 플레이어 발밑에 아이템 하나 떨어뜨림
    private void SpawnDevItem()
    {
        if (!TryGetPlayerAndDropper(out _, out LootDropper dropper)) return;

        ItemBase baseItem = GetSpawnItemBase();
        if (baseItem == null)
        {
            Debug.LogWarning("ItemDatabase에 기본 아이템이 없어 만들 수 없습니다.");
            return;
        }

        dropper.DropDevItem(playerStats.transform.position, baseItem, spawnRarity, spawnItemLevel);
    }

    // 플레이어 발밑에 노멀·매직·레어·유니크 장비와 물약을 하나씩 떨어뜨림 (등급 색 표 ItemRarityColors를 고를 때)
    private void DropRarityColorPreview()
    {
        if (!TryGetPlayerAndDropper(out _, out LootDropper dropper)) return;
        dropper.DropColorPreview(playerStats.transform.position);
    }

    // 플레이어와 LootDropper를 찾아 둠 (드랍은 귀신 프리팹의 아이템 모양·글꼴을 빌려 씀)
    private bool TryGetPlayerAndDropper(out PlayerStats stats, out LootDropper dropper)
    {
        stats = playerStats;
        if (stats == null)
        {
            stats = FindFirstObjectByType<PlayerStats>();
            playerStats = stats;
        }

        if (stats == null)
        {
            Debug.LogWarning("플레이어가 없어 아이템을 떨어뜨리지 못했습니다.");
            dropper = null;
            return false;
        }

        // 지역을 옮기면 이전 지역 귀신이 사라지므로, 없거나 파괴된 참조면 다시 찾음
        if (cachedDropper == null)
        {
            cachedDropper = FindFirstObjectByType<LootDropper>(FindObjectsInactive.Include);
        }

        dropper = cachedDropper;
        if (dropper == null)
        {
            Debug.LogWarning("씬에 LootDropper가 붙은 귀신이 없어 아이템을 떨어뜨리지 못했습니다.");
            return false;
        }

        return true;
    }

    // 귀신 소환 구역: 종류·등급·레벨을 고른 뒤 플레이어 앞에 한 마리 세움 (바로 깨어남)
    private void DrawEnemySpawnSection()
    {
        GUILayout.Label("귀신 소환");

        EnsureSpawnEnemyLabel();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(28f))) CycleSpawnEnemy(-1);
        GUILayout.Label(spawnEnemyLabel, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("▶", GUILayout.Width(28f))) CycleSpawnEnemy(1);
        GUILayout.EndHorizontal();

        EnemyData selectedForRank = GetSpawnEnemyData();
        bool selectedIsBoss = selectedForRank != null && selectedForRank.isBoss;
        if (selectedIsBoss)
        {
            // 보스는 등급(정예 등) 없이 보스 전용으로만 소환
            spawnRank = MonsterRank.Normal;
            GUILayout.Label("등급: 보스 (고정)");
        }
        else
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < RankLabels.Length; i++)
            {
                MonsterRank rank = (MonsterRank)i;
                bool selected = spawnRank == rank;
                if (GUILayout.Toggle(selected, RankLabels[i], GUI.skin.button) && !selected)
                {
                    spawnRank = rank;
                }
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("몬스터 레벨", GUILayout.Width(80f));
        if (GUILayout.Button("-", GUILayout.Width(28f))) spawnEnemyLevel = Mathf.Max(1, spawnEnemyLevel - 1);
        GUILayout.Label(spawnEnemyLevel.ToString(), GUILayout.Width(36f));
        if (GUILayout.Button("+", GUILayout.Width(28f))) spawnEnemyLevel = Mathf.Min(99, spawnEnemyLevel + 1);
        if (GUILayout.Button("+5", GUILayout.Width(36f))) spawnEnemyLevel = Mathf.Min(99, spawnEnemyLevel + 5);
        GUILayout.EndHorizontal();

        string spawnButton = selectedIsBoss ? "보스 소환 (앞에, 바로 싸움)" : "귀신 소환 (앞에, 바로 싸움)";
        if (GUILayout.Button(spawnButton)) SpawnDevEnemy();
    }

    private void EnsureSpawnEnemyLabel()
    {
        EnemyData data = GetSpawnEnemyData();
        if (data == null)
        {
            spawnEnemyLabel = "귀신: (목록 없음)";
            return;
        }

        string name = string.IsNullOrEmpty(data.displayName) ? data.enemyId : data.displayName;
        spawnEnemyLabel = data.isBoss ? $"보스: {name}" : $"귀신: {name}";
    }

    private EnemyData GetSpawnEnemyData()
    {
        EnemyDatabase db = EnemyDatabase.Instance;
        if (db == null || db.Enemies == null || db.Enemies.Count == 0) return null;

        if (spawnEnemyIndex < 0) spawnEnemyIndex = 0;
        if (spawnEnemyIndex >= db.Enemies.Count) spawnEnemyIndex = db.Enemies.Count - 1;
        return db.Enemies[spawnEnemyIndex];
    }

    private void CycleSpawnEnemy(int direction)
    {
        EnemyDatabase db = EnemyDatabase.Instance;
        if (db == null || db.Enemies == null || db.Enemies.Count == 0) return;

        int count = db.Enemies.Count;
        spawnEnemyIndex = (spawnEnemyIndex + direction + count) % count;
        EnsureSpawnEnemyLabel();
    }

    // 플레이어 앞 NavMesh 위에 고른 귀신을 한 마리 세움
    // 망자 등: 바로 앞(3m)에 세우고 바로 싸움 / 창귀(호출형): 인식 거리 밖에 잠든 채로 세움
    private void SpawnDevEnemy()
    {
        if (RegionManager.IsBusy)
        {
            Debug.LogWarning("지역을 불러오는 중이라 귀신을 소환하지 않습니다.");
            return;
        }

        if (playerStats == null)
        {
            playerStats = FindFirstObjectByType<PlayerStats>();
            if (playerStats == null)
            {
                Debug.LogWarning("플레이어가 없어 귀신을 소환하지 못했습니다.");
                return;
            }
        }

        RegionSettings region = FindFirstObjectByType<RegionSettings>();
        if (region == null)
        {
            Debug.LogWarning("RegionSettings가 없어 귀신을 소환하지 못했습니다. 지역 씬이 열려 있는지 확인해 주세요.");
            return;
        }

        EnemyData data = GetSpawnEnemyData();
        if (data == null)
        {
            Debug.LogWarning("EnemyDatabase에 귀신 종류가 없어 소환할 수 없습니다.");
            return;
        }

        // 보스는 정예/이름있는정예 등급을 쓰지 않음
        MonsterRank rank = data.isBoss ? MonsterRank.Normal : spawnRank;

        // 호출형은 인식 거리 밖(+여유)에 두고, 여러 마리일 때 서로 가깝게 흩어짐 (부르는 소리 테스트용)
        float distance = data.CanCall ? data.aggroRange + SleepingSpawnMargin : SpawnDistance;
        float spread = data.CanCall ? CallPackSpread : 0f;
        Vector3 spawnPos = FindSpawnPositionInFront(playerStats.transform, distance, spread);
        string label = data.isBoss ? null : MakeSpawnLabel(data, rank);
        Enemy enemy = region.SpawnMonster(data, spawnEnemyLevel, rank, label, spawnPos);
        if (enemy == null)
        {
            Debug.LogWarning("귀신 소환에 실패했습니다 (프리팹·풀 확인).");
            return;
        }

        // 망자 등은 바로 싸움. 창귀(호출형)는 잠든 채로 두어 다가가면 부르는 소리를 볼 수 있게 함
        if (data.CanCall)
            GameLog.Region($"개발자 소환(잠듦, {distance:0.#}m 앞): {data.displayName} Lv.{spawnEnemyLevel} ({rank}) — 다가가면 부르는 소리");
        else
        {
            enemy.WakeUp();
            GameLog.Region(data.isBoss
                ? $"개발자 보스 소환: {data.displayName} Lv.{spawnEnemyLevel}"
                : $"개발자 소환: {data.displayName} Lv.{spawnEnemyLevel} ({rank})");
        }

        // 보스는 체력바·사망 풀힐·클리어 알림을 위해 BossEncounter에 연결
        if (data.isBoss)
        {
            BossEncounter.BeginFor(enemy);
        }

        devSpawnedEnemies.Add(enemy);
    }

    // 플레이어가 바라보는 쪽 distance m 앞의 NavMesh 위 (spread > 0이면 그 주변을 살짝 흩어 둠)
    private static Vector3 FindSpawnPositionInFront(Transform player, float distance, float spread = 0f)
    {
        Vector3 candidate = player.position + player.forward * distance;
        if (spread > 0f)
        {
            Vector2 offset = Random.insideUnitCircle * spread;
            candidate += new Vector3(offset.x, 0f, offset.y);
        }

        // 멀리 소환할수록 NavMesh를 넓게 찾아 봄
        float sampleRadius = Mathf.Max(5f, distance * 0.5f);
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        if (NavMesh.SamplePosition(player.position, out hit, 3f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return player.position;
    }

    private static string MakeSpawnLabel(EnemyData data, MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Elite: return MonsterFactory.MakeEliteName(data);
            case MonsterRank.NamedElite: return MonsterFactory.MakeNamedEliteName(data);
            case MonsterRank.Minion: return MonsterFactory.MakeMinionName(data);
            default: return null; // 일반은 이름표 없음 (기존 고정 배치와 같음)
        }
    }

    // 지역이 바뀔 때: 개발자 메뉴로 소환한 귀신을 모두 창고로 (보상 없이)
    private void ClearDevSpawnedEnemies()
    {
        for (int i = devSpawnedEnemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = devSpawnedEnemies[i];
            if (enemy != null) enemy.Despawn();
        }
        devSpawnedEnemies.Clear();
        cachedDropper = null; // 이전 지역 귀신 참조를 버림
    }

    // 지역 이동 구역: 지금 지역을 보여주고, 다른 지역 버튼을 누르면 start 입구로 이동
    private void DrawRegionTravelSection()
    {
        GUILayout.Label("지역 이동");
        GUILayout.Label(currentRegionLabel);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < TravelRegionScenes.Length; i++)
        {
            string sceneName = TravelRegionScenes[i];
            bool isCurrent = RegionManager.Instance != null
                             && RegionManager.Instance.CurrentRegionScene == sceneName;

            // 지금 있는 지역·불러오는 중이면 버튼 비활성
            GUI.enabled = !isCurrent && !RegionManager.IsBusy;
            if (GUILayout.Button(TravelRegionLabels[i]))
            {
                TravelToRegion(sceneName);
            }
            GUI.enabled = true;
        }
        GUILayout.EndHorizontal();
    }

    private void UpdateCurrentRegionLabel()
    {
        if (RegionManager.Instance == null)
        {
            currentRegionLabel = "지역: (관리자 없음)";
            return;
        }

        if (RegionManager.IsBusy)
        {
            currentRegionLabel = "지역: 불러오는 중…";
            return;
        }

        string scene = RegionManager.Instance.CurrentRegionScene;
        string display = scene;
        for (int i = 0; i < TravelRegionScenes.Length; i++)
        {
            if (TravelRegionScenes[i] == scene)
            {
                display = TravelRegionLabels[i];
                break;
            }
        }

        currentRegionLabel = string.IsNullOrEmpty(scene) ? "지역: -" : $"지역: {display} ({scene})";
    }

    private void TravelToRegion(string sceneName)
    {
        if (RegionManager.Instance == null)
        {
            Debug.LogWarning("RegionManager가 없어 지역을 옮기지 못했습니다.");
            return;
        }

        if (RegionManager.IsBusy) return;

        // 같은 지역이면 무시 (버튼이 비활성이어도 안전장치)
        if (RegionManager.Instance.CurrentRegionScene == sceneName) return;

        GameLog.Region($"개발자 지역 이동 → {sceneName} (입구: {TravelEntryId})");
        RegionManager.Instance.TravelTo(sceneName, TravelEntryId);
    }

    // 적 체력·피해·드랍 배율 (새로 나오는 귀신부터 — 소환 또는 지역 재입장)
    private void DrawMultiplierSection()
    {
        GUILayout.Label("배율 (새 귀신부터 적용)");
        GUILayout.Label("이미 나온 무리는 지역을 다시 들어가면 반영됩니다.");

        DrawMultSlider("적 체력", ref DevCheats.EnemyHealthMult);
        DrawMultSlider("적 피해", ref DevCheats.EnemyDamageMult);
        DrawMultSlider("드랍", ref DevCheats.EnemyLootMult);

        if (GUILayout.Button("배율 모두 1배로"))
        {
            DevCheats.EnemyHealthMult = 1f;
            DevCheats.EnemyDamageMult = 1f;
            DevCheats.EnemyLootMult = 1f;
        }

        // 스킬 포인트는 S5(스킬 창)에서 생김 — 자리만 안내
        GUILayout.Label("스킬 포인트 설정: S5에서 추가 예정");
    }

    private static void DrawMultSlider(string label, ref float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(56f));
        value = GUILayout.HorizontalSlider(value, MultMin, MultMax, GUILayout.ExpandWidth(true));
        // 0.05 단위로 맞춰 읽기 쉽게
        value = Mathf.Round(value * 20f) / 20f;
        GUILayout.Label($"{value:0.00}×", GUILayout.Width(48f));
        GUILayout.EndHorizontal();
    }

    // 레벨이 바뀌었을 때만 표시 글자를 새로 만듦 (매 프레임 문자열을 만들지 않기 위함)
    private void UpdateLevelLabel()
    {
        int level = playerStats != null ? playerStats.level : -1;
        if (level == shownLevel) return;

        shownLevel = level;
        levelLabel = level >= 0 ? $"레벨: {level}" : "레벨: - (플레이어 없음)";
    }

    // 새 능력치 계산기의 최종값이 바뀌었을 때만 글자를 새로 만듦
    private void UpdateStatsLabels()
    {
        CharacterStats stats = playerStats != null ? playerStats.Stats : null;
        if (stats == null) return;

        stats.Refresh();
        if (stats.Version == shownStatsVersion) return;
        shownStatsVersion = stats.Version;

        statsDefenseLabel = $"체력 {stats.Get(StatType.MaxHealth):0} / 기력 {stats.Get(StatType.MaxMana):0} (초당 회복 {stats.Get(StatType.ManaRegen):0.#})";
        statsAttackLabel = $"무기 피해 {stats.Get(StatType.WeaponDamageMin):0}~{stats.Get(StatType.WeaponDamageMax):0} / 치명타 {stats.Get(StatType.CritChance) * 100f:0}% · +{stats.Get(StatType.CritDamage) * 100f:0}%";
        statsArmorLabel = $"방어력 {stats.Get(StatType.Armor):0} (물리 -{DamageCalculator.GetArmorReduction(stats, 1) * 100f:0.#}%, 적 레벨 1) / 피해 감소 {stats.Get(StatType.DamageReduction) * 100f:0}%";
        statsUtilityLabel = $"쿨타임 -{stats.Get(StatType.CooldownReduction) * 100f:0}% / 기력 소모 -{stats.Get(StatType.ManaCostReduction) * 100f:0}% / 공속 +{stats.Get(StatType.AttackSpeed) * 100f:0}% / 이속 +{stats.Get(StatType.MoveSpeed) * 100f:0}%";
    }

    // DPS 측정 글자를 일정 간격으로만 새로 만듦
    private void UpdateMeterLabels()
    {
        if (Time.unscaledTime < nextMeterRefreshTime) return;
        nextMeterRefreshTime = Time.unscaledTime + MeterRefreshInterval;

        meterDamageLabel = $"누적 피해: {DamageMeter.TotalDamage:0} ({DamageMeter.HitCount}회, 최대 {DamageMeter.MaxHit:0})";
        meterDpsLabel = DamageMeter.IsIdle
            ? $"DPS: {DamageMeter.Dps:0.0} (측정 {DamageMeter.ElapsedSeconds:0.0}초, 멈춤)"
            : $"DPS: {DamageMeter.Dps:0.0} (측정 {DamageMeter.ElapsedSeconds:0.0}초)";
    }
}
#endif
