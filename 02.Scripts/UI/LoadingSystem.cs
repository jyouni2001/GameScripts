using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingSystem : MonoBehaviour
{
    [SerializeField] private GameObject circleObject;
    private Material circleMaterialInstance;

    [SerializeField] private CanvasGroup fadeCanvasGroup;

    private void Awake()
    {
        if (circleObject != null)
        {
            Image circleImage = circleObject.GetComponent<Image>();

            if (circleImage != null)
            {
                circleMaterialInstance = circleImage.material;
            }

            circleObject.SetActive(false);
        }

        if(fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
        }
    }

    public void LoadScene(string sceneName)
    {
        // 이미 실행 중인 로딩 코루틴이 있다면 중지하고 새로 시작
        StopAllCoroutines();

        // 로딩 시작: Circle UI 활성화
        if (circleObject != null)
        {
            circleObject.SetActive(true);
        }

        StartCoroutine(LoadSceneAsync(sceneName));

    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        // 로딩이 90% 완료될 때까지 대기 (씬 활성화 직전)
        while (op.progress < 0.9f)
        {
            // op.progress는 0.0에서 0.9 사이의 값을 반환하므로,
            // 이를 0.0에서 1.0 사이의 값으로 정규화(Normalize)합니다.
            float progress = Mathf.Clamp01(op.progress / 0.9f);

            // 캐시해둔 머티리얼 인스턴스의 셰이더 프로퍼티 값을 업데이트합니다.
            if (circleMaterialInstance != null)
            {
                circleMaterialInstance.SetFloat("_Progressing", progress);
            }

            yield return null;
        }

        // 로딩 완료 직전: 100% 채워진 모습을 보여줍니다.
        if (circleMaterialInstance != null)
        {
            circleMaterialInstance.SetFloat("_Progressing", 1f);
        }

        // 페이드 아웃 효과 (알파 0 -> 1)
        if (fadeCanvasGroup != null)
        {
            float fadeDuration = 1.0f; // 페이드 지속 시간
            float elapsedTime = 0f;

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeDuration);
                yield return null;
            }

            // 알파값이 정확히 1로 설정되도록 보장
            fadeCanvasGroup.alpha = 1f;
        }

        yield return new WaitForSeconds(1.0f);
        op.allowSceneActivation = true;

        // 씬 활성화가 완전히 끝날 때까지 대기합니다.
        yield return op;

        // 로딩 완료: Circle UI 다시 비활성화
        if (circleObject != null)
        {
            circleObject.SetActive(false);
        }
    }
}

