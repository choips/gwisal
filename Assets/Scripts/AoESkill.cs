using System.Collections.Generic;
using UnityEngine;

// 도사의 광역 스킬 '부적 폭발'
// 생성된 첫 프레임에 폭발 반경 안의 모든 적(Enemy 태그)에게 데미지를 주고,
// 폭발 이펙트가 퍼졌다가 옅어지며 1초 뒤 오브젝트 풀로 돌아갑니다 (풀 오브젝트라 상태 초기화는 OnEnable에서).
// 데미지는 플레이어의 무기 피해 × 배율로 정해집니다 (무기 장착 시 함께 강해짐, DamageCalculator 공식).
public class AoESkill : MonoBehaviour
{
    private const string EnemyTag = "Enemy"; // 데미지를 줄 대상의 태그
    private const float FallbackDamage = 20f; // 시전자 정보가 없을 때(씬에 직접 놓았을 때) 쓰는 데미지
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor"); // 발광 색 속성

    // 폭발할 때마다 새로 만들지 않도록 모든 폭발이 함께 쓰는 임시 목록
    private static readonly Collider[] hitBuffer = new Collider[64]; // 범위 안 콜라이더 (최대 64개)
    private static readonly HashSet<Enemy> damagedEnemies = new HashSet<Enemy>(); // 이번 폭발에서 이미 맞은 적

    [Header("폭발 설정")]
    [Tooltip("폭발 반경 (이 거리 안의 적이 모두 피해를 입음)")]
    public float explosionRadius = 3f;

    [Tooltip("무기 피해에 곱해지는 배율 (2 = 무기 피해의 200%, 무기 피해 8~12면 데미지 16~24) — 스킬로 쓰면 Skill Data의 배율로 덮어씀")]
    public float attackPowerMultiplier = 2f; // 공격력 배율

    [Tooltip("폭발 후 이펙트가 사라질 때까지의 시간(초)")]
    [SerializeField] private float lifeTime = 1f;

    [Header("이펙트 연출")]
    [Tooltip("체크하면 구체 크기를 폭발 반경에 자동으로 맞춤 (보이는 범위 = 실제 피해 범위)")]
    [SerializeField] private bool matchVisualToRadius = true;

    [Tooltip("작은 크기에서 최대 크기로 퍼지는 데 걸리는 시간(초)")]
    [SerializeField] private float expandDuration = 0.15f;

    private Material effectMaterial; // 투명도를 바꿀 이 폭발 전용 재질
    private Color startColor;        // 처음 색상 (투명도 포함)
    private bool hasEmission;        // 재질의 Emission(발광)이 켜져 있는지 여부
    private Color startEmission;     // 처음 발광 색상
    private Vector3 targetScale;     // 최종 크기
    private float elapsed;           // 생성 후 지난 시간
    private PlayerStats owner;       // 폭발을 일으킨 플레이어 (치명타 판정용, 없으면 치명타 없음)
    private float damage = FallbackDamage; // 치명타 판정 전 기본 데미지
    private float prefabMultiplier;  // 프리팹에 적힌 원래 배율 (SetOwner가 덮어쓰므로 재사용할 때 되돌림)
    private bool hasDealtDamage;     // 이번 폭발의 데미지를 이미 줬는지 여부

    // 시전한 플레이어를 기억하고 데미지를 정함 (시전한 스크립트가 꺼낸 직후, 첫 Update보다 먼저 호출)
    public void SetOwner(PlayerStats stats)
    {
        owner = stats;
        if (owner != null)
        {
            damage = owner.RollWeaponDamage(attackPowerMultiplier, true); // 무기 피해 × 배율 (치명타는 적마다 판정)
        }
    }

    // 스킬 데이터의 피해 배율로 폭발시킬 때 사용 (AmuletExplosionSkill이 꺼낸 직후 호출)
    public void SetOwner(PlayerStats stats, float damageMultiplier)
    {
        attackPowerMultiplier = damageMultiplier;
        SetOwner(stats);
    }

