using JY;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 해와 달의 위치를 시간에 따라 제어하는 클래스
/// TimeSystem과 연동하여 실시간으로 천체의 위치를 업데이트
/// 빛 방향도 자동으로 조절
/// </summary>
public class SunMoonController : MonoBehaviour
{
    #region Fields & Properties

    [Header("천체 오브젝트")]
    [SerializeField] private Transform sunTransform;
    [SerializeField] private Transform moonTransform;

    [Header("천체 렌즈플레어")] 
    [SerializeField] private LensFlareComponentSRP sunLensFlare;
    [SerializeField] private LensFlareComponentSRP moonLensFlare;
    
    
    
    [Header("빛 컴포넌트")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Light moonLight;
    
    [Header("회전 설정")]
    [SerializeField] private float rotationRadius = 50f; // 회전 반지름
    [SerializeField] private Vector3 rotationCenter = Vector3.zero; // 회전 중심점
    [SerializeField] private bool smoothRotation = true; // 부드러운 회전 여부
    [SerializeField] private float rotationSpeed = 2f; // 부드러운 회전 속도
    
    [Header("빛 설정")]
    [SerializeField] private bool autoControlLight = true; // 자동 빛 제어
    [SerializeField] private AnimationCurve sunIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 해 밝기 곡선
    [SerializeField] private AnimationCurve moonIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0.3f); // 달 밝기 곡선
    [SerializeField] private float maxSunIntensity = 1.5f;
    [SerializeField] private float maxMoonIntensity = 0.5f;
    
    
    
    [Header("디버그")]
    [SerializeField] private bool showDebugInfo;
    
    private TimeSystem timeSystem;
    private float targetSunAngle;
    private float targetMoonAngle;
    [SerializeField] private float currentSunAngle;
    [SerializeField] private float currentMoonAngle;

    #endregion

    #region Unity Lifecycle Methods
    
    private void Start()
    {
        InitializeSunMoonSystem();
    }
    
    private void Update()
    {
        if (smoothRotation)
        {
            UpdateSmoothRotation();
        }
    }
    
    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// 해와 달 시스템 초기화
    /// </summary>
    private void InitializeSunMoonSystem()
    {
        // TimeSystem 참조 가져오기
        timeSystem = TimeSystem.Instance;
        
        if (timeSystem == null)
        {
            Debug.LogError("TimeSystem을 찾을 수 없습니다!");
            return;
        }
        
        // Light 컴포넌트 자동 탐지 (설정되지 않은 경우)
        if (sunLight == null && sunTransform != null)
        {
            sunLight = sunTransform.GetComponent<Light>();
        }
        
        if (moonLight == null && moonTransform != null)
        {
            moonLight = moonTransform.GetComponent<Light>();
        }
        
        // 이벤트 구독
        SubscribeEvents();
        
        // 초기 위치 설정
        UpdateCelestialBodies(timeSystem.CurrentHour, timeSystem.CurrentMinute);
        
        // 초기 각도 동기화
        currentSunAngle = NormalizeAngle(targetSunAngle);
        currentMoonAngle = NormalizeAngle(targetMoonAngle);
        
        // 렌즈플레어 초기화 (아침 시작 전제)
        moonLensFlare.intensity = 0f;
        sunLensFlare.intensity = 1.5f;
        
        // 초기 적용
        ApplyRotation();
    }

    /// <summary>
    /// 이벤트 구독
    /// </summary>
    private void SubscribeEvents()
    {
        if (timeSystem != null)
        {
            timeSystem.OnMinuteChanged += UpdateCelestialBodies;
        }
    }

    /// <summary>
    /// 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeEvents()
    {
        if (timeSystem != null)
        {
            timeSystem.OnMinuteChanged -= UpdateCelestialBodies;
        }
    }

    #endregion

    #region Celestial Body Update Methods

    /// <summary>
    /// 해와 달의 위치 업데이트
    /// </summary>
    /// <param name="hour">현재 시간</param>
    /// <param name="minute">현재 분</param>
    private void UpdateCelestialBodies(int hour, int minute)
    {
        // 정확한 시간 계산 (분 단위 포함)
        float currentTime = hour + (minute / 60f);
        
        // 해의 각도 계산 - 수정됨
        // 12시(정오)에 90도(가장 위), 0시(자정)에 -90도(가장 아래)
        // 6시: 0도(수평), 18시: 180도(수평 반대편)
        targetSunAngle = (currentTime * 15f) - 90f; // 15도/시간 (360도/24시간)
        
        // 달의 각도 계산 (해와 정확히 반대편)
        targetMoonAngle = targetSunAngle + 180f;
        
        // 각도 정규화 (-180 ~ 180)
        targetSunAngle = NormalizeAngle(targetSunAngle);
        targetMoonAngle = NormalizeAngle(targetMoonAngle);
        
        // 즉시 적용 또는 부드러운 회전 설정
        if (!smoothRotation)
        {
            currentSunAngle = targetSunAngle;
            currentMoonAngle = targetMoonAngle;
            ApplyRotation();
        }
        
        // 디버그 정보 출력
        if (showDebugInfo)
        {
            Debug.Log($"시간: {hour:00}:{minute:00} | 해 각도: {currentSunAngle:F1}°/{targetSunAngle:F1}° | 달 각도: {currentMoonAngle:F1}°/{targetMoonAngle:F1}°");
        }
    }

