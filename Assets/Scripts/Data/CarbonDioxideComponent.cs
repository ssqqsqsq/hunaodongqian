using System;
using UnityEngine;

public class CarbonDioxideComponent : MonoBehaviour
{
    [Header("碳酸值配置")]
    [SerializeField] private float maxCarbonDioxide = 300f;
    [SerializeField] private float minCarbonDioxide = 0f;
    [SerializeField] private float hypercapniaThreshold = 200f;

    [Header("碳酸值变化速率")]
    [SerializeField] private float deathChangeRate = -1000f;
    [SerializeField] private float idleChangeRate = -20f;
    [SerializeField] private float swimChangeRate = -10f;
    [SerializeField] private float dashChangeRate = 10f;

    // 当前碳酸值
    public float CurrentCarbonDioxide { get; private set; }

    // 当前碳酸值变化速率（已含 Debuff 偏移）
    public float CurrentChangeRate { get; private set; }


    // 碳酸值变化事件
    public event Action<float> OnCarbonDioxideChanged;

    // 状态决定的基准速率，以及 Debuff 叠加的偏移
    private float stateChangeRate;
    private float debuffRateOffset;

    private PlayerSM SM;

    private void Awake()
    {
        CurrentCarbonDioxide = minCarbonDioxide;

        SM = GetComponent<PlayerSM>();

        stateChangeRate = idleChangeRate;
        RefreshChangeRate();
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
        UpdateCarbonDioxide();
    }

    // 根据当前变化速率更新碳酸值
    private void UpdateCarbonDioxide()
    {
        float oldValue = CurrentCarbonDioxide;

        CurrentCarbonDioxide += CurrentChangeRate * Time.deltaTime;

        // 限制数值范围
        CurrentCarbonDioxide = Mathf.Clamp(
            CurrentCarbonDioxide,
            minCarbonDioxide,
            maxCarbonDioxide
        );

        // 数值发生变化时广播事件
        if (!Mathf.Approximately(oldValue, CurrentCarbonDioxide))
        {
            OnCarbonDioxideChanged?.Invoke(CurrentCarbonDioxide);
        }

    }

    // 根据角色状态调整碳酸值变化速率
    private void OnCharacterStateChange(BaseState newState)
    {
        if (newState == SM.deathState)
        {
            stateChangeRate = deathChangeRate;
        }
        else if (newState == SM.idleState)
        {
            stateChangeRate = idleChangeRate;
        }
        else if (newState == SM.swimState)
        {
            stateChangeRate = swimChangeRate;
        }
        else if (newState == SM.dashState)
        {
            stateChangeRate = dashChangeRate;
        }

        RefreshChangeRate();
    }

    // Debuff 对变化速率的叠加（可为负）
    public void AddDebuffRateOffset(float offset)
    {
        debuffRateOffset += offset;
        RefreshChangeRate();
    }

    private void RefreshChangeRate()
    {
        CurrentChangeRate = stateChangeRate + debuffRateOffset;
    }
}
