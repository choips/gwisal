using TMPro;                // TMP_FontAsset 사용 (출구 이름표 글꼴)
using UnityEngine;

// 지역 출구 (지역 씬에 두는 오브젝트, 콜라이더 필요)
// 플레이어가 이 콜라이더 안으로 들어오면 RegionManager가 다음 지역 씬의 정해진 입구로 옮겨 줍니다.
// 콜라이더는 자동으로 "통과 가능(Is Trigger)"이 되고, 닿았는지 알아내기 위한 Rigidbody(Is Kinematic)도 자동으로 붙습니다.
// 주의: 도착할 지역의 입구(RegionEntry)를 그 지역의 출구 콜라이더 안에 두면 도착하자마자 다시 이동하므로 3m 이상 떨어뜨려 두세요.
[RequireComponent(typeof(Collider))]
public class RegionExit : MonoBehaviour
{
    [Header("이동할 곳")]
    [Tooltip("이동할 지역 씬 이름 (File > Build Profiles의 Scene List에 들어 있어야 합니다)")]
    [SerializeField] private string targetScene = "Region_02";

    [Tooltip("도착할 입구 ID (이동할 지역 씬에 있는 RegionEntry의 Entry Id와 같은 글자)")]
    [SerializeField] private string targetEntryId = "start";

    [Header("출구 이름표 (머리 위 글자)")]
    [Tooltip("한글 글꼴 (NotoSansKR-Regular SDF). 비워 두면 이름표를 띄우지 않음")]
    [SerializeField] private TMP_FontAsset labelFont;
    [SerializeField] private float labelFontSize = 5f;
    [Tooltip("출구 중심에서 이름표까지의 높이(m)")]
    [SerializeField] private float labelHeight = 2.5f;
    [SerializeField] private Color labelColor = new Color(0.6f, 0.9f, 1f); // 하늘색

    private void Awake()
    {
        // 벽처럼 막지 않고 통과하면서 "들어왔다"는 알림만 받도록
        Collider exitCollider = GetComponent<Collider>();
        exitCollider.isTrigger = true;

        // 플레이어에는 Rigidbody가 없어서, 출구 쪽에 움직이지 않는 Rigidbody가 있어야 트리거 알림이 옴
        if (!TryGetComponent(out Rigidbody body))
        {
            body = gameObject.AddComponent<Rigidbody>();
        }
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void Start()
    {
        if (labelFont != null)
        {
            string regionName = Loc.Get("region.name." + targetScene);
            gameObject.AddComponent<ItemNameLabel>().Setup(
                Loc.Format("region.exit_label", regionName), labelColor, labelFont, labelFontSize, labelHeight);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (RegionManager.Instance == null)
        {
            Debug.LogWarning("RegionExit: RegionManager가 없어 이동할 수 없습니다. Core 씬을 열고 Play 해 주세요.");
            return;
        }

        RegionManager.Instance.TravelTo(targetScene, targetEntryId);
    }

    // Scene 뷰에 출구 범위를 주황색 상자로 표시
    private void OnDrawGizmos()
    {
        if (!TryGetComponent(out Collider exitCollider)) return;

        Bounds bounds = exitCollider.bounds;
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
