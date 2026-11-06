using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using UnityEngine;

public class DebuffConfigLoader : MonoBehaviour
{
    [Header("JSON 配置文件")]
    [SerializeField] private TextAsset configFile;

    readonly Dictionary<int, DebuffConfig> debuffDict = new Dictionary<int, DebuffConfig>();

    public IReadOnlyDictionary<int, DebuffConfig> All => debuffDict;

    void Awake()
    {
        LoadConfig();
    }

    [ContextMenu("重新加载 Debuff 配置")]
    public void LoadConfig()
    {
        debuffDict.Clear();

        if (configFile == null)
        {
            Debug.LogError("DebuffConfigLoader：JSON 配置文件未绑定！");
            return;
        }

        RawRoot root;
        try
        {
            root = JsonConvert.DeserializeObject<RawRoot>(configFile.text);
        }
        catch (JsonException e)
        {
            Debug.LogError($"DebuffConfigLoader：JSON 解析失败 —— {e.Message}");
            return;
        }

        if (root == null || root.buffList == null)
        {
            Debug.LogError("DebuffConfigLoader：JSON 数据格式错误（缺少 buffList）");
            return;
        }

        int skipped = 0;
        foreach (RawDebuff raw in root.buffList)
        {
            DebuffConfig cfg = ToRuntime(raw);
            if (cfg == null)
            {
                skipped++;
                continue;
            }

            if (debuffDict.ContainsKey(cfg.id))
            {
                Debug.LogWarning($"DebuffConfigLoader：重复的 Debuff ID —— {cfg.id}，已忽略后出现的那条");
                skipped++;
                continue;
            }
            debuffDict.Add(cfg.id, cfg);
        }

        Debug.Log($"Debuff 配置加载完成：{debuffDict.Count} 条" + (skipped > 0 ? $"，跳过 {skipped} 条" : ""));
    }

    public DebuffConfig GetConfig(int id)
    {
        if (debuffDict.TryGetValue(id, out DebuffConfig cfg))
            return cfg;
        Debug.LogWarning($"DebuffConfigLoader：未找到 Debuff ID —— {id}");
        return null;
    }

    public bool TryGetConfig(int id, out DebuffConfig cfg)
    {
        return debuffDict.TryGetValue(id, out cfg);
    }

    static DebuffConfig ToRuntime(RawDebuff raw)
    {
        if (raw == null) return null;

        TriggerCondition trigger = ToTrigger(raw);
        if (trigger == null) return null;

        var cfg = new DebuffConfig
        {
            id = raw.id,
            name = raw.name,
            nameForGame = raw.nameForGame,
            group = raw.group,
            trigger = trigger,
            observeTime = DebuffScale.TimeFromTable(raw.obstime),
            graceTime = DebuffScale.TimeFromTable(raw.gracetime),
            healTime = DebuffScale.TimeFromTable(raw.healtime),
        };

        if (raw.effect != null)
        {
            foreach (int[] e in raw.effect)
            {
                EffectSpec spec = ToEffect(cfg.id, e);
                if (spec != null)
                    cfg.effects.Add(spec);
            }
        }
        return cfg;
    }

    // trigger 的第一个整数是操作类型，先按类型分派，再校验该类型的长度
    static TriggerCondition ToTrigger(RawDebuff raw)
    {
        int[] t = raw.trigger;
        if (t == null || t.Length < 2)
        {
            Debug.LogError($"DebuffConfigLoader：Debuff[{raw.id}] 的 trigger 至少需要 2 个整数");
            return null;
        }

        switch (t[0])
        {
            // 数值比对：0 Tag 阈值 运算符
            case 0:
            {
                if (t.Length != 4)
                {
                    Debug.LogError($"DebuffConfigLoader：Debuff[{raw.id}] 数值型 trigger 需要 4 个整数 [0,Tag,阈值,运算符]，实际 {t.Length} 个");
                    return null;
                }

                var attributeType = (DataType)t[1];
                if (!AttributeDomain.IsDefined(attributeType))
                {
                    Debug.LogError($"DebuffConfigLoader：Debuff[{raw.id}] 触发属性 {t[1]} 未定义");
                    return null;
                }

                if (!System.Enum.IsDefined(typeof(TriggerOperator), t[3]))
                {
                    Debug.LogError($"DebuffConfigLoader：Debuff[{raw.id}] 运算符 {t[3]} 未定义（1大 2小 3等于 4大于等于 5小于等于 6不等于）");
                    return null;
                }

                return new TriggerCondition
                {
                    kind = TriggerKind.Numeric,
                    attribute = attributeType,
                    threshold = t[2],
                    op = (TriggerOperator)t[3],
                };
            }

            // flag为真：1 flagNum
            case 1:
            {
                if (t.Length != 2)
                {
                    Debug.LogError($"DebuffConfigLoader：Debuff[{raw.id}] flag型 trigger 需要 2 个整数 [1,flagNum]，实际 {t.Length} 个");
                    return null;
                }

                if (!FlagTable.IsValidIndex(t[1]))
                {
                    Debug.LogError($"DebuffConfigLoader：Debuff[{raw.id}] flagNum={t[1]} 超出 flag 表范围（{FlagTable.MinIndex}..{FlagTable.MaxIndex}）");
                    return null;
                }

                return new TriggerCondition
                {
                    kind = TriggerKind.Flag,
                    flagNum = t[1],
                };
            }

            default:
                Debug.LogError($"DebuffConfigLoader：Debuff[{raw.id}] 的触发操作类型 {t[0]} 未定义（0数值比对 / 1flag为真）");
                return null;
        }
    }

    static EffectSpec ToEffect(int debuffId, int[] raw)
    {
        if (raw == null || raw.Length < 1)
        {
            Debug.LogError($"DebuffConfigLoader：Debuff[{debuffId}] 有空effect，跳过");
            return null;
        }

        if (!System.Enum.IsDefined(typeof(EffectTag), raw[0]))
        {
            Debug.LogError($"DebuffConfigLoader：Debuff[{debuffId}] 效果类型 {raw[0]} 未定义，跳过该效果");
            return null;
        }

        var spec = new EffectSpec
        {
            tag = (EffectTag)raw[0],
        };

        if (spec.tag == EffectTag.Unknown)
        {
            Debug.LogError($"DebuffConfigLoader：Debuff[{debuffId}] 效果类型 0（Unknown）无意义，跳过该效果");
            return null;
        }

        if (spec.tag == EffectTag.ValueChange)
        {
            if (raw.Length != 3)
            {
                Debug.LogError($"DebuffConfigLoader：Debuff[{debuffId}] 数值变化格式错误，需要 [4,属性,增量]，实际 {raw.Length} 个");
                return null;
            }

            var attribute = (DataType)raw[1];
            if (!AttributeDomain.IsDefined(attribute))
            {
                Debug.LogError($"DebuffConfigLoader：Debuff[{debuffId}] 数值变化的目标属性 {raw[1]} 未定义");
                return null;
            }

            spec.attribute = attribute;
            spec.operands = new[] { (float)raw[2] };
            return spec;
        }

        if (raw.Length > 1)
        {
            var operands = new float[raw.Length - 1];
            for (int i = 1; i < raw.Length; i++)
                operands[i - 1] = raw[i];
            spec.operands = operands;
        }
        return spec;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    class RawDebuff
    {
        public int id;
        public string name;
        public string nameForGame;
        public int group;
        public int[] trigger;
        public int obstime;
        public int gracetime;
        public int healtime;
        public List<int[]> effect;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    class RawRoot
    {
        public List<RawDebuff> buffList;
    }
}