    /// <summary>
    /// 부드러운 회전 업데이트 - 수정된 버전
    /// </summary>
    private void UpdateSmoothRotation()
    {
        // 해의 부드러운 회전
        float sunAngleDiff = Mathf.DeltaAngle(currentSunAngle, targetSunAngle);
        if (Mathf.Abs(sunAngleDiff) > 0.1f)
        {
            currentSunAngle += sunAngleDiff * rotationSpeed * Time.deltaTime;
            // 각도 정규화 추가
            currentSunAngle = NormalizeAngle(currentSunAngle);
        }
        else
        {
            currentSunAngle = targetSunAngle;
        }
        
        // 달의 부드러운 회전
        float moonAngleDiff = Mathf.DeltaAngle(currentMoonAngle, targetMoonAngle);
        if (Mathf.Abs(moonAngleDiff) > 0.1f)
        {
            currentMoonAngle += moonAngleDiff * rotationSpeed * Time.deltaTime;
            // 각도 정규화 추가
            currentMoonAngle = NormalizeAngle(currentMoonAngle);
        }
        else
        {
            currentMoonAngle = targetMoonAngle;
        }
        
        // 회전 적용
        ApplyRotation();
    }

    /// <summary>
    /// 실제 회전 적용
    /// </summary>
    private void ApplyRotation()
    {
        // 해의 위치 및 방향 설정
        if (sunTransform != null)
        {
            Vector3 sunPosition = CalculatePosition(currentSunAngle);
            sunTransform.position = sunPosition;
            
            // 해가 회전 중심을 향하도록 회전 설정
            Vector3 directionToCenter = (rotationCenter - sunPosition).normalized;
            sunTransform.rotation = Quaternion.LookRotation(directionToCenter);
        }
        
        // 달의 위치 및 방향 설정
        if (moonTransform != null)
        {
            Vector3 moonPosition = CalculatePosition(currentMoonAngle);
            moonTransform.position = moonPosition;
            
            // 달이 회전 중심을 향하도록 회전 설정
            Vector3 directionToCenter = (rotationCenter - moonPosition).normalized;
            moonTransform.rotation = Quaternion.LookRotation(directionToCenter);
        }
        
        // 빛 제어
        if (autoControlLight)
        {
            UpdateLightSettings();
        }
    }

    /// <summary>
    /// 빛 설정 업데이트
    /// </summary>
    private void UpdateLightSettings()
    {
        // 해의 높이에 따른 밝기 계산 (0 = 수평선, 1 = 정점)
        float sunHeight = Mathf.Clamp01((Mathf.Sin(currentSunAngle * Mathf.Deg2Rad) + 1f) / 2f);
        float moonHeight = Mathf.Clamp01((Mathf.Sin(currentMoonAngle * Mathf.Deg2Rad) + 1f) / 2f);
        
        // 해 빛 설정
        if (sunLight != null)
        {
            // 해가 지평선 위에 있을 때만 빛을 켬
            bool sunVisible = (currentSunAngle > -10f || currentSunAngle < -170f) && currentSunAngle < 180f;
            //sunLight.enabled = sunVisible;
            
            if (sunVisible)
            {
                sunLight.intensity = sunIntensityCurve.Evaluate(sunHeight) * maxSunIntensity;
                
                // 해의 색상 변화 (일출/일몰 시 붉은색)
                float sunColorFactor = Mathf.Clamp01(sunHeight * 2f); // 낮은 각도에서 붉게
                sunLight.color = Color.Lerp(new Color(1f, 0.6f, 0.4f), Color.white, sunColorFactor);
            }
            else
            {
                sunLight.intensity = 0f;
            }
            
            if (sunLensFlare != null)
            {
                sunLensFlare.intensity = (currentSunAngle > 0f && currentSunAngle < 180f) ? 1.5f : 0f;
            }
        }
        
        // 달 빛 설정
        if (moonLight != null)
        {
            // 달이 지평선 위에 있을 때만 빛을 켬
            bool moonVisible = currentMoonAngle > 0f && currentMoonAngle < 180f;
            // moonLight.enabled = moonVisible;

            if (moonVisible)
            {
                moonLight.intensity = moonIntensityCurve.Evaluate(moonHeight) * maxMoonIntensity;
                moonLight.color = new Color(0.8f, 0.8f, 1f); // 차가운 달빛
            }
            else
            {
                moonLight.intensity = 0f;
            }
            
            // LensFlare 컴포넌트의 intensity 설정
            
            if (moonLensFlare != null)
            {
                moonLensFlare.intensity = moonVisible ? 1f : 0f;
            }
            
        }
    }

