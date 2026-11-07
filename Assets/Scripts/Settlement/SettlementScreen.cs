using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 结算结局
public enum SettlementOutcome
{
    CheckpointSuccess, // 打卡成功：完成打卡后撤离
    HastyEscape,       // 仓皇逃离：没打卡就撤离 / 非撤离任务进撤离区
    Death,             // 不幸身亡
}

// 结算界面：运行时自动搭建 Canvas，展示标题、用时 / 最大深度、神秘园TV占位、收集品小图。
// 挂到任意物体即可；依赖没绑定时自动在场景里找（LevelStats 缺失会自动补一个）。
public class SettlementScreen : MonoBehaviour
{
    [Header("依赖（留空自动在场景里找）")]
    [SerializeField] private LevelStats stats;
    [SerializeField] private MissionManager missionManager;
    [SerializeField] private PlayerSM player;

    [Header("显示设置")]
    [Tooltip("死亡 / 撤离结束时自动弹出结算")]
    [SerializeField] private bool autoShow = true;
    [Tooltip("弹出时暂停游戏")]
    [SerializeField] private bool pauseGameOnShow = true;

    [Header("神秘园TV")]
    [Tooltip("神秘园剪辑占位图，可留空（留空显示文字占位框）")]
    [SerializeField] private Sprite mysteryGardenImage;
    [Tooltip("没有占位图时显示的文字")]
    [SerializeField] private string videoPlaceholder = "神秘园TV（占位）";

