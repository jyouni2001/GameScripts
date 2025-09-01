using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

public class GraphicSetting : MonoBehaviour
{
    [Header("Resolution")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenBtn;

    [Header("V-Sync")]
    public Toggle vSyncBtn;

    private const int UNLIMITED_FRAMERATE_VALUE = 241;

    private const string FULLSCREEN_KEY = "FullscreenMode";
    private const string RESOLUTION_WIDTH_KEY = "ResolutionWidth";
    private const string RESOLUTION_HEIGHT_KEY = "ResolutionHeight";
    private const string VSYNC_KEY = "VSync";
    private const string FRAMERATE_KEY = "FrameRate";

    List<Resolution> resolutions = new List<Resolution>();
    
    int resolutionNum;
    int currentFrameRate;

    [Header("FrameRate")]
    [SerializeField] private Slider frameRateSlider;
    [SerializeField] private TextMeshProUGUI frameRateValueText;

    void Start()
    {
        InitUI();
        InitFullScreen();
        InitVSync();
        InitFrameRate();
        InitCurrentResolution();

        currentFrameRate = PlayerPrefs.GetInt(FRAMERATE_KEY, 120);
    }

    private void Update()
    {
        frameRateValueText.text = frameRateSlider.value.ToString("F0");
    }

    /// <summary>
    /// 시작 시 현재 모니터에 맞춰 해상도 값 드랍다운에 넣기
    /// </summary>
    void InitUI()
    {
        HashSet<string> addedResolutions = new HashSet<string>(); // 중복 체크용

        for (int i = 0; i < Screen.resolutions.Length; i++)
        {
            //float refreshRate = (float)Screen.resolutions[i].refreshRateRatio.value; 

            /*if (Mathf.Abs(refreshRate) == 60f)
            {*/
            // 중복 체크를 위한 키 생성
            string resolutionKey = Screen.resolutions[i].width + "x" +
                                 Screen.resolutions[i].height; // + "_" +
                                                               //refreshRate.ToString("F0");

            // 중복되지 않은 경우만 추가
            if (!addedResolutions.Contains(resolutionKey))
            {
                addedResolutions.Add(resolutionKey);
                resolutions.Add(Screen.resolutions[i]);
            }
            //}
        }

        resolutionDropdown.options.Clear();

        int optionNum = 0;
        float currentRefreshRate = (float)Screen.currentResolution.refreshRateRatio.numerator /
                              Screen.currentResolution.refreshRateRatio.denominator;

        foreach (Resolution item in resolutions)
        {
            TMP_Dropdown.OptionData option = new TMP_Dropdown.OptionData();
            //float itemRefreshRate = (float)item.refreshRateRatio.numerator / item.refreshRateRatio.denominator;

            option.text = item.width + "x" + item.height; //+ " " + itemRefreshRate.ToString("F0") + "Hz";
            // option.text = item.width + "x" + item.height + " " + item.refreshRateRatio + "Hz";
            resolutionDropdown.options.Add(option);

            if (item.width == Screen.currentResolution.width &&
           item.height == Screen.currentResolution.height /*&&
           Mathf.Abs(itemRefreshRate - currentRefreshRate) < 0.5f*/)
            {
                resolutionDropdown.value = optionNum;
            }
            optionNum++;
        }

        resolutionDropdown.RefreshShownValue();
    }

    /// <summary>
    /// 시작 시 저장된 전체화면 불러오기
    /// </summary>
    void InitFullScreen()
    {
        // 게임 시작 시 PlayerPrefs 값에 따라 전체화면 모드 복원
        // PlayerPrefs.GetInt는 키가 없을 경우 기본값 0을 반환
        // 0: Windowed, 1: FullscreenWindow
        int savedFullscreenValue = PlayerPrefs.GetInt(FULLSCREEN_KEY, (int)FullScreenMode.Windowed);

        FullScreenMode savedMode = (FullScreenMode)savedFullscreenValue;

        // 토글의 초기 상태를 PlayerPrefs 값에 맞게 설정
        fullscreenBtn.isOn = (savedMode == FullScreenMode.FullScreenWindow);

        // 스크린 해상도와 모드 적용
        Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, savedMode);
    }

