using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;   // 새 Input System (Mouse.current) 사용
using UnityEngine.UI;            // Image 사용

// 아이템 툴팁 (인벤토리의 아이템 버튼에 마우스를 올리면 나타나는 설명 창)
// 아이템 이름·등급·능력치를 보여 주고, 지금 장착 중인 장비와 비교해서
// 장착하면 능력치가 얼마나 오르는지(초록 ▲) / 내리는지(빨강 ▼)를 표시합니다.
// 창 크기는 글 길이에 맞춰 자동으로 바뀌고, 마우스를 따라다니며 화면 밖으로 나가지 않습니다.
[RequireComponent(typeof(RectTransform))]
public class ItemTooltip : MonoBehaviour
{
    [Header("UI 연결")]
    [Tooltip("툴팁 내용을 표시할 텍스트 (이 오브젝트의 자식)")]
    [SerializeField] private TextMeshProUGUI contentText;

    [Header("크기 / 위치")]
    [Tooltip("글이 이 너비(픽셀, 1920×1080 기준)를 넘으면 다음 줄로 넘어감")]
    [SerializeField] private float maxTextWidth = 360f;

    [Tooltip("글과 창 테두리 사이의 여백")]
    [SerializeField] private Vector2 padding = new Vector2(16f, 12f);

    [Tooltip("마우스 커서에서 떨어진 거리 (커서가 글을 가리지 않도록)")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(20f, 20f);

    [Header("비교 색")]
    [SerializeField] private Color betterColor = new Color(0.3f, 1f, 0.3f);   // 더 좋아짐: 초록
    [SerializeField] private Color worseColor = new Color(1f, 0.35f, 0.35f);  // 나빠짐: 빨강
    [SerializeField] private Color sameColor = new Color(0.7f, 0.7f, 0.7f);   // 같음: 회색
    [SerializeField] private Color hintColor = new Color(0.6f, 0.6f, 0.6f);   // 안내 문구: 회색

    private RectTransform panelRect;  // 툴팁 창(이 오브젝트)의 RectTransform
    private Canvas rootCanvas;        // 화면 크기 배율(Canvas Scaler)을 알기 위한 최상위 캔버스
    private PlayerStats playerStats;  // "현재 → 장착 후" 능력치 계산용
    private ItemData shownItem;       // 지금 툴팁에 표시 중인 아이템

    private void Awake()
    {
        panelRect = GetComponent<RectTransform>();

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        rootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;

        // 툴팁이 마우스 클릭/올리기를 가로채면, 아래 버튼에서 마우스가 "나간" 것으로 판정되어 깜빡이므로 꺼 둠
        if (TryGetComponent(out Image background))
        {
            background.raycastTarget = false;
        }

        if (contentText != null)
        {
            contentText.raycastTarget = false;
            contentText.richText = true;                          // <color> 같은 색 태그 사용
            contentText.alignment = TextAlignmentOptions.TopLeft;
            contentText.textWrappingMode = TextWrappingModes.Normal; // 너비를 넘으면 줄바꿈

            // 글 영역을 창 안쪽에 여백만큼 띄워서 꽉 채움
            RectTransform textRect = contentText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = padding;
            textRect.offsetMax = -padding;
        }
        else
        {
            Debug.LogWarning("ItemTooltip에 Content Text가 연결되지 않았습니다.");
        }

        // Show보다 Awake가 늦게 불린 경우(처음에 꺼져 있던 경우)에는 이미 표시할 아이템이 있으므로 숨기지 않음
        if (shownItem == null)
        {
            gameObject.SetActive(false);
        }
    }

    // 아이템 툴팁 표시 (아이템 버튼에 마우스가 올라갔을 때 호출)
    // isEquipped: 장비 칸의 장착 중인 아이템이면 true (비교 대신 "해제하면 바뀌는 능력치"를 보여 줌)
    public void Show(ItemData item, bool isEquipped = false)
    {
        if (item == null) return;

        shownItem = item;
        gameObject.SetActive(true); // 처음 켜질 때는 여기서 Awake가 먼저 실행됨
        if (contentText == null) return;

        string content = BuildContent(item, isEquipped);
        contentText.text = content;

        // 글 길이에 맞춰 창 크기 조절 (너비는 최대 maxTextWidth까지, 넘으면 줄바꿈되어 높이가 늘어남)
        Vector2 textSize = contentText.GetPreferredValues(content, maxTextWidth, 0f);
        textSize.x = Mathf.Min(textSize.x, maxTextWidth);
        panelRect.sizeDelta = textSize + padding * 2f;

        FollowMouse();
    }

    // 툴팁 숨기기 (마우스가 버튼에서 나갔을 때 호출)
    // item을 넘기면, 그 아이템을 보여 주고 있을 때만 숨김 (다른 버튼으로 옮겨 간 뒤 늦게 온 "나감" 신호 무시)
    public void Hide(ItemData item = null)
    {
        if (item != null && item != shownItem) return;

        shownItem = null;
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        FollowMouse();
    }

    // 마우스 커서 오른쪽 아래에 창을 두되, 화면 밖으로 나가면 반대쪽(왼쪽/위쪽)으로 뒤집음
    private void FollowMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mousePosition = mouse.position.ReadValue();

        // Canvas Scaler 때문에 실제 화면에서는 창이 scaleFactor배 크기로 그려짐
        float scale = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
        Vector2 sizeOnScreen = panelRect.sizeDelta * scale;
        Vector2 offsetOnScreen = cursorOffset * scale;

        bool flipX = mousePosition.x + offsetOnScreen.x + sizeOnScreen.x > Screen.width; // 오른쪽이 넘침 → 왼쪽에 표시
        bool flipY = mousePosition.y - offsetOnScreen.y - sizeOnScreen.y < 0f;           // 아래쪽이 넘침 → 위쪽에 표시

        // pivot(기준점): 창의 어느 모서리를 커서 쪽에 붙일지 (0,1 = 왼쪽 위 모서리)
        panelRect.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);