    [Header("外观")]
    [SerializeField] private Color backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.94f);
    [SerializeField] private Color defaultTitleColor = Color.white;
    [SerializeField] private Color checkpointTitleColor = new Color(0.45f, 1f, 0.55f);
    [SerializeField] private Color escapeTitleColor = new Color(1f, 0.78f, 0.35f);
    [SerializeField] private Color deathTitleColor = new Color(1f, 0.4f, 0.36f);

    [Header("死亡")]
    [Tooltip("效果12：死亡后屏幕逐渐变黑（到全黑）再弹结算的秒数")]
    [SerializeField] private float deathFadeDuration = 1.5f;

    [Header("重开")]
    [Tooltip("弹出后按 R 重新加载当前场景（测试用）")]
    [SerializeField] private bool allowRestartWithR = true;

    [Header("调试")]
    [Tooltip("打印结算流程的关键日志")]
    [SerializeField] private bool verboseLog = true;
    [Tooltip("F3 打卡成功 / F4 仓皇逃离 / F5 不幸身亡 / F6 打印状态 / F7 加测试收集品")]
    [SerializeField] private bool debugHotkeys = true;

    // ---- 运行时 UI 引用 ----
    private GameObject canvasGo;
    private Text titleText;
    private Text timeText;
    private Text depthText;
    private Image videoImage;
    private Text videoPlaceholderText;
    private RectTransform collectibleRow;
    private Text emptyCollectibleText;
    private Text hintText;
    private Image deathFade;

    private Font uiFont;
    private bool shown;
    private bool fading;

    // 结算是否已进入（渐黑开始即算）；功能键据此禁用
    public static SettlementScreen Instance { get; private set; }
    public bool IsShown { get; private set; }

    // 场景里没有 SettlementScreen 时自动补一个，方便直接测试（不需要手动挂）
    private static bool _sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (!_sceneHooked)
        {
            _sceneHooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded; // 场景重载（R 重开）后重建
        }
        if (FindFirstObjectByType<SettlementScreen>(FindObjectsInactive.Include) != null) return;

        var go = new GameObject("~SettlementScreen");
        go.AddComponent<SettlementScreen>();
        Debug.Log("[结算] 场景里没有 SettlementScreen，已自动创建一个（调试用；要自定义占位图请手动挂到物体上）");
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
    }

    private void Awake()
    {
        Instance = this;
        AutoBind();
        BuildUi();
        SetVisible(false);

        if (verboseLog)
            Debug.Log($"[结算] 初始化完成：stats={(stats != null)}, missionManager={(missionManager != null)}, player={(player != null)}, autoShow={autoShow}, debugHotkeys={debugHotkeys}");
    }

    private void OnDestroy()
    {
        if (missionManager != null)
        {
            missionManager.OnEvacuationSucceeded -= HandleEvacuationSucceeded;
            missionManager.OnEvacuationFailed -= HandleEvacuationFailed;
        }
        if (player != null) player.OnDeath -= HandleDeath;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (debugHotkeys) HandleDebugHotkeys();

        if (!shown || !allowRestartWithR) return;
        if (Input.GetKeyDown(KeyCode.R)) RestartScene();
    }

    private void HandleDebugHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.F3)) ForceShow(SettlementOutcome.CheckpointSuccess);
        if (Input.GetKeyDown(KeyCode.F4)) ForceShow(SettlementOutcome.HastyEscape);
        if (Input.GetKeyDown(KeyCode.F5)) ForceShow(SettlementOutcome.Death);
        if (Input.GetKeyDown(KeyCode.F6)) LogState();
        if (Input.GetKeyDown(KeyCode.F7)) DebugAddFakeCollectibles();
    }

    private void AutoBind()
    {
        if (stats == null) stats = GetComponent<LevelStats>();
        if (stats == null) stats = FindFirstObjectByType<LevelStats>();
        if (stats == null)
        {
            stats = gameObject.AddComponent<LevelStats>();
            Debug.LogWarning("SettlementScreen：场景里没有 LevelStats，已自动补一个到当前物体");
        }

        if (missionManager == null) missionManager = FindFirstObjectByType<MissionManager>();
        if (player == null) player = FindFirstObjectByType<PlayerSM>();

        if (!autoShow) return;

        if (missionManager != null)
        {
            missionManager.OnEvacuationSucceeded += HandleEvacuationSucceeded;
            missionManager.OnEvacuationFailed += HandleEvacuationFailed;
        }
        else
        {
            Debug.LogWarning("SettlementScreen：找不到 MissionManager，撤离结局不会自动弹结算");
        }

        if (player != null)
            player.OnDeath += HandleDeath;
        else
            Debug.LogWarning("SettlementScreen：找不到 PlayerSM，死亡结局不会自动弹结算");

        if (verboseLog)
            Debug.Log("[结算] 自动绑定完成：已订阅撤离 / 死亡事件");
    }

    // ---- 事件 → 结局 ----

    private void HandleEvacuationSucceeded()
    {
        bool checkpointDone = stats != null && stats.CheckpointCompleted;
        if (verboseLog)
            Debug.Log($"[结算] 收到撤离成功事件：打卡完成={checkpointDone} → {(checkpointDone ? "打卡成功" : "仓皇逃离")}");

        Show(checkpointDone ? SettlementOutcome.CheckpointSuccess : SettlementOutcome.HastyEscape);
    }

    private void HandleEvacuationFailed()
    {
        if (verboseLog)
            Debug.Log("[结算] 收到撤离失败事件（非撤离任务）→ 仓皇逃离");

        Show(SettlementOutcome.HastyEscape);
    }

    private void HandleDeath()
    {
        if (verboseLog)
            Debug.Log("[结算] 收到玩家死亡事件 → 结算（不幸身亡）");

        Show(SettlementOutcome.Death);
    }

    // 其它系统也可以直接调用指定结局
    public void ShowCheckpointSuccess() => Show(SettlementOutcome.CheckpointSuccess);
    public void ShowHastyEscape() => Show(SettlementOutcome.HastyEscape);
    public void ShowDeath() => Show(SettlementOutcome.Death);

    public void Show(SettlementOutcome outcome)
    {
        if (shown || fading)
        {
            if (verboseLog) Debug.Log($"[结算] Show({outcome}) 被忽略：结算已在进行中");
            return;
        }
        fading = true;
        IsShown = true; // 渐黑开始就禁用功能键

        if (verboseLog) Debug.Log($"[结算] 开始结算流程：屏幕渐黑 → 全黑 → 弹出（{outcome}）");
        StartCoroutine(ShowSequence(outcome));
    }

    // 屏幕逐渐变黑 → 全黑 → 再弹出结算 UI
    private IEnumerator ShowSequence(SettlementOutcome outcome)
    {
        deathFade.gameObject.SetActive(true);
        deathFade.color = new Color(0f, 0f, 0f, 0f);

        float t = 0f;
        while (t < deathFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            deathFade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / deathFadeDuration));
            yield return null;
        }

        deathFade.color = new Color(0f, 0f, 0f, 1f);
        ShowNow(outcome);
    }

    private void ShowNow(SettlementOutcome outcome)
    {
        shown = true;
        fading = false;

        if (verboseLog)
        {
            float t = stats != null ? stats.ElapsedTime : 0f;
            float d = stats != null ? stats.MaxDepth : 0f;
            int c = stats != null ? stats.Collected.Count : 0;
            Debug.Log($"[结算] 弹出结算：{outcome} | 用时 {t:0.0}s | 最大深度 {d:0.0}m | 收集 {c} 个 | 打卡完成={(stats != null && stats.CheckpointCompleted)}");
        }

        if (stats != null) stats.StopTimer();

        Fill(outcome);
        SetVisible(true);

        if (pauseGameOnShow) Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (verboseLog)
            Debug.Log($"[结算] 面板激活={canvasGo != null && canvasGo.activeSelf}，timeScale={Time.timeScale}");
    }

    private void RestartScene()
    {
        Time.timeScale = 1f;

        int index = SceneManager.GetActiveScene().buildIndex;
        if (index >= 0) SceneManager.LoadScene(index);
        else Debug.LogWarning("SettlementScreen：当前场景未加入 Build Settings，无法用 R 重开");
    }

    // ---- 调试 ----

    private void ForceShow(SettlementOutcome outcome)
    {
        if (verboseLog) Debug.Log($"[结算] 调试热键强制显示：{outcome}");

        if (shown || fading)
        {
            shown = false;
            fading = false;
            IsShown = false;
            Time.timeScale = 1f;
            if (deathFade != null) deathFade.gameObject.SetActive(false);
        }
        Show(outcome);
    }

    [ContextMenu("打印依赖状态")]
    public void LogState()
    {
        string statsInfo = stats == null
            ? "无"
            : $"有（用时 {stats.ElapsedTime:0.0}s / 最大深度 {stats.MaxDepth:0.0}m / 收集 {stats.Collected.Count} 个 / 打卡完成 {stats.CheckpointCompleted}）";

        Debug.Log(
            "[结算] ===== 状态 =====\n" +
            $"- 本组件在物体：{name}（{(gameObject.activeInHierarchy ? "激活" : "未激活")}）\n" +
            $"- LevelStats：{statsInfo}\n" +
            $"- MissionManager：{(missionManager != null ? $"有（当前任务 {missionManager.CurrentMission}）" : "无")}\n" +
            $"- PlayerSM：{(player != null ? $"有（死亡={player.IsDead}）" : "无")}\n" +
            $"- 面板已弹出：{shown}；Canvas 激活：{(canvasGo != null && canvasGo.activeSelf)}\n" +
            $"- timeScale：{Time.timeScale}\n" +
            "- 调试热键：F3 打卡成功 / F4 仓皇逃离 / F5 不幸身亡 / F6 打印状态 / F7 加测试收集品");
    }

    private void DebugAddFakeCollectibles()
    {
        if (stats == null)
        {
            Debug.LogWarning("[结算] 找不到 LevelStats，无法添加测试收集品");
            return;
        }

        for (int i = 1; i <= 3; i++)
            stats.AddCollectible(new CollectibleData("debug_" + i, "测试收集品" + i, null));

        Debug.Log($"[结算] 调试：已添加 3 个测试收集品，当前共 {stats.Collected.Count} 个");

        if (shown) ForceShow(SettlementOutcome.CheckpointSuccess);
    }

    // ---- 填充内容 ----

    private void Fill(SettlementOutcome outcome)
    {
        titleText.text = TitleOf(outcome);
        titleText.color = ColorOf(outcome);

        float time = stats != null ? stats.ElapsedTime : 0f;
        float depth = stats != null ? stats.MaxDepth : 0f;

        timeText.text = "本关总用时：" + FormatTime(time);
        depthText.text = "到达的最大深度：" + depth.ToString("0.0") + " m";

        FillVideo();
        FillCollectibles(stats != null ? stats.Collected : null);
    }

    private static string TitleOf(SettlementOutcome outcome)
    {
        switch (outcome)
        {
            case SettlementOutcome.CheckpointSuccess: return "打卡成功";
            case SettlementOutcome.HastyEscape: return "仓皇逃离";
            case SettlementOutcome.Death: return "不幸身亡";
            default: return string.Empty;
        }
    }

    private Color ColorOf(SettlementOutcome outcome)
    {
        switch (outcome)
        {
            case SettlementOutcome.CheckpointSuccess: return checkpointTitleColor;
            case SettlementOutcome.HastyEscape: return escapeTitleColor;
            case SettlementOutcome.Death: return deathTitleColor;
            default: return defaultTitleColor;
        }
    }

    // xx分xx秒
    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = total / 60;
        int secs = total % 60;
        return $"{minutes}分{secs:00}秒";
    }

    private void FillVideo()
    {
        if (mysteryGardenImage != null)
        {
            videoImage.sprite = mysteryGardenImage;
            videoImage.color = Color.white;
            videoImage.preserveAspect = true;
            videoPlaceholderText.gameObject.SetActive(false);
        }
        else
        {
            videoImage.sprite = null;
            videoImage.color = new Color(0.1f, 0.13f, 0.17f, 1f);
            videoPlaceholderText.gameObject.SetActive(true);
        }
    }

    private void FillCollectibles(IReadOnlyList<CollectibleData> items)
    {
        for (int i = collectibleRow.childCount - 1; i >= 0; i--)
            Destroy(collectibleRow.GetChild(i).gameObject);

        bool empty = items == null || items.Count == 0;
        emptyCollectibleText.gameObject.SetActive(empty);
        if (empty) return;

        const float cellW = 150f;
        const float gap = 14f;
        float total = items.Count * cellW + (items.Count - 1) * gap;
        float startX = -total * 0.5f + cellW * 0.5f;

        for (int i = 0; i < items.Count; i++)
            CreateCollectibleCell(items[i], startX + i * (cellW + gap));
    }

    private void CreateCollectibleCell(CollectibleData data, float x)
    {
        RectTransform cell = NewRect(collectibleRow, "Collectible_" + data.id);
        cell.anchorMin = cell.anchorMax = new Vector2(0.5f, 0.5f);
        cell.pivot = new Vector2(0.5f, 0.5f);
        cell.sizeDelta = new Vector2(150f, 200f);
        cell.anchoredPosition = new Vector2(x, 0f);

        Image icon = NewImage(cell, "Icon", Color.white);
        RectTransform irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
        irt.pivot = new Vector2(0.5f, 1f);
        irt.sizeDelta = new Vector2(120f, 120f);
        irt.anchoredPosition = Vector2.zero;

        if (data.icon != null)
        {
            icon.sprite = data.icon;
            icon.preserveAspect = true;
            icon.color = Color.white;
        }
        else
        {
            icon.sprite = null;
            icon.color = new Color(0.3f, 0.34f, 0.42f, 1f);
        }

        Text nameText = CreateText(cell, "Name", 20, TextAnchor.MiddleCenter,
            new Color(0.9f, 0.92f, 0.95f, 1f));
        nameText.text = string.IsNullOrEmpty(data.displayName) ? data.id : data.displayName;

        RectTransform nrt = nameText.rectTransform;
        nrt.anchorMin = new Vector2(0f, 0f);
        nrt.anchorMax = new Vector2(1f, 0f);
        nrt.pivot = new Vector2(0.5f, 0f);
        nrt.sizeDelta = new Vector2(0f, 56f);
        nrt.anchoredPosition = new Vector2(0f, 6f);
    }

    // ---- 搭建 UI ----

    private void BuildUi()
    {
        uiFont = GetBuiltinFont();

        var canvasGoObj = new GameObject("SettlementCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGoObj.transform.SetParent(transform, false);
        canvasGo = canvasGoObj;

        Canvas canvas = canvasGoObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 10; // 让调试 HUD 仍然盖在最上层

        CanvasScaler scaler = canvasGoObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 全屏暗色背景
        Image bg = NewImage(canvasGoObj.transform, "Background", backgroundColor);
        Stretch(bg.rectTransform);

        // 死亡淡黑层（效果12）：在结算内容之下、游戏画面之上，一开始隐藏
        deathFade = NewImage(canvasGoObj.transform, "DeathFade", new Color(0f, 0f, 0f, 0f));
        Stretch(deathFade.rectTransform);
        deathFade.gameObject.SetActive(false);

        // 顶部大标题
        titleText = CreateText(canvasGoObj.transform, "Title", 60, TextAnchor.MiddleCenter, defaultTitleColor);
        titleText.fontStyle = FontStyle.Bold;
        RectTransform trt = titleText.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(1400f, 120f);
        trt.anchoredPosition = new Vector2(0f, -70f);

        // 用时 / 最大深度，并排
        RectTransform statsRow = NewRect(canvasGoObj.transform, "StatsRow");
        statsRow.anchorMin = statsRow.anchorMax = new Vector2(0.5f, 1f);
        statsRow.pivot = new Vector2(0.5f, 1f);
        statsRow.sizeDelta = new Vector2(1400f, 60f);
        statsRow.anchoredPosition = new Vector2(0f, -200f);

        timeText = CreateText(statsRow, "TimeText", 36, TextAnchor.MiddleRight,
            new Color(0.92f, 0.94f, 0.96f, 1f));
        RectTransform ttrt = timeText.rectTransform;
        ttrt.anchorMin = Vector2.zero;
        ttrt.anchorMax = new Vector2(0.5f, 1f);
        ttrt.offsetMin = Vector2.zero;
        ttrt.offsetMax = new Vector2(-40f, 0f);

        depthText = CreateText(statsRow, "DepthText", 36, TextAnchor.MiddleLeft,
            new Color(0.92f, 0.94f, 0.96f, 1f));
        RectTransform drt = depthText.rectTransform;
        drt.anchorMin = new Vector2(0.5f, 0f);
        drt.anchorMax = Vector2.one;
        drt.offsetMin = new Vector2(40f, 0f);
        drt.offsetMax = Vector2.zero;

        // 正中间：神秘园TV 占位
        videoImage = NewImage(canvasGoObj.transform, "MysteryGardenTV",
            new Color(0.1f, 0.13f, 0.17f, 1f));
        RectTransform vrt = videoImage.rectTransform;
        vrt.anchorMin = vrt.anchorMax = new Vector2(0.5f, 0.5f);
        vrt.pivot = new Vector2(0.5f, 0.5f);
        vrt.sizeDelta = new Vector2(680f, 380f);
        vrt.anchoredPosition = new Vector2(0f, 30f);

        videoPlaceholderText = CreateText(videoImage.transform, "VideoPlaceholder", 28,
            TextAnchor.MiddleCenter, new Color(0.7f, 0.76f, 0.82f, 1f));
        videoPlaceholderText.text = videoPlaceholder;
        Stretch(videoPlaceholderText.rectTransform);

        // 底部：收集品
        RectTransform bottomArea = NewRect(canvasGoObj.transform, "BottomArea");
        bottomArea.anchorMin = bottomArea.anchorMax = new Vector2(0.5f, 0f);
        bottomArea.pivot = new Vector2(0.5f, 0f);
        bottomArea.sizeDelta = new Vector2(1500f, 320f);
        bottomArea.anchoredPosition = new Vector2(0f, 50f);

        Text heading = CreateText(bottomArea, "Heading", 26, TextAnchor.MiddleCenter,
            new Color(0.75f, 0.79f, 0.84f, 1f));
        heading.text = "本关收集品";
        RectTransform hrt = heading.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(800f, 40f);
        hrt.anchoredPosition = Vector2.zero;

        collectibleRow = NewRect(bottomArea, "CollectibleRow");
        collectibleRow.anchorMin = collectibleRow.anchorMax = new Vector2(0.5f, 1f);
        collectibleRow.pivot = new Vector2(0.5f, 1f);
        collectibleRow.sizeDelta = new Vector2(1500f, 260f);
        collectibleRow.anchoredPosition = new Vector2(0f, -45f);

        emptyCollectibleText = CreateText(bottomArea, "EmptyCollectible", 30,
            TextAnchor.MiddleCenter, new Color(0.6f, 0.63f, 0.68f, 1f));
        emptyCollectibleText.text = "无";
        RectTransform ert = emptyCollectibleText.rectTransform;
        ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0f);
        ert.pivot = new Vector2(0.5f, 0f);
        ert.sizeDelta = new Vector2(400f, 50f);
        ert.anchoredPosition = new Vector2(0f, 150f);

        // 底部小提示
        hintText = CreateText(canvasGoObj.transform, "Hint", 20, TextAnchor.MiddleCenter,
            new Color(0.45f, 0.48f, 0.52f, 1f));
        hintText.text = allowRestartWithR ? "按 R 重新开始" : string.Empty;
        RectTransform hirt = hintText.rectTransform;
        hirt.anchorMin = hirt.anchorMax = new Vector2(0.5f, 0f);
        hirt.pivot = new Vector2(0.5f, 0f);
        hirt.sizeDelta = new Vector2(800f, 30f);
        hirt.anchoredPosition = new Vector2(0f, 10f);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGo != null) canvasGo.SetActive(visible);
    }

    // ---- 小工具 ----

    private Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private Text CreateText(Transform parent, string name, int fontSize, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);

        var t = go.GetComponent<Text>();
        t.font = uiFont;
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Font GetBuiltinFont()
    {
        // 内置字体不含中文字形，优先用系统中文字体
        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun", "PingFang SC" }, 20);
        if (font != null) return font;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}
