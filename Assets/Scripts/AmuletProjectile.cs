using UnityEngine;

// 도사의 원거리 스킬 '부적' 투사체
// 생성된 방향(앞쪽)으로 날아가다가 적(Enemy 태그)에 닿으면 데미지를 주고 사라집니다.
// 데미지는 발사한 순간 플레이어 공격력 × 배율로 정해집니다 (레벨업·무기 장착 시 함께 강해짐).
// 아무것도 맞히지 못하면 일정 시간 뒤 자동으로 파괴됩니다.
public class AmuletProjectile : MonoBehaviour
{
    private const string EnemyTag = "Enemy"; // 데미지를 줄 대상의 태그
    private const float FallbackDamage = 20f; // 시전자 정보가 없을 때(씬에 직접 놓았을 때) 쓰는 데미지

    [Header("투사체 설정")]
    [Tooltip("1초에 날아가는 거리")]
    public float speed = 15f; // 이동 속도

    [Tooltip("플레이어 공격력에 곱해지는 배율 (2 = 공격력의 2배, 공격력 10이면 데미지 20)")]
    public float attackPowerMultiplier = 2f; // 공격력 배율

    [Tooltip("생성 후 이 시간(초)이 지나면 자동 파괴")]
    [SerializeField] private float lifeTime = 3f;

    [Header("적중 이펙트")]
    [Tooltip("적에게 맞았을 때 맞은 자리에 생성할 이펙트 프리팹 (파티클 시스템, 비워두면 생략)")]
    [SerializeField] private GameObject hitEffectPrefab;

    [Tooltip("이펙트를 확실히 지우기 위한 최대 유지 시간(초)")]
    [SerializeField] private float hitEffectMaxLifetime = 2f;

    private bool hasHit; // 이미 적을 맞혔는지 여부 (한 프레임에 여러 적과 겹쳐도 한 번만 처리)
    private PlayerStats owner; // 부적을 던진 플레이어 (치명타 판정용, 없으면 치명타 없음)
    private float damage = FallbackDamage; // 치명타 판정 전 기본 데미지

    // 발사한 플레이어를 기억하고 데미지를 정함 (PlayerController가 생성 직후 호출)
    // 날아가는 도중 레벨업해도 데미지가 바뀌지 않도록 발사 순간의 공격력으로 고정
    public void SetOwner(PlayerStats stats)
    {
        owner = stats;
        if (owner != null)
        {
            damage = owner.attackPower * attackPowerMultiplier;
        }
    }

    private void Start()
    {
        // 허공으로 날아가도 lifeTime초 뒤에 자동으로 파괴
        Destroy(gameObject, lifeTime);
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

        Destroy(gameObject);
    }

    // 적의 표면 중 부적과 가장 가까운 지점에 이펙트 생성 (날아온 쪽을 향하도록 회전)
    private void SpawnHitEffect(Collider hitCollider)
    {
        if (hitEffectPrefab == null) return;

        Vector3 hitPoint = hitCollider.ClosestPoint(transform.position);
        GameObject effect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(-transform.forward));

        // 파티클의 Stop Action 설정을 빠뜨려도 이펙트가 쌓이지 않도록 일정 시간 뒤 제거
        Destroy(effect, hitEffectMaxLifetime);
    }
}
