using UnityEngine;
using UnityEngine.InputSystem;   // 새 Input System (InputAction, InputSystem.actions) 사용

// 게임에서 쓰는 입력 행동(Input Action)을 한곳에서 찾아 두고 꺼내 쓰게 하는 정적 클래스
// 행동과 키 연결은 Assets/InputSystem_Actions (프로젝트 전체 Input Actions)의 "Player" 묶음에 정의되어 있습니다.
// 키를 직접 읽지 않고 "행동"으로 읽기 때문에, 나중에 키 변경·게임패드·터치를 같은 코드로 처리할 수 있습니다.
// 씬에 붙이지 않아도 되며, 어디서든 GameInput.Inventory.WasPressedThisFrame()처럼 사용합니다.
public static class GameInput
{
    private const string PlayerMapName = "Player"; // Input Actions 파일 안의 행동 묶음(Action Map) 이름

    public const int SkillSlotCount = 6; // 스킬 칸 수 (Skill1 ~ Skill6)

    private static bool isInitialized;

    private static InputAction point;
    private static InputAction select;
    private static InputAction moveDirection;
    private static InputAction cancel;
    private static InputAction usePotion;
    private static InputAction inventory;
    private static InputAction quickSave;
    private static InputAction quickLoad;
    private static InputAction zoom;
    private static InputAction devMenu;
    private static readonly InputAction[] skills = new InputAction[SkillSlotCount];
    private static InputAction emptyAction; // 잘못된 스킬 칸 번호를 요청했을 때 돌려줄, 키 연결이 없는 빈 행동

    // 커서 위치 (화면 좌표, 마우스)
    public static InputAction Point { get { EnsureInitialized(); return point; } }

    // 이동·대상 선택 (마우스 좌클릭: 바닥 이동 / 적 공격 / 아이템 줍기, 게임패드 A)
    public static InputAction Select { get { EnsureInitialized(); return select; } }

    // 이동 방향 (게임패드 왼쪽 스틱, -1 ~ 1)
    public static InputAction MoveDirection { get { EnsureInitialized(); return moveDirection; } }

    // 취소 (ESC: 부적 폭발 조준 취소)
    public static InputAction Cancel { get { EnsureInitialized(); return cancel; } }

    // 기력 물약 마시기 (1 키)
    public static InputAction UsePotion { get { EnsureInitialized(); return usePotion; } }

    // 인벤토리 열기/닫기 (I 키)
    public static InputAction Inventory { get { EnsureInitialized(); return inventory; } }

    // 빠른 저장 / 불러오기 (S / L 키)
    public static InputAction QuickSave { get { EnsureInitialized(); return quickSave; } }
    public static InputAction QuickLoad { get { EnsureInitialized(); return quickLoad; } }

    // 카메라 확대/축소 (마우스 휠, 위로 굴리면 양수)
    public static InputAction Zoom { get { EnsureInitialized(); return zoom; } }

    // 개발자 메뉴 (F1)
    public static InputAction DevMenu { get { EnsureInitialized(); return devMenu; } }

    // 지금 커서가 가리키는 화면 좌표
    public static Vector2 PointerPosition => Point.ReadValue<Vector2>();

    // 스킬 칸 행동 (slotNumber: 1 ~ 6)
    // 지금 연결: 2번 = 마우스 우클릭(부적 투사체), 3번 = Q(부적 폭발), 4~6번 = 2·3·4 키 (아직 쓰는 스킬 없음)
    public static InputAction GetSkill(int slotNumber)
    {
        EnsureInitialized();

        if (slotNumber < 1 || slotNumber > SkillSlotCount)
        {
            Debug.LogError($"[GameInput] 스킬 칸 번호는 1 ~ {SkillSlotCount} 사이여야 합니다. (요청한 번호: {slotNumber})");
            return emptyAction;
        }

        return skills[slotNumber - 1];
    }

    // 플레이를 시작할 때마다 저장해 둔 행동을 비움
    // (에디터 설정에 따라 정적 변수가 이전 플레이의 값을 기억할 수 있기 때문)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        isInitialized = false;
        point = select = moveDirection = cancel = usePotion = null;
        inventory = quickSave = quickLoad = zoom = devMenu = null;
        emptyAction = null;
        for (int i = 0; i < SkillSlotCount; i++)
        {
            skills[i] = null;
        }
    }

    // 첫 씬이 열린 직후 한 번 미리 찾아 둠 (빠진 행동이 있으면 이때 Console에 경고가 뜸)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeOnStart()
    {
        EnsureInitialized();
    }

    // 행동을 처음 사용할 때 한 번만 Input Actions에서 찾아 저장 (매 프레임 찾지 않음)
    private static void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;

        InputActionMap map = InputSystem.actions != null ? InputSystem.actions.FindActionMap(PlayerMapName) : null;
        if (map == null)
        {
            Debug.LogWarning($"[GameInput] 프로젝트 전체 Input Actions에서 \"{PlayerMapName}\" 묶음을 찾지 못했습니다. " +
                             "Edit > Project Settings > Input System Package의 Project-wide Actions에 InputSystem_Actions가 지정되어 있는지 확인하세요.");
        }
        else
        {
            map.Enable();
        }

        emptyAction = new InputAction("Empty");

        point = FindAction(map, "Point");
        select = FindAction(map, "Select");
        moveDirection = FindAction(map, "MoveDirection");
        cancel = FindAction(map, "Cancel");
        usePotion = FindAction(map, "UsePotion");
        inventory = FindAction(map, "Inventory");
        quickSave = FindAction(map, "QuickSave");
        quickLoad = FindAction(map, "QuickLoad");
        zoom = FindAction(map, "Zoom");
        devMenu = FindAction(map, "DevMenu");

        for (int i = 0; i < SkillSlotCount; i++)
        {
            skills[i] = FindAction(map, "Skill" + (i + 1));
        }
    }

    // 묶음에서 이름으로 행동을 찾음
    // 찾지 못하면 경고를 띄우고 키 연결이 없는 빈 행동을 돌려줌 → 그 입력만 동작하지 않을 뿐 게임이 멈추지는 않음
    private static InputAction FindAction(InputActionMap map, string actionName)
    {
        InputAction action = map?.FindAction(actionName);
        if (action != null) return action;

        Debug.LogWarning($"[GameInput] 입력 행동 \"{actionName}\"을(를) 찾지 못했습니다. " +
                         "Assets/InputSystem_Actions를 더블클릭해 Player 묶음에 이 이름의 행동이 있는지 확인하세요.");
        return new InputAction(actionName);
    }
}
