// 적을 꺼내 배치한 쪽 (무작위 배치기 EnemySpawner, 고정 배치 무리 EnemyPack)
// 적이 죽으면 자신을 배치한 쪽에 알려서 남은 수를 세거나 무리 정보를 정리하게 합니다.
public interface IEnemyOwner
{
    void OnEnemyDied(Enemy enemy);
}
