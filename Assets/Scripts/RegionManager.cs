using System;
using System.Collections;
using TMPro;                        // 지역 이름 글자 (TextMeshPro)
using UnityEngine;
using UnityEngine.AI;               // NavMeshAgent 사용 (지역 입구로 순간이동)
using UnityEngine.SceneManagement;  // 씬 추가 로드·내리기
using UnityEngine.UI;               // 화면을 덮는 검은 그림(Image)

// 지역 관리자 (Core 씬에 하나만 둠)
// 게임은 "Core 씬(플레이어·카메라·UI·관리자) + 지역 씬 하나(땅·NavMesh·조명·몬스터 배치기)"로 돌아갑니다.
// 지역을 옮길 때는 지금 지역 씬만 내리고 새 지역 씬을 추가로 불러오므로,
// Core에 있는 플레이어의 체력·기력·레벨·경험치·인벤토리·장비는 그대로 유지됩니다.
public class RegionManager : MonoBehaviour
{
    public static RegionManager Instance { get; private set; }

    // 지역을 불러오는 중인지 (불러오는 동안 다른 스크립트는 플레이어·NavMesh를 쓰지 않고 기다림)
    public static bool IsBusy => Instance != null && Instance.IsLoading;

    // 에디터에서 지역 씬을 열고 Play 했을 때 시작 지역 대신 불러올 씬 이름을 담는 칸 (Editor/RegionPlayModeStarter가 씀)
    public const string EditorStartRegionKey = "Gwisal.EditorStartRegion";

    [Header("시작 지역")]
    [Tooltip("게임을 시작하면 불러올 지역 씬 이름 (File > Build Profiles의 Scene List에 들어 있어야 합니다)")]
    [SerializeField] private string startRegionScene = "Region_01";

    [Tooltip("시작 지역에서 플레이어가 설 입구 ID (지역 씬의 RegionEntry와 같은 글자)")]
    [SerializeField] private string startEntryId = "start";

    [Header("화면 전환")]
    [Tooltip("지역을 옮길 때 화면이 어두워지고 다시 밝아지는 시간 (초)")]
    [Min(0f)] [SerializeField] private float fadeDuration = 0.4f;

    [Header("지역 이름 (도착하면 화면 가운데에 크게 표시)")]
    [Tooltip("한글 글꼴 (NotoSansKR-Regular SDF). 비워 두면 지역 이름을 알림 문구로만 띄움")]
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private float titleFontSize = 64f;
    [SerializeField] private Color titleColor = new Color(0.95f, 0.9f, 0.8f); // 한지 빛 미색
    [Tooltip("또렷하게 보이는 시간 (초)")]
    [Min(0f)] [SerializeField] private float titleShowTime = 2f;
    [Tooltip("나타나고 사라지는 데 걸리는 시간 (초)")]
    [Min(0f)] [SerializeField] private float titleFadeTime = 0.6f;

    [Header("상태 (Play 중 확인용)")]
    [Tooltip("지금 불러와 있는 지역 씬 이름")]
    [SerializeField] private string currentRegionScene;

    public string CurrentRegionScene => currentRegionScene;
    public bool IsLoading { get; private set; }

    // 새 지역에 도착해 플레이어를 입구에 세운 직후 알림 (인자: 지역 씬 이름)
    public event Action<string> RegionLoaded;

    private PlayerStats player;
    private PlayerController playerController;
    private NavMeshAgent playerAgent;
    private CameraFollow cameraFollow;

    private Image fadeImage;             // 화면 전체를 덮는 검은 그림 (투명도로 어둡게/밝게)
    private TextMeshProUGUI titleText;   // 도착한 지역 이름
    private Coroutine titleRoutine;      // 지역 이름이 나타났다 사라지는 중인 코루틴

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("RegionManager가 두 개 있습니다. 나중에 생긴 것을 지웁니다.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        player = FindFirstObjectByType<PlayerStats>();
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            playerAgent = player.GetComponent<NavMeshAgent>();
        }
        else
        {
            Debug.LogWarning("RegionManager: 씬에서 Player(PlayerStats)를 찾지 못했습니다.");
        }