    /// <summary>
    /// 시작 시 저장된 V-Sync 설정 불러오기
    /// </summary>
    void InitVSync()
    {
        // PlayerPrefs에서 V-Sync 설정 불러오기 (기본값: 0, 비활성화)
        int savedVSync = PlayerPrefs.GetInt(VSYNC_KEY, 0);
        QualitySettings.vSyncCount = savedVSync;
        vSyncBtn.isOn = (savedVSync == 1); // 토글 상태 설정
    }

    /// <summary>
    /// 시작 시 저장된 프레임률 설정 불러오기
    /// </summary>
    void InitFrameRate()
    {
        int savedFrameRate = PlayerPrefs.GetInt(FRAMERATE_KEY, 120);
        frameRateSlider.value = savedFrameRate;
        OnFrameRateSliderChanged(savedFrameRate); // 슬라이더 값 변경 시 텍스트 업데이트
    }

    /// <summary>
    /// 시작 시 저장된 해상도 불러오기
    /// </summary>
    void InitCurrentResolution()
    {
        // PlayerPrefs에 저장된 해상도와 화면 모드 값 불러오기
        int savedWidth = PlayerPrefs.GetInt(RESOLUTION_WIDTH_KEY, Screen.currentResolution.width);
        int savedHeight = PlayerPrefs.GetInt(RESOLUTION_HEIGHT_KEY, Screen.currentResolution.height);
        FullScreenMode savedMode = (FullScreenMode)PlayerPrefs.GetInt(FULLSCREEN_KEY, (int)FullScreenMode.Windowed);

        // 저장된 해상도를 찾아서 드롭다운의 값으로 설정
        for (int i = 0; i < resolutions.Count; i++)
        {
            if (resolutions[i].width == savedWidth && resolutions[i].height == savedHeight)
            {
                resolutionDropdown.value = i;
                break;
            }
        }

        // UI를 갱신
        resolutionDropdown.RefreshShownValue();

        // 저장된 해상도와 화면 모드 적용
        Screen.SetResolution(savedWidth, savedHeight, savedMode);
    }
       
    /// <summary>
    /// 슬라이더 값에 따라 텍스트 변경
    /// </summary>
    public void OnFrameRateSliderChanged(float value)
    {
        // 슬라이더 값을 정수로 변환하여 텍스트에 표시
        frameRateValueText.text = ((int)value).ToString();
    }


    /// <summary>
    /// InitUI에서 관련한 해상도가 드랍다운에 들어갈 시, 드랍다운의 값을 가져옴
    /// </summary>
    /// <param name="x"></param>
    public void DropboxOptionChange(int x)
    {
        resolutionNum = x;
    }

    /// <summary>
    /// Apply버튼 실행 시 현재 상태가 저장
    /// </summary>
    public void Btn_ChangeResolution()
    {
        // 화면 상태 확인
        FullScreenMode mode = fullscreenBtn.isOn ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        // 프레임률 적용 및 저장
        int newFrameRate = (int)frameRateSlider.value;
        Application.targetFrameRate = newFrameRate;

        // 수직동기화 상태 저장
        int vSyncValue = vSyncBtn.isOn ? 1 : 0;
        QualitySettings.vSyncCount = vSyncValue;

        // 해상도와 화면 모드 적용
        Screen.SetResolution(resolutions[resolutionNum].width, resolutions[resolutionNum].height, mode);
               
        // 변경된 화면 모드를 PlayerPrefs에 저장
        PlayerPrefs.SetInt(RESOLUTION_WIDTH_KEY, resolutions[resolutionNum].width);
        PlayerPrefs.SetInt(RESOLUTION_HEIGHT_KEY, resolutions[resolutionNum].height);
        PlayerPrefs.SetInt(FULLSCREEN_KEY, (int)mode);
        PlayerPrefs.SetInt(VSYNC_KEY, vSyncValue);
        PlayerPrefs.SetInt(FRAMERATE_KEY, newFrameRate);

        PlayerPrefs.Save();
    }
}
