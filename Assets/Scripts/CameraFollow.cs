using UnityEngine;

// 디아블로식 쿼터뷰 카메라
// 거리·내려다보는 각도·시야각이 고정된 채 플레이어를 화면 가운데에 두고 부드럽게 따라갑니다.
// 디아블로 2처럼 모든 사람이 같은 시야로 플레이하도록 줌(확대/축소)은 없습니다.
// 시야 값은 사용자가 직접 보며 고른 값입니다 (2026-10-15: 30m · 45° · 30° → 위 약 15m / 아래 약 9m / 좌우 약 ±14m).
// 다시 조정할 때는 플레이 중 Main Camera의 Inspector에서 바꾸면 바로 보입니다 (플레이를 멈추면 원래 값으로 돌아감).
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("따라갈 대상")]
    [Tooltip("비워두면 씬에서 PlayerController를 가진 오브젝트를 자동으로 찾습니다")]
    [SerializeField] private Transform target;

    [Header("시야 (고정)")]
    [Tooltip("플레이어에서 카메라까지의 거리(m) — 멀수록 넓게 보임")]
    [Min(1f)] [SerializeField] private float distance = 30f;

    [Tooltip("내려다보는 각도(도) — 90이면 바로 위에서, 작을수록 비스듬히 (화면 위쪽이 더 멀리 보임)")]
    [Range(20f, 89f)] [SerializeField] private float pitch = 45f;

    [Tooltip("세로 시야각(도) — 작을수록 원근감이 줄어 위아래가 고르게 보임 (같은 넓이를 보려면 거리를 늘려야 함)")]
    [Range(10f, 90f)] [SerializeField] private float fieldOfView = 30f;

    [Tooltip("따라가는 부드러움 (작을수록 빠르게 붙음, 0이면 즉시)")]
    [Min(0f)] [SerializeField] private float smoothTime = 0.15f;

    private Camera cam;
    private Vector3 offset;   // 플레이어 기준 카메라 위치 (거리·각도에서 계산)
    private Vector3 velocity; // SmoothDamp가 내부적으로 쓰는 현재 속도

    private void Awake()
    {
        cam = GetComponent<Camera>();
        ApplyView();
    }

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

        // 시작하자마자 제자리로 맞춤 (처음에 카메라가 미끄러져 오는 것 방지)
        SnapToTarget();
    }

    // 플레이 중 Inspector에서 값을 바꿔도 바로 보이게 함
    private void OnValidate()
    {
        if (Application.isPlaying && cam != null) ApplyView();
    }

    // 플레이어가 Update에서 이동을 마친 뒤 따라가야 화면 떨림이 없음
    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;

        if (smoothTime > 0f)
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
        }
        else
        {
            transform.position = desiredPosition;
        }
    }

    // 미끄러지지 않고 대상 위치로 바로 맞춤 (지역 이동처럼 플레이어가 순간이동했을 때)
    public void SnapToTarget()
    {
        if (target == null) return;
        velocity = Vector3.zero;
        transform.position = target.position + offset;
    }

    // 거리·각도·시야각을 카메라에 적용 (카메라가 바라보는 방향의 정반대로 distance만큼 물러나 플레이어가 화면 정중앙)
    private void ApplyView()
    {
        Quaternion rotation = Quaternion.Euler(pitch, transform.eulerAngles.y, 0f); // 좌우 방향은 씬에 놓인 그대로
        transform.rotation = rotation;
        offset = rotation * Vector3.back * distance;
        if (cam != null) cam.fieldOfView = fieldOfView;
    }
}
