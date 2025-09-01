using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SoundSetting : MonoBehaviour
{
    [Header("오디오 믹서")]
    [SerializeField] private AudioMixer masterMixer;

    [Header("UI 요소")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    // PlayerPrefs에 저장할 때 사용할 키
    private const string BGM_VOLUME_KEY = "BGM_Volume";
    private const string SFX_VOLUME_KEY = "SFX_Volume";

    private void Start()
    {
        // 슬라이더에 리스너(Listener) 연결
        bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);

        // 게임 시작 시 저장된 볼륨 값 불러오기
        LoadVolumeSettings();
    }

    private void LoadVolumeSettings()
    {
        // 저장된 BGM, SFX 볼륨 값을 불러옴
        float bgmVolume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 1f);
        float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);

        // 슬라이더의 값을 불러온 값으로 설정
        bgmSlider.value = bgmVolume;
        sfxSlider.value = sfxVolume;

        // AudioMixer에 값 적용
        SetBGMVolume(bgmVolume);
        SetSFXVolume(sfxVolume);
    }

    private void Update()
    {
        SetBGMVolume(bgmSlider.value);
        SetSFXVolume(sfxSlider.value);
    }

    // BGM 볼륨 조절 함수
    public void SetBGMVolume(float volume)
    {
        // 슬라이더 값(0.0001 ~ 1)을 AudioMixer의 데시벨(-80 ~ 0) 값으로 변환
        masterMixer.SetFloat("BGM", Mathf.Log10(volume) * 20);
        // 변경된 값을 저장
        PlayerPrefs.SetFloat(BGM_VOLUME_KEY, volume);
    }

    // SFX 볼륨 조절 함수
    public void SetSFXVolume(float volume)
    {
        masterMixer.SetFloat("SFX", Mathf.Log10(volume) * 20);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, volume);
    }
}