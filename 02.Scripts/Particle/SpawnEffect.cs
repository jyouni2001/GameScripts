using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnEffect : MonoBehaviour
{

    [SerializeField] private GameObject smokeEffectPrefab; // 프리팹 참조
    private List<GameObject> smokeEffectPool = new List<GameObject>(); // 오브젝트 풀
    [SerializeField] private int poolSize = 20; // 풀 크기
    [SerializeField] private float effectDuration = 1f; // 이펙트 지속 시간

    void Start()
    {
        // 오브젝트 풀 초기화
        InitializePool();
    }

    /// <summary>
    /// 오브젝트 풀 초기화
    /// </summary>
    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject effect = Instantiate(smokeEffectPrefab);
            effect.transform.parent = transform;
            effect.SetActive(false); // 비활성화
            smokeEffectPool.Add(effect);
        }
    }

    /// <summary>
    /// 사용 가능한 이펙트 가져오기
    /// </summary>
    /// <returns></returns>
    private GameObject GetPooledEffect()
    {
        foreach (GameObject effect in smokeEffectPool)
        {
            if (!effect.activeInHierarchy)
                return effect;
        }

        // 풀이 가득 찼으면 새로 생성
        GameObject newEffect = Instantiate(smokeEffectPrefab);
        newEffect.SetActive(false);
        smokeEffectPool.Add(newEffect);
        return newEffect;
    }

    /// <summary>
    /// 건축 완료 시 호출
    /// </summary>
    /// <param name="position"></param>
    public void OnBuildingPlaced(Vector3 position)
    {
        GameObject effect = GetPooledEffect();
        effect.transform.position = position;
        effect.SetActive(true);

        effect.TryGetComponent<ParticleSystem>(out ParticleSystem ps);

        // Particle System 재생
        //ParticleSystem ps = effect.GetComponent<ParticleSystem>();
       
        if (ps != null)
        {
            ps.Play();
        }

        // 일정 시간 후 비활성화
        StartCoroutine(DeactivateEffect(effect, effectDuration));
    }

    /// <summary>
    /// 이펙트 비활성화 코루틴
    /// </summary>
    /// <param name="effect"></param>
    /// <param name="delay"></param>
    /// <returns></returns>
    private IEnumerator DeactivateEffect(GameObject effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        effect.SetActive(false);
    }

}
