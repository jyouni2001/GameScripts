using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class LanguageUI : MonoBehaviour
{
    [SerializeField] TMP_Dropdown Dropdown_Language;
    private const string LANGUAGE_KEY = "SelectedLanguage";

    private void Start()
    {
        Dropdown_Language.onValueChanged.AddListener(ClickLanguage);

        StartCoroutine(InitializeLanguage());
        //InitializeDropdown();
    }
    public void ClickLanguage(int index)
    {
        StartCoroutine(ChangeLanguage(index));
        //LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[index];
    }

    private IEnumerator ChangeLanguage(int index)
    {
        // 로케일 초기화 대기
        yield return LocalizationSettings.InitializationOperation;

        // 선택된 로케일로 변경
        var selectedLocale = LocalizationSettings.AvailableLocales.Locales[index];
        LocalizationSettings.SelectedLocale = selectedLocale;

        // PlayerPrefs에 언어 코드 저장
        PlayerPrefs.SetString(LANGUAGE_KEY, selectedLocale.Identifier.Code);
        PlayerPrefs.Save();
    }

    private IEnumerator InitializeLanguage()
    {
        // 로케일 초기화 대기
        yield return LocalizationSettings.InitializationOperation;

        // 저장된 언어 코드 로드
        string savedLanguageCode = PlayerPrefs.GetString(LANGUAGE_KEY, string.Empty);
        int dropdownIndex = 0;

        if (!string.IsNullOrEmpty(savedLanguageCode))
        {
            // 저장된 언어 코드에 해당하는 로케일 찾기
            var locale = LocalizationSettings.AvailableLocales.Locales
                .FirstOrDefault(l => l.Identifier.Code == savedLanguageCode);

            if (locale != null)
            {
                // 저장된 로케일로 설정
                LocalizationSettings.SelectedLocale = locale;
                dropdownIndex = LocalizationSettings.AvailableLocales.Locales.IndexOf(locale);
            }
        }

        // 드롭다운 초기 값 설정
        Dropdown_Language.value = dropdownIndex;
    }

    private void InitializeDropdown()
    {
        // 현재 선택된 로케일의 인덱스를 드롭다운에 반영
        var currentLocale = LocalizationSettings.SelectedLocale;
        int index = LocalizationSettings.AvailableLocales.Locales.IndexOf(currentLocale);
        if (index >= 0)
        {
            Dropdown_Language.value = index;
        }
    }

    private void OnDestroy()
    {
        // 이벤트 리스너 제거 (메모리 누수 방지)
        Dropdown_Language.onValueChanged.RemoveListener(ClickLanguage);
    }
}
