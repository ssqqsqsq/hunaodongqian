// ============================================================
// 临时调试 HUD（可直接删除本文件，不留任何残留）
// - 不需要挂到任何物体上，也不需要改场景：进 Play 自动出现
// - UI 全部由代码在运行时创建（Canvas + Text），删文件即彻底移除
// - F1 显示/隐藏    F2 只看生效中 / 显示全部
// ============================================================

using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class DebugHud : MonoBehaviour
{
    // 场景加载后自动生成一个 HUD 物体，开发者什么都不用做
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("~DebugHud");
        go.AddComponent<DebugHud>();
        DontDestroyOnLoad(go);
    }

    private const float RefreshInterval = 0.1f; // 10Hz 刷新，够看且不刷屏
    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);

    private DebuffManager manager;
    private DebuffConfigLoader loader;
    private PlayerSM player;
    private HeartRateComponent heart;
    private CarbonDioxideComponent co2;
    private NitrogenNarcosisComponent n2;
    private DepthComponent depth;
    private OxygenComponent oxygen;

    private GameObject panelRoot;
    private RectTransform panelRect;
    private Text text;
    private bool visible = true;
    private bool showAll;      // false：只列生效中的；true：21 条全列
    private float timer;
    private string stateName = "-";

    private readonly StringBuilder sb = new StringBuilder(4096);

    private void Awake()
    {
        BuildUi();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            visible = !visible;
            if (panelRoot != null) panelRoot.SetActive(visible);
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            showAll = !showAll;
        }

        if (!visible) return;

        Resolve();

        timer += Time.unscaledDeltaTime;
        if (timer < RefreshInterval) return;
        timer = 0f;

        Refresh();
    }

    // ---------- 建 UI ----------

    private void BuildUi()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panelGo = new GameObject("Panel", typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);

        Image panel = panelGo.GetComponent<Image>();
        panel.color = PanelColor;

        panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(12f, -12f);
        panelRect.sizeDelta = new Vector2(820f, 470f);

        var textGo = new GameObject("Text", typeof(Text));
        textGo.transform.SetParent(panelGo.transform, false);

        text = textGo.GetComponent<Text>();
        text.font = GetBuiltinFont();
        text.fontSize = 17;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;

        RectTransform trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(10f, 10f);
        trt.offsetMax = new Vector2(-10f, -10f);

        Shadow shadow = textGo.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(1f, -1f);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);

        panelRoot = canvasGo;
    }

    private static Font GetBuiltinFont()
    {
        // 内置 LegacyRuntime.ttf/Arial 不含中文字形，优先用系统里的中文字体
        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun", "PingFang SC" }, 18);
        if (font != null) return font;

        // 上面都找不到时退回内置字体（中文会显示为方块，但英文数字仍可读）
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    // ---------- 找引用（找不到就显示提示，不会报错） ----------

    private void Resolve()
    {
        if (player != null) return;

        player = FindFirstObjectByType<PlayerSM>();
        if (player == null) return;

        heart = player.GetComponent<HeartRateComponent>();
        co2 = player.GetComponent<CarbonDioxideComponent>();
        n2 = player.GetComponent<NitrogenNarcosisComponent>();
        depth = player.GetComponent<DepthComponent>();
        oxygen = player.GetComponent<OxygenComponent>();

        manager = FindFirstObjectByType<DebuffManager>();
        loader = FindFirstObjectByType<DebuffConfigLoader>();

        player.OnStateChanged += OnPlayerStateChanged;
    }

    private void OnPlayerStateChanged(BaseState state)
    {
        stateName = state.GetType().Name.Replace("State", "");
    }

    // ---------- 刷新文本 ----------

    private void Refresh()
    {
        if (text == null) return;

        sb.Clear();
        sb.Append("<b>~ Debug HUD</b>   <size=14><color=#AAAAAA>F1 隐藏    F2 全部/生效中</color></size>\n");

        if (player == null)
        {
            sb.Append("\n<color=#FF8080>场景里找不到 PlayerSM</color>\n");
            Apply();
            return;
        }

        AppendPlayer();
        sb.Append('\n');
        AppendAttributes();
        sb.Append('\n');
        AppendDebuffs();

        Apply();
    }

    // 写完文本后把面板高度收到内容高度
    private void Apply()
    {
        text.text = sb.ToString();

        if (panelRect != null)
        {
            float height = Mathf.Max(180f, text.preferredHeight + 26f);
            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, height);
        }
    }

    private void AppendPlayer()
    {
        sb.Append($"状态: <b>{stateName}</b>    死亡: {FlagText(player.IsDead)}    强制疾跑: {FlagText(player.ForceSprint)}\n");
        sb.Append($"坐标: ({player.transform.position.x:0.0}, {player.transform.position.y:0.0})\n");
    }

    private void AppendAttributes()
    {
        sb.Append($"{"心率",-6}{(heart != null ? heart.CurrentHeartRate : 0f),7:0.0} bpm\n");

        sb.Append($"{"碳酸",-6}{(co2 != null ? co2.CurrentCarbonDioxide : 0f),7:0.0} / 30");
        sb.Append($"    速率 {SRate(co2 != null ? co2.CurrentChangeRate : 0f)}\n");

        sb.Append($"{"氮醉",-6}{(n2 != null ? n2.CurrentNitrogenNarcosis : 0f),7:0.0} / 30");
        sb.Append($"    速率 {SRate(n2 != null ? n2.CurrentChangeRate : 0f)}\n");

        sb.Append($"{"深度",-6}{(depth != null ? depth.CurrentDepth : 0f),7:0.0} m\n");

        if (oxygen != null)
        {
            sb.Append($"{"总气量",-6}{oxygen.TotalOxygen,7:0.0}");
            sb.Append($"    本瓶 {oxygen.CurrentOxygen:0.0}    消耗 {oxygen.CurrentOxygenDepletionRate:0.00}/s");
            sb.Append($"    备用瓶 {FlagText(oxygen.HasAirTank)}\n");
        }
        else
        {
            sb.Append("总气量   (找不到 OxygenComponent)\n");
        }
    }

    private void AppendDebuffs()
    {
        sb.Append("<b>Debuffs</b>");

        if (manager == null)
        {
            sb.Append("\n  <color=#FF8080>场景里没有 DebuffManager，Debuff 不会跑</color>\n");
            return;
        }

        int total = loader != null ? loader.All.Count : 0;
        sb.Append($"   已加载 {total} 条\n");
        sb.Append("<size=14><color=#999999>  格式：阶段  已读/总  |  观·宽·疗 = 配置时长(s)，∞ 表示 -1</color></size>\n");

        if (loader == null) return;

        int shown = 0;
        foreach (DebuffConfig cfg in loader.All.Values)
        {
            DebuffPhase phase = manager.GetPhase(cfg.id);
            if (!showAll && phase == DebuffPhase.Inactive) continue;

            shown++;
            sb.Append($"  [{cfg.id:00}] {cfg.name,-12} {PhaseText(phase),-34} {ProgressText(cfg, phase),-18}");
            sb.Append($"观{D(cfg.observeTime)} 宽{D(cfg.graceTime)} 疗{D(cfg.healTime)}\n");
        }

        if (shown == 0) sb.Append("  <color=#888888>（当前没有生效中的 Debuff，按 F2 看全部）</color>\n");

        sb.Append($"  flags: {manager.Flags}");
    }

    // 当前阶段的读秒进度，例如 "0.35/0.80s"；坐实后显示"永久"
    private string ProgressText(DebuffConfig cfg, DebuffPhase phase)
    {
        switch (phase)
        {
            case DebuffPhase.Active:
                return "<color=#FF5A5A>永久保留</color>";

            case DebuffPhase.Inactive:
                return "";

            default:
            {
                float total = phase == DebuffPhase.Observing ? cfg.observeTime
                            : phase == DebuffPhase.Grace ? cfg.graceTime
                            : cfg.healTime;

                float now = manager.GetPhaseTimer(cfg.id);
                string totalText = total < 0f ? "∞" : total.ToString("0.00");
                return $"{now:0.00}/{totalText}s";
            }
        }
    }

    // ---------- 小工具 ----------

    // 配置里的时长（秒），-1 显示成 ∞
    private static string D(float seconds)
    {
        return seconds < 0f ? "∞" : seconds.ToString("0.##");
    }

    private static string PhaseText(DebuffPhase phase)
    {
        switch (phase)
        {
            case DebuffPhase.Observing: return "<color=#FFD24A>观察期</color>";
            case DebuffPhase.Active: return "<color=#FF5A5A>已坐实</color>";
            case DebuffPhase.Grace: return "<color=#FFA24A>宽容期</color>";
            case DebuffPhase.Heal: return "<color=#7AD07A>疗愈期</color>";
            default: return "<color=#888888>未触发</color>";
        }
    }

    private static string FlagText(bool value)
    {
        return value ? "<color=#FF5A5A>是</color>" : "否";
    }

    private static string SRate(float rate)
    {
        return $"{rate:+0.00;-0.00;0.00}/s";
    }
}
