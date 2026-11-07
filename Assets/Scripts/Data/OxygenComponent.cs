using System;
using UnityEngine;

public class OxygenComponent : MonoBehaviour
{
    [Header("氧气配置")]
    [SerializeField] private float maxOxygen = 1800f; // 单瓶最大氧气值
    [SerializeField] private float oxygenDepletionRate = 10f; // 待机状态下氧气消耗速率（每秒）
    [SerializeField] private float oxygenDepletionRateMoving = 20f; // 移动状态下氧气消耗速率（每秒）
    [SerializeField] private float oxygenDepletionRateDashing = 40f; // 冲刺状态下氧气消耗速率（每秒）
    [SerializeField] public bool HasAirTank = true; // 是否还有一瓶未使用的备用气瓶
    private float spareOxygen; // 备用瓶当前剩余气体（与手持物/功能按钮联动）
    private float heldTankGas; // 手上气瓶气体（卸下但未丢出时仍计入总气量）

    [Header("事件节流")]
    [SerializeField] private float changeEventInterval = 0.1f; // OnOxygenChanged 的最小广播间隔（秒）

    public float CurrentOxygen { get; private set; } // 当前气瓶剩余氧气值
    public bool IsOxygenEmpty { get; private set; } // 是否已经彻底没气
    public float TotalOxygen { get; private set; } // 剩余总气量（当前瓶 + 备用瓶）
    public float CurrentOxygenDepletionRate { get; private set; } // 当前氧气消耗速率（已含 Debuff 偏移）
    public float TotalOxygenConsumed; // 累计消耗的氧气总量（不清零）

    public event Action<float> OnOxygenChanged; // 氧气值变化，已按 changeEventInterval 节流
    public event Action OnTankSwapped; // 备用气瓶被换上
    public event Action OnAirSupplyExhausted; // 彻底没气（由功能键的憋气/换瓶流程处理死亡）
    public event Action OnTankEmpty; // 当前气瓶耗尽（不再自动换瓶，由监听方决定后续）

    private PlayerSM SM;

    // 状态决定的基准速率，以及 Debuff 叠加的偏移
    private float stateDepletionRate;
    private float debuffRateOffset;

    // 距上次广播累计的时间，用于节流
    private float changeEventTimer;

    private void Awake()
    {
        CurrentOxygen = maxOxygen;
        spareOxygen = maxOxygen;

        SM = GetComponent<PlayerSM>();
        if (SM == null)
        {
            Debug.LogError("OxygenComponent：同物体上找不到 PlayerSM，死亡与状态相关的逻辑将失效！");
        }

        stateDepletionRate = oxygenDepletionRate;
        RefreshDepletionRate();

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

    // 消耗氧气：数值夹在 0 以上；当前瓶烧完不再自动换瓶 / 立即死亡，
    // 改为广播 OnTankEmpty，由功能键决定：有备用瓶 → 强制换瓶QTE；只有一个 → 憋气倒计时。
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

        if (!IsOxygenEmpty)
        {
            IsOxygenEmpty = true;
            changeEventTimer = 0f;

            OnOxygenChanged?.Invoke(CurrentOxygen);
            OnTankEmpty?.Invoke();
        }
    }

    // 剩余总气量 = 当前瓶 + 备用瓶 + 手上拿着的气瓶
    private void RefreshTotalOxygen()
    {
        TotalOxygen = CurrentOxygen + (HasAirTank ? spareOxygen : 0f) + heldTankGas;
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
            stateDepletionRate = oxygenDepletionRate;
        }
        else if (newState == SM.swimState)
        {
            stateDepletionRate = oxygenDepletionRateMoving;
        }
        else if (newState == SM.dashState)
        {
            stateDepletionRate = oxygenDepletionRateDashing;
        }
        else if (newState == SM.deathState)
        {
            stateDepletionRate = 0f;
        }

        RefreshDepletionRate();
    }

    // Debuff 对消耗速率的叠加（可为负）
    public void AddDebuffRateOffset(float offset)
    {
        debuffRateOffset += offset;
        RefreshDepletionRate();
    }

    // ---- 备用瓶外部接口（手持物 / 功能按钮） ----
    public float MaxOxygen => maxOxygen;
    public float SpareOxygen => spareOxygen;

    // 把备用瓶装备回身上（气体由手上气瓶同步）
    public void SetSpareTank(float gas)
    {
        spareOxygen = Mathf.Clamp(gas, 0f, maxOxygen);
        HasAirTank = true;
        heldTankGas = 0f; // 装备后不再计入手上
        RefreshTotalOxygen();
    }

    // 切换当前使用的气瓶：当前瓶 ↔ 备用瓶互换（换瓶 QTE 成功后调用）
    public void SwitchTank()
    {
        if (!HasAirTank || spareOxygen <= 0f) return;

        float tmp = CurrentOxygen;
        CurrentOxygen = spareOxygen;
        spareOxygen = tmp;

        if (CurrentOxygen > 0f) IsOxygenEmpty = false; // 气体恢复，下次耗尽可再次广播

        OnOxygenChanged?.Invoke(CurrentOxygen);
        RefreshTotalOxygen();
    }

    // 手上拿着气瓶（卸下的备用瓶在手上时仍计入总气量，丢出后扣除）
    public void SetHeldTank(float gas)
    {
        heldTankGas = Mathf.Clamp(gas, 0f, maxOxygen);
        RefreshTotalOxygen();
    }

    // 丢出/投掷手上气瓶：从总气量中扣除
    public void ClearHeldTank()
    {
        if (heldTankGas == 0f) return;
        heldTankGas = 0f;
        RefreshTotalOxygen();
        OnOxygenChanged?.Invoke(CurrentOxygen);
    }

    // 实际速率 = 状态基准 + Debuff 偏移，不允许为负（负速率不当作回气用）
    private void RefreshDepletionRate()
    {
        CurrentOxygenDepletionRate = Mathf.Max(0f, stateDepletionRate + debuffRateOffset);
    }
}
