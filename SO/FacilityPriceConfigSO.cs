using System;
using UnityEngine;

/// <summary>
/// 시설 사용료 및 명성도를 관리하는 ScriptableObject
/// Unity Inspector에서 쉽게 수정 가능하며, 모든 시설 가격을 중앙에서 관리
/// </summary>
[CreateAssetMenu(fileName = "FacilityPriceConfig", menuName = "Game/Facility Price Config")]
public class FacilityPriceConfigSO : ScriptableObject
{
    [Header("선베드 설정")]
    [Tooltip("선베드 기본 사용료 (방 밖 단독 선베드)")]
    public int sunbedBasePrice = 100;
    
    [Tooltip("선베드 명성도")]
    public int sunbedReputation = 5;
    
    [Tooltip("방 안 선베드는 무료 (방 가격에 포함)")]
    public bool sunbedInRoomIsFree = true;
    
    [Header("식당 설정")]
    [Tooltip("식당 주문 가격")]
    public int restaurantOrderPrice = 100;
    
    [Tooltip("식당 주문 명성도")]
    public int restaurantReputation = 50;
    
    [Header("운동 시설 설정")]
    [Tooltip("운동 시설 사용료 (방 유무 상관없이 유료)")]
    public int healthFacilityPrice = 100;
    
    [Tooltip("운동 시설 명성도")]
    public int healthFacilityReputation = 10;
    
    [Tooltip("운동 시설 무료 제공 여부 (false면 유료)")]
    public bool healthFacilityIsFree = false;
    
    [Header("욕조 설정")]
    [Tooltip("욕조 사용료 (현재 무료)")]
    public int bathtubPrice = 0;
    
    [Tooltip("욕조 명성도")]
    public int bathtubReputation = 0;
    
    [Tooltip("욕조는 방 시설로 무료 제공")]
    public bool bathtubIsFree = true;
    
    [Header("예식장 설정")]
    [Tooltip("예식장 사용료 (방 유무 상관없이 유료)")]
    public int weddingFacilityPrice = 200;
    
    [Tooltip("예식장 명성도")]
    public int weddingFacilityReputation = 30;
    
    [Tooltip("예식장 무료 제공 여부 (false면 유료)")]
    public bool weddingFacilityIsFree = false;
    
    [Header("라운지 설정")]
    [Tooltip("라운지 사용료 (방 유무 상관없이 유료)")]
    public int loungeFacilityPrice = 150;
    
    [Tooltip("라운지 명성도")]
    public int loungeFacilityReputation = 20;
    
    [Tooltip("라운지 무료 제공 여부 (false면 유료)")]
    public bool loungeFacilityIsFree = false;
    
    [Header("연회장 설정")]
    [Tooltip("연회장 사용료 (방 유무 상관없이 유료)")]
    public int hallFacilityPrice = 250;
    
    [Tooltip("연회장 명성도")]
    public int hallFacilityReputation = 40;
    
    [Tooltip("연회장 무료 제공 여부 (false면 유료)")]
    public bool hallFacilityIsFree = false;
    
    [Header("사우나 설정")]
    [Tooltip("사우나 사용료 (방 유무 상관없이 유료)")]
    public int saunaFacilityPrice = 300;
    
    [Tooltip("사우나 명성도")]
    public int saunaFacilityReputation = 50;
    
    [Tooltip("사우나 무료 제공 여부 (false면 유료)")]
    public bool saunaFacilityIsFree = false;
    
    [Header("카페 설정")]
    [Tooltip("카페 사용료 (방 유무 상관없이 유료)")]
    public int cafeFacilityPrice = 150;
    
    [Tooltip("카페 명성도")]
    public int cafeFacilityReputation = 20;
    
    [Tooltip("카페 무료 제공 여부 (false면 유료)")]
    public bool cafeFacilityIsFree = false;
    
    [Header("Bath 설정")]
    [Tooltip("Bath 사용료 (방 유무 상관없이 유료)")]
    public int bathFacilityPrice = 200;
    
    [Tooltip("Bath 명성도")]
    public int bathFacilityReputation = 25;
    
    [Tooltip("Bath 무료 제공 여부 (false면 유료)")]
    public bool bathFacilityIsFree = false;
    
    [Header("Hos(고급식당) 설정")]
    [Tooltip("Hos(고급식당) 사용료 (방 유무 상관없이 유료)")]
    public int hosFacilityPrice = 300;
    
    [Tooltip("Hos(고급식당) 명성도")]
    public int hosFacilityReputation = 30;
    
    [Tooltip("Hos(고급식당) 무료 제공 여부 (false면 유료)")]
    public bool hosFacilityIsFree = false;
    
