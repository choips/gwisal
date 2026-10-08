using UnityEngine;
using UnityEngine.EventSystems;  // 마우스 올리기/나가기 이벤트 (IPointerEnterHandler 등) 사용

// 인벤토리의 아이템 버튼·장비 칸에 붙어서, 마우스가 올라가면 툴팁을 띄우고 나가면 숨깁니다.
// InventoryManager가 자동으로 붙이므로 직접 추가할 필요가 없습니다.
public class ItemTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private ItemData item;        // 이 버튼이 나타내는 아이템 (빈 장비 칸이면 null)
    private ItemTooltip tooltip;  // 띄울 툴팁 창
    private bool isEquipped;      // 장비 칸(장착 중인 아이템)이면 true

    // 버튼 생성 직후, 또는 장비 칸의 내용이 바뀔 때마다 InventoryManager가 호출
    public void Setup(ItemData itemData, ItemTooltip itemTooltip, bool equipped = false)
    {
        // 장비 칸에 마우스를 올린 채 해제·교체하면 버튼은 그대로라서 "나감" 신호가 오지 않으므로, 예전 아이템 툴팁을 닫음
        if (tooltip != null && item != null && item != itemData)
        {
            tooltip.Hide(item);
        }

        item = itemData;
        tooltip = itemTooltip;
        isEquipped = equipped;
    }

    // 마우스가 버튼 위로 올라옴
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.Show(item, isEquipped);
    }

    // 마우스가 버튼 밖으로 나감
    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null && item != null) tooltip.Hide(item);
    }

    // 장착으로 목록이 새로 만들어지거나 인벤토리를 닫아서 버튼이 사라질 때는
    // "나감" 신호가 오지 않으므로 여기서 직접 숨김
    private void OnDisable()
    {
        if (tooltip != null && item != null) tooltip.Hide(item);
    }
}
