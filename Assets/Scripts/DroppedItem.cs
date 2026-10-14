using System.Collections.Generic;
using UnityEngine;

// 바닥에 떨어진 아이템
// LootDropper가 골라 준 빈자리까지 짧은 포물선을 그리며 날아가 착지합니다 (디아블로 2 방식).
// 물리로 날리지 않으므로 아이템끼리 부딪혀 멀리 튕겨 나가지 않습니다.
// 바닥에 있는(또는 날아가는 중인) 아이템의 착지 위치를 목록에 올려 두어, 다음 아이템이 빈자리를 고를 때 씁니다.
public class DroppedItem : MonoBehaviour
{
    // 지금 바닥에 있거나 날아가는 중인 아이템 목록 (빈자리 찾기용)
    private static readonly List<DroppedItem> active = new List<DroppedItem>();
    public static IReadOnlyList<DroppedItem> Active => active;

    // 착지할(또는 이미 착지한) 위치 — 날아가는 중에도 이 자리는 이미 찬 것으로 봄
    public Vector3 LandingPosition { get; private set; }

    // 아직 날아가는 중인지 (바닥 이름표는 착지한 뒤에 보임)
    public bool IsFlying => isFlying;

    private Vector3 startPosition;    // 날아오르기 시작한 위치
    private Quaternion startRotation; // 출발할 때의 회전 (한 바퀴 돌고 이 회전으로 착지)
    private Vector3 flipAxis;         // 공중제비 회전축 (날아가는 방향의 옆쪽)
    private float flightTime;         // 날아가는 시간(초)
    private float arcHeight;          // 포물선 꼭대기 높이(m)
    private int flipCount;            // 공중에서 도는 바퀴 수
    private float elapsed;            // 날아간 시간
    private bool isFlying;

    // Enter Play Mode 설정에서 도메인 다시 불러오기를 꺼도 지난 플레이의 목록이 남지 않게 비움
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveList()
    {
        active.Clear();
    }

    private void Awake()
    {
        // 프리팹의 Rigidbody가 중력·충돌로 움직이지 않도록 고정 (콜라이더는 클릭으로 줍기 위해 그대로 둠)
        if (TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        LandingPosition = transform.position;
    }

    private void OnEnable()
    {
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    // groundPoint 바닥 지점까지 포물선으로 날아가 착지 (아이템 크기만큼 바닥 위에 놓임)
    // time: 날아가는 시간(초, 0이면 바로 그 자리에), height: 포물선 꼭대기 높이(m), flips: 공중에서 도는 바퀴 수
    public void Launch(Vector3 groundPoint, float time, float height, int flips)
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        LandingPosition = groundPoint + Vector3.up * GetHalfHeight();

        // 날아가는 방향의 옆쪽을 축으로 돌아야 앞으로 구르듯 보임 (제자리 낙하면 아무 수평 축)
        Vector3 direction = LandingPosition - startPosition;
        direction.y = 0f;
        flipAxis = direction.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, direction.normalized) : Vector3.right;

        flightTime = time;
        arcHeight = height;
        flipCount = flips;
        elapsed = 0f;

        if (flightTime <= 0f)
        {
            transform.position = LandingPosition;
            isFlying = false;
            return;
        }
        isFlying = true;
    }

    private void Update()
    {
        if (!isFlying) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / flightTime); // 0(출발) ~ 1(착지)

        // 직선으로 옮기면서 위로 4·t·(1−t)만큼 띄움 → t = 0.5에서 arcHeight만큼 가장 높음
        Vector3 position = Vector3.Lerp(startPosition, LandingPosition, t);
        position.y += arcHeight * 4f * t * (1f - t);
        transform.position = position;

        // 정확히 flipCount바퀴(360°의 배수) 돌아서 착지하므로 착지 순간 회전이 튀지 않음
        transform.rotation = Quaternion.AngleAxis(360f * flipCount * t, flipAxis) * startRotation;

        if (t >= 1f) isFlying = false;
    }

    // 아이템 중심에서 바닥면까지의 높이 (모양 그림의 크기 기준, 그림이 없으면 0)
    private float GetHalfHeight()
    {
        Renderer itemRenderer = GetComponentInChildren<Renderer>();
        if (itemRenderer == null) return 0f;
        return Mathf.Max(0f, transform.position.y - itemRenderer.bounds.min.y);
    }
}
