using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스킬바 UI에서 표시할 스킬 종류
public enum SkillSlotType
{
    Amulet,          // 부적 투사체(Amulet Projectile) — 마우스 우클릭
    AmuletExplosion  // 부적 폭발(Amulet Explosion) — Q 키
}

// 스킬바의 스킬 칸 하나
// 쿨타임 동안 어두운 덮개가 시계 방향으로 걷히며 남은 시간을 숫자로 보여주고,
// 기력(MP)이 부족하면 아이콘을 어둡게 바꿉니다.
public class SkillSlotUI : MonoBehaviour
{
    [Header("표시할 스킬")]
    [SerializeField] private SkillSlotType skill;

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

    private PlayerController controller;
    private PlayerStats stats;
    private Color iconNormalColor; // 아이콘의 원래 색

    private void Awake()
    {
        controller = FindFirstObjectByType<PlayerController>();
        stats = FindFirstObjectByType<PlayerStats>();

        if (controller == null || stats == null)
        {
            Debug.LogWarning("SkillSlotUI: 씬에서 Player(PlayerController, PlayerStats)를 찾지 못했습니다.");
        }

        if (iconImage != null)
        {
            iconNormalColor = iconImage.color;
        }
    }

    private void Update()
    {
        if (controller == null || stats == null) return;

        GetSkillInfo(out float remaining, out float cooldown, out float mpCost);

        // 쿨타임 덮개: 막 사용했을 때 1(꽉 참) → 사용 가능해지면 0(사라짐)
        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = cooldown > 0f ? Mathf.Clamp01(remaining / cooldown) : 0f;
        }

        // 남은 시간 숫자: 쿨타임 중일 때만 소수점 한 자리로 표시
        if (cooldownText != null)
        {
            cooldownText.text = remaining > 0f ? remaining.ToString("0.0") : "";
        }

        // 기력이 부족하면 아이콘을 어둡게
        if (iconImage != null)
        {
            bool hasEnoughMP = stats.currentMP >= mpCost;
            iconImage.color = hasEnoughMP ? iconNormalColor : iconNormalColor * notEnoughMPTint;
        }
    }

    // 스킬 종류에 따라 남은 쿨타임, 전체 쿨타임, 기력 소모량을 PlayerController에서 가져옴
    private void GetSkillInfo(out float remaining, out float cooldown, out float mpCost)
    {
        switch (skill)
        {
            case SkillSlotType.AmuletExplosion:
                remaining = controller.AoeCooldownRemaining;
                cooldown = controller.aoeCooldown;
                mpCost = controller.aoeCost;
                break;

            default: // SkillSlotType.Amulet
                remaining = controller.AmuletCooldownRemaining;
                cooldown = controller.skillCooldown;
                mpCost = 0f; // 부적 투사체는 기력을 쓰지 않음
                break;
        }
    }
}
