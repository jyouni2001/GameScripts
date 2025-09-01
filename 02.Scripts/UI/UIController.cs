using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [SerializeField] private GameObject m_SettingUI;
    [SerializeField] private LoadingSystem loadingSystem;
    private bool isLoading = false;

    public void Btn_Start()
    {
        if (!isLoading)
        {
            isLoading = true;
            loadingSystem.LoadScene("MainScene");            
        }
    }

    public void Btn_SettingUI()
    {
        if (m_SettingUI != null) m_SettingUI.SetActive(!m_SettingUI.activeSelf);
    }

    public async void OnLoadGame()
    {
        if (!isLoading)
        {
            isLoading = true;

            // 저장된 게임 로드
            if (SaveManager.Instance != null)
            {
                await SaveManager.Instance.LoadGame();
                loadingSystem.LoadScene("MainScene");
            }
            else
            {
                Debug.LogError("SaveManager 인스턴스를 찾을 수 없습니다.");
            }
        }
    }

    public void Btn_Exit()
    {
        Application.Quit();
    }
}
