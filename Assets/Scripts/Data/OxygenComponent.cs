using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class OxygenComponent : MonoBehaviour
{
    [Header("氧气配置")]
    [SerializeField] private float maxOxygen = 180f; // 最大氧气值
    [SerializeField] private float oxygenDepletionRate = 1f; // 待机状态下氧气消耗速率（每秒）
    [SerializeField] private float oxygenDepletionRateMoving = 2f; // 移动状态下氧气消耗速率（每秒）
    [SerializeField] private float oxygenDepletionRateDashing = 4f; // 冲刺状态下氧气消耗速率（每秒）
    [SerializeField] private bool HasAirTank = true; // 是否拥有氧气罐
    public float CurrentOxygen { get; private set; }
    public bool IsOxygenEmpty { get; private set; }
    
    public float CurrentOxygenDepletionRate { get; private set; }// 当前氧气消耗速率

    public event Action<float> OnOxygenChanged;
    private PlayerSM SM;
    private void Awake()
    {
        CurrentOxygen = maxOxygen;
        SM = GetComponent<PlayerSM>();
        CurrentOxygenDepletionRate = oxygenDepletionRate;
    }
    private void OnEnable()
    {
        // 监听状态切换
        SM.OnStateChanged += OnCharacterStateChange;
    }

    private void OnDisable()
    {
        SM.OnStateChanged -= OnCharacterStateChange;
    }
    void Start()
    {
        
    }

    void Update()
    {
        CurrentOxygen -= CurrentOxygenDepletionRate * Time.deltaTime;
        if(CurrentOxygenDepletionRate * Time.deltaTime>0.5)
        {
            OnOxygenChanged?.Invoke(CurrentOxygen);
        }
        if (CurrentOxygen <= 0f)
        {
            if(HasAirTank)
            {
                //再说
            }
            else
            {
                IsOxygenEmpty = true;
                SM.Kill();
            }
            
        }
    }
    private void OnCharacterStateChange(BaseState newState)
    {
        if(newState== SM.idleState)
        {
            CurrentOxygenDepletionRate = oxygenDepletionRate;
        }
        else if(newState == SM.swimState)
        {
            CurrentOxygenDepletionRate = oxygenDepletionRateMoving;
        }
        else if(newState == SM.dashState)
        {
            CurrentOxygenDepletionRate = oxygenDepletionRateDashing;
        }
        else if(newState == SM.deathState)
        {
            CurrentOxygenDepletionRate = 0f;
        }
    }
}
