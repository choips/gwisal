using UnityEngine;

// 스킬을 쓸 위치 (PlayerSkills가 입력 장치에 맞게 정해서 스킬에 넘겨줌)
// - 마우스: 커서가 가리키는 바닥 (바닥 밖을 가리키면 위치 없음)
// - 게임패드: 가장 가까운 적의 발밑, 주변에 적이 없으면 캐릭터 앞쪽 바닥 (자동 조준)
public readonly struct SkillAim
{
    public readonly bool HasPoint; // 겨눌 바닥 위치가 있는지
    public readonly Vector3 Point; // 겨누는 바닥 위치

    public SkillAim(Vector3 point)
    {
        HasPoint = true;
        Point = point;
    }

    public static SkillAim None => default; // 위치 없음
}

// 모든 플레이어 스킬의 공통 부분 (직접 붙이지 않고, 스킬별 스크립트가 상속해서 사용)
// - 스킬 데이터(SkillData)에서 쿨타임·기력 소모·피해 배율을 읽어 옵니다.
// - 쿨타임 계산과 기력 지불을 대신 처리합니다.
// - PlayerSkills가 스킬 칸의 입력(누름 / 누르고 있음 / 뗌 / 취소)과 겨눈 위치(SkillAim)를 이 스크립트에 전달합니다.
// 스킬별 스크립트는 필요한 입력 함수만 골라서 덮어씁니다(override).
//   예) 부적 투사체: 누르는 순간 발사 / 부적 폭발: 누르고 있는 동안 조준, 떼면 폭발
public abstract class SkillBase : MonoBehaviour
{
    [Tooltip("이 스킬의 데이터 (Project 창의 Skill Data 파일을 끌어다 놓기)")]
    [SerializeField] protected SkillData data;

    protected PlayerStats stats;           // 기력·공격력을 가진 플레이어 능력치
    protected PlayerController controller; // 바닥 위치 찾기·이동 멈추기 등을 도와주는 플레이어 조작 스크립트

    private float nextUseTime; // 다음에 쓸 수 있는 시각 (게임 시작 후 경과 초)

    public SkillData Data => data;

    // 실제 쿨타임 = Skill Data 쿨타임 × (1 − 쿨타임 감소) / 실제 기력 소모 = Skill Data 소모 × (1 − 기력 소모 감소)
    public float Cooldown => data != null ? data.cooldown * (1f - GetPlayerStat(StatType.CooldownReduction)) : 0f;
    public float ManaCost => data != null ? data.manaCost * (1f - GetPlayerStat(StatType.ManaCostReduction)) : 0f;
    public float DamageMultiplier => data != null ? data.damageMultiplier : 1f;

    // 남은 쿨타임(초, 0이면 사용 가능) — 스킬바 UI가 읽어 감 (개발자 메뉴의 쿨타임 없음이 켜져 있으면 항상 0)
    public float CooldownRemaining => DevCheats.NoCooldown ? 0f : Mathf.Max(0f, nextUseTime - Time.time);
    public bool IsOnCooldown => !DevCheats.NoCooldown && Time.time < nextUseTime;

    // 기력이 충분한지 (스킬바 아이콘을 어둡게 할지 정할 때 사용)
    public bool HasEnoughMana => stats == null || stats.currentMP >= ManaCost;

    protected virtual void Awake()
    {
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();

        if (data == null)
        {
            Debug.LogWarning($"{GetType().Name}: Skill Data가 연결되지 않았습니다. Player의 이 컴포넌트에 Skill Data 파일을 끌어다 놓으세요.");
        }
    }

    // 스킬 칸 키를 누른 순간
    public virtual void OnInputPressed(SkillAim aim) { }

    // 스킬 칸 키를 누르고 있는 동안 매 프레임 (누른 순간 프레임 포함, 뗀 순간 프레임 제외)
    public virtual void OnInputHeld(SkillAim aim) { }

    // 스킬 칸 키를 뗀 순간
    public virtual void OnInputReleased(SkillAim aim) { }

    // 조준 취소 (취소 키, 사망 등) — 조준 원 같은 표시를 정리
    public virtual void CancelInput() { }

    // 지금 조준 중인지 (ESC가 창 닫기보다 조준 취소를 먼저 하기 위해 확인)
    public virtual bool IsAiming => false;

    // 플레이어 능력치 최종값 (플레이어 능력치가 없으면 0)
    private float GetPlayerStat(StatType stat)
    {
        return stats != null ? stats.GetStat(stat) : 0f;
    }

    // 쿨타임 시작 (스킬을 실제로 쓴 뒤 호출)
    protected void StartCooldown()
    {
        // 쿨타임 없음 중에 쓴 스킬은 나중에 치트를 꺼도 쿨타임이 남지 않게 함
        nextUseTime = DevCheats.NoCooldown ? Time.time : Time.time + Cooldown;
    }

    // 기력 지불: 충분하면 소모하고 true, 부족하면 "기력이 부족합니다!" 알림 후 false
    protected bool TryPayMana()
    {
        if (ManaCost <= 0f) return true;
        return stats != null && stats.UseMP(ManaCost);
    }
}
