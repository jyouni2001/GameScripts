using UnityEngine;
using UnityEngine.Audio; // AudioMixerGroup을 사용하기 위해 추가

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private AudioClip mainTheme; // 예시 BGM 클립
    [SerializeField] private AudioMixerGroup bgmMixerGroup; // BGM 믹서 그룹

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 BGM이 끊기지 않게 함
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // AudioSource의 Output을 BGM 믹서 그룹으로 설정
        if (bgmAudioSource != null && bgmMixerGroup != null)
        {
            bgmAudioSource.outputAudioMixerGroup = bgmMixerGroup;
        }

        // BGM 재생
        PlayBGM(mainTheme);
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || bgmAudioSource == null) return;

        bgmAudioSource.clip = clip;
        bgmAudioSource.loop = true; // BGM은 보통 반복재생
        bgmAudioSource.Play();
    }
}