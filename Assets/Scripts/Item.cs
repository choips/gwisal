using UnityEngine;

// 바닥에 떨어진 아이템 프리팹에 붙이는 스크립트
// 플레이어가 다가와 PickUp()을 호출하면 획득 처리 후 바닥에서 사라집니다.
public class Item : MonoBehaviour
{
    [Tooltip("이 아이템의 정보 (이름, 종류, 등급, 능력치 증가량)")]
    public ItemData data = new ItemData();

    private bool isPickedUp; // 이미 주운 아이템인지 여부 (중복 획득 방지)

    // 아이템 획득 처리 (가방이 가득 차 있으면 바닥에 그대로 남김)
    public void PickUp()
    {
        if (isPickedUp) return;

        if (InventoryManager.Instance != null)
        {
            // 이 오브젝트는 곧 파괴되므로 인벤토리에는 복사본을 넣음
            if (!InventoryManager.Instance.AddItem(new ItemData(data))) return;
        }
        else
        {
            Debug.LogWarning("씬에 InventoryManager가 없어 인벤토리에 추가하지 못했습니다.");
        }

        isPickedUp = true;
        Debug.Log($"아이템 획득: {data.DisplayName} ({data.StatDescription})");
        Destroy(gameObject);
    }
}
