using UnityEngine;

// 부적 폭발(Amulet Explosion) 스킬 (Player에 붙이고, PlayerSkills의 스킬 칸에 연결)
// 스킬 칸 키를 누르고 있는 동안 커서 위치의 바닥에 폭발 범위(조준 원)를 보여 주고,
// 떼는 순간 기력(MP)을 소모해 그 위치에 광역 폭발을 일으킵니다. 키를 짧게 톡 치면 바로 폭발합니다.
// 조준 중 취소 행동(ESC)이나 사망 시에는 기력을 쓰지 않고 조준만 끝냅니다.
// 쿨타임·기력 소모·피해 배율·폭발 프리팹은 Skill Data 파일(SkillData_AmuletExplosion)에서 정합니다.
public class AmuletExplosionSkill : SkillBase
{
    [Tooltip("누르고 있는 동안 폭발 범위를 바닥에 보여 줄 조준 원 (씬의 AoEIndicator, 비워 두면 조준 원 없이 뗄 때 바로 폭발)")]
    [SerializeField] private AoEIndicator aoeIndicator; // 조준 원

    private AoESkill aoeTemplate; // 폭발 프리팹의 AoESkill (조준 원 크기를 실제 폭발 반경과 맞추기 위해 읽음)
    private bool isAiming;        // 키를 누르고 폭발 위치를 조준하는 중인지 여부

    protected override void Awake()
    {
        base.Awake();

        if (data != null && data.prefab != null)
        {
            aoeTemplate = data.prefab.GetComponent<AoESkill>();
        }
    }

    // 꺼질 때 조준 원이 바닥에 남지 않도록 정리
    private void OnDisable()
    {
        StopAiming();
    }

    public override void OnInputPressed(Vector2 pointerPosition)
    {
        isAiming = true;
    }

    public override void OnInputHeld(Vector2 pointerPosition)
    {
        if (!isAiming) return;
        UpdateIndicator(pointerPosition);
    }

    public override void OnInputReleased(Vector2 pointerPosition)
    {
        if (!isAiming) return;

        StopAiming();
        if (!IsOnCooldown)
        {
            Cast(pointerPosition);
        }
    }

    public override void CancelInput()
    {
        StopAiming();
    }

    // 조준 중: 커서가 가리키는 바닥에 폭발 범위 원을 표시 (바닥 밖을 가리키면 숨김)
    private void UpdateIndicator(Vector2 pointerPosition)
    {
        if (aoeIndicator == null || aoeTemplate == null || controller == null) return;

        if (!controller.TryGetGroundPoint(pointerPosition, out Vector3 groundPoint))
        {
            aoeIndicator.Hide();
            return;
        }

        // 쿨타임이 끝났고 기력도 충분해야 "사용 가능"(금색), 아니면 빨간색
        bool isReady = !IsOnCooldown && HasEnoughMana;
        aoeIndicator.Show(groundPoint, aoeTemplate.explosionRadius, isReady);
    }

    // 조준 끝내기 (조준 원 숨김)
    private void StopAiming()
    {
        isAiming = false;
        if (aoeIndicator != null) aoeIndicator.Hide();
    }

    // 기력을 소모해 커서가 가리키는 바닥 위치에 광역 폭발 생성
    private void Cast(Vector2 pointerPosition)
    {
        if (data == null || data.prefab == null)
        {
            Debug.LogWarning("AmuletExplosionSkill: Skill Data 또는 그 안의 Prefab이 비어 있어 부적 폭발을 쓸 수 없습니다.");
            return;
        }

        if (controller == null) return;

        // 바닥이 아닌 곳이면 기력을 쓰지 않고 취소
        if (!controller.TryGetGroundPoint(pointerPosition, out Vector3 groundPoint)) return;

        // 기력이 부족하면 "기력이 부족합니다!" 알림 후 취소
        if (!TryPayMana()) return;

        GameObject explosion = Instantiate(data.prefab, groundPoint, Quaternion.identity);

        // 폭발의 Start(피해 처리)보다 먼저 실행되므로 피해량과 치명타 판정에 시전자 정보를 쓸 수 있음
        if (explosion.TryGetComponent(out AoESkill aoe))
        {
            aoe.SetOwner(stats, DamageMultiplier);
        }

        StartCooldown();
    }
}
