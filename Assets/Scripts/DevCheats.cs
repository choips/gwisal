using UnityEngine;

// 개발자 메뉴(F1)에서 켜고 끄는 치트 상태 — 씬에 붙이지 않는 정적 클래스
// 게임 코드(PlayerStats·SkillBase·MonsterFactory 등)가 이 값을 읽어 동작을 바꿉니다.
// 정식 출시 빌드에는 개발자 메뉴(DevMenu)가 들어가지 않으므로 스위치는 false, 배율은 1로 남습니다.
public static class DevCheats
{
    public static bool GodMode;      // 무적 (플레이어가 피해를 받지 않음)
    public static bool InfiniteMana; // 기력 무한 (기력이 항상 가득, 스킬을 써도 줄지 않음)
    public static bool NoCooldown;   // 쿨타임 없음 (스킬을 바로 다시 쓸 수 있음)

    // 적 배율 (새로 생기는 귀신부터 적용 — 이미 나와 있는 무리는 지역을 다시 들어가야 반영)
    public static float EnemyHealthMult = 1f; // 적 체력 배율
    public static float EnemyDamageMult = 1f; // 적 피해 배율
    public static float EnemyLootMult = 1f;   // 드랍 개수·좋은 등급 확률 배율

    // 화면에 열려 있는 개발자 메뉴 창의 영역 (화면 왼쪽 위가 0인 좌표, 닫혀 있으면 크기 0)
    public static Rect MenuScreenRect;

    // 커서가 개발자 메뉴 창 위에 있는지 (메뉴를 클릭할 때 캐릭터가 이동·공격하지 않도록 확인)
    // pointerPosition: Input System의 커서 위치 (화면 왼쪽 아래가 0인 좌표)
    public static bool IsPointerOverMenu(Vector2 pointerPosition)
    {
        if (MenuScreenRect.width <= 0f) return false;

        // 메뉴 창은 위쪽이 0인 좌표를 쓰므로 세로 위치를 뒤집어서 비교
        Vector2 guiPosition = new Vector2(pointerPosition.x, Screen.height - pointerPosition.y);
        return MenuScreenRect.Contains(guiPosition);
    }

    // 플레이를 시작할 때마다 치트를 모두 끔 (에디터 설정에 따라 정적 변수가 이전 플레이의 값을 기억할 수 있음)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        GodMode = false;
        InfiniteMana = false;
        NoCooldown = false;
        EnemyHealthMult = 1f;
        EnemyDamageMult = 1f;
        EnemyLootMult = 1f;
        MenuScreenRect = Rect.zero;
    }
}
