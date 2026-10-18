// 적을 꺼내 배치한 쪽 (무작위 배치기 EnemySpawner, 고정 배치 무리 EnemyPack)
// 적이 죽으면 자신을 배치한 쪽에 알려서 남은 수를 세거나 무리 정보를 정리하게 합니다.
// 적이 깨어나면(플레이어가 다가오거나 맞음) 알려서 같은 무리를 함께 깨우게 합니다.
// 창귀의 부르는 소리는 무리당 한 번만 허용합니다 (TryBeginCall).
public interface IEnemyOwner
{
    void OnEnemyDied(Enemy enemy);

    // enemy가 막 깨어남 → 같은 무리의 나머지를 깨움 (enemy 자신은 이미 깨어 있음)
    void OnEnemyAlerted(Enemy enemy);

    // 부르는 소리: 이 무리에서 아직 울지 않았으면 true로 표시하고 허용 (무리당 한 번)
    bool TryBeginCall(Enemy caller);
}
