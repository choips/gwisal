#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// 개발자 메뉴 (F1로 열고 닫기) — 에디터와 개발 빌드(Development Build)에만 포함되고, 정식 출시 빌드에서는 통째로 빠집니다.
// 씬에 붙이지 않아도 플레이를 시작하면 스스로 만들어집니다 (Hierarchy의 DontDestroyOnLoad → [DevMenu]).
// 화면 왼쪽 위 창에서 무적·기력 무한·쿨타임 없음을 켜고 끄고(켠 값은 DevCheats에 저장), 레벨을 바로 올리고,
// 적에게 준 피해(DPS 측정, DamageMeter)를 봅니다.
// Unity 기본 즉석 UI(IMGUI, OnGUI)로 그려서 Canvas·프리팹 설정이 필요 없습니다. 마우스로 조작합니다.
public class DevMenu : MonoBehaviour
{
    private const float ReferenceScreenHeight = 1080f; // 이 화면 높이에서 창이 아래 크기 그대로 보임 (화면이 크면 함께 커짐)
    private const float MeterRefreshInterval = 0.25f;  // DPS 글자를 새로 만드는 간격(초) (매 프레임 문자열을 만들지 않기 위함)
    private static readonly Rect WindowRect = new Rect(10f, 10f, 320f, 340f); // 기준 화면에서 창의 위치·크기

    private bool isOpen;     // 메뉴가 열려 있는지
    private float guiScale = 1f; // 화면 크기에 맞춘 창 확대 배율

    private PlayerStats playerStats;   // 레벨을 올릴 플레이어 (메뉴를 열 때 찾아 둠)
    private int shownLevel = -1;       // 창에 표시 중인 레벨 (바뀔 때만 글자를 새로 만듦)
    private string levelLabel = "레벨: -"; // 창에 표시할 레벨 글자

    private float nextMeterRefreshTime;          // 다음에 DPS 글자를 새로 만들 시각
    private string meterDamageLabel = "누적 피해: 0"; // 누적 피해·적중 횟수·최대 한 방
    private string meterDpsLabel = "DPS: 0";         // 초당 피해·측정 시간

    private int shownStatsVersion = -1;              // 표시 중인 새 능력치의 계산 번호 (바뀔 때만 글자를 새로 만듦)
    private string statsDefenseLabel = "체력 - / 기력 -"; // 새 능력치: 체력·기력
    private string statsAttackLabel = "무기 피해 -";      // 새 능력치: 무기 피해·치명타

    // 첫 씬이 열린 직후 메뉴 오브젝트를 한 번 만듦 (씬이 바뀌어도 유지)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnStart()
    {
        GameObject menuObject = new GameObject("[DevMenu]");
        DontDestroyOnLoad(menuObject);
        menuObject.AddComponent<DevMenu>();
    }

    private void Update()
    {
        if (GameInput.DevMenu.WasPressedThisFrame())
        {
            isOpen = !isOpen;

            // 매 프레임 찾지 않도록 메뉴를 열 때만 플레이어를 찾음
            if (isOpen && playerStats == null)
            {
                playerStats = FindFirstObjectByType<PlayerStats>();
            }
        }

        // 메뉴 창 영역을 알려 둠 → 메뉴를 클릭할 때 뒤쪽 바닥으로 이동하거나 스킬이 나가지 않음
        if (isOpen)
        {
            UpdateLevelLabel();
            UpdateMeterLabels();
            UpdateStatsLabels();

            guiScale = Mathf.Max(1f, Screen.height / ReferenceScreenHeight);
            DevCheats.MenuScreenRect = new Rect(WindowRect.x * guiScale, WindowRect.y * guiScale,
                WindowRect.width * guiScale, WindowRect.height * guiScale);
        }
        else
        {
            DevCheats.MenuScreenRect = Rect.zero;
        }
    }

    private void OnDisable()
    {
        DevCheats.MenuScreenRect = Rect.zero;
    }

    // 화면에 메뉴 창을 그림 (Unity가 화면을 그릴 때마다 호출)
    private void OnGUI()
    {
        if (!isOpen) return;

        GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));

        GUILayout.BeginArea(WindowRect, "개발자 메뉴 (F1)", GUI.skin.window);
        DevCheats.GodMode = GUILayout.Toggle(DevCheats.GodMode, " 무적 (피해를 받지 않음)");
        DevCheats.InfiniteMana = GUILayout.Toggle(DevCheats.InfiniteMana, " 기력 무한");
        DevCheats.NoCooldown = GUILayout.Toggle(DevCheats.NoCooldown, " 쿨타임 없음");

        GUILayout.Space(8f);
        GUILayout.Label(levelLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("레벨 +1")) LevelUp(1);
        if (GUILayout.Button("레벨 +5")) LevelUp(5);
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label(meterDamageLabel);
        GUILayout.Label(meterDpsLabel);
        if (GUILayout.Button("DPS 측정 초기화"))
        {
            DamageMeter.Reset();
            nextMeterRefreshTime = 0f; // 바로 0으로 보이게 함
        }

        GUILayout.Space(8f);
        GUILayout.Label("새 능력치 계산 (비교용)");
        GUILayout.Label(statsDefenseLabel);
        GUILayout.Label(statsAttackLabel);

        GUILayout.EndArea();
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
