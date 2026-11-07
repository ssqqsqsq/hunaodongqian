using System;
using System.Collections.Generic;
using UnityEngine;

// Debuff 的运行时阶段（流程见策划《触发条件/trigger 细则》）
public enum DebuffPhase
{
    Inactive = 0,   // 未触发
    Observing = 1,  // 观察期：条件已满足，正在累计 observeTime
    Active = 2,     // 已坐实：宽容期满，流程结束，效果永久保留且不再判定
    Grace = 3,      // 宽容期：Debuff 已授予，持续满足满 graceTime 则坐实
    Heal = 4,       // 疗愈期：连续不满足，满 healTime 后移除
}

// Debuff 主控：判定触发 -> 推进 observe/grace/heal -> 施加/撤销效果
public class DebuffManager : MonoBehaviour
{
    [Header("依赖（留空则从同物体自动获取）")]
    [SerializeField] private DebuffConfigLoader configLoader;
    [SerializeField] private DataManager dataManager;
    [SerializeField] private PlayerSM playerSM;
    [SerializeField] private HeartRateComponent heartRate;
    [SerializeField] private OxygenComponent oxygen;
    [SerializeField] private CarbonDioxideComponent carbonDioxide;
    [SerializeField] private NitrogenNarcosisComponent nitrogen;

    [Header("调试")]
    [SerializeField] private bool logStateChange = false;

    // flag 表由突发事件系统写入，Debuff 只读
    public FlagTable Flags { get; } = new FlagTable();

    public event Action<DebuffConfig> OnDebuffActivated;   // 进入发作
    public event Action<DebuffConfig> OnDebuffHealed;      // 完全解除
    // 视觉/操作类效果暂时没有接收方，先抛给外部系统接管
    public event Action<DebuffConfig, EffectSpec> OnEffectApplied;
    public event Action<DebuffConfig, EffectSpec> OnEffectRemoved;

    // 一条 Debuff 的运行时状态
    class Runtime
    {
        public DebuffConfig config;
        public DebuffPhase phase = DebuffPhase.Inactive;
        public float timer;
        public bool applied;      // 效果是否已施加
        public bool suppressed;   // 组内未被选中，本帧不参与判定
    }

    readonly Dictionary<int, Runtime> runtimes = new Dictionary<int, Runtime>();
    readonly Dictionary<int, List<Runtime>> groups = new Dictionary<int, List<Runtime>>();
    readonly HashSet<int> warnedEffects = new HashSet<int>();

    private void Awake()
    {
        AutoBind();
    }

    private void Start()
    {
        BuildRuntimes();
    }

    private void AutoBind()
    {
        if (configLoader == null) configLoader = GetComponent<DebuffConfigLoader>();
        if (dataManager == null) dataManager = GetComponent<DataManager>();
        if (playerSM == null) playerSM = GetComponent<PlayerSM>();
        if (heartRate == null) heartRate = GetComponent<HeartRateComponent>();
        if (oxygen == null) oxygen = GetComponent<OxygenComponent>();
        if (carbonDioxide == null) carbonDioxide = GetComponent<CarbonDioxideComponent>();
        if (nitrogen == null) nitrogen = GetComponent<NitrogenNarcosisComponent>();
    }

    private void BuildRuntimes()
    {
        runtimes.Clear();
        groups.Clear();

        if (configLoader == null)
        {
            Debug.LogError("DebuffManager：缺少 DebuffConfigLoader，Debuff 系统不会工作");
            return;
        }

        if (dataManager == null)
        {
            Debug.LogError("DebuffManager：缺少 DataManager，数值型触发无法判定");
        }

        foreach (DebuffConfig cfg in configLoader.All.Values)
        {
            var rt = new Runtime { config = cfg };
            runtimes.Add(cfg.id, rt);

            if (cfg.group <= 0) continue;

            if (!groups.TryGetValue(cfg.group, out List<Runtime> list))
            {
                list = new List<Runtime>();
                groups.Add(cfg.group, list);
            }
            list.Add(rt);
        }
    }

    private void Update()
    {
        if (runtimes.Count == 0) return;

        // 死亡后不再判定新的 Debuff
        if (playerSM != null && playerSM.IsDead) return;

        float dt = Time.deltaTime;

        ResolveGroups();

        foreach (Runtime rt in runtimes.Values)
        {
            if (rt.suppressed && rt.phase == DebuffPhase.Inactive) continue;

            bool triggered = !rt.suppressed && IsTriggered(rt.config);
            Tick(rt, triggered, dt);
        }
    }

