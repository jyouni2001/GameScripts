using UnityEngine;
using TMPro;

public class VisualizationMoney : MonoBehaviour
{
    public TextMeshProUGUI moneyText;

    // Update는 매 프레임 호출되므로, 불필요한 문자열 변환을 최소화하기 위해 캐싱 사용
    private int lastMoney = -1; // 마지막으로 표시된 돈 값
    private string lastFormattedText = ""; // 마지막으로 포맷된 문자열

    void Update()
    {
        int currentMoney = PlayerWallet.Instance.money;

        // 돈 값이 변경되지 않았다면 업데이트 생략 (성능 최적화)
        if (currentMoney != lastMoney)
        {
            lastMoney = currentMoney;
            lastFormattedText = FormatMoney(currentMoney);
            moneyText.text = lastFormattedText;
        }
    }

    // 돈을 k, m, b 단위로 포맷팅하는 함수
    private string FormatMoney(int money)
    {
        if (money >= 1_000_000_000) // 10억 이상
        {
            return $"{(money / 1_000_000_000f):F1}b"; // 소수점 1자리
        }
        else if (money >= 1_000_000) // 100만 이상
        {
            return $"{(money / 1_000_000f):F1}m";
        }
        else if (money >= 1_000) // 1000 이상
        {
            return $"{(money / 1_000f):F1}k";
        }
        else
        {
            return money.ToString(); // 1000 미만은 그대로 표시
        }
    }
}
