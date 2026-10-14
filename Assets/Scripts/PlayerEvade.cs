using UnityEngine;
using UnityEngine.AI;            // NavMeshAgent, NavMesh 사용

// 회피 — 축지(縮地) (Player에 없으면 PlayerController가 스스로 붙임, 모든 직업 공통 기본 능력)
// 회피 행동(Space / 게임패드 LT)을 누르면 마우스 커서 쪽으로 짧게 옮겨 갑니다 (게임패드는 왼쪽 스틱 방향).
// - 충전식: 기본 2회 연속, 쓴 충전은 하나씩 5초마다 돌아옴 (쿨타임 감소 옵션은 적용 안 함)
// - 무적은 없지만 귀신 사이를 통과함 (길찾기 회피를 잠시 끄고 NavMesh 위로만 밀어 옮김)
// - 벽·낭떠러지(NavMesh 끝) 앞에서 멈추고, 거의 못 움직이면 충전을 쓰지 않음
// - 회피하면 부적 폭발 조준 등을 취소하고, 회피 중에는 이동·공격·스킬 입력을 받지 않음
// 숫자(거리·시간·충전 수·충전 시간)는 BalanceConfig에서 바꿉니다. 개발자 메뉴의 "쿨타임 없음"이 켜져 있으면 충전이 줄지 않습니다.
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(NavMeshAgent))]
public class PlayerEvade : MonoBehaviour
{
    [Tooltip("이보다 짧게밖에 못 움직이면(벽 바로 앞 등) 회피하지 않고 충전도 쓰지 않습니다 (m)")]
    [SerializeField] private float minEvadeDistance = 0.5f; // 최소 이동 거리

    [Tooltip("커서가 캐릭터와 이 거리(m)보다 가까우면 커서 대신 캐릭터가 바라보는 쪽으로 회피합니다")]
    [SerializeField] private float cursorDeadRadius = 0.3f; // 커서 방향 무시 반경

    private PlayerController controller;
    private NavMeshAgent agent;
    private PlayerStats stats;
    private PlayerSkills playerSkills;
    private BalanceConfig config;

    private int charges;          // 남은 충전 수
    private float rechargeTimer;  // 지금 차고 있는 충전이 얼마나 찼는지(초)

    private bool isEvading;           // 지금 옮겨 가는 중인지
    private Vector3 evadeVelocity;    // 회피 중 초당 이동량 (방향 × 속도)
    private float evadeTimeLeft;      // 회피가 끝날 때까지 남은 시간(초)
    private ObstacleAvoidanceType savedAvoidance; // 회피 전 길찾기 회피 설정 (끝나면 되돌림)

    // 회피 중인지 (PlayerController·PlayerSkills가 이 동안 입력을 받지 않음)
    public bool IsEvading => isEvading;

    // 최대 충전 수
    public int MaxCharges => config != null ? config.evadeCharges : 2;

    // 쓸 수 있는 충전 수 (쿨타임 없음 치트 중에는 항상 가득)
    public int Charges => DevCheats.NoCooldown ? MaxCharges : charges;