    // 同一个 group 同时只允许一条在跑；全组都休眠时，从满足条件的成员里随机挑一条
    private void ResolveGroups()
    {
        foreach (KeyValuePair<int, List<Runtime>> pair in groups)
        {
            List<Runtime> list = pair.Value;

            Runtime owner = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].phase != DebuffPhase.Inactive)
                {
                    owner = list[i];
                    break;
                }
            }

            if (owner != null)
            {
                for (int i = 0; i < list.Count; i++)
                    list[i].suppressed = list[i] != owner;
                continue;
            }

            List<Runtime> candidates = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (!IsTriggered(list[i].config)) continue;
                if (candidates == null) candidates = new List<Runtime>();
                candidates.Add(list[i]);
            }

            if (candidates == null)
            {
                for (int i = 0; i < list.Count; i++)
                    list[i].suppressed = false;
                continue;
            }

            Runtime pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];

            for (int i = 0; i < list.Count; i++)
                list[i].suppressed = list[i] != pick;

            StartObserve(pick);
        }
    }

    private bool IsTriggered(DebuffConfig cfg)
    {
        TriggerCondition t = cfg.trigger;
        if (t == null) return false;

        if (t.kind == TriggerKind.Flag)
            return t.IsSatisfied(0f, Flags);

        if (dataManager == null) return false;
        return t.IsSatisfied(dataManager.GetValue(t.attribute), null);
    }

    private void Tick(Runtime rt, bool triggered, float dt)
    {
        DebuffConfig cfg = rt.config;

        switch (rt.phase)
        {
            case DebuffPhase.Inactive:
                if (triggered) StartObserve(rt);
                break;

            // 观察期：连续满足才读秒，中途失效直接回未触发
            case DebuffPhase.Observing:
                if (!triggered)
                {
                    rt.phase = DebuffPhase.Inactive;
                    rt.timer = 0f;
                }
                else if (cfg.ObserveInfinite)
                {
                    // 观察期无限：永远不授予
                }
                else if ((rt.timer += dt) >= cfg.observeTime)
                {
                    Activate(rt); // 授予 Debuff，并进入宽容期
                }
                break;

            // 宽容期：持续满足满 graceTime 则坐实（流程结束、Debuff 保留）
            // 中途失效则重置读秒，转疗愈期
            case DebuffPhase.Grace:
                if (!triggered)
                {
                    rt.phase = DebuffPhase.Heal;
                    rt.timer = 0f;
                    if (cfg.healTime <= 0f) Deactivate(rt); // 疗愈时长为 0：直接移除
                }
                else if (cfg.GraceInfinite)
                {
                    // 宽容期无限：只要还满足就一直保留
                }
                else if ((rt.timer += dt) >= cfg.graceTime)
                {
                    rt.phase = DebuffPhase.Active;
                    rt.timer = 0f;
                }
                break;

            // 已坐实：流程结束，不再判定
            case DebuffPhase.Active:
                break;

            // 疗愈期：连续不满足满 healTime 才移除；重新满足则打断疗愈，回宽容期
            case DebuffPhase.Heal:
                if (triggered)
                {
                    rt.phase = DebuffPhase.Grace;
                    rt.timer = 0f;
                }
                else if (!cfg.HealInfinite && (rt.timer += dt) >= cfg.healTime)
                {
                    Deactivate(rt);
                }
                break;
        }
    }

    private void StartObserve(Runtime rt)
    {
        rt.phase = DebuffPhase.Observing;
        rt.timer = 0f;

        if (rt.config.observeTime <= 0f) Activate(rt);
    }

    private void Activate(Runtime rt)
    {
        if (rt.applied) return;

        rt.phase = DebuffPhase.Grace; // 授予瞬间进入宽容期
        rt.timer = 0f;
        rt.applied = true;

        ApplyAll(rt, true);

        if (logStateChange)
            Debug.Log($"Debuff 发作：[{rt.config.id}] {rt.config.name}");

        OnDebuffActivated?.Invoke(rt.config);
    }

    private void Deactivate(Runtime rt)
    {
        rt.phase = DebuffPhase.Inactive;
        rt.timer = 0f;

        bool wasApplied = rt.applied;
        if (rt.applied)
        {
            rt.applied = false;
            ApplyAll(rt, false);
        }

        // 突发事件（flag 触发的 debuff）被解决后 → 触发「心有余悸」（flag5=1）
        if (wasApplied) TriggerResidualFear(rt.config);

        if (logStateChange)
            Debug.Log($"Debuff 解除：[{rt.config.id}] {rt.config.name}");

        OnDebuffHealed?.Invoke(rt.config);
    }

    // 突发事件解决（不再满足 trigger）后触发「心有余悸」：把 flag5 置真，由 flag[1,5] 的 debuff 接手。
    // 心有余悸自身（触发的 flag 就是 5）不参与，避免自我循环。
    private void TriggerResidualFear(DebuffConfig cfg)
    {
        TriggerCondition t = cfg.trigger;
        if (t == null || t.kind != TriggerKind.Flag) return;
        if (t.flagNum == 5) return;

        if (!Flags.IsTrue(5))
        {
            SetFlag(5, true);
            if (logStateChange) Debug.Log("[心有余悸] 突发事件解决 → flag5=1");
        }
    }

    private void ApplyAll(Runtime rt, bool apply)
    {
        foreach (EffectSpec e in rt.config.effects)
            ApplyEffect(rt.config, e, apply);
    }

    // 是否还有别的 Debuff 在施加「强制疾跑」（多个来源时不能随便关掉）
    private bool HasAppliedForceSprint()
    {
        foreach (Runtime rt in runtimes.Values)
        {
            if (!rt.applied) continue;

            foreach (EffectSpec e in rt.config.effects)
            {
                if (e.tag == EffectTag.ForceSprint) return true;
            }
        }
        return false;
    }

    private void ApplyEffect(DebuffConfig cfg, EffectSpec e, bool apply)
    {
        if (e.tag == EffectTag.Death)
        {
            // 死亡不可逆，撤销时不做任何事
            if (apply) playerSM?.Kill();
            return;
        }

        if (e.tag == EffectTag.ForceSprint)
        {
            if (playerSM != null)
                playerSM.ForceSprint = apply || HasAppliedForceSprint();
            return;
        }

        if (e.tag != EffectTag.ValueChange)
        {
            // 视觉/操作类效果交给 EffectController 处理（屏幕昏暗/扭曲/暂留/闪烁、
            // 数值显示错误、操作延迟/反向/随机偏移等），这里只负责把施加/解除广播出去。
            if (apply) OnEffectApplied?.Invoke(cfg, e);
            else OnEffectRemoved?.Invoke(cfg, e);
            return;
        }

        float value = e.operands.Length > 0 ? e.operands[0] : 0f;
        float delta = apply ? value : -value;

        switch (e.attribute)
        {
            case DataType.HeartRate:
                heartRate?.AddDebuffOffset(delta);
                break;

            case DataType.OxygenRate:
                oxygen?.AddDebuffRateOffset(delta);
                break;

            case DataType.CarbonDioxideRate:
                carbonDioxide?.AddDebuffRateOffset(delta);
                break;

            case DataType.NitrogenRate:
                nitrogen?.AddDebuffRateOffset(delta);
                break;

            default:
                int key = cfg.id * 100 + 50 + (int)e.attribute;
                if (warnedEffects.Add(key))
                    Debug.LogWarning($"DebuffManager：暂不支持直接修改属性 {e.attribute}（Debuff[{cfg.id}]），已忽略");
                break;
        }
    }

    // ---- 对外查询 ----

    public DebuffPhase GetPhase(int id)
    {
        return runtimes.TryGetValue(id, out Runtime rt) ? rt.phase : DebuffPhase.Inactive;
    }

    // 当前阶段已经读了多少秒（调试 HUD 用）
    public float GetPhaseTimer(int id)
    {
        return runtimes.TryGetValue(id, out Runtime rt) ? rt.timer : 0f;
    }

    // 效果是否正在生效（发作 / 宽限 / 疗愈中都算）
    public bool IsEffectApplied(int id)
    {
        return runtimes.TryGetValue(id, out Runtime rt) && rt.applied;
    }

    // 给突发事件系统写 flag 用
    public void SetFlag(int flagNum, bool value)
    {
        Flags.Set(flagNum, value);
    }
}
