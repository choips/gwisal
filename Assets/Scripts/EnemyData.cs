using UnityEngine;

// 귀신(적) 한 종류의 데이터 파일 (ScriptableObject — Project 창에서 Create > 귀살 > Enemy Data로 만듦)
// 이름·모양·능력치를 프리팹이 아니라 이 파일에 적어 두면, 프리팹 하나로 여러 종류·변종을 만들 수 있습니다.
// 능력치는 몬스터 레벨 1·일반 등급 기준이고, 레벨·등급(챔피언 등) 배율은 배치할 때 곱해집니다.
[CreateAssetMenu(fileName = "EnemyData", menuName = "귀살/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("코드·세이브에서 이 귀신을 구분하는 영문 ID (한 번 정하면 바꾸지 않기, 예: japgwi)")]
    public string enemyId = "japgwi";

    [Tooltip("머리 위 이름표와 Console에 보이는 이름")]
    public string displayName = "잡귀";

    [Header("모양")]
    [Tooltip("몸 색")]
    public Color bodyColor = Color.gray;

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
}