    // 지금 차고 있는 충전의 진행도 (0 = 막 쓰기 시작, 1 = 다 참). 가득 차 있으면 0
    public float RechargeProgress
    {
        get
        {
            if (Charges >= MaxCharges) return 0f;
            float rechargeTime = config != null ? config.evadeRechargeTime : 5f;
            return Mathf.Clamp01(rechargeTimer / rechargeTime);
        }
    }

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<PlayerStats>();
        playerSkills = GetComponent<PlayerSkills>();
    }

    // 능력치 계산기(밸런스 숫자를 가짐)는 PlayerStats의 Awake에서 만들어지므로 Start에서 가져옴
    private void Start()
    {
        config = stats != null && stats.Stats != null ? stats.Stats.Config : null;
        if (config == null)
        {
            Debug.LogWarning("PlayerEvade: Balance Config를 찾지 못해 기본 숫자(4m·0.25초·2회·5초)를 사용합니다.");
        }
        charges = MaxCharges;

        EvadeSlotUI.EnsureExists();
    }

    // 사망·지역 이동 등으로 꺼지면 회피를 바로 끝냄 (길찾기 회피 설정을 되돌리기 위함)
    private void OnDisable()
    {
        if (isEvading) EndEvade();
    }

    private void Update()
    {
        UpdateRecharge();

        if (isEvading)
        {
            UpdateEvade();
            return;
        }

        if (GameInput.Evade.WasPressedThisFrame())
        {
            TryStartEvade();
        }
    }

    // 쓴 충전을 하나씩 차례로 채움
    private void UpdateRecharge()
    {
        if (charges >= MaxCharges)
        {
            rechargeTimer = 0f;
            return;
        }

        float rechargeTime = config != null ? config.evadeRechargeTime : 5f;
        rechargeTimer += Time.deltaTime;
        if (rechargeTimer >= rechargeTime)
        {
            rechargeTimer -= rechargeTime;
            charges++;
        }
    }

    // 회피 시작 (충전이 없거나, 조작이 꺼져 있거나, 거의 못 움직이면 하지 않음)
    private void TryStartEvade()
    {
        // 사망·지역을 불러오는 중에는 PlayerController가 꺼져 있음
        if (!controller.isActiveAndEnabled || !agent.enabled || !agent.isOnNavMesh) return;
        if (Charges <= 0) return;

        // Player의 위치는 NavMeshAgent의 Base Offset만큼 바닥보다 높으므로, 찾는 반경에 몸 높이를 더해 바닥까지 닿게 함
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit startHit, agent.height + 1f, NavMesh.AllAreas)) return;
        Vector3 start = startHit.position;

        float distance = config != null ? config.evadeDistance : 4f;
        Vector3 end = start + GetEvadeDirection() * distance;

        // 가는 길에 벽·낭떠러지(NavMesh 끝)가 있으면 그 앞까지만
        if (NavMesh.Raycast(start, end, out NavMeshHit wallHit, NavMesh.AllAreas))
        {
            end = wallHit.position;
        }

        Vector3 offset = end - start;
        offset.y = 0f;
        if (offset.magnitude < minEvadeDistance) return;

        // 충전 사용 (쿨타임 없음 치트 중에는 줄지 않음)
        if (!DevCheats.NoCooldown)
        {
            if (charges >= MaxCharges) rechargeTimer = 0f; // 가득 차 있었다면 지금부터 충전 시작
            charges--;
        }

        // 조준·공격 대상·이동을 모두 멈추고 회피 방향을 바라봄
        if (playerSkills != null) playerSkills.CancelAllSkills();
        controller.StopAndFace(transform.position + offset);

        // 귀신 사이를 통과하도록 길찾기 회피를 잠시 끔
        savedAvoidance = agent.obstacleAvoidanceType;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

        float duration = config != null ? config.evadeDuration : 0.25f;
        evadeVelocity = offset / duration;
        evadeTimeLeft = duration;
        isEvading = true;
    }

    // 회피 방향: 마우스 = 커서 쪽 / 게임패드 = 왼쪽 스틱 방향 (가리키는 곳이 없으면 바라보는 쪽)
    private Vector3 GetEvadeDirection()
    {
        Vector3 facing = transform.forward;
        facing.y = 0f;
        facing = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.forward;

        if (GameInput.IsFromGamepad(GameInput.Evade))
        {
            Vector2 stick = GameInput.MoveDirection.ReadValue<Vector2>();
            if (!controller.IsStickTilted(stick)) return facing;

            Vector3 stickDirection = controller.GetScreenRelativeDirection(stick);
            return stickDirection.sqrMagnitude > 0.0001f ? stickDirection : facing;
        }

        if (!controller.TryGetGroundPoint(GameInput.PointerPosition, out Vector3 cursorPoint)) return facing;

        Vector3 toCursor = cursorPoint - transform.position;
        toCursor.y = 0f;
        return toCursor.magnitude > cursorDeadRadius ? toCursor.normalized : facing;
    }

    // 회피 중: NavMesh 위로만 밀어 옮김 (NavMesh 끝에서는 저절로 멈춤)
    private void UpdateEvade()
    {
        if (!controller.isActiveAndEnabled || !agent.enabled || !agent.isOnNavMesh)
        {
            EndEvade();
            return;
        }

        float step = Mathf.Min(Time.deltaTime, evadeTimeLeft);
        agent.Move(evadeVelocity * step);
        evadeTimeLeft -= step;

        if (evadeTimeLeft <= 0f) EndEvade();
    }

    private void EndEvade()
    {
        isEvading = false;
        agent.obstacleAvoidanceType = savedAvoidance;
    }
}
