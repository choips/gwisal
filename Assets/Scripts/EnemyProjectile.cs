using UnityEngine;

// 적이 쏘는 투사체 (도깨비불 불덩이 등)
// 앞쪽으로 날아가다 플레이어에 닿으면 피해를 주고 풀로 돌아갑니다.
// 세로 판정은 ProjectileHitVolume으로 맞춤 (플레이어 키·발사 높이가 달라도 빗나가지 않게).
[RequireComponent(typeof(Collider))]
public class EnemyProjectile : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Tooltip("생성 후 이 시간(초)이 지나면 자동으로 풀로 반납")]
    [SerializeField] private float lifeTime = 4f;

    [Tooltip("세로 판정 최소 길이(m)")]
    [SerializeField] private float minHitHeight = ProjectileHitVolume.DefaultMinHeight;

    private float speed = 8f;
    private float damage;
    private int attackerLevel = 1;
    private bool hasHit;

    private void Awake()
    {
        ProjectileHitVolume.EnsureVertical(gameObject, minHitHeight);
    }

    private void OnEnable()
    {
        hasHit = false;
        damage = 0f;
        attackerLevel = 1;
        speed = 8f;
        ProjectileHitVolume.EnsureVertical(gameObject, minHitHeight);
        ObjectPoolManager.Release(gameObject, lifeTime);
    }

    // 발사한 적이 생성 직후 호출 — 피해·레벨·날아가는 속도
    public void Launch(float attackDamage, int level, float projectileSpeed)
    {
        damage = Mathf.Max(0f, attackDamage);
        attackerLevel = Mathf.Max(1, level);
        speed = Mathf.Max(0.1f, projectileSpeed);
    }

    private void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        if (!other.CompareTag(PlayerTag)) return;

        PlayerStats player = other.GetComponentInParent<PlayerStats>();
        if (player == null) return;

        hasHit = true;
        GameLog.Combat($"적 투사체 적중! 플레이어에게 {damage}의 데미지 (공격자 레벨 {attackerLevel})");
        player.TakeDamage(damage, attackerLevel);
        ObjectPoolManager.Release(gameObject);
    }
}
