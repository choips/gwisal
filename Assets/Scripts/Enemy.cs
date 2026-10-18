using System;                    // Action 이벤트
using System.Collections.Generic; // List 사용
using UnityEngine;
using UnityEngine.AI;            // NavMeshAgent 사용
using Random = UnityEngine.Random; // System.Random과 이름이 겹치지 않게

// 적의 체력(HP)과 AI를 관리하는 스크립트
// - 처음에는 잠든 채 제자리에 있다가, 플레이어가 인식 거리 안에 들어오거나 맞으면 깨어나 쫓아갑니다.
//   깨어나면 배치한 쪽(무리)에 알려 같은 무리 전체가 함께 깨어납니다.
// - 창귀처럼 부르는 소리가 있으면: 처음 발견 때 잠깐 멈춰 울고, 끝난 뒤에 반경 안의 잠든 창귀를 깨웁니다.
//   우는 동안 죽으면 호출이 없고, 무리당 한 번만 웁니다 (EnemyData.callRadius / callDuration).
// - 도깨비불처럼 원거리면: 거리(preferredRange)를 유지하며 투사체를 쏘고, 너무 가까우면 잠깐 달아납니다.
// - 깨어난 뒤에는 플레이어가 추격 포기 거리(BalanceConfig, 25m)보다 멀어질 때까지 쫓아오고,
//   넘으면 제자리로 돌아가며 체력을 회복하고 다시 잠듭니다. 공격 사거리 안이면 멈춰서 쿨타임마다 공격합니다.
// - 피격 시 체력이 깎이고, 체력이 0 이하가 되면 사망합니다.
// - 고정 배치 무리·무작위 배치기가 MonsterFactory로 꺼낸 직후 귀신 데이터(EnemyData: 망자·창귀 등)·몬스터 레벨과 등급을 적용합니다.
//   (아래 Inspector 숫자는 데이터가 적용되기 전의 기본값입니다)
// - 오브젝트 풀로 재사용합니다: 죽거나 지역을 떠나면 파괴하지 않고 창고로 돌아가고(Release), 다시 꺼낼 때 OnEnable에서 상태를 초기화합니다.
//   (씬에 직접 놓은 적은 풀에서 나온 것이 아니므로 예전처럼 파괴됨)
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
    [Tooltip("잠든 적은 이 거리 안에 플레이어가 들어오면 깨어나 쫓아갑니다 (깨어난 뒤에는 BalanceConfig의 추격 포기 거리까지 쫓아옴)")]
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

    [Header("데이터와 레벨 (배치기가 설정, Play 중 확인용)")]
    [Tooltip("이 적에게 적용된 귀신 데이터 파일")]
    [SerializeField] private EnemyData enemyData;

    [Tooltip("몬스터 레벨 (체력·피해 배율, 플레이어 방어력 계산에 쓰임)")]
    [SerializeField] private int level = 1;

    [Header("등급과 보상 (배치기가 설정, Play 중 확인용)")]
    [Tooltip("일반 / 정예 / 이름 있는 정예 / 하수인")]
    [SerializeField] private MonsterRank rank = MonsterRank.Normal;

    [Tooltip("죽을 때 떨어뜨리는 장비 개수")]
    [SerializeField] private int lootDropCount = 1;

    [Tooltip("좋은 등급(매직·레어·유니크) 아이템이 나올 확률 배율")]
    [SerializeField] private float lootRarityMultiplier = 1f;

    [Header("AI 상태 (Play 중 확인용)")]
    [Tooltip("잠듦 / 부르는 소리 / 쫓아감 / 돌아감")]
    [SerializeField] private AIState aiState = AIState.Asleep;

    // AI 상태 (잠듦 → [부르는 소리] → 쫓아감 → (플레이어가 멀어지면) 돌아감 → 잠듦)
    private enum AIState { Asleep, Howling, Chasing, Returning }

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor"); // 발광 색 속성
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");         // URP 기본 색 속성

    private const float HomeArriveDistance = 0.5f; // 제자리에 이만큼(m) 가까워지면 도착으로 봄

    // 피격 판정: 몸 크기·부유 높이와 무관하게 부적(약 허리 높이)이 빗나가지 않도록 최소 크기 (세계 좌표 m)
    private const float MinHitboxHeight = 1.8f;
    private const float MinHitboxWidth = 0.7f;

    private NavMeshAgent agent;        // 적의 길찾기 이동 컴포넌트
    private PlayerStats player;        // 추적/공격할 플레이어
    private BalanceConfig config;      // 추격 포기 거리·길찾기 간격 (플레이어가 가진 밸런스 숫자 모음)
    private IEnemyOwner owner;         // 이 적을 배치한 쪽 (배치기 또는 고정 배치 무리, 씬에 직접 놓은 적은 null)
    private float nextAttackTime = 0f; // 다음 공격이 가능한 시각
    private float nextRepathTime;      // 다음에 길찾기 목적지를 다시 계산할 시각
    private Vector3 homePosition;      // 처음 선 자리 (추격을 포기하면 돌아옴)
    private bool isDead;               // 이미 사망 처리되었는지 여부 (중복 파괴 방지)

    private float callRadius;          // 부르는 소리 반경 (EnemyData, 0이면 없음)
    private float callDuration;        // 울고 멈추는 시간(초)
    private float howlEndTime;         // 부르는 소리가 끝나는 시각
    private bool hasPersonalCallUsed;  // 배치한 쪽이 없을 때(개발자 소환 등) 개체당 한 번

    private bool isRanged;             // 원거리(도깨비불 등)
    private GameObject projectilePrefab;
    private float projectileSpeed = 8f;
    private float preferredRangeMin;
    private float preferredRangeMax;
    private float fleeRange;
    private float fleeDistance;
    private float fleeCooldown;
    private float nextFleeTime;        // 이 시각 이후부터 다시 달아날 수 있음
    private bool isFleeing;            // 지금 달아나는 중인지
    private Vector3 fleeDestination;   // 도망 목적지 (도착 판정용)
    private float fleeEndTime;         // 이 시각이 지나면 도망 강제 종료
    private float defaultBaseOffset;   // 프리팹 NavMeshAgent Base Offset (호버 없을 때 복구)

    private Material[] flashMaterials;  // 번쩍임에 쓸 이 적 전용 재질들
    private Color[] originalEmissions;  // 재질마다 원래 발광 색
    private bool[] originalEmissionOn;  // 재질마다 원래 발광(Emission)이 켜져 있었는지
    private float flashEndTime;         // 번쩍임이 끝나는 시각
    private bool isFlashing;            // 지금 번쩍이는 중인지

    private Vector3 knockbackVelocity;  // 밀려나는 속도 (방향 × 빠르기)
    private float knockbackEndTime;     // 밀려나기가 끝나는 시각

    private Vector3 baseScale;          // 프리팹 원래 크기 (재사용할 때 배치기가 키운 크기를 되돌림)
    private float damageBonusMult = 1f; // 산군 삼키기 등으로 쌓인 피해 배율
    private bool rewardsSuppressed;     // 소환 창귀 등: 경험치·드랍 없음
    private bool usesBossAi;            // EnemyData.isBoss — 일반 Chase 대신 SangunBossAI
    private bool chasePaused;           // 산군 삼키기 등: 추격 AI만 잠시 끔 (피격·사망은 유지)

    // 머리 위 체력바가 읽어가는 체력 비율 (0 ~ 1)
    public float HealthRatio => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

    public MonsterRank Rank => rank;
    public EnemyData Data => enemyData;
    public int Level => level;
    public NavMeshAgent Agent => agent;
    public PlayerStats PlayerTarget => player;
    public float AttackDamage => attackDamage * damageBonusMult;
    public bool UsesBossAi => usesBossAi;

    // 죽었는지 (풀로 돌아간 적은 null이 되지 않으므로, 타겟이 사라졌는지는 이것으로 확인)
    public bool IsDead => isDead;

    // 처치되는 순간 (풀로 돌아가기 직전) — BossEncounter 등이 구독
    public event Action<Enemy> Died;

    // 깨어나 싸우는 중인지 (부르는 소리 중도 이미 반응한 상태로 봄)
    public bool IsAwake => aiState == AIState.Chasing || aiState == AIState.Howling;

    // 잠든 상태인지 (부르는 소리가 깨울 대상)
    public bool IsAsleep => aiState == AIState.Asleep;

    // 부르는 소리로 멈춰 있는지 (우는 동안 처치하면 호출 없음)
    public bool IsHowling => aiState == AIState.Howling;

    private void Awake()
    {
        // 체력바가 첫 프레임에 빈 칸으로 보이지 않도록 Start보다 먼저 채움
        currentHealth = maxHealth;
        baseScale = transform.localScale;

        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogWarning($"{gameObject.name}에 NavMeshAgent가 없어 움직일 수 없습니다. 프리팹에 Nav Mesh Agent를 추가해 주세요.");
        }
        else
        {
            defaultBaseOffset = agent.baseOffset;
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

    // 풀에서 꺼낼 때마다 지난번 상태를 지움 (처음 만들 때도 Awake 다음에 실행됨)
    // 귀신 데이터·등급·몸 색·이름표는 배치기가 꺼낸 직후 다시 적용함
    private void OnEnable()
    {
        isDead = false;
        currentHealth = maxHealth;
        nextAttackTime = 0f;
        nextRepathTime = 0f;
        knockbackEndTime = 0f;
        howlEndTime = 0f;
        hasPersonalCallUsed = false;
        nextFleeTime = 0f;
        isFleeing = false;
        aiState = AIState.Asleep;
        homePosition = transform.position; // 풀은 위치를 먼저 정한 뒤 켜므로 지금 위치 = 배치된 자리
        transform.localScale = baseScale;
        rank = MonsterRank.Normal;
        lootDropCount = 1;
        lootRarityMultiplier = 1f;
        damageBonusMult = 1f;
        rewardsSuppressed = false;
        usesBossAi = false;
        chasePaused = false;
        Died = null;
        StopFlash();
    }

    // 창고로 돌아갈 때: 배치한 쪽과의 연결을 끊고, 배치기가 붙인 머리 위 이름표를 떼어 냄 (다음에 꺼낼 때 새 이름표를 붙임)
    private void OnDisable()
    {
        owner = null;
        if (TryGetComponent(out ItemNameLabel nameLabel)) Destroy(nameLabel);
    }

    // 보상 없이 조용히 창고로 돌려보냄 (지역을 떠날 때 배치기가 남은 적을 정리)
    public void Despawn()
    {
        if (!gameObject.activeInHierarchy) return;
        ObjectPoolManager.Release(gameObject);
    }

    // 배치기·고정 배치 무리가 적을 꺼낸 직후 호출해서 자신을 알려 줌 (죽으면 OnEnemyDied로 알림)
    public void SetOwner(IEnemyOwner newOwner)
    {
        owner = newOwner;
    }

    // 귀신 데이터 적용: 생성 직후 배치기가 호출해서 데이터 파일의 능력치로 바꿈 (등급 배율은 이 다음에 ApplyRank로)
    // healthMultiplier·damageMultiplier: 몬스터 레벨에 따른 배율 (BalanceConfig의 적 레벨 성장)
    public void ApplyData(EnemyData data, int monsterLevel, float healthMultiplier, float damageMultiplier)
    {
        if (data == null) return;

        enemyData = data;
        level = Mathf.Max(1, monsterLevel);

        maxHealth = data.maxHealth * healthMultiplier;
        currentHealth = maxHealth;
        attackDamage = data.attackDamage * damageMultiplier;
        attackCooldown = data.attackCooldown;
        aggroRange = data.aggroRange;
        attackRange = data.attackRange;
        experienceReward = data.experienceReward;
        callRadius = data.callRadius;
        callDuration = data.callDuration;

        isRanged = data.IsRanged;
        projectilePrefab = data.projectilePrefab;
        projectileSpeed = data.projectileSpeed;
        preferredRangeMin = data.preferredRangeMin;
        preferredRangeMax = data.preferredRangeMax;
        fleeRange = data.fleeRange;
        fleeDistance = data.fleeDistance;
        fleeCooldown = data.fleeCooldown;
        isFleeing = false;
        nextFleeTime = 0f;
        damageBonusMult = 1f;
        rewardsSuppressed = false;
        usesBossAi = data.isBoss;

        if (agent != null)
        {
            agent.speed = data.moveSpeed;
            // 도깨비불처럼 떠 있으면 Base Offset으로 높이 올림 (0이면 프리팹 기본값)
            agent.baseOffset = data.hoverHeight > 0f ? data.hoverHeight : defaultBaseOffset;
        }

        // 보스면 SangunBossAI를 붙임 (풀 재사용 때도 한 번만)
        if (usesBossAi && GetComponent<SangunBossAI>() == null)
        {
            gameObject.AddComponent<SangunBossAI>();
        }
    }

    // 체력을 최대로 채움 (보스전 플레이어 사망 시 등)
    public void HealToFull()
    {
        if (isDead) return;
        currentHealth = maxHealth;
    }

    // 최대 체력의 비율만큼 회복 (산군 삼키기 5% 등)
    public void HealPercent(float percentOfMax)
    {
        if (isDead || percentOfMax <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + maxHealth * percentOfMax);
    }

    // 경험치·드랍을 주지 않음 (산군이 소환한 창귀)
    public void SuppressRewards()
    {
        rewardsSuppressed = true;
        experienceReward = 0f;
        lootDropCount = 0;
    }

    // 피해 배율 중첩 (삼키기 +10%, 최대는 호출 쪽에서 제한)
    public void AddDamageBonus(float bonus)
    {
        damageBonusMult += Mathf.Max(0f, bonus);
    }

    public float DamageBonusMult => damageBonusMult;

    // 보스 AI용: 이동 멈춤 / 바라보기 / 피해
    public void RequestStopMoving() => StopMoving();

    public void RequestLookAt(Vector3 direction) => LookAt(direction);

    // 추격 AI만 잠시 끔 (산군이 창귀를 삼킬 때 — 플레이어에게 도망가지 않게)
    public void SetChasePaused(bool paused)
    {
        chasePaused = paused;
        if (paused) StopMoving();
    }

    public void DealAttackDamage(float multiplier = 1f)
    {
        if (player == null || player.IsDead) return;
        float dmg = attackDamage * damageBonusMult * multiplier;
        GameLog.Combat($"{gameObject.name}의 공격! 플레이어에게 {dmg}의 데미지 (×{multiplier:0.##})");
        player.TakeDamage(dmg, level);
    }

    // 등급 적용: 체력·피해·경험치를 각각 배율로 늘리고, 죽을 때 떨어뜨릴 장비 수와 좋은 등급 확률 배율을 정함
    public void ApplyRank(MonsterRank monsterRank, float healthMult, float damageMult, float expMult,
                          int lootDrops, float lootRarityBonus)
    {
        rank = monsterRank;
        maxHealth *= healthMult;
        currentHealth = maxHealth;
        attackDamage *= damageMult;
        experienceReward = Mathf.Round(experienceReward * expMult);

        lootDropCount = Mathf.Max(lootDrops, 0);
        lootRarityMultiplier = Mathf.Max(lootRarityBonus, 0f);
    }

    // 몸 색 바꾸기 (이 적 전용 재질 복사본만 바뀌므로 다른 적에게 영향 없음)
    public void SetBodyColor(Color color)
    {
        if (flashMaterials == null) return;

        foreach (Material material in flashMaterials)
        {
            // URP Lit 셰이더의 기본 색은 _BaseColor, 옛 셰이더는 _Color
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            else material.color = color;
        }
    }

    // 피격 판정(Box Collider)을 몸 크기·부유 높이에 맞게 다시 맞춤
    // - 발(NavMesh)부터 머리까지 덮고, 세로·가로는 최소 크기를 보장 → 키 작거나 떠 있어도 부적이 통과하지 않음
    // - MonsterFactory가 몸 크기를 정한 직후에 호출해야 함 (lossyScale 기준)
    public void RefreshHitbox()
    {
        if (!TryGetComponent(out BoxCollider box)) return;

        float scaleX = Mathf.Max(Mathf.Abs(transform.lossyScale.x), 0.01f);
        float scaleY = Mathf.Max(Mathf.Abs(transform.lossyScale.y), 0.01f);
        float scaleZ = Mathf.Max(Mathf.Abs(transform.lossyScale.z), 0.01f);

        // 피벗(몸 중심) 기준 세계 좌표(m): 발 = -Base Offset, 머리 = 단위 큐브 윗면
        float baseOffset = agent != null ? agent.baseOffset : defaultBaseOffset;
        float worldBottom = -baseOffset;
        float worldVisualTop = 0.5f * scaleY;
        float worldTop = Mathf.Max(worldVisualTop, worldBottom + MinHitboxHeight);

        float worldHeight = worldTop - worldBottom;
        float worldCenterY = (worldTop + worldBottom) * 0.5f;
        float worldWidth = Mathf.Max(MinHitboxWidth, Mathf.Max(scaleX, scaleZ));

        // Collider size/center는 로컬 → (로컬 × lossyScale)이 세계 크기가 됨
        box.center = new Vector3(0f, worldCenterY / scaleY, 0f);
        box.size = new Vector3(worldWidth / scaleX, worldHeight / scaleY, worldWidth / scaleZ);
    }

    private void Start()
    {
        // 씬에서 PlayerStats가 붙은 플레이어를 찾아 둠 (매 프레임 찾으면 느리므로 한 번만)
        player = FindFirstObjectByType<PlayerStats>();
        if (player == null)
        {
            Debug.LogWarning($"{gameObject.name}: 씬에서 PlayerStats를 찾지 못해 AI가 동작하지 않습니다.");
            return;
        }

        // PlayerStats는 Awake에서 능력치 계산기를 만들므로 여기서는 항상 준비되어 있음
        if (player.Stats != null) config = player.Stats.Config;
    }

    private void Update()
    {
        UpdateFlash();

        if (isDead || agent == null || !agent.isOnNavMesh) return;

        // 부르는 소리 중에는 제자리 (밀려나기도 잠시 무시 — 울기 시간이 끊기지 않게)
        if (aiState == AIState.Howling)
        {
            UpdateHowling();
            return;
        }

        // 밀려나는 중에는 추격·공격을 멈추고 뒤로 미끄러짐 (Move는 NavMesh 밖으로 나가지 않음)
        if (Time.time < knockbackEndTime)
        {
            agent.Move(knockbackVelocity * Time.deltaTime);
            return;
        }

        if (player == null || config == null)
        {
            StopMoving();
            return;
        }

        // 높이(y) 차이는 무시하고 바닥 평면 기준으로 거리 계산
        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        switch (aiState)
        {
            case AIState.Asleep:
                if (!player.IsDead && distance <= aggroRange) Alert();
                break;

            case AIState.Returning:
                if (!player.IsDead && distance <= aggroRange) Alert();
                else if (HasArrivedHome())
                {
                    aiState = AIState.Asleep;
                    currentHealth = maxHealth;
                    hasPersonalCallUsed = false; // 다시 잠들면 단독 소환 창귀는 부르는 소리를 다시 쓸 수 있음
                    StopMoving();
                }
                else
                {
                    // 돌아가는 동안 체력이 차오름
                    currentHealth = Mathf.Min(maxHealth, currentHealth + maxHealth * config.enemyReturnHealPerSecond * Time.deltaTime);
                }
                break;
        }

        // 보스는 SangunBossAI가 Chase를 담당 (잠듦·피격 반응·사망은 여기 유지)
        // chasePaused: 삼키기 대상으로 끌려가는 동안 플레이어 추격을 끔
        if (aiState == AIState.Chasing && !usesBossAi && !chasePaused)
        {
            if (isRanged) UpdateChaseRanged(toPlayer, distance);
            else UpdateChaseMelee(toPlayer, distance);
        }
    }

    // 근접: 공격 사거리 안이면 멈춰서 때리고, 밖이면 쫓아감
    private void UpdateChaseMelee(Vector3 toPlayer, float distance)
    {
        if (player.IsDead || distance > config.enemyLeashRange)
        {
            BeginReturn();
            return;
        }

        if (distance <= attackRange)
        {
            StopMoving();
            LookAt(toPlayer);
            nextRepathTime = 0f; // 플레이어가 사거리를 벗어나면 바로 다시 쫓아가도록

            if (Time.time >= nextAttackTime)
            {
                AttackPlayerMelee();
            }
            return;
        }

        // 길찾기는 비싸므로 매 프레임이 아니라 정해진 간격마다 (귀신마다 간격이 달라 같은 프레임에 몰리지 않음)
        if (Time.time >= nextRepathTime)
        {
            agent.isStopped = false;
            agent.SetDestination(player.transform.position);
            nextRepathTime = Time.time + Random.Range(config.enemyRepathIntervalMin, config.enemyRepathIntervalMax);
        }
    }

    // 원거리(도깨비불): 6~9m 유지하며 발사, 4m 안이면 3m 달아남(이후 fleeCooldown초 동안 다시 안 달아남)
    private void UpdateChaseRanged(Vector3 toPlayer, float distance)
    {
        if (player.IsDead || distance > config.enemyLeashRange)
        {
            isFleeing = false;
            BeginReturn();
            return;
        }

        // 달아나는 중: 목적지 근처·시간 초과·길찾기 실패면 끝 (remainingDistance만 보면 안 끝나는 경우가 있음)
        if (isFleeing)
        {
            LookAt(toPlayer);
            if (HasFinishedFlee())
            {
                isFleeing = false;
                StopMoving();
            }
            return;
        }

        // 너무 가까우면 반대쪽으로 달아남 (쿨타임 중이면 그냥 거리 유지 로직으로)
        if (fleeRange > 0f && distance <= fleeRange && Time.time >= nextFleeTime)
        {
            BeginFlee(toPlayer);
            return;
        }

        float minRange = preferredRangeMin > 0f ? preferredRangeMin : attackRange;
        float maxRange = preferredRangeMax > minRange ? preferredRangeMax : minRange + 3f;

        if (distance > maxRange)
        {
            // 너무 멀면 다가감
            if (Time.time >= nextRepathTime)
            {
                agent.isStopped = false;
                agent.SetDestination(player.transform.position);
                nextRepathTime = Time.time + Random.Range(config.enemyRepathIntervalMin, config.enemyRepathIntervalMax);
            }
        }
        else if (distance < minRange)
        {
            // 방금 도망한 쿨 중이면 더 밀지 말고 서서 쏨 (쫓아오면 끝없이 뒤로만 가는 느낌 방지)
            if (Time.time < nextFleeTime)
            {
                StopMoving();
                LookAt(toPlayer);
                if (Time.time >= nextAttackTime) FireProjectile(toPlayer);
            }
            else if (Time.time >= nextRepathTime)
            {
                // 쿨이 끝난 뒤에만 살짝 뒤로 걸어 거리를 벌림
                Vector3 away = transform.position - toPlayer.normalized * 2f;
                if (NavMesh.SamplePosition(away, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                }
                nextRepathTime = Time.time + Random.Range(config.enemyRepathIntervalMin, config.enemyRepathIntervalMax);
            }
        }
        else
        {
            // 적정 거리: 멈춰서 발사
            StopMoving();
            LookAt(toPlayer);
            nextRepathTime = 0f;

            if (Time.time >= nextAttackTime)
            {
                FireProjectile(toPlayer);
            }
        }
    }

    // 플레이어 반대쪽으로 fleeDistance만큼 달아남
    private void BeginFlee(Vector3 toPlayer)
    {
        if (fleeDistance <= 0f || toPlayer.sqrMagnitude < 0.0001f) return;

        Vector3 away = transform.position - toPlayer.normalized * fleeDistance;
        if (!NavMesh.SamplePosition(away, out NavMeshHit hit, fleeDistance + 1f, NavMesh.AllAreas))
        {
            nextFleeTime = Time.time + fleeCooldown; // 갈 곳이 없어도 쿨은 씀 (계속 시도하지 않게)
            return;
        }

        isFleeing = true;
        fleeDestination = hit.position;
        float travelTime = fleeDistance / Mathf.Max(0.5f, agent.speed);
        fleeEndTime = Time.time + travelTime + 0.4f; // 여유를 두고, 넘으면 강제 종료
        nextFleeTime = Time.time + fleeCooldown;
        agent.isStopped = false;
        agent.SetDestination(fleeDestination);
        nextRepathTime = 0f;
        GameLog.Combat($"{gameObject.name} 도망! ({fleeDistance:0.#}m, 다음 도망까지 {fleeCooldown:0.#}초)");
    }

    // 도망 목적지 근처·시간 초과·경로 불가면 true
    private bool HasFinishedFlee()
    {
        if (Time.time >= fleeEndTime) return true;
        if (agent == null || !agent.isOnNavMesh) return true;

        Vector3 flat = fleeDestination - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude <= 0.8f * 0.8f) return true;

        if (!agent.pathPending)
        {
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid) return true;
            float remaining = agent.remainingDistance;
            if (!float.IsInfinity(remaining) && remaining <= agent.stoppingDistance + 0.5f) return true;
        }

        return false;
    }

    // 불덩이 등 투사체 발사 (풀에서 꺼내 EnemyProjectile.Launch)
    private void FireProjectile(Vector3 toPlayer)
    {
        if (projectilePrefab == null)
        {
            AttackPlayerMelee(); // 프리팹이 빠졌으면 근접으로라도
            return;
        }

        if (toPlayer.sqrMagnitude < 0.0001f) toPlayer = transform.forward;

        Vector3 spawnPosition = transform.position + Vector3.up * 0.2f;
        Quaternion rotation = Quaternion.LookRotation(toPlayer.normalized);
        GameObject shot = ObjectPoolManager.Spawn(projectilePrefab, spawnPosition, rotation);
        if (shot != null && shot.TryGetComponent(out EnemyProjectile projectile))
        {
            projectile.Launch(attackDamage, level, projectileSpeed);
        }

        GameLog.Combat($"{gameObject.name} 투사체 발사! 피해 {attackDamage}");
        nextAttackTime = Time.time + attackCooldown;
    }

    // 플레이어를 발견(또는 피격)했을 때: 창귀면 부르는 소리부터, 아니면 바로 싸움 + 무리 깨우기
    private void Alert()
    {
        if (isDead || aiState == AIState.Chasing || aiState == AIState.Howling) return;

        // 부르는 소리: 무리당(또는 단독 소환이면 개체당) 한 번 — 울기가 끝난 뒤에 주변을 깨움
        if (callRadius > 0f && TryBeginCall())
        {
            BeginHowling();
            return;
        }

        WakeUp();
        if (owner != null) owner.OnEnemyAlerted(this);
    }

    // 부르는 소리를 이 적(또는 무리)에서 쓸 수 있으면 true
    private bool TryBeginCall()
    {
        if (owner != null) return owner.TryBeginCall(this);

        // 개발자 메뉴 소환처럼 배치한 쪽이 없으면 개체당 한 번
        if (hasPersonalCallUsed) return false;
        hasPersonalCallUsed = true;
        return true;
    }

    // 제자리에서 울기 시작 (이 동안 죽으면 CompleteHowling이 호출되지 않아 떼가 오지 않음)
    private void BeginHowling()
    {
        aiState = AIState.Howling;
        howlEndTime = Time.time + Mathf.Max(callDuration, 0f);
        StopMoving();
        GameLog.Combat($"{gameObject.name} 부르는 소리! ({callDuration:0.##}초, 반경 {callRadius:0.##}m)");
    }

    // 울기 시간이 끝나면 쫓아가고, 반경 안의 잠든 호출형 귀신 + 같은 무리를 깨움
    private void UpdateHowling()
    {
        StopMoving();
        if (Time.time < howlEndTime) return;

        CompleteHowling();
    }

    private void CompleteHowling()
    {
        GameLog.Combat($"{gameObject.name} 부르는 소리 끝 — 주변 창귀를 깨웁니다.");
        WakeUp();
        WakeNearbyCallTargets();
        if (owner != null) owner.OnEnemyAlerted(this);
    }

    // 반경 안의 잠든 호출형 귀신(callRadius > 0인 EnemyData)을 깨움 — 한 번만 불리므로 Find 사용 가능
    private void WakeNearbyCallTargets()
    {
        if (callRadius <= 0f) return;

        float radiusSq = callRadius * callRadius;
        Enemy[] all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy other in all)
        {
            if (other == null || other == this || other.IsDead || !other.IsAsleep) continue;
            if (other.Data == null || !other.Data.CanCall) continue;

            Vector3 delta = other.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= radiusSq) other.WakeUp();
        }
    }

    // 깨어나 싸움 시작 (무리가 같은 무리 구성원을 깨울 때·부르는 소리가 끝날 때도 호출 — 다시 무리에 알리지는 않음)
    public void WakeUp()
    {
        if (isDead || aiState == AIState.Chasing) return;

        aiState = AIState.Chasing;
        nextRepathTime = 0f; // 깨어나자마자 길찾기
    }

    // 추격을 포기하고 처음 선 자리로 돌아가기 시작 (돌아가는 동안 Update에서 체력 회복)
    private void BeginReturn()
    {
        isFleeing = false;
        aiState = AIState.Returning;
        agent.isStopped = false;
        agent.SetDestination(homePosition);
    }

    private bool HasArrivedHome()
    {
        if (agent.pathPending) return false;
        return agent.remainingDistance <= agent.stoppingDistance + HomeArriveDistance;
    }

    // 근접 공격: 플레이어에게 바로 데미지
    private void AttackPlayerMelee()
    {
        GameLog.Combat($"{gameObject.name}의 공격! 플레이어에게 {attackDamage}의 데미지");
        player.TakeDamage(attackDamage, level);

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

        DamageMeter.Record(damage); // 개발자 메뉴의 DPS 측정용 기록

        currentHealth -= damage;
        GameLog.Combat($"{gameObject.name} 남은 체력: {Mathf.Max(currentHealth, 0f)} / {maxHealth}");

        // 마지막 일격에도 숫자가 보이도록 사망 처리보다 먼저 생성
        ShowDamageText(damage, isCritical);

        StartFlash();
        StartKnockback(hitFrom, isCritical);

        // 맞으면 거리와 상관없이 깨어나고 같은 무리도 깨움 (한 방에 죽어도 무리는 깨어남)
        Alert();

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
            StopFlash();
        }
    }

    // 번쩍임을 바로 끝내고 원래 발광 상태로 되돌림
    private void StopFlash()
    {
        isFlashing = false;
        if (flashMaterials == null) return;

        for (int i = 0; i < flashMaterials.Length; i++)
        {
            flashMaterials[i].SetColor(EmissionColorId, originalEmissions[i]);
            if (!originalEmissionOn[i]) flashMaterials[i].DisableKeyword("_EMISSION");
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
        nextRepathTime = 0f; // 밀려나기가 끝나면 바로 다시 쫓아가도록

        StopMoving();
    }

    // 머리 위에 데미지 숫자 띄우기
    private void ShowDamageText(float damage, bool isCritical)
    {
        if (damageTextPrefab == null) return;

        Vector3 spawnPosition = transform.position + Vector3.up * damageTextHeight;
        GameObject textObject = ObjectPoolManager.Spawn(damageTextPrefab, spawnPosition, Quaternion.identity);

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
        GameLog.Combat($"{gameObject.name} 사망!");

        if (!rewardsSuppressed)
        {
            if (player != null) player.AddExperience(experienceReward);
            else Debug.LogWarning("씬에서 PlayerStats를 찾지 못해 경험치를 지급하지 못했습니다.");

            if (lootDropCount > 0 && TryGetComponent(out LootDropper lootDropper))
            {
                lootDropper.DropItem(transform.position, lootDropCount, lootRarityMultiplier, level, config);
            }
        }

        if (owner != null) owner.OnEnemyDied(this);

        Died?.Invoke(this);

        // 파괴하지 않고 창고로 돌려보냄 (풀에서 나오지 않은 적은 파괴됨)
        ObjectPoolManager.Release(gameObject);
    }

    // Scene 뷰에서 적을 선택하면 인식 거리(노랑)·공격 사거리(빨강)·부르는 소리 반경(주황)을 원으로 표시
    // Play 중에는 추격 포기 거리(회색)와 제자리(초록)도
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        float gizmoCallRadius = Application.isPlaying ? callRadius
            : (enemyData != null ? enemyData.callRadius : 0f);
        if (gizmoCallRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.55f, 0.15f, 1f);
            Gizmos.DrawWireSphere(transform.position, gizmoCallRadius);
        }

        if (!Application.isPlaying) return;

        if (config != null)
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, config.enemyLeashRange);
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(homePosition, 0.5f);
    }
}
