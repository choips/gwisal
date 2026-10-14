using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;  // UI 위에 커서가 있는지 확인 (EventSystem) 사용

// 바닥 아이템 이름표 관리자 — 이름표를 화면 UI(WorldUICanvas)에 그리고, 겹치면 위로 차곡차곡 쌓습니다 (디아블로 2 방식).
// 씬에 놓지 않아도 LootDropper가 처음 아이템을 떨어뜨릴 때 스스로 만들어집니다 ([WorldUI] → ItemLabels).
// 이름 박스를 왼쪽 클릭하면 플레이어가 그 아이템으로 걸어가 줍습니다 (작은 아이템 모양을 정확히 누를 필요 없음).
// 글자 크기·박스 여백 배율은 BalanceConfig(바닥 아이템 이름표)에서 조절합니다 (S7 설정 메뉴가 생기기 전까지).
// 카메라가 움직인 뒤에 자리를 정해야 이름표가 흔들리지 않으므로 다른 스크립트보다 늦게 실행합니다.
[DefaultExecutionOrder(1000)]
public class ItemLabelManager : MonoBehaviour
{
    private const float BaseFontSize = 18f;                                // 배율 100%일 때 글자 크기 (1920×1080 기준)
    private static readonly Vector2 BasePadding = new Vector2(8f, 3f);     // 배율 100%일 때 글자 둘레 여백 (좌우, 위아래)
    private const float StackGap = 2f;                                     // 쌓인 이름표 사이 간격
    private const float ScreenMargin = 100f;                               // 화면 밖으로 이만큼 벗어나면 숨김

    private static ItemLabelManager instance;
    private static ItemLabelBox hoveredLabel; // 지금 마우스가 올라가 있는 이름표 (없으면 null)

    private readonly List<ItemLabelBox> labels = new List<ItemLabelBox>();   // 모든 이름표
    private readonly List<ItemLabelBox> visible = new List<ItemLabelBox>();  // 이번 프레임에 보이는 이름표 (쌓기용)
    private readonly List<Rect> placed = new List<Rect>();                   // 이번 프레임에 자리를 정한 박스들

    private WorldUICanvas worldUI;
    private PlayerController player;
    private BalanceConfig config;
    private float appliedTextScale = -1f; // 지금 이름표에 적용된 글자 배율 (바뀌면 크기를 다시 계산)
    private float appliedBoxScale = -1f;  // 지금 이름표에 적용된 박스 여백 배율

    // 쌓는 순서: 화면 아래쪽(가까운 아이템)부터, 같으면 왼쪽부터
    private static readonly System.Comparison<ItemLabelBox> CompareByNaturalPosition = (a, b) =>
    {
        int byY = a.NaturalPosition.y.CompareTo(b.NaturalPosition.y);
        return byY != 0 ? byY : a.NaturalPosition.x.CompareTo(b.NaturalPosition.x);
    };

    // 마우스가 바닥 아이템 이름표 위에 있는지
    public static bool IsPointerOverLabel => hoveredLabel != null;

