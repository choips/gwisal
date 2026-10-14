using System;
using UnityEngine;

// 아이템 등급 색 표 — 바닥 이름표·바닥 구슬·툴팁·인벤토리 테두리가 모두 이 표에서 색을 가져옵니다.
// 파일은 반드시 Assets/Resources/ItemRarityColors.asset 한 개만 둡니다 (게임이 Resources에서 이 이름으로 불러옴).
// 색은 디아블로와 겹치지 않게 한국 전통 색으로 정합니다 (한지·쪽빛·황토·주사·금박).
// 어두운 UI 배경에서 글자가 잘 읽히도록 원래 색보다 조금 밝게 잡았습니다.
// 플레이 중에 Inspector에서 색을 바꾸면 바닥 아이템·인벤토리에 바로 반영됩니다 (에디터에서만, 바꾼 값은 플레이를 멈춰도 남음).
[CreateAssetMenu(fileName = "ItemRarityColors", menuName = "귀살/Item Rarity Colors")]
public class ItemRarityColors : ScriptableObject
{
    public const string ResourcePath = "ItemRarityColors"; // Resources 폴더 안의 파일 이름 (확장자 제외)

    [Header("등급 색")]
    [Tooltip("노멀(Normal) — 한지")]
    [SerializeField] private Color normalColor = new Color(0.91f, 0.87f, 0.78f);

    [Tooltip("매직(Magic) — 쪽빛")]
    [SerializeField] private Color magicColor = new Color(0.40f, 0.56f, 0.85f);

    [Tooltip("레어(Rare) — 황토")]
    [SerializeField] private Color rareColor = new Color(0.83f, 0.62f, 0.30f);

    [Tooltip("유니크(Unique) — 주사 붉은색")]
    [SerializeField] private Color uniqueColor = new Color(0.89f, 0.30f, 0.20f);

    [Tooltip("세트(Set) — 금박 (세트 등급은 아직 아이템에 없음, 생기면 이 색을 씀)")]
    [SerializeField] private Color setColor = new Color(0.95f, 0.82f, 0.42f);

    [Header("등급 없는 아이템")]
    [Tooltip("물약 — 이름표·바닥 구슬·인벤토리 칸 색")]
    [SerializeField] private Color potionColor = new Color(0.35f, 0.85f, 1f);

    private static ItemRarityColors instance;

    // 에디터에서 색을 바꿨을 때 알림 (바닥 아이템·인벤토리가 듣고 색을 다시 칠함)
    public static event Action ColorsChanged;

    // 어디서든 ItemRarityColors.Instance로 접근 (처음 한 번만 Resources에서 불러옴, 파일이 없으면 기본 색으로 임시 생성)
    public static ItemRarityColors Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<ItemRarityColors>(ResourcePath);
                if (instance == null)
                {
                    Debug.LogWarning($"Assets/Resources/{ResourcePath}.asset 파일이 없어 기본 등급 색을 씁니다. Project 창에서 Resources 폴더 우클릭 → Create → 귀살 → Item Rarity Colors로 만들어 주세요.");
                    instance = CreateInstance<ItemRarityColors>();
                }
            }
            return instance;
        }
    }

    public Color PotionColor => potionColor;

    // 등급에 맞는 색
    public Color Get(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Magic:  return magicColor;
            case ItemRarity.Rare:   return rareColor;
            case ItemRarity.Unique: return uniqueColor;
            default:                return normalColor;
        }
    }

#if UNITY_EDITOR
    // Inspector에서 값을 바꾸면 Unity가 호출
    private void OnValidate()
    {
        if (Application.isPlaying) ColorsChanged?.Invoke();
    }
#endif
}
