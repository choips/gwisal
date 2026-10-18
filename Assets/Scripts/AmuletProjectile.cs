using UnityEngine;

// 도사의 원거리 스킬 '부적' 투사체 (SkillProjectile 상속)
// 생성된 방향(앞쪽)으로 날아가다가 적(Enemy 태그)에 닿으면 데미지를 주고 사라집니다.
// 데미지는 발사한 순간 플레이어의 무기 피해 × 배율로 정해집니다 (무기 장착 시 함께 강해짐, DamageCalculator 공식).
// 세로 판정·수명·이동은 SkillProjectile / ProjectileHitVolume이 담당합니다.
public class AmuletProjectile : SkillProjectile
{
    private const string EnemyTag = "Enemy";
    private const float FallbackDamage = 20f; // 시전자 정보가 없을 때 쓰는 데미지

    [Header("부적")]
    [Tooltip("무기 피해에 곱해지는 배율 (2 = 무기 피해의 200%) — 스킬로 쏘면 Skill Data의 배율로 덮어씀")]
    public float attackPowerMultiplier = 2f;

    [Header("적중 이펙트")]
    [Tooltip("적에게 맞았을 때 맞은 자리에 생성할 이펙트 프리팹 (비워두면 생략)")]
    [SerializeField] private GameObject hitEffectPrefab;

    [Tooltip("이펙트를 확실히 지우기 위한 최대 유지 시간(초)")]
    [SerializeField] private float hitEffectMaxLifetime = 2f;

    private PlayerStats owner;
    private float damage = FallbackDamage;
    private float prefabMultiplier; // 프리팹에 적힌 원래 배율 (재사용할 때 되돌림)

    protected override void Awake()
    {
        // Inspector의 speed가 예전에 public 필드였던 값을 쓰도록, 직렬화된 speed를 그대로 둠
        prefabMultiplier = attackPowerMultiplier;
        base.Awake();
    }

    protected override void OnEnable()
    {
        owner = null;
        attackPowerMultiplier = prefabMultiplier;
        damage = FallbackDamage;
        base.OnEnable();
    }

    // 발사한 플레이어를 기억하고 데미지를 정함 (발사한 스크립트가 생성 직후 호출)
    public void SetOwner(PlayerStats stats)
    {
        owner = stats;
        if (owner != null)
        {
            damage = owner.RollWeaponDamage(attackPowerMultiplier, true);
        }
    }

    // 스킬 데이터의 피해 배율로 발사할 때 사용 (AmuletProjectileSkill이 생성 직후 호출)
    public void SetOwner(PlayerStats stats, float damageMultiplier)
    {
        attackPowerMultiplier = damageMultiplier;
        SetOwner(stats);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        if (!other.CompareTag(EnemyTag)) return;

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null) return;

        hasHit = true;

        bool isCritical = false;
        float finalDamage = owner != null ? owner.RollDamage(damage, out isCritical) : damage;

        GameLog.Combat($"부적 {(isCritical ? "치명타" : "적중")}! {other.name}에게 {finalDamage}의 데미지");
        enemy.TakeDamage(finalDamage, isCritical, transform.position);

        SpawnHitEffect(other);
        ObjectPoolManager.Release(gameObject);
    }

    private void SpawnHitEffect(Collider hitCollider)
    {
        if (hitEffectPrefab == null) return;

        Vector3 hitPoint = hitCollider.ClosestPoint(transform.position);
        GameObject effect = ObjectPoolManager.Spawn(hitEffectPrefab, hitPoint, Quaternion.LookRotation(-transform.forward));
        ObjectPoolManager.Release(effect, hitEffectMaxLifetime);
    }
}
