using UnityEngine;
using UnityEngine.UI;   // CanvasScaler, GraphicRaycaster 사용

// 월드 위치를 따라다니는 화면 UI를 그리는 캔버스 (바닥 아이템 이름표, 나중에 귀신 체력바·데미지 숫자도 여기에)
// 씬에 놓지 않아도 처음 쓸 때 스스로 만들어집니다 (Hierarchy의 DontDestroyOnLoad → [WorldUI]).
// 인벤토리·툴팁 같은 다른 UI보다 뒤에 그려지고, 클릭도 다른 UI가 먼저 받습니다 (Sort Order가 더 낮음).
// 크기 기준은 Core 씬 Canvas와 같은 1920×1080이라, 화면이 커지면 함께 커집니다.
public class WorldUICanvas : MonoBehaviour
{
    private const int SortingOrder = -10;                                   // 다른 Canvas(0)보다 뒤에 그림
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f); // Core 씬 Canvas와 같은 기준 해상도

    private static WorldUICanvas instance;

    private Canvas canvas;
    private Camera mainCamera;

    // 어디서든 WorldUICanvas.Instance로 접근 (없으면 만듦)
    public static WorldUICanvas Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject canvasObject = new GameObject("[WorldUI]");
                DontDestroyOnLoad(canvasObject);
                instance = canvasObject.AddComponent<WorldUICanvas>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>(); // 이름표 클릭·마우스 올리기를 받기 위해 필요
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // 화면 전체를 덮는 층(빈 RectTransform)을 하나 만듦 — 먼저 만든 층이 뒤에 그려짐
    // 층 안의 UI는 왼쪽 아래 (0, 0) 기준 좌표로 놓습니다 (TryWorldToCanvas가 돌려주는 좌표)
    public RectTransform CreateLayer(string layerName)
    {
        GameObject layerObject = new GameObject(layerName, typeof(RectTransform));
        RectTransform layer = (RectTransform)layerObject.transform;
        layer.SetParent(transform, false);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.pivot = Vector2.zero;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        return layer;
    }

    // 월드 위치를 캔버스 좌표(왼쪽 아래 0, 기준 해상도 단위)로 바꿈 — 카메라 뒤쪽이면 false
    public bool TryWorldToCanvas(Vector3 worldPosition, out Vector2 canvasPosition)
    {
        canvasPosition = Vector2.zero;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return false;

        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z <= 0f) return false;

        canvasPosition = new Vector2(screenPoint.x, screenPoint.y) / canvas.scaleFactor;
        return true;
    }

    // 캔버스 전체 크기 (기준 해상도 단위)
    public Vector2 CanvasSize => new Vector2(Screen.width, Screen.height) / canvas.scaleFactor;
}
