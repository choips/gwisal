#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// 개발자 메뉴 (F1로 열고 닫기) — 에디터와 개발 빌드(Development Build)에만 포함되고, 정식 출시 빌드에서는 통째로 빠집니다.
// 씬에 붙이지 않아도 플레이를 시작하면 스스로 만들어집니다 (Hierarchy의 DontDestroyOnLoad → [DevMenu]).
// 화면 왼쪽 위 창에서 무적·기력 무한·쿨타임 없음을 켜고 끕니다 (켠 값은 DevCheats에 저장).
// Unity 기본 즉석 UI(IMGUI, OnGUI)로 그려서 Canvas·프리팹 설정이 필요 없습니다. 마우스로 조작합니다.
public class DevMenu : MonoBehaviour
{
    private const float ReferenceScreenHeight = 1080f; // 이 화면 높이에서 창이 아래 크기 그대로 보임 (화면이 크면 함께 커짐)
    private static readonly Rect WindowRect = new Rect(10f, 10f, 280f, 130f); // 기준 화면에서 창의 위치·크기

    private bool isOpen;     // 메뉴가 열려 있는지
    private float guiScale = 1f; // 화면 크기에 맞춘 창 확대 배율

    // 첫 씬이 열린 직후 메뉴 오브젝트를 한 번 만듦 (씬이 바뀌어도 유지)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnStart()
    {
        GameObject menuObject = new GameObject("[DevMenu]");
        DontDestroyOnLoad(menuObject);
        menuObject.AddComponent<DevMenu>();
    }

    private void Update()
    {
        if (GameInput.DevMenu.WasPressedThisFrame())
        {
            isOpen = !isOpen;
        }

        // 메뉴 창 영역을 알려 둠 → 메뉴를 클릭할 때 뒤쪽 바닥으로 이동하거나 스킬이 나가지 않음
        if (isOpen)
        {
            guiScale = Mathf.Max(1f, Screen.height / ReferenceScreenHeight);
            DevCheats.MenuScreenRect = new Rect(WindowRect.x * guiScale, WindowRect.y * guiScale,
                WindowRect.width * guiScale, WindowRect.height * guiScale);
        }
        else
        {
            DevCheats.MenuScreenRect = Rect.zero;
        }
    }

    private void OnDisable()
    {
        DevCheats.MenuScreenRect = Rect.zero;
    }

    // 화면에 메뉴 창을 그림 (Unity가 화면을 그릴 때마다 호출)
    private void OnGUI()
    {
        if (!isOpen) return;

        GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));

        GUILayout.BeginArea(WindowRect, "개발자 메뉴 (F1)", GUI.skin.window);
        DevCheats.GodMode = GUILayout.Toggle(DevCheats.GodMode, " 무적 (피해를 받지 않음)");
        DevCheats.InfiniteMana = GUILayout.Toggle(DevCheats.InfiniteMana, " 기력 무한");
        DevCheats.NoCooldown = GUILayout.Toggle(DevCheats.NoCooldown, " 쿨타임 없음");
        GUILayout.EndArea();
    }
}
#endif