        if (Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();

        CreateOverlay();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 첫 지역이 열리기 전에는 밟을 NavMesh가 없으므로, 첫 Update 전에 조작을 꺼 두고 시작 지역을 불러옴
    // (모든 오브젝트의 Awake가 끝난 뒤인 Start에서 꺼야 스킬 취소 등이 안전하게 동작함)
    private void Start()
    {
        string regionScene = startRegionScene;

#if UNITY_EDITOR
        // 지역 씬을 열어 둔 채 Play 했으면 그 지역부터 시작 (게임 빌드에서는 항상 Start Region Scene)
        string editorRegion = UnityEditor.SessionState.GetString(EditorStartRegionKey, "");
        if (!string.IsNullOrEmpty(editorRegion))
        {
            regionScene = editorRegion;
            UnityEditor.SessionState.EraseString(EditorStartRegionKey);
        }
#endif

        StartCoroutine(LoadRegionRoutine(regionScene, startEntryId));
    }

    // 다른 지역으로 이동 (지역 출구 등에서 호출)
    public void TravelTo(string sceneName, string entryId)
    {
        if (IsLoading)
        {
            Debug.LogWarning($"RegionManager: 지역을 불러오는 중이라 '{sceneName}'(으)로 이동하지 않습니다.");
            return;
        }
        StartCoroutine(LoadRegionRoutine(sceneName, entryId));
    }

    private IEnumerator LoadRegionRoutine(string sceneName, string entryId)
    {
        // 갈 수 없는 씬이면 지금 지역을 내리기 전에 멈춤 (땅 없는 곳에 갇히지 않도록)
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"RegionManager: 지역 씬 '{sceneName}'을(를) 불러올 수 없습니다. " +
                           "File > Build Profiles의 Scene List에 그 씬이 들어 있는지, 이름 철자가 맞는지 확인해 주세요.");
            SetFadeAlpha(0f);
            yield break;
        }

        IsLoading = true;
        SetPlayerControl(false);

        // 한 프레임 기다려 모든 오브젝트의 Start(카메라 거리 계산 등)가 끝난 뒤에 진행
        yield return null;

        // 화면을 어둡게 (게임 시작 때는 이미 검은 화면)
        yield return FadeTo(1f);

        // 1) 지금 지역 내리기 (그 지역의 몬스터·떨어진 아이템도 함께 사라짐)
        if (!string.IsNullOrEmpty(currentRegionScene))
        {
            Scene oldScene = SceneManager.GetSceneByName(currentRegionScene);
            if (oldScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(oldScene);
            }
            currentRegionScene = null;
        }

        // 2) 새 지역 불러오기 (에디터에서 미리 함께 열어 둔 경우에는 그대로 사용)
        Scene newScene = SceneManager.GetSceneByName(sceneName);
        if (!newScene.isLoaded)
        {
            AsyncOperation loading = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (loading == null)
            {
                Debug.LogError($"RegionManager: 지역 씬 '{sceneName}'을(를) 불러오지 못했습니다.");
                IsLoading = false;
                SetFadeAlpha(0f);
                yield break;
            }
            yield return loading;
            newScene = SceneManager.GetSceneByName(sceneName);
        }

        // 3) 새 지역을 "활성 씬"으로: 조명 설정을 이 씬 것으로 쓰고, 새로 생기는 몬스터·아이템도 이 씬에 들어감
        SceneManager.SetActiveScene(newScene);
        currentRegionScene = sceneName;

        // 4) 플레이어를 입구에 세움 (부활 위치도 이 입구로 바꿈)
        PlacePlayer(newScene, entryId);

        IsLoading = false;
        SetPlayerControl(true);
        GameLog.Region($"지역 '{sceneName}'에 도착했습니다. (입구: {entryId})");
        RegionLoaded?.Invoke(sceneName);

