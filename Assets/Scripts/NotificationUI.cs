using TMPro;                // TextMeshProUGUI 사용
using UnityEngine;

// 화면 위쪽 가운데에 잠깐 떴다가 서서히 사라지는 알림 문구 ("가방이 가득 찼습니다!", "게임 저장 완료!" 등)
// Canvas 안의 TextMeshPro 텍스트(NoticeText)에 붙이고, 어디서든 NotificationUI.Show("문구")로 띄웁니다.
// 새 알림이 오면 이전 알림을 바로 바꿔 끼웁니다.
[RequireComponent(typeof(TextMeshProUGUI))]
public class NotificationUI : MonoBehaviour
{
    // 알림 종류별 글자 색: 일반(흰색) / 성공(초록) / 경고(빨강)
    public enum NoticeType { Info, Success, Warning }

    // 씬에 하나만 있는 알림 창에 접근하기 위한 전역 참조
    public static NotificationUI Instance { get; private set; }

    [Header("시간")]
    [Tooltip("알림이 또렷하게 보이는 시간 (초)")]
    [SerializeField] private float displayTime = 1.5f;

    [Tooltip("서서히 사라지는 시간 (초)")]
    [SerializeField] private float fadeTime = 0.5f;

    [Tooltip("사라지면서 위로 떠오르는 거리 (픽셀)")]
    [SerializeField] private float riseDistance = 20f;

    [Header("글자 색")]
    [SerializeField] private Color infoColor = Color.white;
    [SerializeField] private Color successColor = new Color(0.55f, 1f, 0.55f);   // 연두색
    [SerializeField] private Color warningColor = new Color(1f, 0.45f, 0.35f);   // 주황빛 빨강

    private TextMeshProUGUI noticeText;
    private RectTransform rect;
    private Vector2 basePosition;   // 처음 배치한 위치 (떠오르기 전)
    private float remainingTime;    // 남은 표시 시간 (0 이하이면 숨김 상태)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("NotificationUI가 씬에 2개 이상 있어 중복된 것을 제거합니다.");
            Destroy(this);
            return;
        }
        Instance = this;

        noticeText = GetComponent<TextMeshProUGUI>();
        rect = (RectTransform)transform;
        basePosition = rect.anchoredPosition;

        noticeText.raycastTarget = false; // 알림 글자가 바닥·적 클릭을 가로막지 않도록
        noticeText.text = "";
        SetAlpha(0f);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 알림 띄우기 (Console에도 같은 문구를 남김). 씬에 알림 창이 없으면 Console에만 표시
    public static void Show(string message, NoticeType type = NoticeType.Info)
    {
        Debug.Log(message);

        if (Instance != null)
        {
            Instance.Display(message, type);
        }
    }

    private void Display(string message, NoticeType type)
    {
        noticeText.text = message;
        noticeText.color = GetColor(type);
        rect.anchoredPosition = basePosition;
        remainingTime = displayTime + fadeTime;
    }

    private void Update()
    {
        if (remainingTime <= 0f) return;

        // 게임이 일시 정지(Time.timeScale = 0)되어도 알림은 사라지도록 실제 시간 사용
        remainingTime -= Time.unscaledDeltaTime;

        // 마지막 fadeTime초 동안 투명해지면서 살짝 위로 떠오름
        float alpha = fadeTime > 0f ? Mathf.Clamp01(remainingTime / fadeTime) : (remainingTime > 0f ? 1f : 0f);
        SetAlpha(alpha);
        rect.anchoredPosition = basePosition + Vector2.up * riseDistance * (1f - alpha);
    }

    private Color GetColor(NoticeType type)
    {
        switch (type)
        {
            case NoticeType.Success: return successColor;
            case NoticeType.Warning: return warningColor;
            default: return infoColor;
        }
    }

    private void SetAlpha(float alpha)
    {
        Color color = noticeText.color;
        color.a = alpha;
        noticeText.color = color;
    }
}
