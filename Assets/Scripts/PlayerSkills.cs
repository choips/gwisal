using System;
using UnityEngine;
using UnityEngine.EventSystems;  // UI 위에 커서가 있는지 확인 (EventSystem) 사용
using UnityEngine.InputSystem;   // InputAction 사용

// 플레이어의 스킬 칸 1~6 관리자 (Player에 붙임)
// - 칸마다 스킬(SkillBase를 상속한 스킬 스크립트)을 하나씩 넣어 둡니다.
// - 매 프레임 스킬 칸 행동(GameInput.GetSkill)의 입력을 읽어 그 칸의 스킬에 전달합니다.
// - 겨눌 위치도 함께 넘깁니다: 마우스는 커서 아래 바닥, 게임패드는 가장 가까운 적(자동 조준).
// - 취소 행동(ESC)을 누르면 모든 스킬의 조준을 취소합니다.
// - 사망 등으로 PlayerController가 꺼져 있으면 스킬 입력을 받지 않습니다.
// 키 연결: 스킬 1번 칸 = 패드 X(□), 2번 = 우클릭 / 패드 Y(△), 3번 = Q / 패드 B(○), 4~6번 = 2·3·4 키 / 패드 LB·RB·RT
// (Assets/InputSystem_Actions에서 변경)
[RequireComponent(typeof(PlayerController))]
public class PlayerSkills : MonoBehaviour
{
    [Tooltip("스킬 칸 1~6 (Element 0 = 1번 칸). Player에 붙인 스킬 컴포넌트를 끌어다 놓습니다. 비워 둔 칸은 쓰지 않습니다.")]
    [SerializeField] private SkillBase[] slots = new SkillBase[GameInput.SkillSlotCount];

    [Header("게임패드 자동 조준")]
    [Tooltip("게임패드로 스킬을 쓰면 이 반경(m) 안의 가장 가까운 적을 자동으로 겨눕니다")]
    [SerializeField] private float autoAimRadius = 12f; // 자동 조준 반경

    [Tooltip("주변에 적이 없을 때 캐릭터 앞쪽 이 거리(m)의 바닥을 겨눕니다")]
    [SerializeField] private float noTargetAimDistance = 5f; // 적이 없을 때 앞쪽 거리

    private PlayerController controller;
    private readonly bool[] pressedWithGamepad = new bool[GameInput.SkillSlotCount]; // 칸마다 이번 누름이 게임패드였는지 (뗄 때도 같은 방식으로 겨누기 위해 기억)

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        EnsureSlotCount();
    }

    // Inspector에서 칸 수를 잘못 바꿔도 항상 6칸으로 맞춤
    private void OnValidate()
    {
        EnsureSlotCount();
    }

    private void EnsureSlotCount()
    {
        if (slots == null)
        {
            slots = new SkillBase[GameInput.SkillSlotCount];
        }
        else if (slots.Length != GameInput.SkillSlotCount)
        {
            Array.Resize(ref slots, GameInput.SkillSlotCount);
        }
    }

    // 칸 번호(1~6)의 스킬 (비어 있거나 번호가 잘못되면 null)
    public SkillBase GetSkill(int slotNumber)
    {
        if (slotNumber < 1 || slotNumber > slots.Length) return null;
        return slots[slotNumber - 1];
    }

    // 그 칸에 스킬이 들어 있는지
    public bool HasSkill(int slotNumber)
    {
        return GetSkill(slotNumber) != null;
    }

    // 사망 등으로 조작이 꺼질 때 모든 스킬의 조준을 정리
    public void CancelAllSkills()
    {
        foreach (SkillBase skill in slots)
        {
            if (skill != null) skill.CancelInput();
        }
    }

    private void Update()
    {
        // 사망 중에는 PlayerController가 꺼지므로 스킬도 쓰지 않음
        if (controller == null || !controller.isActiveAndEnabled) return;

        if (GameInput.Cancel.WasPressedThisFrame())
        {
            CancelAllSkills();
        }

        Vector2 pointerPosition = GameInput.PointerPosition;
        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        for (int i = 0; i < slots.Length; i++)
        {
            SkillBase skill = slots[i];
            if (skill == null) continue;

            InputAction action = GameInput.GetSkill(i + 1);

            bool wasPressed = action.WasPressedThisFrame();
            bool wasReleased = action.WasReleasedThisFrame();
            bool isHeld = !wasReleased && action.IsPressed();
            if (!wasPressed && !wasReleased && !isHeld) continue;

            // 뗀 순간에는 어느 장치였는지 알 수 없으므로 누른 순간에 기억해 둔 값을 씀
            if (wasPressed) pressedWithGamepad[i] = GameInput.IsFromGamepad(action);

            SkillAim aim = GetAim(pressedWithGamepad[i], pointerPosition);

            // 인벤토리 같은 UI 위에서 마우스 버튼으로 누른 것은 스킬 사용이 아님 (키보드 키는 UI 위에서도 사용 가능)
            if (wasPressed && !(pointerOverUI && GameInput.IsFromMouse(action)))
            {
                skill.OnInputPressed(aim);
            }

            // 짧게 톡 치면 누름과 뗌이 같은 프레임에 함께 일어날 수 있으므로 누름 처리 뒤에 바로 확인
            if (wasReleased)
            {
                skill.OnInputReleased(aim);
            }
            else if (isHeld)
            {
                skill.OnInputHeld(aim);
            }
        }
    }

    // 스킬을 겨눌 바닥 위치 정하기
    // - 마우스·키보드: 커서가 가리키는 바닥
    // - 게임패드: 가장 가까운 적의 발밑, 적이 없으면 캐릭터 앞쪽 바닥
    private SkillAim GetAim(bool useGamepad, Vector2 pointerPosition)
    {
        if (!useGamepad)
        {
            return controller.TryGetGroundPoint(pointerPosition, out Vector3 cursorPoint)
                ? new SkillAim(cursorPoint)
                : SkillAim.None;
        }

        Vector3 desiredPoint = controller.TryFindNearestEnemy(autoAimRadius, out Transform nearestEnemy)
            ? nearestEnemy.position
            : transform.position + transform.forward * noTargetAimDistance;

        // 바닥을 못 찾아도(맵 가장자리 등) 그 위치를 그대로 씀 → 게임패드는 항상 쓸 수 있음
        controller.TryGetGroundPointBelow(desiredPoint, out Vector3 groundPoint);
        return new SkillAim(groundPoint);
    }
}
