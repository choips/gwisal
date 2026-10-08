using UnityEngine;
using UnityEngine.AI;            // NavMeshAgent, NavMesh 사용
using UnityEngine.EventSystems;  // UI 위에 마우스가 있는지 확인 (EventSystem) 사용
using UnityEngine.InputSystem;   // 새 Input System (Mouse.current) 사용

// 디아블로 스타일 클릭 이동 + 적/아이템 타겟팅 컨트롤러
// 마우스 좌클릭 위치로 Raycast를 쏴서
//  - 바닥(Ground 레이어)을 클릭하면 그 위치로 이동하고
//  - 적(Enemy 태그)을 클릭하면 타겟으로 지정해 다가간 뒤 사거리 안에서 공격하고
//  - 아이템(Item 태그)을 클릭하면 다가가서 줍습니다.
// 마우스 우클릭 시 커서 방향으로 부적(투사체)을 발사합니다.
// Q 키를 누르면 기력(MP)을 소모해 커서 위치에 광역 폭발을 일으킵니다.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerController : MonoBehaviour
{
    private const string EnemyTag = "Enemy"; // 적 오브젝트에 붙일 태그 이름
    private const string ItemTag = "Item";   // 아이템 오브젝트에 붙일 태그 이름

    [Header("참조")]
    [Tooltip("Raycast를 쏠 카메라 (비워두면 Main Camera를 자동 사용)")]
    [SerializeField] private Camera mainCamera;

    [Header("Raycast 설정")]
    [Tooltip("바닥으로 인식할 레이어 (Ground 레이어를 지정하세요)")]
    [SerializeField] private LayerMask groundLayer;

    [Tooltip("Raycast 최대 거리")]
    [SerializeField] private float maxRayDistance = 100f;

    [Header("이동 설정")]
    [Tooltip("체크하면 좌클릭을 누르고 있는 동안 계속 마우스 위치로 이동합니다 (디아블로 방식)")]
    [SerializeField] private bool moveWhileHolding = true;

    [Tooltip("클릭 지점 주변에서 NavMesh 위의 유효한 지점을 찾는 반경")]
    [SerializeField] private float navMeshSampleRadius = 1f;

    [Header("공격 설정")]
    [Tooltip("이 거리 안에 적이 들어오면 멈추고 공격합니다")]
    public float attackRange = 2.0f; // 공격 사거리

    [Tooltip("1초당 공격 횟수 (1이면 1초에 한 번, 2면 0.5초에 한 번)")]
    public float attackRate = 1f; // 공격 속도

    [Tooltip("공격할 때 적을 향해 회전하는 속도")]
    [SerializeField] private float rotationSpeed = 10f;

    [Header("아이템 획득 설정")]
    [Tooltip("이 거리 안에 아이템이 들어오면 멈추고 줍습니다")]
    public float pickupRange = 1.5f; // 아이템 획득 사거리

    [Header("부적 스킬 설정 (마우스 우클릭)")]
    [Tooltip("부적 스킬을 다시 쓸 수 있을 때까지의 대기 시간(초)")]
    public float skillCooldown = 1f; // 스킬 쿨타임

    [Tooltip("발사할 부적 프리팹 (AmuletProjectile 스크립트가 붙어 있어야 합니다)")]
    public GameObject amuletPrefab; // 부적 프리팹

    [Tooltip("부적이 생성될 위치 (플레이어 자식으로 만든 빈 오브젝트)")]
    public Transform firePoint; // 발사 위치

    [Header("광역 스킬 설정 (Q 키)")]
    [Tooltip("커서 위치에 생성할 광역 폭발 프리팹 (AoESkill 스크립트가 붙어 있어야 합니다)")]
    public GameObject aoePrefab; // 광역 스킬 프리팹

    [Tooltip("광역 스킬 1회 사용 시 소모되는 기력")]
    public float aoeCost = 20f; // 스킬 소모 MP

    [Tooltip("광역 스킬을 다시 쓸 수 있을 때까지의 대기 시간(초, 0이면 쿨타임 없음)")]
    public float aoeCooldown = 1f; // 광역 스킬 쿨타임

    private NavMeshAgent agent;   // 캐릭터 길찾기 이동 컴포넌트
    private PlayerStats stats;    // 플레이어 능력치 (공격력 등)
    private Transform target;     // 현재 타겟팅 중인 대상 (적 또는 아이템, 없으면 null)
    private Enemy targetEnemy;    // 타겟이 적일 때: 체력 관리 스크립트
    private Item targetItem;      // 타겟이 아이템일 때: 아이템 스크립트
    private bool isAttacking;     // 사거리 안에 들어와 멈춘(공격 중인) 상태인지 여부
    private float nextAttackTime = 0f; // 다음 공격이 가능한 시각 (게임 시작 후 경과 초)
    private float nextSkillTime = 0f;  // 다음 부적 스킬 사용이 가능한 시각
    private float nextAoeTime = 0f;    // 다음 광역 스킬 사용이 가능한 시각

    // 스킬바 UI가 읽어가는 남은 쿨타임(초, 0이면 사용 가능)
    public float AmuletCooldownRemaining => Mathf.Max(0f, nextSkillTime - Time.time);
    public float AoeCooldownRemaining => Mathf.Max(0f, nextAoeTime - Time.time);

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<PlayerStats>();
        if (stats == null)
        {
            Debug.LogWarning("Player에 PlayerStats 컴포넌트가 없어 공격할 수 없습니다. Add Component로 추가해 주세요.");
        }

        // 카메라가 지정되지 않았다면 "MainCamera" 태그가 붙은 카메라를 사용
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    // 사망 등으로 조작이 꺼질 때 타겟을 잊어서, 부활 후 이전 행동을 이어가지 않도록 함
    private void OnDisable()
    {
        target = null;
        targetEnemy = null;
        targetItem = null;
        isAttacking = false;
    }

    private void Update()
    {
        HandleKeyboardInput();
        HandleMouseInput();
        UpdateTargeting();
    }

    // 키보드 입력 처리 (Q: 광역 스킬, 쿨타임이 지났을 때만)
    private void HandleKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (keyboard.qKey.wasPressedThisFrame && Time.time >= nextAoeTime)
        {
            CastAoE(mouse.position.ReadValue());
        }
    }

    // 기력을 소모해 마우스가 가리키는 바닥 위치에 광역 폭발 생성
    private void CastAoE(Vector2 screenPosition)
    {
        if (mainCamera == null) return;

        if (aoePrefab == null)
        {
            Debug.LogWarning("PlayerController에 Aoe Prefab이 연결되지 않아 광역 스킬을 쓸 수 없습니다.");
            return;
        }

        // a. 마우스가 가리키는 바닥 위치 찾기 (바닥이 아닌 곳이면 기력을 쓰지 않고 취소)
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        // b. 기력이 충분한지 확인하고 소모
        if (stats == null || !stats.UseMP(aoeCost)) return;

        // c. 바닥 위치에 광역 폭발 생성
        Instantiate(aoePrefab, hit.point, Quaternion.identity);

        nextAoeTime = Time.time + aoeCooldown;
    }

    // 마우스 입력 처리 (좌클릭: 이동/공격/줍기, 우클릭: 부적 스킬)
    private void HandleMouseInput()
    {
        // 마우스가 연결되어 있지 않으면 아무것도 하지 않음
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // 인벤토리 같은 UI 위를 클릭한 경우 뒤쪽 바닥으로 이동하지 않도록 무시
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 mousePosition = mouse.position.ReadValue();

        // 우클릭: 쿨타임이 지났다면 커서 방향으로 부적 발사
        if (mouse.rightButton.wasPressedThisFrame && Time.time >= nextSkillTime)
        {
            CastAmulet(mousePosition);
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            // 새로 클릭한 순간: 적/아이템/바닥 중 무엇인지 판단해서 처리
            HandleClick(mousePosition);
        }
        else if (moveWhileHolding && mouse.leftButton.isPressed && target == null)
        {
            // 누르고 있는 중: 적이나 아이템을 타겟팅하고 있지 않을 때만 마우스를 따라 이동
            // (대상을 클릭한 채 버튼을 계속 누르고 있어도 타겟이 풀리지 않도록 하기 위함)
            HandleClick(mousePosition);
        }
    }

    // 화면상의 마우스 좌표로 Ray를 쏴서 맞은 대상에 따라 타겟팅 또는 이동
    private void HandleClick(Vector2 screenPosition)
    {
        if (mainCamera == null) return;

        // 카메라에서 마우스 위치 방향으로 나가는 광선 생성
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        // 적도 맞아야 하므로 모든 레이어를 대상으로 검사 (Trigger 콜라이더는 무시)
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        if (hit.collider.CompareTag(EnemyTag))
        {
            // 적을 클릭함 → 적 타겟팅
            SetEnemyTarget(hit.collider.transform);
        }
        else if (hit.collider.CompareTag(ItemTag))
        {
            // 아이템을 클릭함 → 아이템 타겟팅
            SetItemTarget(hit.collider.transform, hit.point);
        }
        else if (IsInLayerMask(hit.collider.gameObject.layer, groundLayer))
        {
            // 바닥을 클릭함 → 타겟을 해제하고 그 위치로 이동
            ClearTarget();
            MoveTo(hit.point);
        }
    }

    // 마우스가 가리키는 바닥 방향으로 몸을 돌리고 부적을 발사
    private void CastAmulet(Vector2 screenPosition)
    {
        if (mainCamera == null) return;

        if (amuletPrefab == null)
        {
            Debug.LogWarning("PlayerController에 Amulet Prefab이 연결되지 않아 부적을 발사할 수 없습니다.");
            return;
        }

        // a. 마우스가 가리키는 바닥 좌표 구하기 (적이 커서 아래에 있어도 광선이 통과해 바닥에 맞음)
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        // 스킬을 쓰는 동안에는 이동/공격을 멈춤 (이동 중이면 NavMeshAgent가 몸을 다시 진행 방향으로 돌려 버림)
        ClearTarget();
        agent.ResetPath();

        // b. 마우스 좌표를 바라보도록 회전 (높이를 플레이어와 같게 맞춰 Y축 회전만 적용)
        Vector3 lookPoint = new Vector3(hit.point.x, transform.position.y, hit.point.z);
        transform.LookAt(lookPoint);

        // c. 발사 위치에 부적 생성 (발사 위치에서 마우스 지점을 향하도록 수평 방향으로 회전)
        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
        Vector3 direction = hit.point - spawnPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

        Instantiate(amuletPrefab, spawnPosition, Quaternion.LookRotation(direction));

        nextSkillTime = Time.time + skillCooldown;
    }

    // 적을 타겟으로 지정
    private void SetEnemyTarget(Transform newTarget)
    {
        // 같은 적을 다시 클릭한 경우에는 공격 상태를 유지
        if (target == newTarget) return;

        ClearTarget();
        target = newTarget;

        // 콜라이더가 자식 오브젝트에 있어도 찾을 수 있도록 부모 방향까지 검색
        targetEnemy = newTarget.GetComponentInParent<Enemy>();
        if (targetEnemy == null)
        {
            Debug.LogWarning($"{newTarget.name}에 Enemy 스크립트가 없어 공격할 수 없습니다.");
        }
    }

    // 아이템을 타겟으로 지정
    private void SetItemTarget(Transform newTarget, Vector3 clickPoint)
    {
        ClearTarget();

        Item item = newTarget.GetComponentInParent<Item>();
        if (item == null)
        {
            // Item 스크립트가 없으면 주울 수 없으므로 그 위치로 이동만 함
            Debug.LogWarning($"{newTarget.name}에 Item 스크립트가 없어 주울 수 없습니다.");
            MoveTo(clickPoint);
            return;
        }

        target = newTarget;
        targetItem = item;
    }

    // 타겟 해제 후 다시 이동 가능한 상태로 되돌림
    private void ClearTarget()
    {
        target = null;
        targetEnemy = null;
        targetItem = null;
        isAttacking = false;
        agent.isStopped = false;
    }

    // 지정한 위치(NavMesh 위의 가장 가까운 지점)로 이동
    private void MoveTo(Vector3 position)
    {
        // 클릭 지점이 NavMesh 밖일 수 있으므로, 가장 가까운 NavMesh 위 지점을 찾음
        if (NavMesh.SamplePosition(position, out NavMeshHit navHit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(navHit.position);
        }
    }

    // 매 프레임 타겟 종류에 따라 추적/공격 또는 추적/줍기 처리
    private void UpdateTargeting()
    {
        if (target == null)
        {
            // 적이 파괴(Destroy)되는 등 타겟이 사라졌다면 타겟 정보도 모두 정리
            // (Unity의 != null은 파괴된 오브젝트를 null로 취급하므로, 참조가 남아 있는지는 is not null로 검사)
            if (targetEnemy is not null || targetItem is not null || isAttacking) ClearTarget();
            return;
        }

        // 높이(y) 차이는 무시하고 바닥 평면 기준으로 거리 계산
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (targetItem != null)
        {
            UpdateItemTarget(distance);
        }
        else
        {
            UpdateEnemyTarget(toTarget, distance);
        }
    }

    // 아이템 타겟: 사거리 밖이면 다가가고, 안이면 멈춰서 줍기
    private void UpdateItemTarget(float distance)
    {
        if (distance <= pickupRange)
        {
            agent.isStopped = true;
            agent.ResetPath();

            targetItem.PickUp();
            ClearTarget();
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
        }
    }

    // 적 타겟: 사거리 밖이면 다가가고, 안이면 멈춰서 바라보며 공격
    private void UpdateEnemyTarget(Vector3 toTarget, float distance)
    {
        if (distance <= attackRange)
        {
            // 사거리 안: 처음 들어온 순간에만 멈추고 공격 메시지 출력
            if (!isAttacking)
            {
                isAttacking = true;
                agent.isStopped = true;
                agent.ResetPath();
                Debug.Log("적을 공격합니다!");
            }

            // 멈춰 있는 동안 계속 적을 바라보도록 회전
            LookAtTarget(toTarget);

            // 쿨타임이 끝났을 때만 공격
            if (Time.time >= nextAttackTime)
            {
                Attack();
            }
        }
        else
        {
            // 사거리 밖: 적에게 다가감 (적이 움직여도 따라가도록 매 프레임 목적지 갱신)
            isAttacking = false;
            agent.isStopped = false;
            agent.SetDestination(target.position);
        }
    }

    // 타겟에게 데미지를 주고 다음 공격 시각을 설정
    private void Attack()
    {
        if (targetEnemy == null || stats == null) return;

        float damage = stats.attackPower;
        Debug.Log($"적중! {damage}의 데미지");
        targetEnemy.TakeDamage(damage);

        // attackRate가 0 이하이면 0으로 나누게 되므로 최소값을 보장
        nextAttackTime = Time.time + (1f / Mathf.Max(attackRate, 0.01f));
    }

    // 적 방향으로 부드럽게 회전
    private void LookAtTarget(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    // 해당 레이어가 LayerMask에 포함되어 있는지 확인
    private static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    // Scene 뷰에서 플레이어를 선택하면 공격 사거리(빨강)와 획득 사거리(노랑)를 원으로 표시
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
    }
}