    /// <summary>
    /// 각도를 바탕으로 위치 계산
    /// </summary>
    /// <param name="angle">각도 (도 단위)</param>
    /// <returns>계산된 위치</returns>
    private Vector3 CalculatePosition(float angle)
    {
        float radianAngle = angle * Mathf.Deg2Rad;
        
        // Y축(높이)과 Z축(전후)을 기준으로 한 원형 회전
        // X축은 좌우 움직임 (원한다면 추가 가능)
        float x = rotationCenter.x;
        float y = rotationCenter.y + (rotationRadius * Mathf.Sin(radianAngle));
        float z = rotationCenter.z + (rotationRadius * Mathf.Cos(radianAngle));
        
        return new Vector3(x, y, z);
    }

    /// <summary>
    /// 각도 정규화 (-180 ~ 180)
    /// </summary>
    /// <param name="angle">정규화할 각도</param>
    /// <returns>정규화된 각도</returns>
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f)
            angle -= 360f;
        while (angle < -180f)
            angle += 360f;
        return angle;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 해와 달의 회전 반지름 설정
    /// </summary>
    /// <param name="radius">새로운 반지름</param>
    public void SetRotationRadius(float radius)
    {
        rotationRadius = Mathf.Max(0, radius);
        ApplyRotation(); // 즉시 적용
    }

    /// <summary>
    /// 회전 중심점 설정
    /// </summary>
    /// <param name="center">새로운 중심점</param>
    public void SetRotationCenter(Vector3 center)
    {
        rotationCenter = center;
        ApplyRotation(); // 즉시 적용
    }

    /// <summary>
    /// 부드러운 회전 설정
    /// </summary>
    /// <param name="smooth">부드러운 회전 여부</param>
    /// <param name="speed">회전 속도 (smooth가 true일 때만 적용)</param>
    public void SetSmoothRotation(bool smooth, float speed = 2f)
    {
        smoothRotation = smooth;
        rotationSpeed = Mathf.Max(0.1f, speed);
    }

    /// <summary>
    /// 해와 달을 특정 시간에 맞춰 즉시 이동
    /// </summary>
    /// <param name="hour">시간</param>
    /// <param name="minute">분</param>
    public void SetImmediatePosition(int hour, int minute)
    {
        UpdateCelestialBodies(hour, minute);
        currentSunAngle = NormalizeAngle(targetSunAngle);
        currentMoonAngle = NormalizeAngle(targetMoonAngle);
        ApplyRotation();
    }

    /// <summary>
    /// 빛 자동 제어 설정
    /// </summary>
    /// <param name="autoControl">자동 제어 여부</param>
    public void SetAutoLightControl(bool autoControl)
    {
        autoControlLight = autoControl;
        if (autoControl)
        {
            UpdateLightSettings();
        }
    }

    /// <summary>
    /// 해 빛 강도 설정
    /// </summary>
    /// <param name="intensity">빛 강도</param>
    public void SetSunLightIntensity(float intensity)
    {
        maxSunIntensity = Mathf.Max(0, intensity);
        if (autoControlLight)
        {
            UpdateLightSettings();
        }
    }

    /// <summary>
    /// 달 빛 강도 설정
    /// </summary>
    /// <param name="intensity">빛 강도</param>
    public void SetMoonLightIntensity(float intensity)
    {
        maxMoonIntensity = Mathf.Max(0, intensity);
        if (autoControlLight)
        {
            UpdateLightSettings();
        }
    }

    #endregion

    #region Debug Methods
    
    /// <summary>
    /// 현재 상태 정보 반환
    /// </summary>
    /// <returns>상태 정보 문자열</returns>
    public string GetStatusInfo()
    {
        if (timeSystem == null) return "TimeSystem 없음";
        
        return $"시간: {timeSystem.CurrentHour:00}:{timeSystem.CurrentMinute:00}\n" +
               $"해 각도: {currentSunAngle:F1}°\n" +
               $"달 각도: {currentMoonAngle:F1}°\n" +
               $"회전 반지름: {rotationRadius}\n" +
               $"부드러운 회전: {smoothRotation}\n" +
               $"자동 빛 제어: {autoControlLight}";
    }

    /// <summary>
    /// 기즈모 그리기 (Scene 뷰에서 회전 경로 표시)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 회전 중심점 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(rotationCenter, 1f);
        
        // 회전 경로 표시
        Gizmos.color = Color.white;
        Vector3 previousPoint = Vector3.zero;
        for (int i = 0; i <= 36; i++)
        {
            float angle = (i * 10f) - 90f; // -90도부터 270도까지
            Vector3 point = CalculatePosition(angle);
            
            if (i > 0)
            {
                Gizmos.DrawLine(previousPoint, point);
            }
            previousPoint = point;
        }
        
        // 현재 해와 달 위치 표시
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(CalculatePosition(currentSunAngle), 2f);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(CalculatePosition(currentMoonAngle), 1.5f);
        }
    }

    #endregion
}