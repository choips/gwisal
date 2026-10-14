using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;  // 마우스 올리기·클릭 받기 (IPointerEnterHandler 등)
using UnityEngine.UI;            // Image 사용

// 바닥 아이템 이름 박스 하나 (어두운 반투명 바탕 + 등급 색 글자)
// ItemLabelManager가 만들고 위치를 정하므로 직접 붙일 필요가 없습니다.
// 마우스를 올리면 바탕이 밝아지고, 왼쪽 클릭하면 플레이어가 그 아이템으로 걸어가 줍습니다.
public class ItemLabelBox : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    private static readonly Color BoxColor = new Color(0.05f, 0.05f, 0.07f, 0.65f);     // 평소 바탕
    private static readonly Color HoverBoxColor = new Color(0.28f, 0.28f, 0.32f, 0.9f); // 마우스를 올렸을 때 바탕

    private ItemLabelManager manager;
    private Image box;
    private TextMeshProUGUI label;

    public Item Item { get; private set; }               // 이 이름표의 바닥 아이템
    public float Height { get; private set; }            // 아이템 중심에서 이름표까지의 높이(m)
    public RectTransform Rect { get; private set; }
    public Vector2 Size { get; private set; }            // 박스 크기 (캔버스 단위)
    public Vector2 NaturalPosition { get; set; }         // 쌓기 전 원래 자리 (아이템 바로 위, 캔버스 좌표)
    public bool IsShown { get; private set; }

    // 이름 박스 만들기
    public static ItemLabelBox Create(ItemLabelManager owner, RectTransform parent, Item item, TMP_FontAsset font, float height)
    {
        GameObject root = new GameObject($"{item.name} 이름표", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        ItemLabelBox labelBox = root.AddComponent<ItemLabelBox>();
        labelBox.Build(owner, item, font, height);
        return labelBox;
    }

    private void Build(ItemLabelManager owner, Item item, TMP_FontAsset font, float height)
    {
        manager = owner;
        Item = item;
        Height = height;

        Rect = (RectTransform)transform;
        Rect.anchorMin = Vector2.zero;   // 층의 왼쪽 아래 기준 좌표로 놓음
        Rect.anchorMax = Vector2.zero;
        Rect.pivot = new Vector2(0.5f, 0.5f);

        box = gameObject.AddComponent<Image>();
        box.color = BoxColor;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        label = textObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = item.data.Name;
        label.color = item.data.NameColor;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false; // 클릭은 바탕(박스)이 받음

        IsShown = true; // 만든 직후에는 켜져 있음 (관리자가 크기를 정한 뒤 SetShown으로 정리)
    }

    // 글자 크기·박스 여백을 정하고 박스 크기를 글자에 맞춤
    public void ApplySize(float fontSize, Vector2 padding)
    {
        label.fontSize = fontSize;
        Vector2 textSize = label.GetPreferredValues(label.text);
        Size = textSize + padding * 2f;
        Rect.sizeDelta = Size;
    }

    public void SetColor(Color color)
    {
        label.color = color;
    }

    // 보이기·숨기기 (바뀔 때만 켜고 끔)
    public void SetShown(bool shown)
    {
        if (IsShown == shown) return;
        IsShown = shown;
        gameObject.SetActive(shown);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        box.color = HoverBoxColor;
        Rect.SetAsLastSibling(); // 마우스를 올린 이름표가 다른 이름표 위에 보이게
        manager.SetHovered(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        box.color = BoxColor;
        manager.ClearHovered(this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (DevCheats.IsPointerOverMenu(eventData.position)) return; // 개발자 메뉴(F1) 창 아래쪽이면 무시

        manager.PickUp(this);
    }

    // 숨겨지거나 사라지면 밝아진 상태를 풀고 "마우스가 이름표 위" 표시도 지움
    private void OnDisable()
    {
        if (box != null) box.color = BoxColor;
        if (manager != null) manager.ClearHovered(this);
    }
}
