using UnityEngine;

// 창 패널 오브젝트에 붙이면 ESC로 닫을 수 있게 되는 컴포넌트
// 패널이 켜지면(SetActive(true)) 열린 창 목록(UIWindowStack) 맨 뒤에 들어가고, 꺼지면 빠집니다.
// ESC로 닫을 때는 패널을 끕니다(SetActive(false)). 단축키(I 등)로 켜고 끄는 코드는 그대로 두면 됩니다.
// 인벤토리 패널에는 InventoryManager가 플레이 시작 때 스스로 붙이므로 직접 붙일 필요가 없습니다.
public class UIWindow : MonoBehaviour, IUIWindow
{
    [Tooltip("켜면 '작은 팝업'(확인 창·고정된 툴팁 등)으로 취급해 일반 창보다 먼저 ESC로 닫힙니다")]
    [SerializeField] private bool isPopup = false; // 작은 팝업인지

    private void OnEnable()
    {
        if (isPopup) UIWindowStack.PopupOpened(this);
        else UIWindowStack.WindowOpened(this);
    }

    private void OnDisable()
    {
        if (isPopup) UIWindowStack.PopupClosed(this);
        else UIWindowStack.WindowClosed(this);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
