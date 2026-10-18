using UnityEngine;

// 플레이어 스킬용 투사체 공통 부모
// 새 투사체 스킬(쪽부적·나중에 추가할 원거리 스킬 등)은 이 클래스를 상속하세요.
// - 풀 재사용 시 OnEnable에서 상태를 지우고 lifeTime 뒤 반납
// - 세로 판정은 ProjectileHitVolume으로 항상 맞춤 (키 작거나 떠 있는 귀신도 맞음)
// - 이동은 기본으로 transform.forward 방향 (원하지 않으면 Update를 덮어씀)
[RequireComponent(typeof(Collider))]
public abstract class SkillProjectile : MonoBehaviour
{
    [Header("투사체 공통")]
    [Tooltip("1초에 날아가는 거리 (0이면 이 클래스가 이동하지 않음)")]
    [SerializeField] protected float speed = 15f;

    [Tooltip("생성 후 이 시간(초)이 지나면 풀로 반납")]
    [SerializeField] protected float lifeTime = 3f;

    [Tooltip("세로 판정 최소 길이(m). 귀신 키·부유와 무관하게 이만큼은 덮음")]
    [SerializeField] protected float minHitHeight = ProjectileHitVolume.DefaultMinHeight;

    protected bool hasHit; // 한 번 맞힌 뒤 중복 처리 방지

    protected virtual void Awake()
    {
        ProjectileHitVolume.EnsureVertical(gameObject, minHitHeight);
    }

    // 풀에서 꺼낼 때마다: 적중 여부 초기화 + 세로 판정 재확인 + 수명 예약
    protected virtual void OnEnable()
    {
        hasHit = false;
        ProjectileHitVolume.EnsureVertical(gameObject, minHitHeight);
        ObjectPoolManager.Release(gameObject, lifeTime);
    }

    protected virtual void Update()
    {
        if (speed <= 0f) return;
        transform.position += transform.forward * speed * Time.deltaTime;
    }
}
