using System;
using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance { get; set; }
    
    public int money = 1000;
    public event Action<int> OnMoneyChanged; // 돈이 변경될 때 발동할 이벤트

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void AddMoney(int amount)
    {
        money += amount;
        OnMoneyChanged?.Invoke(money); // 돈이 추가될 때 이벤트 발생
    }

    public void SpendMoney(int amount)
    {
        if (money >= amount)
        {
            money -= amount;
            OnMoneyChanged?.Invoke(money); // 돈이 감소될 때 이벤트 발생
        }
    }
}