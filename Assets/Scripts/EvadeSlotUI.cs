using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;   // 키 이름(GetBindingDisplayString) 사용
using UnityEngine.UI;

// 스킬바 옆의 회피 — 축지(Space) 칸 (Canvas → SkillBar에 붙임 — 붙어 있지 않으면 PlayerEvade가 플레이 시작 때 스스로 붙임)
// 플레이를 시작하면 스킬바 오른쪽에 칸을 스스로 만듭니다: 위쪽 = 키 이름(Input Actions에서 읽음), 아래쪽 = 충전 점(기본 2개).
// 남은 충전은 밝은 점, 차고 있는 점에는 어두운 덮개가 시계 방향으로 걷히고, 아직 차례가 아닌 점은 덮개가 꽉 차 있습니다.
public class EvadeSlotUI : MonoBehaviour
{
    [Header("칸 모양")]
    [Tooltip("스킬바 오른쪽 끝에서 칸까지 띄울 간격(픽셀)")]
    [SerializeField] private float gap = 10f;

    [Tooltip("칸 크기(픽셀)")]
    [SerializeField] private Vector2 slotSize = new Vector2(64f, 80f);

    [Tooltip("칸을 위아래로 옮길 양(픽셀) — 다른 스킬 칸과 높이를 맞출 때")]
    [SerializeField] private float verticalOffset = 10f;

    [Tooltip("충전 점 하나의 크기(픽셀)")]
    [SerializeField] private float dotSize = 18f;

    [Tooltip("(선택) 키 이름 글꼴 — 비워 두면 TextMeshPro 기본 글꼴")]
    [SerializeField] private TMP_FontAsset font;

    [Header("색상")]
    [SerializeField] private Color backgroundColor = new Color(0.12f, 0.12f, 0.15f, 0.9f); // 칸 배경
    [SerializeField] private Color dotColor = new Color(0.91f, 0.87f, 0.78f, 1f);          // 충전된 점 (한지 색)
    [SerializeField] private Color coverColor = new Color(0f, 0f, 0f, 0.75f);              // 충전 중 덮개

    private const string KeyboardGroup = "Keyboard&Mouse"; // 키 이름을 읽을 Input Actions 장치 묶음

    private static Sprite whiteSprite; // 덮개(Filled)는 그림이 있어야 동작하므로 흰 그림을 하나 만들어 같이 씀

    private PlayerEvade playerEvade;
    private RectTransform slotRect;   // 만든 칸
    private RectTransform dotsRow;    // 점들을 담는 줄
    private Image[] dotCovers = new Image[0]; // 점마다의 덮개

    // 씬에 회피 칸이 없으면 스킬 칸(SkillSlotUI)이 들어 있는 스킬바에 붙임 (플레이 시작 때 한 번)
    public static void EnsureExists()
    {
        if (FindFirstObjectByType<EvadeSlotUI>(FindObjectsInactive.Include) != null) return;

        SkillSlotUI skillSlot = FindFirstObjectByType<SkillSlotUI>(FindObjectsInactive.Include);
        if (skillSlot == null || skillSlot.transform.parent == null)
        {
            Debug.LogWarning("EvadeSlotUI: 스킬바(SkillSlotUI가 들어 있는 오브젝트)를 찾지 못해 회피 칸을 만들지 않습니다.");
            return;
        }

        skillSlot.transform.parent.gameObject.AddComponent<EvadeSlotUI>();
    }

    private void Awake()
    {
        playerEvade = FindFirstObjectByType<PlayerEvade>();
        if (playerEvade == null)
        {
            Debug.LogWarning("EvadeSlotUI: 씬에서 Player의 PlayerEvade를 찾지 못해 회피 칸을 만들지 않습니다. Player에 Player Evade 컴포넌트를 추가하세요.");
            enabled = false;
            return;
        }

        if (whiteSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        BuildSlot();
    }

    private void Update()
    {
        int maxCharges = playerEvade.MaxCharges;
        if (dotCovers.Length != maxCharges) BuildDots(maxCharges);

        int charges = playerEvade.Charges;
        float progress = playerEvade.RechargeProgress;

        for (int i = 0; i < dotCovers.Length; i++)
        {
            // 남은 충전 = 덮개 없음 / 지금 차는 점 = 남은 만큼 덮개 / 그 뒤 점 = 꽉 찬 덮개
            float fill = i < charges ? 0f : (i == charges ? 1f - progress : 1f);
            dotCovers[i].fillAmount = fill; // 값이 같으면 Unity가 다시 그리지 않음
        }
    }

    // 스킬바 오른쪽에 칸(배경 + 키 이름 + 점 줄)을 만듦
    private void BuildSlot()
    {
        slotRect = CreateRect("SkillSlot_Evade", transform);
        slotRect.anchorMin = slotRect.anchorMax = new Vector2(1f, 0.5f);
        slotRect.pivot = new Vector2(0f, 0.5f);
        slotRect.anchoredPosition = new Vector2(gap, verticalOffset);
        slotRect.sizeDelta = slotSize;
        AddImage(slotRect.gameObject, backgroundColor);

        // 키 이름 (Input Actions에서 키를 바꾸면 따라 바뀜)
        RectTransform labelRect = CreateRect("KeyLabel", slotRect);
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = GameInput.Evade.GetBindingDisplayString(InputBinding.MaskByGroup(KeyboardGroup));
        label.fontSize = 16f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        // 점 줄 (아래쪽 절반, 가운데 정렬)
        dotsRow = CreateRect("ChargeDots", slotRect);
        dotsRow.anchorMin = new Vector2(0f, 0f);
        dotsRow.anchorMax = new Vector2(1f, 0.5f);
        dotsRow.offsetMin = dotsRow.offsetMax = Vector2.zero;
        HorizontalLayoutGroup layout = dotsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 6f;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
    }

    // 충전 점을 최대 충전 수만큼 다시 만듦 (Balance Config에서 충전 수를 바꾸면 따라 바뀜)
    private void BuildDots(int count)
    {
        for (int i = dotsRow.childCount - 1; i >= 0; i--)
        {
            Destroy(dotsRow.GetChild(i).gameObject);
        }

        dotCovers = new Image[count];
        for (int i = 0; i < count; i++)
        {
            RectTransform dot = CreateRect("Dot" + (i + 1), dotsRow);
            dot.sizeDelta = new Vector2(dotSize, dotSize);
            AddImage(dot.gameObject, dotColor);

            RectTransform cover = CreateRect("Cover", dot);
            cover.anchorMin = Vector2.zero;
            cover.anchorMax = Vector2.one;
            cover.offsetMin = cover.offsetMax = Vector2.zero;
            Image coverImage = AddImage(cover.gameObject, coverColor);
            coverImage.type = Image.Type.Filled;
            coverImage.fillMethod = Image.FillMethod.Radial360;
            coverImage.fillOrigin = (int)Image.Origin360.Top;
            coverImage.fillClockwise = true;
            coverImage.fillAmount = 0f;
            dotCovers[i] = coverImage;
        }
    }

    // RectTransform을 가진 UI 오브젝트 만들기 (처음부터 RectTransform으로 만들어 바꿔 끼우는 문제를 피함)
    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)uiObject.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    // 클릭을 막지 않는 단색 이미지 붙이기
    private static Image AddImage(GameObject target, Color color)
    {
        Image image = target.AddComponent<Image>();
        image.sprite = whiteSprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
