using UnityEngine;
using TMPro; // TextMeshPro 사용 (필요 시 UnityEngine.UI로 대체 가능)
using System.Text;

public class PerformanceMonitor : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI performanceText; // UI 텍스트 참조
    [SerializeField] private float updateInterval = 0.5f; // 업데이트 간격 (초)
    [SerializeField] private float gcMemoryThreshold = 100f; // GC 메모리 경고 임계값 (MB)

    private float accumTime = 0f; // 누적 시간
    private int frames = 0; // 프레임 카운트
    private float fps = 0f; // 계산된 FPS
    private StringBuilder sb = new StringBuilder(); // 성능 최적화를 위해 StringBuilder 사용

    private void Start()
    {
        if (performanceText == null)
        {
            Debug.LogWarning("PerformanceMonitor: TextMeshProUGUI가 할당되지 않았습니다!");
            enabled = false; // 텍스트가 없으면 비활성화
        }
    }

    private void Update()
    {
        accumTime += Time.deltaTime;
        frames++;

        // 지정된 간격마다 FPS와 메모리 정보 갱신
        if (accumTime >= updateInterval)
        {
            // FPS 계산
            fps = frames / accumTime;
            frames = 0;
            accumTime = 0f;

            // 메모리 사용량 (MB 단위)
            float gcMemory = System.GC.GetTotalMemory(false) / (1024f * 1024f); // GC 관리 메모리
            float usedMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f); // Unity 할당 메모리

            // 텍스트 업데이트 (StringBuilder로 성능 최적화)
            sb.Clear();
            sb.AppendLine($"FPS: {fps:F1}");
            sb.AppendLine($"Allocated Memory: {usedMemory:F2} MB");
            sb.AppendLine($"GC Memory: {gcMemory:F2} MB");

            performanceText.text = sb.ToString();

            // 창의적 추가: GC 메모리가 임계값 초과 시 색상 변경
            performanceText.color = gcMemory > gcMemoryThreshold ? Color.red : Color.white;
        }

        // 디버깅용: F2 키로 GC 강제 호출 (선택 사항)
        if (Input.GetKeyDown(KeyCode.F2))
        {
            System.GC.Collect();
            Debug.Log("GC 강제 호출됨");
        }
    }
}