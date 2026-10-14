#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// 개발자 메뉴 (F1로 열고 닫기) — 에디터와 개발 빌드(Development Build)에만 포함되고, 정식 출시 빌드에서는 통째로 빠집니다.
// 씬에 붙이지 않아도 플레이를 시작하면 스스로 만들어집니다 (Hierarchy의 DontDestroyOnLoad → [DevMenu]).
// 화면 왼쪽 위 창에서 무적·기력 무한·쿨타임 없음·전투 로그(GameLog)를 켜고 끄고(켠 값은 DevCheats·GameLog에 저장), 레벨을 바로 올리고, 등급별 아이템을 떨어뜨려 색을 보고,
// 적에게 준 피해(DPS 측정, DamageMeter)와 능력치를 보고, 테스트용 능력치 수정치를 켜고 끕니다.
// Unity 기본 즉석 UI(IMGUI, OnGUI)로 그려서 Canvas·프리팹 설정이 필요 없습니다. 마우스로 조작합니다.
// 열린 창 목록(UIWindowStack)에 등록되어 ESC로도 닫힙니다.
public class DevMenu : MonoBehaviour, IUIWindow
{
    private const float ReferenceScreenHeight = 1080f; // 이 화면 높이에서 창이 아래 크기 그대로 보임 (화면이 크면 함께 커짐)
    private const float MeterRefreshInterval = 0.25f;  // DPS 글자를 새로 만드는 간격(초) (매 프레임 문자열을 만들지 않기 위함)
    private const int WindowId = 9001;                 // IMGUI 창 구분 번호 (다른 창과 겹치지 않게 아무 숫자)
    private const float WindowWidth = 400f;            // 기준 화면에서 창 너비 (높이는 내용에 맞춰 자동)

    private bool isOpen;     // 메뉴가 열려 있는지
    private float guiScale = 1f; // 화면 크기에 맞춘 창 확대 배율
    private Rect windowRect = new Rect(10f, 10f, WindowWidth, 0f); // 기준 화면에서 창의 위치·크기 (높이는 그릴 때 자동으로 정해짐)

    // OnGUI에서 매번 새로 만들지 않도록 미리 만들어 두는 것들 (Awake에서 만듦)
    private GUI.WindowFunction drawWindowFunction;     // 창 안쪽을 그리는 함수
    private GUILayoutOption[] windowLayoutOptions;     // 창 너비 고정
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
        windowLayoutOptions = new[] { GUILayout.Width(WindowWidth) };
        toggleLayoutOptions = new[] { GUILayout.Width(WindowWidth * 0.5f - 12f) };
    }

    private void Update()
    {
        if (GameInput.DevMenu.WasPressedThisFrame())
        {
            SetOpen(!isOpen);
        }

        // 메뉴 창 영역을 알려 둠 → 메뉴를 클릭할 때 뒤쪽 바닥으로 이동하거나 스킬이 나가지 않음
        if (isOpen)
        {
            UpdateLevelLabel();
            UpdateMeterLabels();
            UpdateStatsLabels();

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

        GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));

        // GUILayout.Window는 내용 전체가 들어가도록 창 높이를 스스로 늘려 줌 (돌려받은 크기를 저장해 클릭 막기에도 씀)
        windowRect = GUILayout.Window(WindowId, windowRect, drawWindowFunction, "개발자 메뉴 (F1)", windowLayoutOptions);
    }

    // 창 안쪽 내용
    private void DrawWindow(int windowId)
    {
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
        if (GUILayout.Button("등급 색 보기 (등급별 아이템 떨어뜨리기)")) DropRarityColorPreview();

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

    // 플레이어 발밑에 노멀·매직·레어·유니크 장비와 물약을 하나씩 떨어뜨림 (등급 색 표 ItemRarityColors를 고를 때)
    private void DropRarityColorPreview()
    {
        if (playerStats == null)
        {
            playerStats = FindFirstObjectByType<PlayerStats>();
            if (playerStats == null) return;
        }

        // 드랍 표·이름표 글꼴은 귀신의 LootDropper에 들어 있으므로 빌려 씀 (풀에 들어가 꺼진 귀신의 것도 됨)
        LootDropper dropper = FindFirstObjectByType<LootDropper>(FindObjectsInactive.Include);
        if (dropper == null)
        {
            Debug.LogWarning("씬에 LootDropper가 붙은 귀신이 없어 아이템을 떨어뜨리지 못했습니다.");
            return;
        }

        dropper.DropColorPreview(playerStats.transform.position);
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
