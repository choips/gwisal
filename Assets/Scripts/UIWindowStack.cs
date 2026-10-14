using System;
using System.Collections.Generic;
using UnityEngine;

// ESC로 닫을 수 있는 창 (인벤토리·개발자 메뉴·앞으로 생길 스킬 창·상점·지도 등)
public interface IUIWindow
{
    // ESC로 닫을 때 호출됨 → 창이 스스로 닫히고 UIWindowStack.WindowClosed(또는 PopupClosed)를 불러야 함
    void Close();
}

// 열린 창 목록 관리자 — ESC를 누를 때마다 아래 순서로 "한 가지만" 합니다.
//   1. 부적 폭발(Amulet Explosion) 조준 중이면 조준 취소
//   2. 작은 팝업(확인 창·고정된 툴팁 등)이 있으면 가장 마지막에 연 팝업을 닫음
//   3. 열린 창이 있으면 가장 마지막에 연 창 하나를 닫음
//   4. 아무 창도 없으면 ESC 메뉴를 엶 (S7 전까지는 아무것도 안 함)
// 대화창·두루마리 컷신 중(BlockEscape)에는 ESC가 아무것도 하지 않습니다.
// 씬에 붙이지 않아도 플레이를 시작하면 스스로 만들어집니다 (Hierarchy의 DontDestroyOnLoad → [UIWindowStack]).
// 새 창을 만들 때: 패널 오브젝트에 UIWindow 컴포넌트를 붙이거나, IUIWindow를 구현하고 열고 닫을 때 WindowOpened/WindowClosed를 부릅니다.
public class UIWindowStack : MonoBehaviour
{
    private static readonly List<IUIWindow> windows = new List<IUIWindow>(); // 열린 창 (맨 뒤 = 가장 마지막에 연 창)
    private static readonly List<IUIWindow> popups = new List<IUIWindow>();  // 열린 작은 팝업 (맨 뒤 = 가장 마지막에 연 팝업)
    private static readonly List<Func<bool>> aimCancelers = new List<Func<bool>>(); // 조준 취소 함수 (조준 중이었으면 취소하고 true)
    private static readonly HashSet<object> escapeBlockers = new HashSet<object>(); // ESC를 막고 있는 것 (대화창·컷신)

    // 아무 창도 없을 때 ESC로 열 메뉴 (S7에서 ESC 메뉴가 연결함, 비어 있으면 아무것도 안 함)
    public static Action OpenEscapeMenu;

    // 열린 창이 하나라도 있는지
    public static bool HasOpenWindow => windows.Count > 0 || popups.Count > 0;

    // 플레이를 시작할 때마다 목록을 비움 (에디터 설정에 따라 정적 변수가 이전 플레이의 값을 기억할 수 있기 때문)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        windows.Clear();
        popups.Clear();
        aimCancelers.Clear();
        escapeBlockers.Clear();
        OpenEscapeMenu = null;
    }

    // 첫 씬이 열린 직후 ESC를 읽을 오브젝트를 한 번 만듦 (씬이 바뀌어도 유지)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnStart()
    {
        GameObject stackObject = new GameObject("[UIWindowStack]");
        DontDestroyOnLoad(stackObject);
        stackObject.AddComponent<UIWindowStack>();
    }

    // ---------- 창 ----------

    // 창이 열렸을 때 (목록 맨 뒤에 넣음, 이미 있으면 맨 뒤로 옮김)
    public static void WindowOpened(IUIWindow window)
    {
        AddToTop(windows, window);
    }

    // 창이 닫혔을 때 (어떤 방법으로 닫혔든 목록에서 뺌)
    public static void WindowClosed(IUIWindow window)
    {
        windows.Remove(window);
    }

    // ---------- 작은 팝업 (창보다 먼저 닫힘) ----------

    public static void PopupOpened(IUIWindow popup)
    {
        AddToTop(popups, popup);
    }

    public static void PopupClosed(IUIWindow popup)
    {
        popups.Remove(popup);
    }

    // ---------- 조준 취소 (가장 먼저 처리) ----------

    // 조준 중이면 취소하고 true, 조준 중이 아니면 false를 돌려주는 함수를 등록 (PlayerSkills)
    public static void AddAimCanceler(Func<bool> canceler)
    {
        if (canceler != null && !aimCancelers.Contains(canceler)) aimCancelers.Add(canceler);
    }

    public static void RemoveAimCanceler(Func<bool> canceler)
    {
        aimCancelers.Remove(canceler);
    }

    // ---------- 대화창·컷신 중 ESC 막기 ----------

    // owner = 막는 쪽 (대화창 스크립트 등). 여러 곳이 막으면 모두 풀어야 ESC가 다시 동작
    public static void BlockEscape(object owner)
    {
        if (owner != null) escapeBlockers.Add(owner);
    }

    public static void UnblockEscape(object owner)
    {
        if (owner != null) escapeBlockers.Remove(owner);
    }

    // ---------- ESC 처리 ----------

    private void Update()
    {
        if (GameInput.Cancel.WasPressedThisFrame())
        {
            HandleEscape();
        }
    }

    // ESC 한 번 = 아래 순서 중 가장 먼저 해당하는 한 가지만 함
    private static void HandleEscape()
    {
        if (escapeBlockers.Count > 0) return;

        // 1. 조준 취소
        for (int i = 0; i < aimCancelers.Count; i++)
        {
            if (aimCancelers[i]()) return;
        }

        // 2. 작은 팝업 → 3. 창
        if (TryCloseTop(popups)) return;
        if (TryCloseTop(windows)) return;

        // 4. ESC 메뉴
        OpenEscapeMenu?.Invoke();
    }

    // 목록 맨 뒤(가장 마지막에 연 것) 하나를 닫음 (닫을 것이 있었으면 true)
    private static bool TryCloseTop(List<IUIWindow> list)
    {
        while (list.Count > 0)
        {
            int lastIndex = list.Count - 1;
            IUIWindow top = list[lastIndex];

            // 씬 이동 등으로 이미 사라진 창은 건너뜀 (Unity 오브젝트는 파괴되면 == null)
            if (top == null || (top is UnityEngine.Object unityObject && unityObject == null))
            {
                list.RemoveAt(lastIndex);
                continue;
            }

            top.Close();

            // 창이 WindowClosed를 부르지 않았어도 같은 창을 계속 닫으려 하지 않도록 목록에서 뺌
            list.Remove(top);
            return true;
        }
        return false;
    }

    private static void AddToTop(List<IUIWindow> list, IUIWindow window)
    {
        if (window == null) return;
        list.Remove(window);
        list.Add(window);
    }
}
