using UnityEngine;
using UnityEngine.AI;            // NavMeshAgent, NavMesh 사용
using UnityEngine.EventSystems;  // UI 위에 마우스가 있는지 확인 (EventSystem) 사용
using UnityEngine.InputSystem;   // 새 Input System (InputAction) 사용

// 디아블로 스타일 클릭 이동 + 적/아이템 타겟팅 컨트롤러
// 입력은 키를 직접 읽지 않고 GameInput의 행동으로 읽습니다 (키 연결: Assets/InputSystem_Actions의 Player 묶음).
// 마우스 좌클릭 위치로 Raycast를 쏴서
//  - 바닥(Ground 레이어)을 클릭하면 그 위치로 이동하고
//  - 적(Enemy 태그)을 클릭하면 타겟으로 지정해 다가간 뒤 사거리 안에서 공격하고
//  - 아이템(Item 태그)을 클릭하면 다가가서 줍습니다.
// 게임패드: 왼쪽 스틱으로 화면 기준 방향 이동, A 버튼으로 가장 가까운 적 공격(적이 없으면 가장 가까운 아이템 줍기)
// 스킬은 PlayerSkills(스킬 칸 1~6)와 각 스킬 스크립트가 처리하고,
// 이 스크립트는 스킬이 쓰는 바닥 위치 찾기(TryGetGroundPoint)와 멈추고 바라보기(StopAndFace)를 제공합니다.
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

    [Tooltip("바닥을 클릭한 위치에 띄울 표시 이펙트 프리팹 (비워두면 생략)")]
    [SerializeField] private GameObject clickMarkerPrefab;

    [Tooltip("표시 이펙트를 확실히 지우기 위한 최대 유지 시간(초)")]
    [SerializeField] private float clickMarkerMaxLifetime = 1f;

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

    [Header("게임패드 설정")]
    [Tooltip("왼쪽 스틱을 이만큼 이상 기울여야 이동합니다 (0 ~ 1)")]
    [SerializeField, Range(0.05f, 0.9f)] private float stickDeadZone = 0.2f; // 스틱 무시 범위

    [Tooltip("스틱 방향으로 이 거리(m)만큼 앞을 목적지로 잡아 이동합니다 (너무 작으면 끊기고, 너무 크면 벽 앞에서 돌아감)")]
    [SerializeField] private float stickMoveLookAhead = 1.5f; // 스틱 이동 목적지 거리

    [Tooltip("게임패드 A 버튼으로 공격·줍기할 대상을 찾는 반경(m)")]
    [SerializeField] private float gamepadInteractRadius = 6f; // A 버튼 대상 찾기 반경

    private static readonly Collider[] nearbyColliders = new Collider[32]; // 주변 대상 찾기용 (매번 새로 만들지 않도록 미리 준비)

    private NavMeshAgent agent;   // 캐릭터 길찾기 이동 컴포넌트
    private PlayerStats stats;    // 플레이어 능력치 (공격력 등)
    private PlayerSkills playerSkills; // 스킬 칸 관리자 (사망 시 조준 취소용)
    private Transform target;     // 현재 타겟팅 중인 대상 (적 또는 아이템, 없으면 null)
    private Enemy targetEnemy;    // 타겟이 적일 때: 체력 관리 스크립트
    private Item targetItem;      // 타겟이 아이템일 때: 아이템 스크립트
    private bool isAttacking;     // 사거리 안에 들어와 멈춘(공격 중인) 상태인지 여부
    private float nextAttackTime = 0f; // 다음 공격이 가능한 시각 (게임 시작 후 경과 초)
    private GameObject currentClickMarker; // 지금 바닥에 떠 있는 클릭 표시
    private bool isStickMoving;            // 지난 프레임에 왼쪽 스틱으로 이동 중이었는지

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<PlayerStats>();
        playerSkills = GetComponent<PlayerSkills>();
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

    // 사망 등으로 조작이 꺼질 때 타겟과 조준을 잊어서, 부활 후 이전 행동을 이어가지 않도록 함
    private void OnDisable()
    {
        target = null;
        targetEnemy = null;
        targetItem = null;
        isAttacking = false;
        isStickMoving = false;
        if (playerSkills != null) playerSkills.CancelAllSkills();
    }

    private void Update()
    {
        HandleStickMove();
        HandleGamepadSelect();
        HandleMouseInput();
        UpdateTargeting();
    }

    // 게임패드 왼쪽 스틱: 화면 기준 방향(위 = 화면 안쪽)으로 이동, 스틱을 놓으면 멈춤
    // 스틱으로 움직이면 공격·줍기 대상은 해제됨 (디아블로 콘솔판 방식)
    private void HandleStickMove()
    {
        Vector2 stick = GameInput.MoveDirection.ReadValue<Vector2>();

        if (stick.sqrMagnitude < stickDeadZone * stickDeadZone)
        {
            // 스틱을 막 놓았을 때 한 번만 멈춤 (마우스 이동 중에는 건드리지 않음)
            if (isStickMoving)
            {
                isStickMoving = false;
                if (target == null) agent.ResetPath();
            }
            return;
        }

        isStickMoving = true;
        if (target != null) ClearTarget();

        Vector3 direction = GetScreenRelativeDirection(stick);
        if (direction.sqrMagnitude < 0.0001f) return;

        // Player의 위치는 NavMeshAgent의 Base Offset만큼 바닥보다 높으므로, 찾는 반경에 몸 높이를 더해 바닥까지 닿게 함
        Vector3 destination = transform.position + direction * stickMoveLookAhead;
        float searchRadius = stickMoveLookAhead + agent.height;
        if (NavMesh.SamplePosition(destination, out NavMeshHit navHit, searchRadius, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(navHit.position);
        }
    }

    // 스틱 입력(화면의 위·오른쪽)을 바닥 위의 방향으로 바꿈 (카메라가 바라보는 쪽 = 위)
    private Vector3 GetScreenRelativeDirection(Vector2 stick)
    {
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (mainCamera != null)
        {
            Transform cameraTransform = mainCamera.transform;
            forward = cameraTransform.forward;
            right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
        }

        Vector3 direction = right * stick.x + forward * stick.y;
        return direction.normalized;
    }

    // 게임패드 A 버튼(선택 행동): 가까운 적을 공격, 주변에 적이 없으면 가장 가까운 아이템을 주우러 감
    private void HandleGamepadSelect()
    {
        InputAction select = GameInput.Select;
        if (!select.WasPressedThisFrame() || !GameInput.IsFromGamepad(select)) return;

        if (TryFindNearest(EnemyTag, gamepadInteractRadius, out Transform nearestEnemy))
        {
            SetEnemyTarget(nearestEnemy);
        }
        else if (TryFindNearest(ItemTag, gamepadInteractRadius, out Transform nearestItem))
        {
            SetItemTarget(nearestItem, nearestItem.position);
        }
    }

    // 플레이어 주변 반경 안에서 해당 태그를 가진 가장 가까운 대상 찾기 (없으면 false)
    // 스킬 자동 조준(게임패드)에서도 사용
    public bool TryFindNearest(string targetTag, float radius, out Transform nearest)
    {
        nearest = null;
        Vector3 center = transform.position;
        int count = Physics.OverlapSphereNonAlloc(center, radius, nearbyColliders,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider candidate = nearbyColliders[i];
            if (!candidate.CompareTag(targetTag)) continue;

            Vector3 offset = candidate.transform.position - center;
            offset.y = 0f;
            float distance = offset.sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = candidate.transform;
            }
        }

        return nearest != null;
    }

    // 스킬을 쓸 때: 타겟과 이동을 멈추고 지정한 바닥 지점을 바라봄 (높이는 무시하고 Y축 회전만)
    public void StopAndFace(Vector3 point)
    {
        ClearTarget();
        agent.ResetPath();

        Vector3 lookPoint = new Vector3(point.x, transform.position.y, point.z);
        transform.LookAt(lookPoint);
    }

    // 화면상의 마우스 좌표가 가리키는 바닥(Ground 레이어) 좌표 구하기 (바닥이 아니면 false)
    // 적이 커서 아래에 있어도 Ground 레이어만 검사하므로 광선이 적을 통과해 바닥에 맞음
    // 스킬 스크립트도 조준 위치를 구할 때 사용
    public bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 groundPoint)
    {
        groundPoint = Vector3.zero;
        if (mainCamera == null) return false;

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        groundPoint = hit.point;
        return true;
    }

    // 커서 입력 처리 (선택 행동 = 좌클릭: 이동/공격/줍기)
    // 키 연결은 Assets/InputSystem_Actions의 Player 묶음에서 바꿉니다.
    private void HandleMouseInput()
    {
        // 인벤토리 같은 UI 위를 클릭한 경우 뒤쪽 바닥으로 이동하지 않도록 무시
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 pointerPosition = GameInput.PointerPosition;

        // 게임패드 A 버튼은 HandleGamepadSelect가 처리 (커서 위치를 쓰지 않음)
        InputAction select = GameInput.Select;
        if (GameInput.IsFromGamepad(select)) return;

        if (select.WasPressedThisFrame())
        {
            // 새로 클릭한 순간: 적/아이템/바닥 중 무엇인지 판단해서 처리
            HandleClick(pointerPosition, true);
        }
        else if (moveWhileHolding && select.IsPressed() && target == null)
        {
            // 누르고 있는 중: 적이나 아이템을 타겟팅하고 있지 않을 때만 커서를 따라 이동
            // (대상을 클릭한 채 버튼을 계속 누르고 있어도 타겟이 풀리지 않도록 하기 위함)
            HandleClick(pointerPosition, false);
        }
    }

    // 화면상의 마우스 좌표로 Ray를 쏴서 맞은 대상에 따라 타겟팅 또는 이동
    // isNewClick: 이번 프레임에 새로 누른 클릭인지 (누르고 있는 중이면 false → 바닥 표시를 만들지 않음)
    private void HandleClick(Vector2 screenPosition, bool isNewClick)
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

            if (isNewClick)
            {
                SpawnClickMarker(hit.point);
            }
        }
    }

    // 클릭한 바닥 위치에 표시 이펙트 생성 (이전 표시는 바로 지워서 하나만 보이게 함)
    private void SpawnClickMarker(Vector3 position)
    {
        if (clickMarkerPrefab == null) return;

        if (currentClickMarker != null)
        {
            Destroy(currentClickMarker);
        }

        // 바닥과 겹쳐 깜빡이지 않도록 아주 살짝 띄움
        currentClickMarker = Instantiate(clickMarkerPrefab, position + Vector3.up * 0.02f, Quaternion.identity);
        Destroy(currentClickMarker, clickMarkerMaxLifetime);
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

        float damage = stats.RollDamage(stats.attackPower, out bool isCritical);
        Debug.Log(isCritical ? $"치명타! {damage}의 데미지" : $"적중! {damage}의 데미지");
        targetEnemy.TakeDamage(damage, isCritical);

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
