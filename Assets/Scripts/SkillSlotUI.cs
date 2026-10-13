using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스킬바의 스킬 칸 하나
// Player의 PlayerSkills에서 지정한 칸 번호(1~6)의 스킬을 읽어 표시합니다.
// 쿨타임 동안 어두운 덮개가 시계 방향으로 걷히며 남은 시간을 숫자로 보여주고,
// 기력(MP)이 부족하면 아이콘을 어둡게 바꿉니다.
// 스킬 데이터(Skill Data)에 아이콘이 있으면 시작할 때 그 아이콘으로 바꿉니다 (비어 있으면 원래 아이콘 유지).
public class SkillSlotUI : MonoBehaviour
{
    [Header("표시할 스킬 칸")]
    [Tooltip("PlayerSkills의 칸 번호 (1~6). 부적 투사체 = 2 (우클릭), 부적 폭발 = 3 (Q)")]
    [Range(1, GameInput.SkillSlotCount)]
    [SerializeField] private int slotNumber = 2; // 스킬 칸 번호

    [Header("UI 연결")]
    [Tooltip("스킬 아이콘 이미지 (기력 부족 시 색이 바뀜)")]
    [SerializeField] private Image iconImage;

    [Tooltip("쿨타임 덮개 (Image Type: Filled, Fill Method: Radial 360)")]
    [SerializeField] private Image cooldownOverlay;

    [Tooltip("남은 쿨타임 숫자 (선택)")]
    [SerializeField] private TextMeshProUGUI cooldownText;

    [Header("색상")]
    [Tooltip("기력이 부족할 때 아이콘에 곱해질 색")]
    [SerializeField] private Color notEnoughMPTint = new Color(0.35f, 0.35f, 0.6f);

    private PlayerSkills playerSkills;
    private Color iconNormalColor;       // 아이콘의 원래 색
    private int shownTenths = -1;        // 지금 숫자로 보여 주는 남은 시간(0.1초 단위, -1 = 숫자 없음)

    private void Awake()
    {
        playerSkills = FindFirstObjectByType<PlayerSkills>();

        if (playerSkills == null)
        {
            Debug.LogWarning("SkillSlotUI: 씬에서 Player의 PlayerSkills를 찾지 못했습니다.");
        }

        if (iconImage != null)
        {
            iconNormalColor = iconImage.color;
        }
    }

    // 스킬 컴포넌트들의 Awake가 끝난 뒤 아이콘을 적용
    private void Start()
    {
        SkillBase skill = playerSkills != null ? playerSkills.GetSkill(slotNumber) : null;
        if (skill != null && skill.Data != null && skill.Data.icon != null && iconImage != null)
        {
            iconImage.sprite = skill.Data.icon;
        }
    }

    private void Update()
    {
        if (playerSkills == null) return;

        SkillBase skill = playerSkills.GetSkill(slotNumber);

        // 칸이 비어 있으면 덮개·숫자 없이 원래 색으로
        float remaining = skill != null ? skill.CooldownRemaining : 0f;
        float cooldown = skill != null ? skill.Cooldown : 0f;
        bool hasEnoughMP = skill == null || skill.HasEnoughMana;

        // 쿨타임 덮개: 막 사용했을 때 1(꽉 참) → 사용 가능해지면 0(사라짐)
        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = cooldown > 0f ? Mathf.Clamp01(remaining / cooldown) : 0f;
        }

        // 남은 시간 숫자: 쿨타임 중일 때만 소수점 한 자리로 표시 (값이 바뀔 때만 글자를 새로 만듦)
        if (cooldownText != null)
        {
            int tenths = remaining > 0f ? Mathf.CeilToInt(remaining * 10f) : -1;
            if (tenths != shownTenths)
            {
                shownTenths = tenths;
                cooldownText.text = tenths >= 0 ? (tenths / 10f).ToString("0.0") : "";
            }
        }

        // 기력이 부족하면 아이콘을 어둡게
        if (iconImage != null)
        {
            iconImage.color = hasEnoughMP ? iconNormalColor : iconNormalColor * notEnoughMPTint;
        }
    }
}
