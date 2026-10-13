using System;
using UnityEngine;
using UnityEngine.EventSystems;  // UI 위에 커서가 있는지 확인 (EventSystem) 사용
using UnityEngine.InputSystem;   // InputAction, Mouse 사용

// 플레이어의 스킬 칸 1~6 관리자 (Player에 붙임)
// - 칸마다 스킬(SkillBase를 상속한 스킬 스크립트)을 하나씩 넣어 둡니다.
// - 매 프레임 스킬 칸 행동(GameInput.GetSkill)의 입력을 읽어 그 칸의 스킬에 전달합니다.
// - 취소 행동(ESC)을 누르면 모든 스킬의 조준을 취소합니다.
// - 사망 등으로 PlayerController가 꺼져 있으면 스킬 입력을 받지 않습니다.
// 키 연결: 스킬 2번 칸 = 마우스 우클릭, 3번 칸 = Q, 4~6번 칸 = 2·3·4 키 (Assets/InputSystem_Actions에서 변경)
[RequireComponent(typeof(PlayerController))]
public class PlayerSkills : MonoBehaviour
{
    [Tooltip("스킬 칸 1~6 (Element 0 = 1번 칸). Player에 붙인 스킬 컴포넌트를 끌어다 놓습니다. 비워 둔 칸은 쓰지 않습니다.")]
    [SerializeField] private SkillBase[] slots = new SkillBase[GameInput.SkillSlotCount];

    private PlayerController controller;

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

            // 인벤토리 같은 UI 위에서 마우스 버튼으로 누른 것은 스킬 사용이 아님 (키보드 키는 UI 위에서도 사용 가능)
            if (action.WasPressedThisFrame() && !(pointerOverUI && IsMouseButton(action)))
            {
                skill.OnInputPressed(pointerPosition);
            }

            // 짧게 톡 치면 누름과 뗌이 같은 프레임에 함께 일어날 수 있으므로 누름 처리 뒤에 바로 확인
            if (action.WasReleasedThisFrame())
            {
                skill.OnInputReleased(pointerPosition);
            }
            else if (action.IsPressed())
            {
                skill.OnInputHeld(pointerPosition);
            }
        }
    }

    // 이번 입력이 마우스 버튼에서 왔는지
    private static bool IsMouseButton(InputAction action)
    {
        return action.activeControl != null && action.activeControl.device is Mouse;
    }
}
