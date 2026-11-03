
using UnityEngine;
using System;


// 心率组件
public class HeartRateComponent : MonoBehaviour
{
    [Header("基础心率")]
    [SerializeField] private float baseHeartRate = 90f;

    [Header("状态心率偏移")]
    [SerializeField] private float idleOffset = 0f;
    [SerializeField] private float swimOffset = 10f;
    [SerializeField] private float dashOffset = 20f;
    [SerializeField] private float deathOffset = -180f;

    [Header("随机波动")]
    [SerializeField] private float fluctuationRange = 2f;
    [SerializeField] private float fluctuationSpeed = 0.5f;

    [Header("组件引用")]
    [SerializeField] private PlayerSM SM;

    // 当前心率
    public float CurrentHeartRate { get; private set; }

    // 当前状态产生的心率偏移
    private float stateOffset;

    // Debuff产生的心率偏移
    private float debuffOffset;

    private void Awake()
    {
        if (SM == null)
        {
            SM = GetComponent<PlayerSM>();
        }

        if (SM == null)
        {
            Debug.LogError("HeartRateComponent：未找到 PlayerSM！");
            enabled = false;
            return;
        }

        stateOffset = idleOffset;
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
        UpdateHeartRate();
    }

     // 根据状态更新心率偏移

    private void OnCharacterStateChange(BaseState newState)
    {
        if (newState == SM.idleState)
        {
            stateOffset = idleOffset;
        }
        else if (newState == SM.swimState)
        {
            stateOffset = swimOffset;
        }
        else if (newState == SM.dashState)
        {
            stateOffset = dashOffset;
        }
        else if (newState == SM.deathState)
        {
            stateOffset = deathOffset;
        }
    }

    
    // 计算当前心率
    
    private void UpdateHeartRate()
    {
        // 产生 -3 到 +3 之间的平滑波动
        float fluctuation =
            (Mathf.PerlinNoise(Time.time * fluctuationSpeed, 0f) * 2f - 1f)
            * fluctuationRange;

        // 基础心率 + 状态偏移 + Debuff偏移 + 随机波动
        CurrentHeartRate =
            baseHeartRate + stateOffset + debuffOffset + fluctuation;

        // 心率不能低于 0
        CurrentHeartRate = Mathf.Max(0f, CurrentHeartRate);
    }

   
    // 设置 Debuff 对心率的影响
    
    public void SetDebuffOffset(float offset)
    {
        debuffOffset = offset;
    }


    // 增加 Debuff 对心率的影响
    
    public void AddDebuffOffset(float offset)
    {
        debuffOffset += offset;
    }


    // 清除 Debuff 对心率的影响

    public void ClearDebuffOffset()
    {
        debuffOffset = 0f;
    }
}
