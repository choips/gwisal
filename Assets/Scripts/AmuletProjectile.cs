using UnityEngine;

// 도사의 원거리 스킬 '부적' 투사체
// 생성된 방향(앞쪽)으로 날아가다가 적(Enemy 태그)에 닿으면 데미지를 주고 사라집니다.
// 아무것도 맞히지 못하면 일정 시간 뒤 자동으로 파괴됩니다.
public class AmuletProjectile : MonoBehaviour
{
    private const string EnemyTag = "Enemy"; // 데미지를 줄 대상의 태그

    [Header("투사체 설정")]
    [Tooltip("1초에 날아가는 거리")]
    public float speed = 15f; // 이동 속도

    [Tooltip("적에게 주는 데미지")]
    public float damage = 20f; // 데미지

    [Tooltip("생성 후 이 시간(초)이 지나면 자동 파괴")]
    [SerializeField] private float lifeTime = 3f;

    private bool hasHit; // 이미 적을 맞혔는지 여부 (한 프레임에 여러 적과 겹쳐도 한 번만 처리)
    private PlayerStats owner; // 부적을 던진 플레이어 (치명타 판정용, 없으면 치명타 없음)

    // 발사한 플레이어를 기억 (PlayerController가 생성 직후 호출)
    public void SetOwner(PlayerStats stats)
    {
        owner = stats;
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

        Destroy(gameObject);
    }
}
