using System;
using TMPro;                // TextMeshProUGUI 사용
using UnityEngine;
using UnityEngine.UI;       // Image, Button, Outline 사용

// 격자 가방의 칸 하나 (아이콘 + 등급 색 테두리 + 묶음 개수)
// InventoryManager가 Create()로 자동 생성하므로 직접 씬에 붙일 필요가 없습니다.
public class ItemSlotUI : MonoBehaviour
{
    private static readonly Color EmptyBorderColor = new Color(0.3f, 0.3f, 0.35f); // 빈 칸 테두리 색

    private Image background;
    private Outline border;               // 등급 색 테두리
    private Image icon;
    private TextMeshProUGUI letterText;   // 아이콘 그림이 없을 때 대신 보여 줄 글자 (검/갑/약)
    private TextMeshProUGUI countText;    // 물약 묶음 개수 (×3)
    private Button button;
    private ItemTooltipTrigger tooltipTrigger;
    private ItemTooltip tooltip;

    private ItemData item;                 // 이 칸에 들어 있는 아이템 (빈 칸이면 null)
    private Action<ItemData> onClick;      // 칸을 클릭했을 때 실행할 동작 (장착/사용)

    // 칸 하나를 만들어 parent 아래에 붙임
    public static ItemSlotUI Create(Transform parent, TMP_FontAsset font, ItemTooltip tooltip,
                                    Action<ItemData> onClick, Color slotColor)
    {
        GameObject root = new GameObject("ItemSlot", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        ItemSlotUI slot = root.AddComponent<ItemSlotUI>();
        slot.Build(font, tooltip, onClick, slotColor);
        slot.Clear();
        return slot;
    }

    private void Build(TMP_FontAsset font, ItemTooltip itemTooltip, Action<ItemData> clickAction, Color slotColor)
    {
        tooltip = itemTooltip;
        onClick = clickAction;

        background = gameObject.AddComponent<Image>();
        background.color = slotColor;

        border = gameObject.AddComponent<Outline>();
        border.effectDistance = new Vector2(2f, -2f);

        button = gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(HandleClick);

        icon = CreateChild<Image>("Icon", 5f);
        icon.preserveAspect = true;  // 그림 비율 유지
        icon.raycastTarget = false;  // 클릭은 칸(배경)이 받도록

        letterText = CreateChild<TextMeshProUGUI>("Letter", 0f);
        SetupText(letterText, font, 24f, TextAlignmentOptions.Center);

        countText = CreateChild<TextMeshProUGUI>("Count", 3f);
        SetupText(countText, font, 15f, TextAlignmentOptions.BottomRight);
        countText.fontStyle = FontStyles.Bold;

        if (tooltip != null)
        {
            tooltipTrigger = gameObject.AddComponent<ItemTooltipTrigger>();
        }
    }

    // 칸에 아이템을 표시 (count: 같은 물약 묶음 개수, iconSprite: 없으면 글자로 대신 표시)
    public void Show(ItemData newItem, int count, Sprite iconSprite)
    {
        item = newItem;
        button.interactable = true;
        border.effectColor = newItem.NameColor;

        icon.sprite = iconSprite;
        icon.enabled = iconSprite != null;

        letterText.text = iconSprite == null ? GetFallbackLetter(newItem) : "";
        letterText.color = newItem.NameColor;

        countText.text = count > 1 ? $"×{count}" : "";

        if (tooltipTrigger != null) tooltipTrigger.Setup(newItem, tooltip);
    }

    // 빈 칸으로 만듦
    public void Clear()
    {
        item = null;
        button.interactable = false;
        border.effectColor = EmptyBorderColor;

        icon.sprite = null;
        icon.enabled = false;
        letterText.text = "";
        countText.text = "";

        if (tooltipTrigger != null) tooltipTrigger.Setup(null, tooltip);
    }

    private void HandleClick()
    {
        if (item != null) onClick?.Invoke(item);
    }

    // 칸 전체를 채우는(가장자리 inset 픽셀만큼 안쪽) 자식 UI 생성
    private T CreateChild<T>(string childName, float inset) where T : Component
    {
        GameObject child = new GameObject(childName, typeof(RectTransform));
        child.transform.SetParent(transform, false);

        RectTransform rect = (RectTransform)child.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);

        return child.AddComponent<T>();
    }

    private static void SetupText(TextMeshProUGUI text, TMP_FontAsset font, float fontSize, TextAlignmentOptions alignment)
    {
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = "";
    }

    private static string GetFallbackLetter(ItemData data)
    {
        switch (data.itemType)
        {
            case ItemType.Weapon: return "검";
            case ItemType.Armor: return "갑";
            case ItemType.ManaPotion: return "약";
            default: return "?";
        }
    }
}
