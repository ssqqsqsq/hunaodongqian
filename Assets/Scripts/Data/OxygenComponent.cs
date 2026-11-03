using System;
using UnityEngine;

public class OxygenComponent : MonoBehaviour
{
    [Header("氧气配置")]
    [SerializeField] private float maxOxygen = 180f; // 单瓶最大氧气值
    [SerializeField] private float oxygenDepletionRate = 1f; // 待机状态下氧气消耗速率（每秒）
    [SerializeField] private float oxygenDepletionRateMoving = 2f; // 移动状态下氧气消耗速率（每秒）
    [SerializeField] private float oxygenDepletionRateDashing = 4f; // 冲刺状态下氧气消耗速率（每秒）
    [SerializeField] public bool HasAirTank = true; // 是否还有一瓶未使用的备用气瓶

    [Header("事件节流")]
    [SerializeField] private float changeEventInterval = 0.1f; // OnOxygenChanged 的最小广播间隔（秒）

    public float CurrentOxygen { get; private set; } // 当前气瓶剩余氧气值
    public bool IsOxygenEmpty { get; private set; } // 是否已经彻底没气
    public float TotalOxygen { get; private set; } // 剩余总气量（当前瓶 + 备用瓶）
    public float CurrentOxygenDepletionRate { get; private set; } // 当前氧气消耗速率
    public float TotalOxygenConsumed; // 累计消耗的氧气总量（不清零）

    public event Action<float> OnOxygenChanged; // 氧气值变化，已按 changeEventInterval 节流
    public event Action OnTankSwapped; // 备用气瓶被换上
    public event Action OnAirSupplyExhausted; // 彻底没气（此时会调用 Kill）

    private PlayerSM SM;

    // 距上次广播累计的时间，用于节流
    private float changeEventTimer;

    private void Awake()
    {
        CurrentOxygen = maxOxygen;

        SM = GetComponent<PlayerSM>();
        if (SM == null)
        {
            Debug.LogError("OxygenComponent：同物体上找不到 PlayerSM，死亡与状态相关的逻辑将失效！");
        }

        CurrentOxygenDepletionRate = oxygenDepletionRate;

        RefreshTotalOxygen();
    }

    private void OnEnable()
    {
        if (SM != null)
        {
            SM.OnStateChanged += OnCharacterStateChange;
        }
    }

    private void OnDisable()
    {
        if (SM != null)
        {
            SM.OnStateChanged -= OnCharacterStateChange;
        }
    }

    private void Update()
    {
        ConsumeOxygen();
        RefreshTotalOxygen();
        TickChangedEvent();
    }

    // 消耗氧气：数值夹在 0 以上，烧完一瓶自动换备用瓶
    private void ConsumeOxygen()
    {
        // 死亡后不再消耗，避免死亡瞬间把备用瓶也换掉
        if (SM != null && SM.IsDead) return;

        // 速率为 0（或负数）时不处理；目前没有回充方案，负速率不当作回气用
        float delta = CurrentOxygenDepletionRate * Time.deltaTime;
        if (delta <= 0f) return;

        CurrentOxygen -= delta;
        TotalOxygenConsumed += delta;

        if (CurrentOxygen > 0f) return;

        // 这一瓶烧完了
        CurrentOxygen = 0f;

        if (HasAirTank)
        {
            // 换上备用瓶：直接充满，并立刻广播一次，让 HUD 及时刷新
            HasAirTank = false;
            CurrentOxygen = maxOxygen;
            changeEventTimer = 0f;

            OnTankSwapped?.Invoke();
            OnOxygenChanged?.Invoke(CurrentOxygen);
        }
        else if (!IsOxygenEmpty)
        {
            // 没有备用瓶了，真没气：只触发一次
            IsOxygenEmpty = true;

            OnAirSupplyExhausted?.Invoke();
            SM?.Kill();
        }
    }

    // 剩余总气量 = 当前瓶 + 备用瓶（备用瓶用过就不算）
    private void RefreshTotalOxygen()
    {
        TotalOxygen = CurrentOxygen + (HasAirTank ? maxOxygen : 0f);
    }

    // 节流广播：每 changeEventInterval 秒最多广播一次
    private void TickChangedEvent()
    {
        changeEventTimer += Time.deltaTime;

        if (changeEventTimer < changeEventInterval) return;

        changeEventTimer = 0f;
        OnOxygenChanged?.Invoke(CurrentOxygen);
    }

    // 根据角色状态切换消耗速率
    private void OnCharacterStateChange(BaseState newState)
    {
        if (newState == SM.idleState)
        {
            CurrentOxygenDepletionRate = oxygenDepletionRate;
        }
        else if (newState == SM.swimState)
        {
            CurrentOxygenDepletionRate = oxygenDepletionRateMoving;
        }
        else if (newState == SM.dashState)
        {
            CurrentOxygenDepletionRate = oxygenDepletionRateDashing;
        }
        else if (newState == SM.deathState)
        {
            CurrentOxygenDepletionRate = 0f;
        }
    }
}
