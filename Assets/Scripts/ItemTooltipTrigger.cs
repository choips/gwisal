using UnityEngine;
using UnityEngine.EventSystems;  // 마우스 올리기/나가기 이벤트 (IPointerEnterHandler 등) 사용

// 인벤토리의 아이템 버튼에 붙어서, 마우스가 올라가면 툴팁을 띄우고 나가면 숨깁니다.
// InventoryManager가 버튼을 만들 때 자동으로 붙이므로 직접 추가할 필요가 없습니다.
public class ItemTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private ItemData item;        // 이 버튼이 나타내는 아이템
    private ItemTooltip tooltip;  // 띄울 툴팁 창

    // 버튼 생성 직후 InventoryManager가 호출
    public void Setup(ItemData itemData, ItemTooltip itemTooltip)
    {
        item = itemData;
        tooltip = itemTooltip;
    }

    // 마우스가 버튼 위로 올라옴
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.Show(item);
    }

    // 마우스가 버튼 밖으로 나감
    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.Hide(item);
    }

    // 장착으로 목록이 새로 만들어지거나 인벤토리를 닫아서 버튼이 사라질 때는
    // "나감" 신호가 오지 않으므로 여기서 직접 숨김
    private void OnDisable()
    {
        if (tooltip != null) tooltip.Hide(item);
    }
}
