
using System;
using UnityEngine;

public class CarbonDioxideComponent : MonoBehaviour
{
    [Header("碳酸值配置")]
    [SerializeField] private float maxCarbonDioxide = 30f;
    [SerializeField] private float minCarbonDioxide = 0f;
    [SerializeField] private float hypercapniaThreshold = 20f;

    [Header("碳酸值变化速率")]
    [SerializeField] private float deathChangeRate = -100f;
    [SerializeField] private float idleChangeRate = -2f;
    [SerializeField] private float swimChangeRate = -1f;
    [SerializeField] private float dashChangeRate = 1f;

    // 当前碳酸值
    public float CurrentCarbonDioxide { get; private set; }

    // 当前碳酸值变化速率
    public float CurrentChangeRate { get; private set; }


    // 碳酸值变化事件
    public event Action<float> OnCarbonDioxideChanged;



    private PlayerSM SM;

    private void Awake()
    {
        CurrentCarbonDioxide = minCarbonDioxide;

        SM = GetComponent<PlayerSM>();

        CurrentChangeRate = idleChangeRate;
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
            CurrentChangeRate = deathChangeRate;
        }
        else if (newState == SM.idleState)
        {
            CurrentChangeRate = idleChangeRate;
        }
        else if (newState == SM.swimState)
        {
            CurrentChangeRate = swimChangeRate;
        }
        else if (newState == SM.dashState)
        {
            CurrentChangeRate = dashChangeRate;
        }
    }


}
