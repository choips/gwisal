using UnityEngine;

// 도사의 원거리 스킬 '부적' 투사체
// 생성된 방향(앞쪽)으로 날아가다가 적(Enemy 태그)에 닿으면 데미지를 주고 사라집니다.
// 데미지는 발사한 순간 플레이어의 무기 피해 × 배율로 정해집니다 (무기 장착 시 함께 강해짐, DamageCalculator 공식).
// 아무것도 맞히지 못하면 일정 시간 뒤 오브젝트 풀로 돌아갑니다 (풀 오브젝트라 상태 초기화는 OnEnable에서).
// 판정 상자(Box Collider)는 바닥 아래 ~ 머리 위(약 -0.1 ~ 1.9m)까지 세로로 길게 잡아, 키 작은 적도 높이 차이로 빗나가지 않게 합니다.
// (프리팹 크기 Y가 0.05라서 Size Y 40 = 실제 2m, Center Y 6 = 실제 0.3m 위 / 발사 높이가 바뀌면 함께 조정)
public class AmuletProjectile : MonoBehaviour
{
    private const string EnemyTag = "Enemy"; // 데미지를 줄 대상의 태그
    private const float FallbackDamage = 20f; // 시전자 정보가 없을 때(씬에 직접 놓았을 때) 쓰는 데미지

    [Header("투사체 설정")]
    [Tooltip("1초에 날아가는 거리")]
    public float speed = 15f; // 이동 속도

    [Tooltip("무기 피해에 곱해지는 배율 (2 = 무기 피해의 200%, 무기 피해 8~12면 데미지 16~24) — 스킬로 쏘면 Skill Data의 배율로 덮어씀")]
    public float attackPowerMultiplier = 2f; // 공격력 배율

    [Tooltip("생성 후 이 시간(초)이 지나면 자동으로 사라짐 (풀로 반납)")]
    [SerializeField] private float lifeTime = 3f;

    [Header("적중 이펙트")]
    [Tooltip("적에게 맞았을 때 맞은 자리에 생성할 이펙트 프리팹 (파티클 시스템, 비워두면 생략)")]
    [SerializeField] private GameObject hitEffectPrefab;

    [Tooltip("이펙트를 확실히 지우기 위한 최대 유지 시간(초)")]
    [SerializeField] private float hitEffectMaxLifetime = 2f;

    private bool hasHit; // 이미 적을 맞혔는지 여부 (한 프레임에 여러 적과 겹쳐도 한 번만 처리)
    private PlayerStats owner; // 부적을 던진 플레이어 (치명타 판정용, 없으면 치명타 없음)
    private float damage = FallbackDamage; // 치명타 판정 전 기본 데미지
    private float prefabMultiplier; // 프리팹에 적힌 원래 배율 (SetOwner가 덮어쓰므로 재사용할 때 되돌림)

    private void Awake()
    {
        prefabMultiplier = attackPowerMultiplier;
    }

    // 풀에서 꺼낼 때마다 실행: 지난번 발사의 상태를 지우고 lifeTime초 뒤 반납 예약
    private void OnEnable()
    {
        hasHit = false;
        owner = null;
        attackPowerMultiplier = prefabMultiplier;
        damage = FallbackDamage;

        ObjectPoolManager.Release(gameObject, lifeTime);
    }

    // 발사한 플레이어를 기억하고 데미지를 정함 (발사한 스크립트가 생성 직후 호출)
    // 날아가는 도중 레벨업해도 데미지가 바뀌지 않도록 발사 순간의 공격력으로 고정
    public void SetOwner(PlayerStats stats)
    {
        owner = stats;
        if (owner != null)
        {
            damage = owner.RollWeaponDamage(attackPowerMultiplier, true); // 무기 피해 × 배율 (치명타는 맞을 때 판정)
        }
    }

    // 스킬 데이터의 피해 배율로 발사할 때 사용 (AmuletProjectileSkill이 생성 직후 호출)
    public void SetOwner(PlayerStats stats, float damageMultiplier)
    {
        attackPowerMultiplier = damageMultiplier;
        SetOwner(stats);
    }

    private void Update()
    {
        // 바라보는 방향(앞쪽)으로 계속 이동
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        if (!other.CompareTag(EnemyTag)) return;

        // 콜라이더가 자식 오브젝트에 있어도 찾을 수 있도록 부모 방향까지 검색
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null) return;

        hasHit = true;

        bool isCritical = false;
        float finalDamage = owner != null ? owner.RollDamage(damage, out isCritical) : damage;

        Debug.Log($"부적 {(isCritical ? "치명타" : "적중")}! {other.name}에게 {finalDamage}의 데미지");
        enemy.TakeDamage(finalDamage, isCritical, transform.position);

        SpawnHitEffect(other);

        ObjectPoolManager.Release(gameObject);
    }

    // 적의 표면 중 부적과 가장 가까운 지점에 이펙트 생성 (날아온 쪽을 향하도록 회전)
    private void SpawnHitEffect(Collider hitCollider)
    {
        if (hitEffectPrefab == null) return;

        Vector3 hitPoint = hitCollider.ClosestPoint(transform.position);
        GameObject effect = ObjectPoolManager.Spawn(hitEffectPrefab, hitPoint, Quaternion.LookRotation(-transform.forward));

        // 파티클이 끝나면 PooledEffect가 반납하지만, 혹시 끝나지 않아도 쌓이지 않도록 일정 시간 뒤 반납
        ObjectPoolManager.Release(effect, hitEffectMaxLifetime);
    }
}
