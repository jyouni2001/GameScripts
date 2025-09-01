using UnityEngine;
using UnityEngine.SceneManagement;
using JY;

public class GameManager : MonoBehaviour
{
    [SerializeField] private float autoSaveInterval = 300f; // 5분마다 자동 저장
    [SerializeField] private float timeSinceLastSave;

    private void Start()
    {
        // SaveManager가 이미 로드된 경우, 추가 로드 불필요
        if (SaveManager.Instance == null)
        {
            Debug.LogError("SaveManager가 없습니다. 초기화 필요.");
            return;
        }
        else
        {
            Debug.Log("SaveManager 로드 완료");
        }
    }

    private void Update()
    {
        // 자동 저장
        timeSinceLastSave += Time.deltaTime;
        if (timeSinceLastSave >= autoSaveInterval)
        {
            timeSinceLastSave = 0f;
            SaveGame();
            Debug.Log("자동 저장 완료");
        }
    }

    public void SaveGame()
    {
        SaveManager.Instance.SaveGame();
    }

    public void ReturnToStartMenu()
    {
        // 게임 저장 후 시작 화면으로 돌아가기
        SaveManager.Instance.SaveGame();
        SceneManager.LoadScene("StartScene");
    }
}