    // 처음 만들어질 때 한 번만: 프리팹 원래 값과 재질을 기억
    private void Awake()
    {
        prefabMultiplier = attackPowerMultiplier;

        // 기본 Sphere는 지름이 1이므로, 반경 r에 맞추려면 크기를 r × 2로 설정
        targetScale = matchVisualToRadius ? Vector3.one * explosionRadius * 2f : transform.localScale;

        Renderer effectRenderer = GetComponentInChildren<Renderer>();
        if (effectRenderer != null)
        {
            // .material은 이 폭발 전용 복사본을 만들어, 다른 폭발의 투명도에 영향을 주지 않음 (재사용 중에는 같은 복사본을 계속 씀)
            effectMaterial = effectRenderer.material;
            startColor = effectMaterial.color;

            // 투명 재질의 Preserve Specular Lighting이 켜져 있으면 발광은 투명도의 영향을 받지 않으므로 따로 줄여야 함
            hasEmission = effectMaterial.IsKeywordEnabled("_EMISSION") && effectMaterial.HasProperty(EmissionColorId);
            if (hasEmission)
            {
                startEmission = effectMaterial.GetColor(EmissionColorId);
            }
        }
    }

    // 풀에서 꺼낼 때마다 실행: 지난번 폭발의 상태를 지우고 lifeTime초 뒤 반납 예약
    private void OnEnable()
    {
        elapsed = 0f;
        hasDealtDamage = false;
        owner = null;
        attackPowerMultiplier = prefabMultiplier;
        damage = FallbackDamage;

        transform.localScale = targetScale * 0.2f;
        ApplyFade(1f);

        ObjectPoolManager.Release(gameObject, lifeTime);
    }

    // 재질 복사본은 오브젝트와 함께 지워지지 않으므로 직접 정리
    private void OnDestroy()
    {
        if (effectMaterial != null) Destroy(effectMaterial);
    }

    // 폭발 반경 안의 모든 적에게 데미지
    private void DealDamage()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, hitBuffer,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        // 적 하나에 콜라이더가 여러 개 있어도 데미지는 한 번만 주기 위해 기록
        damagedEnemies.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            if (!hit.CompareTag(EnemyTag)) continue;

            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null || !damagedEnemies.Add(enemy)) continue;

            // 적마다 치명타를 따로 판정
            bool isCritical = false;
            float finalDamage = owner != null ? owner.RollDamage(damage, out isCritical) : damage;
            enemy.TakeDamage(finalDamage, isCritical, transform.position); // 폭발 중심에서 바깥쪽으로 밀려남
        }

        GameLog.Combat($"부적 폭발! 적 {damagedEnemies.Count}명에게 기본 {damage}의 데미지");
        damagedEnemies.Clear(); // 적 참조를 붙잡아 두지 않도록 비움
    }

    private void Update()
    {
        // 첫 프레임에 한 번만 데미지 (꺼낸 직후 SetOwner로 받은 시전자 정보를 쓰기 위해 OnEnable이 아니라 여기서)
        if (!hasDealtDamage)
        {
            hasDealtDamage = true;
            DealDamage();
        }

        elapsed += Time.deltaTime;

        // 작게 시작해서 빠르게 퍼지는 연출
        float expandProgress = expandDuration > 0f ? Mathf.Clamp01(elapsed / expandDuration) : 1f;
        transform.localScale = Vector3.Lerp(targetScale * 0.2f, targetScale, expandProgress);

        // 점점 투명해지는 연출 (재질이 Transparent일 때만 눈에 보임)
        ApplyFade(1f - Mathf.Clamp01(elapsed / lifeTime)); // 1 → 0
    }

    // 재질의 투명도와 발광을 fade(1 = 처음 그대로, 0 = 완전히 투명) 비율로 맞춤
    private void ApplyFade(float fade)
    {
        if (effectMaterial == null) return;

        Color color = startColor;
        color.a = startColor.a * fade;
        effectMaterial.color = color;

        if (hasEmission)
        {
            effectMaterial.SetColor(EmissionColorId, startEmission * fade);
        }
    }

    // Scene 뷰에서 선택하면 실제 피해 범위를 주황색 원으로 표시
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
