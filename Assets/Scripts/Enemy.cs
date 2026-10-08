using System.Collections.Generic; // List 사용
using UnityEngine;
using UnityEngine.AI;            // NavMeshAgent 사용

// 적의 체력(HP)과 AI를 관리하는 스크립트
// - 플레이어가 인식 거리 안에 들어오면 쫓아가고, 공격 사거리 안이면 멈춰서 쿨타임마다 공격합니다.
// - 피격 시 체력이 깎이고, 체력이 0 이하가 되면 오브젝트가 파괴(사망)됩니다.
[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    [Header("체력 설정")]
    [Tooltip("적의 최대 체력")]
    public float maxHealth = 30f;

    [Tooltip("현재 체력 (Play 중에 Inspector에서 확인용)")]
    [SerializeField] private float currentHealth;

    [Header("보상 설정")]
    [Tooltip("처치 시 플레이어가 얻는 경험치")]
    public float experienceReward = 50f;

    [Header("데미지 텍스트")]
    [Tooltip("피격 시 머리 위에 띄울 데미지 숫자 프리팹 (DamageText 스크립트가 붙어 있어야 합니다)")]
    public GameObject damageTextPrefab;

    [Tooltip("적의 중심에서 데미지 숫자가 나타날 높이")]
    [SerializeField] private float damageTextHeight = 1.5f;

    [Header("AI 설정")]
    [Tooltip("이 거리 안에 플레이어가 들어오면 쫓아갑니다")]
    public float aggroRange = 10f; // 플레이어 인식 거리

    [Tooltip("이 거리 안에 플레이어가 들어오면 멈춰서 공격합니다")]
    public float attackRange = 1.5f; // 공격 사거리

    [Tooltip("한 번 공격할 때 플레이어에게 주는 데미지")]
    public float attackDamage = 5f; // 공격력

    [Tooltip("공격과 공격 사이의 대기 시간(초)")]
    public float attackCooldown = 1.5f; // 공격 쿨타임

    [Tooltip("공격할 때 플레이어를 향해 회전하는 속도")]
    [SerializeField] private float rotationSpeed = 10f;

    [Header("피격 연출 - 번쩍임")]
    [Tooltip("맞았을 때 빛나는 색")]
    [SerializeField] private Color flashColor = Color.white;

    [Tooltip("빛나는 세기 (클수록 더 하얗게)")]
    [SerializeField] private float flashIntensity = 1.5f;

    [Tooltip("번쩍임이 사라지는 데 걸리는 시간(초)")]
    [SerializeField] private float flashDuration = 0.12f;

    [Header("피격 연출 - 밀려나기(넉백)")]
    [Tooltip("한 번 맞을 때 뒤로 밀려나는 거리 (0이면 밀려나지 않음)")]
    [SerializeField] private float knockbackDistance = 0.4f;

    [Tooltip("밀려나는 데 걸리는 시간(초), 이 동안은 추격·공격을 잠시 멈춤")]
    [SerializeField] private float knockbackDuration = 0.1f;

    [Tooltip("치명타일 때 밀려나는 거리 배율")]
    [SerializeField] private float criticalKnockbackMultiplier = 2f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor"); // 발광 색 속성

    private NavMeshAgent agent;        // 적의 길찾기 이동 컴포넌트
    private PlayerStats player;        // 추적/공격할 플레이어
    private EnemySpawner spawner;      // 이 적을 생성한 스포너 (씬에 직접 배치한 적은 null)
    private float nextAttackTime = 0f; // 다음 공격이 가능한 시각
    private bool isDead;               // 이미 사망 처리되었는지 여부 (중복 파괴 방지)

    private Material[] flashMaterials;  // 번쩍임에 쓸 이 적 전용 재질들
    private Color[] originalEmissions;  // 재질마다 원래 발광 색
    private bool[] originalEmissionOn;  // 재질마다 원래 발광(Emission)이 켜져 있었는지
    private float flashEndTime;         // 번쩍임이 끝나는 시각
    private bool isFlashing;            // 지금 번쩍이는 중인지

    private Vector3 knockbackVelocity;  // 밀려나는 속도 (방향 × 빠르기)
    private float knockbackEndTime;     // 밀려나기가 끝나는 시각

    // 머리 위 체력바가 읽어가는 체력 비율 (0 ~ 1)
    public float HealthRatio => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

    private void Awake()
    {
        // 체력바가 첫 프레임에 빈 칸으로 보이지 않도록 Start보다 먼저 채움
        currentHealth = maxHealth;

        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogWarning($"{gameObject.name}에 NavMeshAgent가 없어 움직일 수 없습니다. 프리팹에 Nav Mesh Agent를 추가해 주세요.");
        }

        SetupFlashMaterials();
    }

    // 몸체(MeshRenderer)의 재질을 이 적 전용 복사본으로 만들고 원래 발광 상태를 기억
    // (체력바 같은 UI는 MeshRenderer가 아니므로 제외됨)
    private void SetupFlashMaterials()
    {
        List<Material> materials = new List<Material>();
        foreach (MeshRenderer meshRenderer in GetComponentsInChildren<MeshRenderer>())
        {
            // .materials는 복사본을 만들어, 다른 적이나 원본 재질에 영향을 주지 않음
            materials.AddRange(meshRenderer.materials);
        }

        flashMaterials = materials.ToArray();
        originalEmissions = new Color[flashMaterials.Length];
        originalEmissionOn = new bool[flashMaterials.Length];

        for (int i = 0; i < flashMaterials.Length; i++)
        {
            Material material = flashMaterials[i];
            originalEmissionOn[i] = material.IsKeywordEnabled("_EMISSION");
            originalEmissions[i] = material.HasProperty(EmissionColorId)
                ? material.GetColor(EmissionColorId)
                : Color.black;
        }
    }

    // 스포너가 적을 생성한 직후 호출해서 자신을 알려 줌
    public void SetSpawner(EnemySpawner owner)
    {
        spawner = owner;
    }

    private void Start()
    {
        // 씬에서 PlayerStats가 붙은 플레이어를 찾아 둠 (매 프레임 찾으면 느리므로 한 번만)
        player = FindFirstObjectByType<PlayerStats>();
        if (player == null)
        {
            Debug.LogWarning($"{gameObject.name}: 씬에서 PlayerStats를 찾지 못해 AI가 동작하지 않습니다.");
        }
    }

    private void Update()
    {
        UpdateFlash();

        if (isDead || agent == null || !agent.isOnNavMesh) return;

        // 밀려나는 중에는 추격·공격을 멈추고 뒤로 미끄러짐 (Move는 NavMesh 밖으로 나가지 않음)
        if (Time.time < knockbackEndTime)
        {
            agent.Move(knockbackVelocity * Time.deltaTime);
            return;
        }

        // 플레이어가 없거나 이미 죽었다면 제자리에 멈춤
        if (player == null || player.IsDead)
        {
            StopMoving();
            return;
        }

        // 높이(y) 차이는 무시하고 바닥 평면 기준으로 거리 계산
        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        if (distance <= attackRange)
        {
            // b. 공격 사거리 안: 멈추고 플레이어를 바라보며 쿨타임마다 공격
            StopMoving();
            LookAt(toPlayer);

            if (Time.time >= nextAttackTime)
            {
                AttackPlayer();
            }
        }
        else if (distance <= aggroRange)
        {
            // a. 인식 거리 안, 공격 사거리 밖: 플레이어를 향해 이동
            agent.isStopped = false;
            agent.SetDestination(player.transform.position);
        }
        else
        {
            // 인식 거리 밖: 제자리에 대기
            StopMoving();
        }
    }

    // 플레이어에게 데미지를 주고 다음 공격 시각을 설정
    private void AttackPlayer()
    {
        Debug.Log($"{gameObject.name}의 공격! 플레이어에게 {attackDamage}의 데미지");
        player.TakeDamage(attackDamage);

        nextAttackTime = Time.time + attackCooldown;
    }

    // 이동을 멈추고 남은 경로 삭제
    private void StopMoving()
    {
        if (!agent.isOnNavMesh) return;

        agent.isStopped = true;
        agent.ResetPath();
    }

    // 지정한 방향으로 부드럽게 회전
    private void LookAt(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    // 데미지를 받아 체력을 깎고, 0 이하가 되면 사망 처리
    // isCritical: 치명타면 데미지 숫자를 크고 빨갛게 표시, 더 멀리 밀려남
    // hitFrom: 공격이 날아온 위치 (이 반대 방향으로 밀려남, 비우면 플레이어 위치 기준)
    public void TakeDamage(float damage, bool isCritical = false, Vector3? hitFrom = null)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($"{gameObject.name} 남은 체력: {Mathf.Max(currentHealth, 0f)} / {maxHealth}");

        // 마지막 일격에도 숫자가 보이도록 사망 처리보다 먼저 생성
        ShowDamageText(damage, isCritical);

        StartFlash();
        StartKnockback(hitFrom, isCritical);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    // 코드로 만든 재질 복사본은 자동으로 지워지지 않으므로 적이 사라질 때 함께 정리
    private void OnDestroy()
    {
        if (flashMaterials == null) return;

        foreach (Material material in flashMaterials)
        {
            if (material != null) Destroy(material);
        }
    }

    // 하얗게 번쩍이기 시작 (발광을 켜고 Update에서 서서히 원래대로 되돌림)
    private void StartFlash()
    {
        if (flashMaterials == null || flashMaterials.Length == 0 || flashDuration <= 0f) return;

        foreach (Material material in flashMaterials)
        {
            material.EnableKeyword("_EMISSION");
        }

        isFlashing = true;
        flashEndTime = Time.time + flashDuration;
    }

    // 번쩍임 세기를 시간에 따라 줄이고, 끝나면 원래 발광 상태로 복구
    private void UpdateFlash()
    {
        if (!isFlashing) return;

        float remaining = Mathf.Clamp01((flashEndTime - Time.time) / flashDuration); // 1 → 0
        Color flash = flashColor * flashIntensity;

        for (int i = 0; i < flashMaterials.Length; i++)
        {
            flashMaterials[i].SetColor(EmissionColorId, Color.Lerp(originalEmissions[i], flash, remaining));
        }

        if (remaining <= 0f)
        {
            isFlashing = false;
            for (int i = 0; i < flashMaterials.Length; i++)
            {
                flashMaterials[i].SetColor(EmissionColorId, originalEmissions[i]);
                if (!originalEmissionOn[i]) flashMaterials[i].DisableKeyword("_EMISSION");
            }
        }
    }

    // 공격이 날아온 반대 방향으로 잠깐 밀려나기 시작
    private void StartKnockback(Vector3? hitFrom, bool isCritical)
    {
        if (knockbackDistance <= 0f || knockbackDuration <= 0f) return;
        if (agent == null || !agent.isOnNavMesh) return;

        Vector3 sourcePosition;
        if (hitFrom.HasValue) sourcePosition = hitFrom.Value;
        else if (player != null) sourcePosition = player.transform.position;
        else return;

        Vector3 direction = transform.position - sourcePosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return; // 정확히 같은 위치면 방향을 정할 수 없음

        float distance = knockbackDistance * (isCritical ? criticalKnockbackMultiplier : 1f);
        knockbackVelocity = direction.normalized * (distance / knockbackDuration);
        knockbackEndTime = Time.time + knockbackDuration;

        StopMoving();
    }

    // 머리 위에 데미지 숫자 띄우기
    private void ShowDamageText(float damage, bool isCritical)
    {
        if (damageTextPrefab == null) return;

        Vector3 spawnPosition = transform.position + Vector3.up * damageTextHeight;
        GameObject textObject = Instantiate(damageTextPrefab, spawnPosition, Quaternion.identity);

        if (textObject.TryGetComponent(out DamageText damageText))
        {
            damageText.Setup(damage, isCritical);
        }
        else
        {
            Debug.LogWarning($"{damageTextPrefab.name} 프리팹에 DamageText 스크립트가 없습니다.");
        }
    }

    // 사망 처리
    private void Die()
    {
        isDead = true;
        Debug.Log($"{gameObject.name} 사망!");

        // 플레이어에게 경험치 지급
        if (player != null)
        {
            player.AddExperience(experienceReward);
        }
        else
        {
            Debug.LogWarning("씬에서 PlayerStats를 찾지 못해 경험치를 지급하지 못했습니다.");
        }

        // LootDropper가 붙어 있는 적만 아이템을 드랍
        if (TryGetComponent(out LootDropper lootDropper))
        {
            lootDropper.DropItem(transform.position);
        }

        // 자신을 생성한 스포너에게 사망을 알려 적 수를 줄이게 함
        if (spawner != null)
        {
            spawner.OnEnemyDied(this);
        }

        Destroy(gameObject);
    }

    // Scene 뷰에서 적을 선택하면 인식 거리(노랑)와 공격 사거리(빨강)를 원으로 표시
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
