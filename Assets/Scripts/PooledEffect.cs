using UnityEngine;

// 오브젝트 풀로 재사용하는 파티클 이펙트 (클릭 표시, 부적 적중 이펙트 등의 프리팹에 붙임)
// - 파티클이 끝나면 파괴하지 않고 풀로 돌려보냅니다.
//   (Inspector의 Stop Action이 Destroy여도, 시작할 때 코드에서 Callback으로 바꿈 → 끝나면 OnParticleSystemStopped 실행)
// - 다시 꺼낼 때마다 파티클을 처음부터 재생합니다.
[RequireComponent(typeof(ParticleSystem))]
public class PooledEffect : MonoBehaviour
{
    private ParticleSystem particles;

    private void Awake()
    {
        particles = GetComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.stopAction = ParticleSystemStopAction.Callback;
    }

    // 꺼낼 때마다 이전 재생의 남은 입자를 지우고 처음부터 재생
    private void OnEnable()
    {
        particles.Clear(true);
        particles.Play(true);
    }

    // 파티클 재생이 끝나면 풀로 반납
    private void OnParticleSystemStopped()
    {
        ObjectPoolManager.Release(gameObject);
    }
}