    // 마우스가 클릭을 막아야 하는 UI(인벤토리·툴팁 등) 위에 있는지 — 바닥 아이템 이름표 위는 막지 않음
    // (이름표 위에서도 누르고 있으면 계속 이동하고, 스킬도 쓸 수 있게)
    public static bool IsPointerOverBlockingUI()
    {
        if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()) return false;
        return !IsPointerOverLabel;
    }

    // 바닥 아이템에 이름표를 붙임 (LootDropper가 아이템을 떨어뜨릴 때 호출)
    // font: 한글 글꼴, height: 아이템 중심에서 이름표까지의 높이(m)
    public static void Register(Item item, TMP_FontAsset font, float height)
    {
        if (item == null) return;

        if (instance == null)
        {
            WorldUICanvas canvas = WorldUICanvas.Instance;
            RectTransform layer = canvas.CreateLayer("ItemLabels");
            instance = layer.gameObject.AddComponent<ItemLabelManager>();
            instance.worldUI = canvas;
        }

        instance.Add(item, font, height);
    }

    private void Add(Item item, TMP_FontAsset font, float height)
    {
        ItemLabelBox labelBox = ItemLabelBox.Create(this, (RectTransform)transform, item, font, height);
        UpdateScales();
        labelBox.ApplySize(BaseFontSize * appliedTextScale, BasePadding * appliedBoxScale);
        labelBox.SetShown(false); // 아이템이 착지한 뒤 LateUpdate에서 보여 줌
        labels.Add(labelBox);
    }

    private void OnEnable()
    {
        ItemRarityColors.ColorsChanged += RefreshColors;
    }

    private void OnDisable()
    {
        ItemRarityColors.ColorsChanged -= RefreshColors;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        hoveredLabel = null;
    }

    private void LateUpdate()
    {
        RemoveDeadLabels();
        if (UpdateScales()) ResizeAll();
        PlaceLabels();
    }

    // 주워졌거나 지역을 떠나 사라진 아이템의 이름표를 지움
    private void RemoveDeadLabels()
    {
        for (int i = labels.Count - 1; i >= 0; i--)
        {
            ItemLabelBox labelBox = labels[i];
            if (labelBox.Item != null) continue;

            labels.RemoveAt(i);
            Destroy(labelBox.gameObject);
        }
    }

    // 아이템 위 원래 자리를 구하고, 화면 아래쪽 이름표부터 차례로 놓으며 겹치면 위로 올림
    private void PlaceLabels()
    {
        visible.Clear();
        Vector2 canvasSize = worldUI.CanvasSize;

        foreach (ItemLabelBox labelBox in labels)
        {
            bool show = TryGetNaturalPosition(labelBox, canvasSize, out Vector2 naturalPosition);
            labelBox.SetShown(show);
            if (!show) continue;

            labelBox.NaturalPosition = naturalPosition;
            visible.Add(labelBox);
        }

        visible.Sort(CompareByNaturalPosition);
        placed.Clear();

        foreach (ItemLabelBox labelBox in visible)
        {
            Rect rect = new Rect(labelBox.NaturalPosition - labelBox.Size * 0.5f, labelBox.Size);

            // 이미 놓인 박스와 겹치면 그 박스 바로 위로 올리고, 안 겹칠 때까지 반복 (위로만 움직이므로 반드시 끝남)
            for (int pass = 0; pass <= placed.Count; pass++)
            {
                bool moved = false;
                foreach (Rect other in placed)
                {
                    if (!rect.Overlaps(other)) continue;
                    rect.y = other.yMax + StackGap;
                    moved = true;
                }
                if (!moved) break;
            }

            placed.Add(rect);
            labelBox.Rect.anchoredPosition = rect.center;
        }
    }

    // 이름표를 보여 줄지와 원래 자리 (아이템이 꺼져 있거나 날아가는 중이거나 화면 밖이면 false)
    private bool TryGetNaturalPosition(ItemLabelBox labelBox, Vector2 canvasSize, out Vector2 naturalPosition)
    {
        naturalPosition = Vector2.zero;
        Item item = labelBox.Item;
        if (!item.isActiveAndEnabled) return false;
        if (item.TryGetComponent(out DroppedItem droppedItem) && droppedItem.IsFlying) return false;

        Vector3 worldPosition = item.transform.position + Vector3.up * labelBox.Height;
        if (!worldUI.TryWorldToCanvas(worldPosition, out naturalPosition)) return false;

        return naturalPosition.x > -ScreenMargin && naturalPosition.x < canvasSize.x + ScreenMargin
            && naturalPosition.y > -ScreenMargin && naturalPosition.y < canvasSize.y + ScreenMargin;
    }

    // BalanceConfig의 글자·박스 배율을 읽음 (바뀌었으면 true)
    private bool UpdateScales()
    {
        if (config == null)
        {
            PlayerStats stats = FindFirstObjectByType<PlayerStats>();
            if (stats != null && stats.Stats != null) config = stats.Stats.Config;
        }

        float textScale = config != null ? config.itemLabelTextScale : 1f;
        float boxScale = config != null ? config.itemLabelBoxScale : 1f;
        if (Mathf.Approximately(textScale, appliedTextScale) && Mathf.Approximately(boxScale, appliedBoxScale)) return false;

        appliedTextScale = textScale;
        appliedBoxScale = boxScale;
        return true;
    }

    private void ResizeAll()
    {
        foreach (ItemLabelBox labelBox in labels)
        {
            labelBox.ApplySize(BaseFontSize * appliedTextScale, BasePadding * appliedBoxScale);
        }
    }

    // 등급 색 표가 바뀌면 이름표 글자 색을 다시 칠함 (에디터에서 색을 고를 때)
    private void RefreshColors()
    {
        foreach (ItemLabelBox labelBox in labels)
        {
            if (labelBox.Item != null) labelBox.SetColor(labelBox.Item.data.NameColor);
        }
    }

    // 이름표를 클릭함 → 플레이어가 그 아이템으로 걸어가 주움 (사망 등으로 조작이 꺼져 있으면 무시)
    public void PickUp(ItemLabelBox labelBox)
    {
        if (labelBox.Item == null) return;

        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null || !player.isActiveAndEnabled) return;

        player.TargetItem(labelBox.Item);
    }

    public void SetHovered(ItemLabelBox labelBox)
    {
        hoveredLabel = labelBox;
    }

    public void ClearHovered(ItemLabelBox labelBox)
    {
        if (hoveredLabel == labelBox) hoveredLabel = null;
    }
}
