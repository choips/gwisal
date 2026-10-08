using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 디아블로식 쿼터뷰 카메라
// 카메라 각도는 그대로 둔 채, 플레이어와의 거리(오프셋)를 유지하며 부드럽게 따라갑니다.
// 마우스 휠로 확대/축소할 수 있습니다.
public class CameraFollow : MonoBehaviour
{
    [Header("따라갈 대상")]
    [Tooltip("비워두면 씬에서 PlayerController를 가진 오브젝트를 자동으로 찾습니다")]
    [SerializeField] private Transform target;

    [Header("위치")]
    [Tooltip("켜면 게임 시작 시 '지금 카메라와 플레이어 사이의 거리'를 그대로 오프셋으로 사용합니다")]
    [SerializeField] private bool useCurrentOffset = true;
    [Tooltip("플레이어 기준 카메라 위치 (useCurrentOffset이 꺼져 있을 때 사용)")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 15f, -10f);
    [Tooltip("따라가는 부드러움 (작을수록 빠르게 붙음, 0이면 즉시)")]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("마우스 휠 줌")]
    [SerializeField] private bool enableZoom = true;
    [Tooltip("휠 한 칸당 확대/축소 비율")]
    [SerializeField] private float zoomStep = 0.1f;
    [Tooltip("가장 가까울 때 배율 (오프셋 × 이 값)")]
    [SerializeField] private float minZoom = 0.5f;
    [Tooltip("가장 멀 때 배율 (오프셋 × 이 값)")]
    [SerializeField] private float maxZoom = 1.5f;
    [Tooltip("줌이 바뀌는 부드러움")]
    [SerializeField] private float zoomSmoothSpeed = 10f;

    private Vector3 velocity;      // SmoothDamp가 내부적으로 쓰는 현재 속도
    private float targetZoom = 1f; // 휠로 정한 목표 배율
    private float currentZoom = 1f; // 실제로 적용 중인 배율 (목표를 향해 서서히 변함)

    private void Start()
    {
        if (target == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning("CameraFollow: 따라갈 대상(Player)을 찾지 못했습니다.");
                enabled = false;
                return;
            }
        }

        if (useCurrentOffset)
        {
            offset = transform.position - target.position;
        }

        // 시작하자마자 제자리로 맞춤 (처음에 카메라가 미끄러져 오는 것 방지)
        transform.position = target.position + offset;
    }

    // 플레이어가 Update에서 이동을 마친 뒤 따라가야 화면 떨림이 없음
    private void LateUpdate()
    {
        if (target == null) return;

        HandleZoom();

        Vector3 desiredPosition = target.position + offset * currentZoom;

        if (smoothTime > 0f)
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
        }
        else
        {
            transform.position = desiredPosition;
        }
    }

    private void HandleZoom()
    {
        if (enableZoom && Mouse.current != null && !IsPointerOverUI())
        {
            float scroll = Mouse.current.scroll.ReadValue().y;

            // 휠 값의 크기는 마우스·설정마다 달라서 방향만 사용
            if (scroll > 0f)
            {
                targetZoom -= zoomStep; // 휠 위로 → 가까이
            }
            else if (scroll < 0f)
            {
                targetZoom += zoomStep; // 휠 아래로 → 멀리
            }

            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        currentZoom = Mathf.Lerp(currentZoom, targetZoom, zoomSmoothSpeed * Time.deltaTime);
    }

    // 인벤토리 스크롤 중에는 줌이 되지 않도록 UI 위인지 확인
    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
