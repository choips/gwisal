using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 화면 위쪽 가운데 보스 이름 + 큰 체력바 + 2페이즈 문구 (Screen Space Overlay)
// BossEncounter가 Bind/Unbind로 켜고 끕니다.
public class BossHealthBarUI : MonoBehaviour
{
    public static BossHealthBarUI Instance { get; private set; }

    [Header("연결")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image barBackground;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private float smoothSpeed = 8f;

    private Enemy boundEnemy;
    private bool showingPhase2;
    private static Sprite whiteSprite;
    private static readonly Color Phase1Fill = new Color(0.85f, 0.15f, 0.15f, 1f);
    private static readonly Color Phase2Fill = new Color(1f, 0.45f, 0.1f, 1f);

    private void Awake()
    {
        Instance = this;
        if (panelRoot == null) panelRoot = gameObject;
        if (fillImage != null) Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static BossHealthBarUI EnsureExists()
    {
        if (Instance != null)
        {
            if (!Instance.gameObject.activeSelf) Instance.gameObject.SetActive(true);
            return Instance;
        }

        BossHealthBarUI existing = FindFirstObjectByType<BossHealthBarUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            if (!existing.gameObject.activeSelf) existing.gameObject.SetActive(true);
            return existing;
        }

        Canvas canvas = FindOverlayCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("BossHealthBarUI: Screen Space Overlay Canvas가 없어 보스 체력바를 만들 수 없습니다.");
            return null;
        }

        GameObject root = new GameObject("BossHealthBar");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRt = root.AddComponent<RectTransform>();
        BossHealthBarUI ui = root.AddComponent<BossHealthBarUI>();
        ui.BuildRuntimeUi(rootRt);
        ui.Hide();
        Instance = ui;
        return ui;
    }

    private static Canvas FindOverlayCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
        }
        return canvases.Length > 0 ? canvases[0] : null;
    }

    private void BuildRuntimeUi(RectTransform rootRt)
    {
        panelRoot = gameObject;

        // 이름 + 바 + 2페이즈 문구가 들어가도록 여유 있게
        rootRt.anchorMin = new Vector2(0.18f, 0.84f);
        rootRt.anchorMax = new Vector2(0.82f, 0.98f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Sprite sprite = GetWhiteSprite();
        TMP_FontAsset font = FindKoreanFont();

        nameText = CreateLabel("Name", new Vector2(0f, 0.62f), new Vector2(1f, 1f), 26f, new Color(1f, 0.92f, 0.55f, 1f), font);
        phaseText = CreateLabel("Phase", new Vector2(0f, 0f), new Vector2(1f, 0.22f), 18f, new Color(1f, 0.55f, 0.2f, 1f), font);
        phaseText.gameObject.SetActive(false);

        GameObject bgGo = new GameObject("BarBg");
        bgGo.transform.SetParent(transform, false);
        RectTransform bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.28f);
        bgRt.anchorMax = new Vector2(1f, 0.58f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        barBackground = bgGo.AddComponent<Image>();
        barBackground.sprite = sprite;
        barBackground.color = new Color(0.12f, 0.08f, 0.08f, 0.92f);
        barBackground.raycastTarget = false;

        GameObject fillGo = new GameObject("BarFill");
        fillGo.transform.SetParent(bgGo.transform, false);
        RectTransform fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(3f, 3f);
        fillRt.offsetMax = new Vector2(-3f, -3f);
        fillImage = fillGo.AddComponent<Image>();
        fillImage.sprite = sprite;
        fillImage.color = Phase1Fill;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;
        fillImage.raycastTarget = false;
    }

    private TMP_Text CreateLabel(string objectName, Vector2 anchorMin, Vector2 anchorMax, float fontSize, Color color, TMP_FontAsset font)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        if (font != null) tmp.font = font;
        return tmp;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        Texture2D tex = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return whiteSprite;
    }

    // 한글이 있는 글꼴을 우선 찾음 (기본 LiberationSans면 산군이 안 보임)
    private static TMP_FontAsset FindKoreanFont()
    {
        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TMP_Text t in texts)
        {
            if (t == null || t.font == null) continue;
            string n = t.font.name;
            if (n.IndexOf("Noto", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("KR", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Korean", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return t.font;
            }
        }

        foreach (TMP_Text t in texts)
        {
            if (t != null && t.font != null) return t.font;
        }

        return null;
    }

    public void Bind(Enemy enemy)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        boundEnemy = enemy;
        showingPhase2 = false;

        ApplyFontIfNeeded(nameText);
        ApplyFontIfNeeded(phaseText);

        if (nameText != null && enemy != null && enemy.Data != null)
        {
            // String Table 우선, 표에 없으면 displayName (한글 글꼴·키 누락 대비)
            string loc = enemy.Data.LocalizedName;
            nameText.text = (!string.IsNullOrEmpty(loc) && !loc.Contains("No translation"))
                ? loc
                : enemy.Data.displayName;
            nameText.ForceMeshUpdate();
        }

        if (phaseText != null)
        {
            phaseText.text = Loc.Get("boss.phase2");
            phaseText.gameObject.SetActive(false);
        }

        if (fillImage != null)
        {
            fillImage.color = Phase1Fill;
            if (enemy != null) fillImage.fillAmount = enemy.HealthRatio;
        }

        if (panelRoot != null) panelRoot.SetActive(true);
        enabled = true;
    }

    private static void ApplyFontIfNeeded(TMP_Text text)
    {
        if (text == null) return;
        if (text.font != null)
        {
            string n = text.font.name;
            if (n.IndexOf("Noto", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("KR", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return;
        }

        TMP_FontAsset font = FindKoreanFont();
        if (font != null) text.font = font;
    }

    public void Unbind()
    {
        boundEnemy = null;
        showingPhase2 = false;
        Hide();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // SangunBossAI 등에서 2페이즈 진입 시 호출
    public void SetPhase2(bool active)
    {
        showingPhase2 = active;
        if (phaseText != null)
        {
            ApplyFontIfNeeded(phaseText);
            if (string.IsNullOrEmpty(phaseText.text) || phaseText.text.Contains("No translation"))
                phaseText.text = "2페이즈 (광폭화)";
            phaseText.gameObject.SetActive(active);
        }
        if (fillImage != null) fillImage.color = active ? Phase2Fill : Phase1Fill;
    }

    private void Update()
    {
        if (boundEnemy == null || boundEnemy.IsDead)
        {
            if (boundEnemy != null && boundEnemy.IsDead) Unbind();
            return;
        }

        if (fillImage != null)
        {
            float ratio = boundEnemy.HealthRatio;
            fillImage.fillAmount = smoothSpeed > 0f
                ? Mathf.Lerp(fillImage.fillAmount, ratio, smoothSpeed * Time.deltaTime)
                : ratio;
        }

        // AI가 SetPhase2를 못 불렀을 때를 대비해 체력 비율로도 표시
        if (!showingPhase2 && boundEnemy.TryGetComponent(out SangunBossAI ai) && ai.IsPhase2)
        {
            SetPhase2(true);
        }
    }
}
