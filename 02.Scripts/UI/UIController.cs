using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [SerializeField] private LoadingSystem loadingSystem;

    public void Btn_Start()
    {
        loadingSystem.LoadScene("MainScene");
    }

    public async void OnLoadGame()
    {
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

    public void Btn_Exit()
    {
        Application.Quit();
    }
}
