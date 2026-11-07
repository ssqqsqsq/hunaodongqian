using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 任务显示：屏幕右上角的任务面板（灰色底框，靠右）。
// 根据打卡区 / 撤离区 / 任务状态切换当前任务文案：
//   · 开局           → 前往地图所示位置打卡
//   · 进入打卡区读秒 → 请在这里待上10秒钟（带剩余秒数）
//   · 中途离开打卡区 → 回到「前往地图所示位置打卡」
//   · 打卡完成       → 任务划线打勾，换行写新任务「离开洞穴」
//   · 撤离成功/失败  → 打勾 / 标红
// 运行时自动创建（无需挂到场景）；文案、颜色、位置都可在 Inspector 调。
public class MissionDisplay : MonoBehaviour
{
    [Header("任务文案")]
    [SerializeField] private string initialTaskText = "前往地图所示位置打卡";
    [SerializeField] private string checkpointWaitText = "请在这里待上10秒钟";
    [Tooltip("打卡倒计时时显示剩余秒数")]
    [SerializeField] private bool showCheckpointCountdown = true;
    [SerializeField] private string evacuationTaskText = "离开洞穴";
    [SerializeField] private string evacuatingText = "正在撤离…";
    [Tooltip("撤离倒计时时显示剩余秒数")]
    [SerializeField] private bool showEvacuationCountdown = true;
    [SerializeField] private string successText = "撤离成功";
    [SerializeField] private string failText = "打卡失败";

    [Header("外观")]
    [SerializeField] private Color boxColor = new Color(0.06f, 0.07f, 0.09f, 0.8f);
    [SerializeField] private Color activeTextColor = new Color(0.95f, 0.96f, 0.98f, 1f);
    [SerializeField] private Color completedTextColor = new Color(0.55f, 0.58f, 0.62f, 1f);
    [SerializeField] private Color successColor = new Color(0.45f, 1f, 0.55f, 1f);
    [SerializeField] private Color failColor = new Color(1f, 0.42f, 0.36f, 1f);
    [SerializeField] private float panelWidth = 460f;
    [SerializeField] private Vector2 panelOffset = new Vector2(-10f, -70f);
    [SerializeField] private float rowHeight = 50f;
    [SerializeField] private int fontSize = 26;

    [Header("调试")]
    [SerializeField] private bool verboseLog = true;

    // 一条任务行
    class TaskItem
    {
        public RectTransform root;
        public Text text;
        public RectTransform line;   // 完成划线
        public string baseText;      // 大任务本体文案
        public string overrideText;  // 进行中的子任务文案（null = 显示本体）
        public bool completed;
    }

    private GameObject canvasGo;
    private RectTransform listRoot;

    private MissionManager mission;
    private CheckpointZone checkpoint;
    private EvacuationZone evacuation;

    private readonly List<TaskItem> items = new List<TaskItem>();
    private TaskItem current;

    // 场景里没有就自动补一个
    private static bool _sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (!_sceneHooked)
        {
            _sceneHooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded; // 场景重载（R 重开）后重建
        }
        if (FindFirstObjectByType<MissionDisplay>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("~MissionDisplay");
        go.AddComponent<MissionDisplay>();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
    }

    private void Awake()
    {
        BuildUi();
    }

    private void Start()
    {
        mission = FindFirstObjectByType<MissionManager>();
        checkpoint = FindFirstObjectByType<CheckpointZone>();
        evacuation = FindFirstObjectByType<EvacuationZone>();

        if (verboseLog)
        {
            if (mission == null) Debug.LogWarning("MissionDisplay：场景里没有 MissionManager");
            if (checkpoint == null) Debug.LogWarning("MissionDisplay：场景里没有 CheckpointZone，打卡文案不会切换");
            if (evacuation == null) Debug.LogWarning("MissionDisplay：场景里没有 EvacuationZone，撤离文案不会切换");
        }

        AddTask(initialTaskText);

        if (checkpoint != null)
        {
            checkpoint.OnCountdownStart += OnCheckpointStart;
            checkpoint.OnCountdownChanged += OnCheckpointChanged;
            checkpoint.OnCountdownReset += OnCheckpointReset;
            checkpoint.OnCheckpointCompleted += OnCheckpointComplete;
        }
        if (evacuation != null)
        {
            evacuation.OnCountdownStart += OnEvacuationStart;
            evacuation.OnCountdownChanged += OnEvacuationChanged;
            evacuation.OnCountdownReset += OnEvacuationReset;
            evacuation.OnCountdownFinished += OnEvacuationFinished;
        }
        if (mission != null)
        {
            mission.OnMissionChanged += OnMissionChanged;
        }
    }

    private void OnDestroy()
    {
        if (checkpoint != null)
        {
            checkpoint.OnCountdownStart -= OnCheckpointStart;
            checkpoint.OnCountdownChanged -= OnCheckpointChanged;
            checkpoint.OnCountdownReset -= OnCheckpointReset;
            checkpoint.OnCheckpointCompleted -= OnCheckpointComplete;
        }
        if (evacuation != null)
        {
            evacuation.OnCountdownStart -= OnEvacuationStart;
            evacuation.OnCountdownChanged -= OnEvacuationChanged;
            evacuation.OnCountdownReset -= OnEvacuationReset;
            evacuation.OnCountdownFinished -= OnEvacuationFinished;
        }
        if (mission != null)
        {
            mission.OnMissionChanged -= OnMissionChanged;
        }
    }

    // ---- 打卡区 ----
    private void OnCheckpointStart()
    {
        SetCurrentOverride(CheckpointWait(10f));
    }

