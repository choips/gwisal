using UnityEngine;
using UnityEngine.SceneManagement;

// 지역 입구 (지역 씬에 두는 빈 오브젝트)
// 이 지역에 들어올 때 플레이어가 서는 자리와 바라보는 방향(파란 화살표 = 앞)입니다.
// 한 지역에 입구가 여러 개면 entryId로 구분합니다. (예: "start", "from_region_02")
public class RegionEntry : MonoBehaviour
{
    [Tooltip("입구 이름 (같은 지역 씬 안에서 겹치지 않게)")]
    [SerializeField] private string entryId = "start";

    public string EntryId => entryId;

    // 씬에서 entryId가 같은 입구 찾기 (없으면 첫 번째 입구, 입구가 하나도 없으면 null)
    // 지역을 불러올 때 한 번만 부르므로 여기서는 GetComponentsInChildren을 써도 괜찮음
    public static RegionEntry Find(Scene scene, string entryId)
    {
        RegionEntry firstEntry = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RegionEntry entry in root.GetComponentsInChildren<RegionEntry>(true))
            {
                if (entry.entryId == entryId) return entry;
                if (firstEntry == null) firstEntry = entry;
            }
        }

        if (firstEntry != null)
        {
            Debug.LogWarning($"RegionEntry: '{scene.name}' 씬에 입구 '{entryId}'가 없어 '{firstEntry.entryId}' 입구를 대신 씁니다.");
        }
        return firstEntry;
    }

    // Scene 뷰에 입구 위치(초록 원)와 바라보는 방향(파란 선)을 표시
    private void OnDrawGizmos()
    {
        Vector3 position = transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(position + Vector3.up * 0.1f, 0.6f);

        Vector3 forward = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(position + Vector3.up * 0.1f, position + Vector3.up * 0.1f + forward * 1.5f);
    }
}
