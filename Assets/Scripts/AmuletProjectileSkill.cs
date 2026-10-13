using UnityEngine;

// 부적 투사체(Amulet Projectile) 스킬 (Player에 붙이고, PlayerSkills의 스킬 칸에 연결)
// 스킬 칸 키를 누르는 순간 겨눈 방향(마우스 커서 / 게임패드는 가장 가까운 적)으로 몸을 돌리고 부적을 발사합니다.
// 쿨타임·기력 소모·피해 배율·부적 프리팹은 Skill Data 파일(SkillData_AmuletProjectile)에서 정합니다.
public class AmuletProjectileSkill : SkillBase
{
    [Tooltip("부적이 생성될 위치 (Player의 자식 오브젝트 FirePoint, 비워 두면 Player 위치)")]
    [SerializeField] private Transform firePoint; // 발사 위치

    public override void OnInputPressed(SkillAim aim)
    {
        if (IsOnCooldown) return;

        if (data == null || data.prefab == null)
        {
            Debug.LogWarning("AmuletProjectileSkill: Skill Data 또는 그 안의 Prefab이 비어 있어 부적을 발사할 수 없습니다.");
            return;
        }

        if (controller == null) return;

        // 겨눈 바닥 위치 (마우스가 바닥 밖을 가리키면 발사하지 않음)
        if (!aim.HasPoint) return;
        Vector3 groundPoint = aim.Point;

        if (!TryPayMana()) return;

        // 이동·공격을 멈추고 발사 방향을 바라봄 (이동 중이면 NavMeshAgent가 몸을 진행 방향으로 다시 돌려 버림)
        controller.StopAndFace(groundPoint);

        // 발사 위치에서 커서 지점을 향하는 수평 방향
        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
        Vector3 direction = groundPoint - spawnPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

        GameObject amulet = ObjectPoolManager.Spawn(data.prefab, spawnPosition, Quaternion.LookRotation(direction));

        // 피해량(공격력 × 배율)과 치명타 판정을 위해 시전자와 배율을 알려 줌
        if (amulet.TryGetComponent(out AmuletProjectile projectile))
        {
            projectile.SetOwner(stats, DamageMultiplier);
        }

        StartCooldown();
    }
}
