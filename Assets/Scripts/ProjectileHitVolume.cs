using UnityEngine;

// 투사체 피격 판정의 세로 길이를 보장하는 공통 도구
// - 귀신이 작거나 떠 있어도, 발사 높이가 달라도 빗나가지 않게 최소 세로 길이(세계 좌표 m)를 맞춤
// - 플레이어 스킬 투사체·적 투사체 모두 이 함수를 쓰면 됨 (새 투사체를 만들 때 꼭 호출)
// - 상대방(귀신) 쪽은 Enemy.RefreshHitbox가 발~최소 높이를 맞춤 → 양쪽에서 막아 둠
public static class ProjectileHitVolume
{
    public const float DefaultMinHeight = 2f; // 바닥~머리 정도를 덮는 기본 세로 (m)

    // go에 붙은 Collider의 세로 세계 길이가 minWorldHeight 이상이 되게 맞춤
    public static void EnsureVertical(GameObject go, float minWorldHeight = DefaultMinHeight)
    {
        if (go == null || minWorldHeight <= 0f) return;

        float scaleY = Mathf.Max(Mathf.Abs(go.transform.lossyScale.y), 0.001f);

        if (go.TryGetComponent(out BoxCollider box))
        {
            EnsureBox(box, scaleY, minWorldHeight);
            return;
        }

        if (go.TryGetComponent(out CapsuleCollider capsule))
        {
            EnsureCapsule(capsule, scaleY, minWorldHeight);
            return;
        }

        // 구(Sphere)만 있으면 세로로 늘릴 수 없어, 판정용 Box를 추가하고 구는 끔
        if (go.TryGetComponent(out SphereCollider sphere))
        {
            EnsureFromSphere(go, sphere, scaleY, minWorldHeight);
        }
    }

    private static void EnsureBox(BoxCollider box, float scaleY, float minWorldHeight)
    {
        box.isTrigger = true;

        Vector3 size = box.size;
        float worldHeight = Mathf.Abs(size.y * scaleY);
        if (worldHeight < minWorldHeight)
        {
            size.y = minWorldHeight / scaleY;
            box.size = size;
        }

        // 날아가는 높이를 기준으로 위·아래를 대칭으로 덮음
        Vector3 center = box.center;
        center.y = 0f;
        box.center = center;
    }

    private static void EnsureCapsule(CapsuleCollider capsule, float scaleY, float minWorldHeight)
    {
        capsule.isTrigger = true;
        capsule.direction = 1; // Y축

        float worldHeight = Mathf.Abs(capsule.height * scaleY);
        if (worldHeight < minWorldHeight)
        {
            capsule.height = minWorldHeight / scaleY;
        }

        Vector3 center = capsule.center;
        center.y = 0f;
        capsule.center = center;
    }

    private static void EnsureFromSphere(GameObject go, SphereCollider sphere, float scaleY, float minWorldHeight)
    {
        sphere.enabled = false;

        BoxCollider box = go.GetComponent<BoxCollider>();
        if (box == null) box = go.AddComponent<BoxCollider>();

        float scaleX = Mathf.Max(Mathf.Abs(go.transform.lossyScale.x), 0.001f);
        float scaleZ = Mathf.Max(Mathf.Abs(go.transform.lossyScale.z), 0.001f);
        float worldDiameter = sphere.radius * 2f * Mathf.Max(scaleX, scaleZ);

        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(
            Mathf.Max(worldDiameter, 0.2f) / scaleX,
            minWorldHeight / scaleY,
            Mathf.Max(worldDiameter, 0.2f) / scaleZ);
    }
}