    [Header("디버그")]
    [Tooltip("가격 정보 로그 출력")]
    public bool showPriceLogs = true;
    
    /// <summary>
    /// 선베드 최종 가격 계산
    /// </summary>
    public int GetSunbedFinalPrice(bool isInRoom)
    {
        if (isInRoom && sunbedInRoomIsFree)
            return 0;
        
        return sunbedBasePrice;
    }
    
    /// <summary>
    /// 식당 주문 최종 가격 계산
    /// </summary>
    public int GetRestaurantFinalPrice()
    {
        return restaurantOrderPrice;
    }
    
    /// <summary>
    /// 운동 시설 최종 가격 계산
    /// </summary>
    public int GetHealthFacilityFinalPrice()
    {
        if (healthFacilityIsFree)
            return 0;
        
        return healthFacilityPrice;
    }
    
    /// <summary>
    /// 욕조 최종 가격 계산
    /// </summary>
    public int GetBathtubFinalPrice()
    {
        if (bathtubIsFree)
            return 0;
        
        return bathtubPrice;
    }
    
    /// <summary>
    /// 예식장 최종 가격 계산
    /// </summary>
    public int GetWeddingFacilityFinalPrice()
    {
        if (weddingFacilityIsFree)
            return 0;
        
        return weddingFacilityPrice;
    }
    
    /// <summary>
    /// 라운지 최종 가격 계산
    /// </summary>
    public int GetLoungeFacilityFinalPrice()
    {
        if (loungeFacilityIsFree)
            return 0;
        
        return loungeFacilityPrice;
    }
    
    /// <summary>
    /// 연회장 최종 가격 계산
    /// </summary>
    public int GetHallFacilityFinalPrice()
    {
        if (hallFacilityIsFree)
            return 0;
        
        return hallFacilityPrice;
    }
    
    /// <summary>
    /// 사우나 최종 가격 계산
    /// </summary>
    public int GetSaunaFacilityFinalPrice()
    {
        if (saunaFacilityIsFree)
            return 0;
        
        return saunaFacilityPrice;
    }
    
    /// <summary>
    /// 카페 최종 가격 계산
    /// </summary>
    public int GetCafeFacilityFinalPrice()
    {
        if (cafeFacilityIsFree)
            return 0;
        
        return cafeFacilityPrice;
    }
    
    /// <summary>
    /// Bath 최종 가격 계산
    /// </summary>
    public int GetBathFacilityFinalPrice()
    {
        if (bathFacilityIsFree)
            return 0;
        
        return bathFacilityPrice;
    }
    
    /// <summary>
    /// Hos(고급식당) 최종 가격 계산
    /// </summary>
    public int GetHosFacilityFinalPrice()
    {
        if (hosFacilityIsFree)
            return 0;
        
        return hosFacilityPrice;
    }
    
    /// <summary>
    /// 설정 유효성 검사
    /// </summary>
    private void OnValidate()
    {
        // 음수 방지
        sunbedBasePrice = Mathf.Max(0, sunbedBasePrice);
        restaurantOrderPrice = Mathf.Max(0, restaurantOrderPrice);
        healthFacilityPrice = Mathf.Max(0, healthFacilityPrice);
        bathtubPrice = Mathf.Max(0, bathtubPrice);
        weddingFacilityPrice = Mathf.Max(0, weddingFacilityPrice);
        loungeFacilityPrice = Mathf.Max(0, loungeFacilityPrice);
        hallFacilityPrice = Mathf.Max(0, hallFacilityPrice);
        saunaFacilityPrice = Mathf.Max(0, saunaFacilityPrice);
        cafeFacilityPrice = Mathf.Max(0, cafeFacilityPrice);
        bathFacilityPrice = Mathf.Max(0, bathFacilityPrice);
        hosFacilityPrice = Mathf.Max(0, hosFacilityPrice);
        
        sunbedReputation = Mathf.Max(0, sunbedReputation);
        restaurantReputation = Mathf.Max(0, restaurantReputation);
        healthFacilityReputation = Mathf.Max(0, healthFacilityReputation);
        bathtubReputation = Mathf.Max(0, bathtubReputation);
        weddingFacilityReputation = Mathf.Max(0, weddingFacilityReputation);
        loungeFacilityReputation = Mathf.Max(0, loungeFacilityReputation);
        hallFacilityReputation = Mathf.Max(0, hallFacilityReputation);
        saunaFacilityReputation = Mathf.Max(0, saunaFacilityReputation);
        cafeFacilityReputation = Mathf.Max(0, cafeFacilityReputation);
        hosFacilityReputation = Mathf.Max(0, hosFacilityReputation);
        bathFacilityReputation = Mathf.Max(0, bathFacilityReputation);
    }
}

