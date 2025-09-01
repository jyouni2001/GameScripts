using System.Collections;
using UnityEngine;

public class IngameFadeSystem : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    private void Start()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f; // 초기 알파값 설정
            StartCoroutine(FadeIn());
        }
    }

    private IEnumerator FadeIn()
    {
        float fadeDuration = 1.0f; // 페이드 지속 시간
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            yield return null;
        }

        // 알파값이 정확히 0으로 설정되도록 보장
        fadeCanvasGroup.alpha = 0f;

        StopCoroutine(FadeIn());
        fadeCanvasGroup.gameObject.SetActive(false);
    }
}
