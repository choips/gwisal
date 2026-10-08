using System.Collections.Generic;
using UnityEngine;

// 도사의 광역 스킬 '부적 폭발'
// 생성되는 순간 폭발 반경 안의 모든 적(Enemy 태그)에게 데미지를 주고,
// 폭발 이펙트가 퍼졌다가 옅어지며 1초 뒤 스스로 파괴됩니다.
// 데미지는 플레이어 공격력 × 배율로 정해집니다 (레벨업·무기 장착 시 함께 강해짐).
public class AoESkill : MonoBehaviour
{
    private const string EnemyTag = "Enemy"; // 데미지를 줄 대상의 태그
    private const float FallbackDamage = 20f; // 시전자 정보가 없을 때(씬에 직접 놓았을 때) 쓰는 데미지
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor"); // 발광 색 속성

    [Header("폭발 설정")]
    [Tooltip("폭발 반경 (이 거리 안의 적이 모두 피해를 입음)")]
    public float explosionRadius = 3f;

    [Tooltip("플레이어 공격력에 곱해지는 배율 (2 = 공격력의 2배, 공격력 10이면 데미지 20)")]
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

    // 시전한 플레이어를 기억하고 데미지를 정함 (PlayerController가 생성 직후, Start보다 먼저 호출)
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
        DealDamage();

        // 기본 Sphere는 지름이 1이므로, 반경 r에 맞추려면 크기를 r × 2로 설정
        targetScale = matchVisualToRadius ? Vector3.one * explosionRadius * 2f : transform.localScale;

        Renderer effectRenderer = GetComponentInChildren<Renderer>();
        if (effectRenderer != null)
        {
            // .material은 이 폭발 전용 복사본을 만들어, 다른 폭발의 투명도에 영향을 주지 않음
            effectMaterial = effectRenderer.material;
            startColor = effectMaterial.color;

            // 투명 재질의 Preserve Specular Lighting이 켜져 있으면 발광은 투명도의 영향을 받지 않으므로 따로 줄여야 함
            hasEmission = effectMaterial.IsKeywordEnabled("_EMISSION") && effectMaterial.HasProperty(EmissionColorId);
            if (hasEmission)
            {
                startEmission = effectMaterial.GetColor(EmissionColorId);
            }
        }

        Destroy(gameObject, lifeTime);
    }

    // 폭발 반경 안의 모든 적에게 데미지
    private void DealDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        // 적 하나에 콜라이더가 여러 개 있어도 데미지는 한 번만 주기 위해 기록
        HashSet<Enemy> damagedEnemies = new HashSet<Enemy>();

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag(EnemyTag)) continue;

            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null || !damagedEnemies.Add(enemy)) continue;

            // 적마다 치명타를 따로 판정
            bool isCritical = false;
            float finalDamage = owner != null ? owner.RollDamage(damage, out isCritical) : damage;
            enemy.TakeDamage(finalDamage, isCritical, transform.position); // 폭발 중심에서 바깥쪽으로 밀려남
        }

        Debug.Log($"부적 폭발! 적 {damagedEnemies.Count}명에게 기본 {damage}의 데미지");
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        // 작게 시작해서 빠르게 퍼지는 연출
        float expandProgress = expandDuration > 0f ? Mathf.Clamp01(elapsed / expandDuration) : 1f;
        transform.localScale = Vector3.Lerp(targetScale * 0.2f, targetScale, expandProgress);

        // 점점 투명해지는 연출 (재질이 Transparent일 때만 눈에 보임)
        if (effectMaterial != null)
        {
            float fade = 1f - Mathf.Clamp01(elapsed / lifeTime); // 1 → 0

            Color color = startColor;
            color.a = startColor.a * fade;
            effectMaterial.color = color;

            if (hasEmission)
            {
                effectMaterial.SetColor(EmissionColorId, startEmission * fade);
            }
        }
    }

    // Scene 뷰에서 선택하면 실제 피해 범위를 주황색 원으로 표시
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
