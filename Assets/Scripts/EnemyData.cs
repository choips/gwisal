using UnityEngine;

// 귀신(적) 한 종류의 데이터 파일 (ScriptableObject — Project 창에서 Create > 귀살 > Enemy Data로 만듦)
// 이름·모양·능력치를 프리팹이 아니라 이 파일에 적어 두면, 프리팹 하나로 여러 종류·변종을 만들 수 있습니다.
// 능력치는 몬스터 레벨 1·일반 등급 기준이고, 레벨·등급(정예 등) 배율은 배치할 때 곱해집니다.
[CreateAssetMenu(fileName = "EnemyData", menuName = "귀살/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("코드·세이브에서 이 귀신을 구분하는 영문 ID (한 번 정하면 바꾸지 않기, 예: mangja)")]
    public string enemyId = "mangja";

    [Tooltip("(참고용) 한국어 이름. 실제 화면 이름은 화면 글자 표 \"UI\"의 enemy.{ID} 키 (예: enemy.mangja)")]
    public string displayName = "망자";

    [Header("모양")]
    [Tooltip("몸 색 (망자 = 회색빛)")]
    public Color bodyColor = new Color(0.55f, 0.55f, 0.58f, 1f);

    [Tooltip("몸 크기 배율 (X 너비, Y 키, Z 두께)")]
    public Vector3 bodyScale = Vector3.one;

    [Header("능력치 (몬스터 레벨 1, 일반 등급 기준)")]
    [Tooltip("최대 체력")]
    [Min(1f)] public float maxHealth = 30f;

    [Tooltip("한 번 공격할 때 플레이어에게 주는 피해 (플레이어의 방어력·피해 감소로 줄어듦)")]
    [Min(0f)] public float attackDamage = 5f;

    [Tooltip("공격과 공격 사이의 대기 시간(초)")]
    [Min(0.1f)] public float attackCooldown = 1.5f;

    [Tooltip("이동 속도 (NavMeshAgent의 Speed)")]
    [Min(0f)] public float moveSpeed = 3.5f;

    [Tooltip("처치 시 플레이어가 얻는 경험치")]
    [Min(0f)] public float experienceReward = 50f;

    [Header("AI")]
    [Tooltip("이 거리 안에 플레이어가 들어오면 쫓아감")]
    [Min(0f)] public float aggroRange = 10f;

    [Tooltip("이 거리 안에 플레이어가 들어오면 멈춰서 공격함")]
    [Min(0f)] public float attackRange = 1.5f;

    [Tooltip("0보다 크면 처음 플레이어를 발견했을 때 이 반경(m) 안의 잠든 호출형 귀신(창귀 등)을 깨움. 0이면 부르는 소리 없음")]
    [Min(0f)] public float callRadius = 0f;

    [Tooltip("부르는 소리로 멈춰 있는 시간(초). 이 동안 죽으면 주변 호출이 일어나지 않음")]
    [Min(0f)] public float callDuration = 0.5f;

    [Header("원거리 (도깨비불 등 — Projectile Prefab이 있으면 원거리)")]
    [Tooltip("쏠 투사체 프리팹 (비우면 근접 공격). 예: Assets/Prefabs/EnemyFireball")]
    public GameObject projectilePrefab;

    [Tooltip("투사체가 1초에 날아가는 거리 (도깨비불 불덩이 = 8)")]
    [Min(0.1f)] public float projectileSpeed = 8f;

    [Tooltip("바닥에서 떠 있는 높이(m). 0이면 프리팹 기본 Base Offset 유지 (도깨비불 = 1.5)")]
    [Min(0f)] public float hoverHeight = 0f;

    [Tooltip("원거리일 때 유지하려는 최소 거리(m) (도깨비불 = 6)")]
    [Min(0f)] public float preferredRangeMin = 0f;

    [Tooltip("원거리일 때 유지하려는 최대 거리(m) (도깨비불 = 9). 이 안에서 발사")]
    [Min(0f)] public float preferredRangeMax = 0f;

    [Tooltip("이 거리 안으로 들어오면 반대쪽으로 달아남 (도깨비불 = 4). 0이면 도망 없음")]
    [Min(0f)] public float fleeRange = 0f;

    [Tooltip("한 번 달아날 때 이동하는 거리(m) (도깨비불 = 3)")]
    [Min(0f)] public float fleeDistance = 0f;

    [Tooltip("달아난 뒤 다시 달아나지 않는 시간(초) (도깨비불 = 5)")]
    [Min(0f)] public float fleeCooldown = 0f;

    [Header("보스 (산군 — isBoss면 SangunBossAI가 패턴을 씀)")]
    [Tooltip("체크하면 일반 Chase 대신 보스 AI 패턴을 씀")]
    public bool isBoss;

    [Tooltip("앞발 예고 시간(초)")]
    [Min(0f)] public float clawTelegraph = 0.5f;

    [Tooltip("앞발 부채꼴 반경(m)")]
    [Min(0f)] public float clawRange = 3f;

    [Tooltip("앞발 부채꼴 각도(도)")]
    [Min(1f)] public float clawArcDegrees = 120f;

    [Tooltip("덮치기 예고 시간(초)")]
    [Min(0f)] public float pounceTelegraph = 1f;

    [Tooltip("덮치기 착지 반경(m)")]
    [Min(0f)] public float pounceRadius = 3f;

    [Tooltip("덮치기 재사용 대기(초)")]
    [Min(0.1f)] public float pounceCooldown = 8f;

    [Tooltip("덮치기 피해 = 기본 피해 × 이 값")]
    [Min(0f)] public float pounceDamageMult = 2f;

    [Tooltip("한 번에 소환하는 창귀 수")]
    [Min(0)] public int summonCount = 4;

    [Tooltip("1페이즈 소환 간격(초)")]
    [Min(0.1f)] public float summonIntervalPhase1 = 20f;

    [Tooltip("2페이즈 소환 간격(초)")]
    [Min(0.1f)] public float summonIntervalPhase2 = 15f;

    [Tooltip("동시에 살아 있을 수 있는 소환 창귀 최대 수")]
    [Min(1)] public int summonMaxAlive = 6;

    [Tooltip("2페이즈로 넘어가는 체력 비율 (0.5 = 50%)")]
    [Range(0.05f, 0.95f)] public float phase2HealthRatio = 0.5f;

    [Tooltip("삼키기 1회당 최대 체력 회복 비율")]
    [Min(0f)] public float swallowHealPercent = 0.05f;

    [Tooltip("삼키기 1회당 피해 증가 (0.1 = +10%)")]
    [Min(0f)] public float swallowDamageBonus = 0.1f;

    [Tooltip("삼키기 최대 중첩")]
    [Min(1)] public int swallowMaxStacks = 3;

    [Tooltip("삼키기 판정 거리(m)")]
    [Min(0.5f)] public float swallowRange = 2.5f;

    [Tooltip("소환할 귀신 데이터 (창귀). 비우면 EnemyDatabase에서 changgwi를 찾음")]
    public EnemyData summonEnemyData;

    // 부르는 소리로 무리를 부를 수 있는 귀신인지 (창귀 등)
    public bool CanCall => callRadius > 0f;

    // 원거리 귀신인지 (투사체 프리팹이 있으면 원거리)
    public bool IsRanged => projectilePrefab != null;

    // 화면에 보일 이름 (화면 글자 표 "UI"의 "enemy.ID" 키, 표를 찾으므로 생성할 때처럼 필요할 때만 부름)
    public string LocalizedName => Loc.Get("enemy." + enemyId);
}
