using UnityEngine;

// 바닥에 떨어진 아이템
// 튀어 오른 뒤 바닥에 착지하는 순간 물리 움직임을 멈추고 그 자리에 고정합니다.
[RequireComponent(typeof(Rigidbody))]
public class DroppedItem : MonoBehaviour
{
    [Tooltip("접촉면이 이 값보다 위를 향해야 '바닥에 착지'로 판단 (1 = 완전히 수평한 바닥)")]
    [SerializeField] private float groundNormalThreshold = 0.7f;

    private Rigidbody rb;
    private bool hasLanded; // 이미 착지해서 고정되었는지 여부

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryLand(collision);
    }

    // 처음 부딪힌 곳이 벽 같은 옆면이었더라도, 이후 바닥에 닿아 있으면 고정
    private void OnCollisionStay(Collision collision)
    {
        TryLand(collision);
    }

    private void TryLand(Collision collision)
    {
        if (hasLanded) return;

        // 접촉면 중 하나라도 위를 향하고 있으면 바닥(또는 다른 물체의 윗면)에 올라탄 것
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y >= groundNormalThreshold)
            {
                Land();
                return;
            }
        }
    }

    // 속도를 없애고 물리 영향을 받지 않도록 고정
    private void Land()
    {
        hasLanded = true;

        // Kinematic으로 바꾸기 전에 속도를 먼저 0으로 만들어야 경고가 뜨지 않음
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }
}
