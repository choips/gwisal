// 계정 공용 세이브 (모든 캐릭터가 함께 쓰는 것, 파일: Account.json)
// 지금은 마지막으로 플레이한 캐릭터 칸 번호만 저장합니다.
// 창고·엽전·엔딩 여부·용병 해금·설정이 생기면 여기에 추가합니다 (새 칸은 맨 뒤에).
[System.Serializable]
public class AccountSaveData
{
    public const int CurrentVersion = 3;

    public int version;
    public int lastCharacterSlot; // 마지막으로 플레이한 캐릭터 칸 번호 (캐릭터 선택 화면은 S7)
}