        // 5) 화면을 밝히면서 지역 이름 표시
        ShowRegionTitle(sceneName);
        yield return FadeTo(0f);
    }

    // 화면 전환용 검은 그림과 지역 이름 글자를 코드로 만듦 (다른 UI보다 위에 그려지는 전용 캔버스)
    private void CreateOverlay()
    {
        GameObject canvasObject = new GameObject("[RegionOverlay]");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // 인벤토리·사망 화면보다 위

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 화면 전체를 덮는 검은 그림 (게임 시작 때는 검은 화면에서 밝아지도록 불투명으로 시작)
        fadeImage = CreateUIChild<Image>(canvasObject.transform, "Fade", Vector2.zero, Vector2.one);
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;

        // 화면 위쪽 1/3 부근에 크게 뜨는 지역 이름
        if (titleFont != null)
        {
            titleText = CreateUIChild<TextMeshProUGUI>(canvasObject.transform, "RegionTitle", new Vector2(0f, 0.62f), new Vector2(1f, 0.78f));
            titleText.font = titleFont;
            titleText.fontSize = titleFontSize;
            titleText.color = titleColor;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            titleText.raycastTarget = false;
            titleText.text = "";
            titleText.alpha = 0f;
        }
    }

    // 캔버스 아래에 UI 오브젝트 하나 만들기 (anchorMin~anchorMax 비율 영역을 꽉 채움)
    private static T CreateUIChild<T>(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax) where T : Component
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return child.AddComponent<T>();
    }

    // 검은 그림의 투명도를 targetAlpha까지 서서히 바꿈 (일시 정지 중에도 동작하도록 실제 시간 사용)
    private IEnumerator FadeTo(float targetAlpha)
    {
        if (fadeImage == null) yield break;

        float startAlpha = fadeImage.color.a;
        if (fadeDuration <= 0f || Mathf.Approximately(startAlpha, targetAlpha))
        {
            SetFadeAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetFadeAlpha(Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration));
            yield return null;
        }
        SetFadeAlpha(targetAlpha);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null) return;
        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }

    // 도착한 지역 이름 표시 (이름은 화면 글자 표의 region.name.씬이름)
    private void ShowRegionTitle(string sceneName)
    {
        string regionName = Loc.Get("region.name." + sceneName);

        if (titleText == null)
        {
            NotificationUI.Show(regionName);
            return;
        }

        titleText.text = regionName;
        if (titleRoutine != null) StopCoroutine(titleRoutine);
        titleRoutine = StartCoroutine(TitleRoutine());
    }

    // 지역 이름: 서서히 나타남 → 잠시 머묾 → 서서히 사라짐
    private IEnumerator TitleRoutine()
    {
        yield return FadeTitle(0f, 1f);

        float waited = 0f;
        while (waited < titleShowTime)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return FadeTitle(1f, 0f);
        titleRoutine = null;
    }

    private IEnumerator FadeTitle(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < titleFadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            titleText.alpha = Mathf.Lerp(from, to, elapsed / titleFadeTime);
            yield return null;
        }
        titleText.alpha = to;
    }

    private void PlacePlayer(Scene scene, string entryId)
    {
        if (player == null) return;

        RegionEntry entry = RegionEntry.Find(scene, entryId);
        if (entry == null)
        {
            Debug.LogWarning($"RegionManager: '{scene.name}' 씬에 RegionEntry가 없어 플레이어를 지금 자리에 둡니다. " +
                             "빈 오브젝트에 RegionEntry를 붙여 입구를 만들어 주세요.");
            return;
        }

        Vector3 position = entry.transform.position;
        Quaternion rotation = Quaternion.Euler(0f, entry.transform.eulerAngles.y, 0f);

        // 입구 바로 아래의 NavMesh 위 지점으로 맞춤 (입구가 살짝 떠 있어도 괜찮게)
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            position = hit.position;
        }
        else
        {
            Debug.LogWarning($"RegionManager: 입구 '{entry.EntryId}' 근처(3m)에 NavMesh가 없습니다. 입구를 땅 위로 옮기거나 NavMesh를 다시 Bake해 주세요.");
        }

        player.transform.SetPositionAndRotation(position, rotation);
        player.SetSpawnPoint(position, rotation);

        if (cameraFollow != null) cameraFollow.SnapToTarget();
    }

    // 플레이어 조작·이동 켜고 끄기 (지역을 불러오는 동안에는 NavMesh가 없어서 꺼 둠)
    private void SetPlayerControl(bool enable)
    {
        if (playerAgent != null)
        {
            playerAgent.enabled = enable;
            // 켜는 순간 NavMesh 위에 다시 올려 둠 (꺼져 있는 동안 옮긴 위치로)
            if (enable && playerAgent.isOnNavMesh) playerAgent.Warp(player.transform.position);
        }

        // 죽어 있는 동안에는 조작을 다시 켜지 않음 (부활하기 버튼이 켬)
        if (playerController != null)
        {
            playerController.enabled = enable && (player == null || !player.IsDead);
        }
    }
}
