using UnityEngine;
using UnityEngine.UI;

public class InitializeFirstButton : MonoBehaviour
{
    [SerializeField] private Button m_WallButton;
    [SerializeField] private Button m_FloorButton;
    [SerializeField] private Button m_FurnitureButton;
    [SerializeField] private Button m_DecoButton;

    void Start()
    {
        if (m_WallButton != null && m_WallButton.transition == Selectable.Transition.SpriteSwap)
        {
            // WallButton을 Pressed 상태로 초기화
            m_WallButton.image.sprite = m_WallButton.spriteState.pressedSprite;
        }

        // 다른 버튼 클릭 시 WallButton을 Normal로 되돌리는 이벤트 설정
        m_FloorButton.onClick.AddListener(ResetWallToNormal);
        m_FurnitureButton.onClick.AddListener(ResetWallToNormal);
        m_DecoButton.onClick.AddListener(ResetWallToNormal);
    }

    private void ResetWallToNormal()
    {
        if (m_WallButton != null && m_WallButton.transition == Selectable.Transition.SpriteSwap)
        {
            m_WallButton.image.sprite = m_WallButton.spriteState.disabledSprite;
        }
    }
}
