using UnityEngine;

// 기본 아이템 한 종류의 데이터 파일 (ScriptableObject — Project 창에서 Create > 귀살 > Item Base로 만듦)
// "낡은 단검", "무명 도포"처럼 아이템의 바탕이 되는 정보입니다. 등급·아이템 레벨·무작위 옵션은
// 드랍될 때 정해져서 ItemData(아이템 한 개)에 담기고, ItemData는 이 파일을 ID로 가리킵니다.
// 새 기본 아이템을 만들면 Assets/Resources/ItemDatabase의 Items 목록에도 넣어야 게임이 ID로 찾을 수 있습니다.
[CreateAssetMenu(fileName = "Item_", menuName = "귀살/Item Base")]
public class ItemBase : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("코드·세이브·드랍 표에서 이 아이템을 구분하는 영문 ID (한 번 정하면 바꾸지 않기, 예: weapon_old_dagger)")]
    public string itemId = "weapon_new";

    [Tooltip("(참고용) 한국어 이름. 실제 화면 이름은 화면 글자 표 \"UI\"의 item.name.{ID} 키 (예: item.name.weapon_old_dagger)")]
    public string displayName = "새 아이템";

    [Tooltip("예전 이름들 (옛 세이브 파일의 아이템을 이 아이템으로 바꿔 불러올 때 사용)")]
    public string[] oldNames = new string[0];

    [Header("종류와 능력치")]
    [Tooltip("아이템 종류 (무기 / 방어구 / 기력 물약)")]
    public ItemType itemType = ItemType.Weapon;

    [Tooltip("노멀 등급 기준 능력치 — 무기: 무기 피해 +N / 방어구: 최대 체력 +N / 기력 물약: 기력 회복량 (매직·레어·유니크는 드랍할 때 배율이 곱해짐)")]
    [Min(0f)] public float baseStatValue = 5f;

    [Tooltip("요구 레벨 (지금은 기록만 하고, 장착 제한·툴팁 표시는 아이템 & 파밍 단계에서)")]
    [Min(1)] public int requiredLevel = 1;

    [Header("모양")]
    [Tooltip("가방·장비 칸에 보일 아이콘 (비워 두면 종류별 공용 아이콘)")]
    public Sprite icon;

    // 화면에 보일 이름 (화면 글자 표 "UI"의 "item.name.ID" 키, 표를 찾으므로 매 프레임 부르지 않기)
    public string LocalizedName => Loc.Get("item.name." + itemId);
}