    private void OnCheckpointChanged(float remaining)
    {
        if (current == null || current.completed || current.overrideText == null) return;
        SetCurrentOverride(CheckpointWait(remaining));
    }

    private void OnCheckpointReset()
    {
        ClearCurrentOverride(); // 中途出去 → 回到「前往地图所示位置打卡」
    }

    private void OnCheckpointComplete()
    {
        CompleteCurrent();
        AddTask(evacuationTaskText);
    }

    private string CheckpointWait(float remaining)
    {
        return showCheckpointCountdown
            ? $"{checkpointWaitText}（剩余 {Mathf.Max(0f, remaining):0.0} 秒）"
            : checkpointWaitText;
    }

    // ---- 撤离区 ----
    private void OnEvacuationStart()
    {
        SetCurrentOverride(Evacuating(5f));
    }

    private void OnEvacuationChanged(float remaining)
    {
        if (current == null || current.completed || current.overrideText == null) return;
        SetCurrentOverride(Evacuating(remaining));
    }

    private void OnEvacuationReset()
    {
        ClearCurrentOverride();
    }

    private void OnEvacuationFinished()
    {
        bool success = mission != null && mission.IsEvacuationMission;

        if (success)
        {
            CompleteCurrent();
            AddTask(successText);
            CompleteCurrent();
        }
        else
        {
            FailCurrent();
        }
    }

    private string Evacuating(float remaining)
    {
        return showEvacuationCountdown
            ? $"{evacuatingText}（剩余 {Mathf.Max(0f, remaining):0.0} 秒）"
            : evacuatingText;
    }

    // 兜底：任务直接切到撤离时，保证打卡任务划线并补上撤离任务
    private void OnMissionChanged(MissionType oldMission, MissionType newMission)
    {
        if (newMission != MissionType.Evacuation) return;

        if (current != null && !current.completed && current.baseText == initialTaskText)
            CompleteCurrent();
        if (current == null || current.baseText != evacuationTaskText)
            AddTask(evacuationTaskText);
    }

    // ---- 任务行操作 ----
    private void SetCurrentOverride(string text)
    {
        if (current == null) return;
        current.overrideText = text;
        current.text.text = text;
    }

    private void ClearCurrentOverride()
    {
        if (current == null) return;
        current.overrideText = null;
        current.text.text = current.baseText;
    }

    private void CompleteCurrent()
    {
        if (current == null) return;

        current.completed = true;
        current.overrideText = null;
        current.text.text = current.baseText + "  ✓";
        current.text.color = completedTextColor;

        // 划线
        current.line.gameObject.SetActive(true);
        current.line.sizeDelta = new Vector2(current.text.preferredWidth + 6f, 3f);

        if (verboseLog) Debug.Log($"[任务] 完成：{current.baseText}");
    }

    private void FailCurrent()
    {
        if (current == null) return;

        current.completed = true;
        current.overrideText = null;
        current.text.text = failText;
        current.text.color = failColor;

        if (verboseLog) Debug.Log($"[任务] 失败：{current.baseText}");
    }

    private TaskItem AddTask(string text)
    {
        var go = new GameObject("Task_" + (items.Count + 1), typeof(RectTransform));
        go.transform.SetParent(listRoot, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, rowHeight);

        Text t = go.AddComponent<Text>();
        t.font = GetBuiltinFont();
        t.fontSize = fontSize;
        t.color = activeTextColor;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = text;

        // 完成划线（初始隐藏）
        var lineGo = new GameObject("StrikeLine", typeof(Image));
        lineGo.transform.SetParent(go.transform, false);
        Image line = lineGo.GetComponent<Image>();
        line.color = completedTextColor;
        line.raycastTarget = false;
        RectTransform lrt = line.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = new Vector2(0f, 3f);
        line.gameObject.SetActive(false);

        var item = new TaskItem { root = rt, text = t, line = lrt, baseText = text };
        items.Add(item);
        current = item;

        if (verboseLog) Debug.Log($"[任务] 新增：{text}");
        return item;
    }

    // ---- UI 搭建 ----
    private void BuildUi()
    {
        var canvasGoObj = new GameObject("MissionCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGoObj.transform.SetParent(transform, false);
        canvasGo = canvasGoObj;

        Canvas canvas = canvasGoObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 12; // 结算(-10)之下、屏幕效果(-15)之上

        CanvasScaler scaler = canvasGoObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 灰色底框（右上角，靠右），高度随内容自适应
        var boxGo = new GameObject("TaskPanel", typeof(Image));
        boxGo.transform.SetParent(canvasGo.transform, false);

        Image box = boxGo.GetComponent<Image>();
        box.color = boxColor;
        box.raycastTarget = false;

        var csf = boxGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform boxRt = box.rectTransform;
        boxRt.anchorMin = boxRt.anchorMax = new Vector2(1f, 1f);
        boxRt.pivot = new Vector2(1f, 1f);
        boxRt.anchoredPosition = panelOffset;
        boxRt.sizeDelta = new Vector2(panelWidth, 0f);

        // 任务列表（自动垂直堆叠）
        var listGo = new GameObject("TaskList", typeof(RectTransform));
        listGo.transform.SetParent(boxGo.transform, false);
        listRoot = listGo.GetComponent<RectTransform>();
        listRoot.anchorMin = Vector2.zero;
        listRoot.anchorMax = Vector2.one;
        listRoot.offsetMin = Vector2.zero;
        listRoot.offsetMax = Vector2.zero;

        var vlg = listGo.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(18, 18, 14, 14);
        vlg.spacing = 6f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
    }

    private static Font GetBuiltinFont()
    {
        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun", "PingFang SC" }, 20);
        if (font != null) return font;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}
