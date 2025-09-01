using UnityEngine;

public class ChangeBuildType : MonoBehaviour
{
    // 빌드 타입 열거
    public enum BuildType
    {
        Wall,
        Floor,        
        Furniture,
        Deco
    }

    // 패널과 빌드 타입을 매핑하는 구조체
    [System.Serializable]
    public struct BuildPanel
    {
        public BuildType type;
        public GameObject panel;
    }

    [SerializeField] private BuildPanel[] buildPanels; // 건축물 패널

    private void Start()
    {
        ChangeBuildTypeButton(0);
    }

    /// <summary>
    /// 모든 패널을 비활성화
    /// </summary>
    private void DeactivateAllPanels()
    {
        foreach (var buildPanel in buildPanels)
        {
            if (buildPanel.panel != null)
            {
                buildPanel.panel.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 특정 타입의 건축물 패널을 활성화
    /// </summary>
    /// <param name="typeIndex"></param>
    public void ChangeBuildTypeButton(int typeIndex)
    {
        DeactivateAllPanels(); // 모든 패널 비활성화

        // 유효한 인덱스 확인 후 해당 패널 활성화
        if (typeIndex >= 0 && typeIndex < buildPanels.Length)
        {
            if (buildPanels[typeIndex].panel != null)
            {
                buildPanels[typeIndex].panel.SetActive(true);
            }
        }
    }

    /// <summary>
    /// 버튼에서 호출할 수 있도록 Enum 직접 사용
    /// </summary>
    /// <param name="type"></param>
    public void ChangeBuildTypeButton(BuildType type)
    {
        DeactivateAllPanels(); 

        // 해당 타입의 패널 활성화
        foreach (var buildPanel in buildPanels)
        {
            if (buildPanel.type == type && buildPanel.panel is not null)
            {
                buildPanel.panel.SetActive(true);
                break;
            }
        }
    }
}