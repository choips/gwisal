using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;  // 클릭 이벤트 (IPointerClickHandler) 사용
using UnityEngine.UI;            // Image 사용

// 스킬바 옆의 기력 물약(Mana Potion) 칸
// 가방에 남은 기력 물약 개수를 보여 주고, 물약이 없으면 아이콘을 어둡게 바꿉니다.
// 칸을 마우스로 클릭해도 물약을 마십니다. (단축키 1과 같은 동작)
public class PotionSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI 연결")]
    [Tooltip("물약 아이콘 이미지 (물약이 없으면 어두워짐)")]
    [SerializeField] private Image iconImage;

    [Tooltip("남은 물약 개수를 표시할 텍스트")]
    [SerializeField] private TextMeshProUGUI countText;

    [Header("색상")]
    [Tooltip("물약이 하나도 없을 때 아이콘에 곱해질 색")]
    [SerializeField] private Color emptyTint = new Color(0.3f, 0.3f, 0.3f);

    private Color iconNormalColor; // 아이콘의 원래 색
    private int lastCount = -1;    // 마지막으로 표시한 개수 (바뀔 때만 글자를 고치기 위함)

    private void Awake()
    {
        if (iconImage != null)
        {
            iconNormalColor = iconImage.color;
        }
    }

    private void Update()
    {
        InventoryManager inventory = InventoryManager.Instance;
        int count = inventory != null ? inventory.CountItems(ItemType.ManaPotion) : 0;
        if (count == lastCount) return;

        lastCount = count;

        if (countText != null)
        {
            countText.text = $"×{count}";
        }

        if (iconImage != null)
        {
            iconImage.color = count > 0 ? iconNormalColor : iconNormalColor * emptyTint;
        }
    }

    // 칸을 클릭하면 물약을 마심 (UI를 클릭한 것이므로 뒤쪽 바닥으로 이동하지 않음)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.DrinkFirstPotion();
        }
    }
}