        Vector2 offset = new Vector2(
            flipX ? -offsetOnScreen.x : offsetOnScreen.x,
            flipY ? offsetOnScreen.y : -offsetOnScreen.y);

        // Screen Space - Overlay 캔버스에서는 화면 픽셀 좌표를 그대로 위치로 쓸 수 있음
        panelRect.position = mousePosition + offset;
    }

    // 툴팁에 들어갈 글 만들기 (TextMeshPro 서식 태그 사용)
    private string BuildContent(ItemData item, bool isEquipped)
    {
        string rarityHex = ColorUtility.ToHtmlStringRGB(item.RarityColor);
        string hintHex = ColorUtility.ToHtmlStringRGB(hintColor);

        string body = isEquipped ? BuildUnequipPreview(item) : BuildComparison(item);
        string hint = isEquipped ? "클릭하여 해제" : "클릭하여 장착";

        return $"<size=125%><color=#{rarityHex}>{item.itemName}</color></size>\n"
             + $"<color=#{rarityHex}>{item.RarityName}</color> {item.TypeName}\n"
             + $"\n{item.StatDescription}\n"
             + $"\n{body}\n"
             + $"\n<color=#{hintHex}>{hint}</color>";
    }

    // 가방 아이템: 같은 종류의 장착 중인 장비와 비교한 결과
    private string BuildComparison(ItemData item)
    {
        ItemData equipped = GetEquippedItem(item.itemType);
        float equippedValue = equipped != null ? equipped.statValue : 0f;
        float difference = item.statValue - equippedValue; // 장착하면 바뀌는 능력치 양

        string hintHex = ColorUtility.ToHtmlStringRGB(hintColor);
        string equippedLine = equipped != null
            ? $"<color=#{hintHex}>장착 중:</color> <color=#{ColorUtility.ToHtmlStringRGB(equipped.RarityColor)}>{equipped.itemName}</color> ({equipped.StatDescription})"
            : $"<color=#{hintHex}>장착 중인 {item.TypeName} 없음</color>";

        return $"{equippedLine}\n{BuildDifference(item, difference)}";
    }

    // 장착 중인 아이템: 해제하면 줄어드는 능력치
    private string BuildUnequipPreview(ItemData item)
    {
        string hintHex = ColorUtility.ToHtmlStringRGB(hintColor);
        return $"<color=#{hintHex}>해제하면</color>\n{BuildDifference(item, -item.statValue)}";
    }

    // 능력치 변화량 한 줄(▲ 초록 / ▼ 빨강 / 변화 없음)과 "현재 → 변경 후" 한 줄
    private string BuildDifference(ItemData item, float difference)
    {
        string differenceLine;
        if (difference > 0f)
        {
            differenceLine = $"<color=#{ColorUtility.ToHtmlStringRGB(betterColor)}>▲ {item.StatName} +{FormatNumber(difference)}</color>";
        }
        else if (difference < 0f)
        {
            differenceLine = $"<color=#{ColorUtility.ToHtmlStringRGB(worseColor)}>▼ {item.StatName} {FormatNumber(difference)}</color>";
        }
        else
        {
            differenceLine = $"<color=#{ColorUtility.ToHtmlStringRGB(sameColor)}>변화 없음</color>";
        }

        // 지금 능력치와 바뀐 뒤 능력치 (예: "공격력 15 → 20")
        PlayerStats stats = GetPlayerStats();
        if (stats != null)
        {
            float current = item.itemType == ItemType.Weapon ? stats.attackPower : stats.maxHP;
            differenceLine += $"\n{item.StatName} {FormatNumber(current)} → {FormatNumber(current + difference)}";
        }

        return differenceLine;
    }

    // 종류에 맞는 장착 중인 장비 (없으면 null)
    private static ItemData GetEquippedItem(ItemType type)
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null) return null;

        return type == ItemType.Weapon ? inventory.EquippedWeapon : inventory.EquippedArmor;
    }

    private PlayerStats GetPlayerStats()
    {
        if (playerStats == null)
        {
            playerStats = FindFirstObjectByType<PlayerStats>();
        }
        return playerStats;
    }

    // 숫자 표시: 정수면 소수점 없이, 아니면 소수점 한 자리까지 (예: 10, 7.5)
    private static string FormatNumber(float value)
    {
        return value.ToString("0.#");
    }
}
