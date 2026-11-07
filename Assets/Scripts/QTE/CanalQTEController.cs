using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 穿越产道 QTE 主控：
//   黄色圆环从屏幕中心放大，与灰色校准圈重合的瞬间按键（Space / 左键）完成一次校准。
//   · 校准成功             → 进度 +1，人物向前移动 1/总进度
//   · 校准失败（太早 / 越过灰圈 0.2s 未按）→ 进度 -1，人物后退 1/总进度
//   · 进度满               → 穿越成功，从出口离开产道
//   · 进度归 0             → 穿越失败，从入口离开产道
// 运行时自动创建（无需挂到场景）；产道判定区（CanalZone）在玩家进入时调用 StartCanalQTE 启动。
public class CanalQTEController : MonoBehaviour
{
    [Header("环生长节奏")]
    [Tooltip("黄色圆环从中心长到灰色校准圈的最短时长（秒）")]
    [SerializeField] private float ringTravelMin = 0.5f;
    [Tooltip("最长时长（秒），每次随机")]
    [SerializeField] private float ringTravelMax = 0.8f;

    [Header("校准判定")]
    [Tooltip("到达灰圈前多少秒内按下算成功")]
    [SerializeField] private float earlyHitTime = 0.1f;
    [Tooltip("越过灰圈后多少秒内按下仍算成功（超过则判校准失败）")]
    [SerializeField] private float lateHitTime = 0.2f;

    [Header("节奏")]
    [Tooltip("每次校准后到下一次的间隔（秒）")]
    [SerializeField] private float nextAttemptDelay = 0.2f;
    [Tooltip("前进 / 后退一步的移动时长（秒）")]
    [SerializeField] private float stepMoveTime = 0.25f;

    [Header("按键")]
    [Tooltip("校准按键")]
    public KeyCode pressKey = KeyCode.Space;
    [Tooltip("鼠标左键也能校准")]
    public bool acceptMouseClick = true;

    [Header("视觉")]
    [SerializeField] private float grayRingSize = 320f;
    [SerializeField] private float minRingSize = 24f;
    [SerializeField] private Color yellowRingColor = new Color(1f, 0.82f, 0.12f, 0.95f);
    [SerializeField] private Color grayRingColor = new Color(0.55f, 0.6f, 0.65f, 0.85f);
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.3f);
    [SerializeField] private Color successColor = new Color(0.4f, 1f, 0.5f, 1f);
    [SerializeField] private Color failColor = new Color(1f, 0.4f, 0.35f, 1f);

    [Header("朝向 / 镜头")]
    [Tooltip("QTE 时让角色沿产道方向对齐身体朝向")]
    [SerializeField] private bool alignFacing = true;
    [Tooltip("镜头拉近倍率（<1 拉近，1 不缩放）")]
    [SerializeField] private float zoomFovFactor = 0.7f;
    [Tooltip("镜头拉近 / 归位的时长（秒）")]
    [SerializeField] private float zoomTime = 0.35f;
    [Tooltip("要缩放的主相机，留空自动取 玩家相机 / Camera.main")]
    [SerializeField] private Camera zoomCamera;

    [Header("调试")]
    [SerializeField] private bool verboseLog = true;

    // ---- 运行时状态（供外部查询 / 事件） ----
    public bool IsActive { get; private set; }
    public int Progress { get; private set; }
    public int TotalProgress { get; private set; }

    // 参数：穿越是否成功
    public event Action<bool> OnQTEEnded;

    private CanalZone currentZone;
    private PlayerSM player;
    private Rigidbody rb;
    private Coroutine session;

    // 本次穿越的起点 / 终点（进入时根据玩家离哪头更近决定）
    private Vector2 startPoint;
    private Vector2 endPoint;

    // ---- UI ----
    private GameObject canvasGo;
    private Image dim;
    private RectTransform yellowRt;
    private Image yellowImg;
    private Text hintText;
    private Text feedbackText;
    private RectTransform dotsRow;
    private readonly List<Image> dots = new List<Image>();
    private Sprite ringSprite;

    // ---- 朝向 / 镜头运行时 ----
    private Vector2 canalDir;
    private Camera zoomCam;
    private Coroutine zoomRoutine;
    private float originalFov;
    private bool originalFovCaptured;

    // 当前一次校准的临时状态
    private float ringTravel; // 本次生长时长
    private float ringTime;   // 本次已生长时间
    private bool judging;     // 本次是否仍在等待 / 允许判定

    // 场景里没有就自动补一个，方便在任何场景直接测试
    private static bool _sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (!_sceneHooked)
        {
            _sceneHooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded; // 场景重载（R 重开）后重建
        }
        if (FindFirstObjectByType<CanalQTEController>(FindObjectsInactive.Include) != null) return;

        var go = new GameObject("~CanalQTEController");
        go.AddComponent<CanalQTEController>();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
    }

    private void Awake()
    {
        BuildUi();
        ShowCanvas(false);
    }

    private void OnDestroy()
    {
        if (session != null) StopCoroutine(session);
        if (zoomRoutine != null) StopCoroutine(zoomRoutine);
        if (IsActive) { IsActive = false; QteState.End(); }
    }

    // ---- 对外入口：由 CanalZone 在玩家进入判定区时调用 ----
    public bool StartCanalQTE(CanalZone zone, PlayerSM target, int totalProgress)
    {
        if (IsActive)
        {
            Debug.LogWarning("CanalQTEController：已有 QTE 进行中，忽略新的启动请求");
            return false;
        }
        if (target == null)
        {
            Debug.LogError("CanalQTEController：启动 QTE 缺少玩家");
            return false;
        }

        IsActive = true;
        Progress = 0;
        TotalProgress = Mathf.Max(1, totalProgress);
        currentZone = zone;
        player = target;
        rb = target.rb;
        QteState.Begin(); // 产道 QTE 期间功能按钮禁用

        // StartCoroutine 会同步执行到第一个 yield，锁人与定位立即生效
        session = StartCoroutine(RunSession());
        return true;
    }

    // 外部强制结束（重开关卡 / 调试）
    public void ForceStop()
    {
        if (!IsActive) return;
        if (session != null) StopCoroutine(session);
        End(false);
    }

    // ---- 主流程 ----
    private IEnumerator RunSession()
    {
        // 强制锁定 + 送到产道入口
        if (player != null)
        {
            player.ControlLocked = true;
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        SetupDirection();
        PlaceAt(startPoint);

        // 沿产道方向对齐身体朝向（穿越时与产道平行）
        canalDir = (endPoint - startPoint).sqrMagnitude > 0.0001f
            ? (endPoint - startPoint).normalized
            : Vector2.right;
        if (alignFacing && player != null) player.ForcedFacingDir = canalDir;

        ShowCanvas(true);
        hintText.text = BuildHint();
        BuildDots();
        UpdateDots();
        BeginZoom();

        while (IsActive && player != null && !player.IsDead)
        {
            yield return StartCoroutine(RunAttempt());
            if (!IsActive || player == null || player.IsDead) break;

            // 按进度把人物挪到对应的产道位置
            yield return StartCoroutine(MoveTo(ProgressPos()));
            if (player == null || player.IsDead) break;

            // 进度满：穿越成功，从出口离开
            if (Progress >= TotalProgress)
            {
                yield return StartCoroutine(MoveTo(LeavePos(true)));
                End(true);
                yield break;
            }

            // 进度归 0：穿越失败，从入口退出
            if (Progress <= 0)
            {
                yield return StartCoroutine(MoveTo(LeavePos(false)));
                End(false);
                yield break;
            }

            // 等待 0.2s 进行下一次校准
            feedbackText.gameObject.SetActive(false);
            yellowRt.gameObject.SetActive(false);
            yield return new WaitForSeconds(nextAttemptDelay);
        }

        // 玩家中途死亡等异常退出
        if (IsActive) End(false);
    }

    // ---- 一次校准 ----
    private IEnumerator RunAttempt()
    {
        judging = true;
        ringTravel = UnityEngine.Random.Range(ringTravelMin, ringTravelMax);
        ringTime = 0f;

        feedbackText.gameObject.SetActive(false);
        yellowImg.color = yellowRingColor;
        yellowRt.gameObject.SetActive(true);
        SetYellowSize(minRingSize);

        while (judging && IsActive)
        {
            ringTime += Time.unscaledDeltaTime;

            // 黄环从中心放大到灰圈；越过灰圈后继续变大（直到 0.2s 判失败）
            float grow = Mathf.Clamp(ringTime / ringTravel, 0f, 2f);
            SetYellowSize(Mathf.Lerp(minRingSize, grayRingSize, grow));

            // 等待中玩家死掉：立即终止本次
            if (player != null && player.IsDead)
            {
                judging = false;
                yield break;
            }

            // 按下按键 → 判定
            if (Input.GetKeyDown(pressKey) || (acceptMouseClick && Input.GetMouseButtonDown(0)))
            {
                JudgePress();
                yield break;
            }

            // 越过灰圈 0.2s 还没按 → 自动校准失败
            if (ringTime >= ringTravel + lateHitTime)
            {
                FinishAttempt(false, "超过灰圈太久");
                yield break;
            }

            yield return null;
        }
    }

    private void JudgePress()
    {
        float remaining = ringTravel - ringTime; // > 0：还没到灰圈
        float late = ringTime - ringTravel;      // > 0：已经越过灰圈

        if (remaining > earlyHitTime)
            FinishAttempt(false, "按得太早");
        else if (late > lateHitTime)
            FinishAttempt(false, "按得太晚");
        else
            FinishAttempt(true, "校准成功");
    }

    private void FinishAttempt(bool success, string reason)
    {
        if (!judging) return;
        judging = false;

        if (success) Progress++;
        else Progress--;

        if (verboseLog)
            Debug.Log($"[产道QTE] {(success ? "校准成功" : "校准失败")}（{reason}），进度 {Progress}/{TotalProgress}");

        feedbackText.text = success ? "+1" : "-1";
        feedbackText.color = success ? successColor : failColor;
        feedbackText.gameObject.SetActive(true);

        // 定格在灰圈大小，用颜色反馈本次结果
        SetYellowSize(grayRingSize);
        yellowImg.color = success ? successColor : failColor;

        UpdateDots();
    }

    // ---- 移动 ----
    private IEnumerator MoveTo(Vector2 target)
    {
        if (rb == null)
        {
            if (player != null) player.transform.position = target;
            yield break;
        }

        Vector2 start = rb.position;
        float t = 0f;
        while (t < stepMoveTime)
        {
            if (player != null && player.IsDead) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / stepMoveTime);
            k = k * k * (3f - 2f * k); // smoothstep 缓动
            rb.position = Vector2.Lerp(start, target, k);
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            yield return null;
        }
        rb.position = target;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void PlaceAt(Vector2 pos)
    {
        if (rb != null) rb.position = pos;
        else if (player != null) player.transform.position = pos;
    }

    // 进入时：离哪头近就从那头进（支持从左或从右进入产道）
    private void SetupDirection()
    {
        if (currentZone == null || player == null)
        {
            startPoint = player != null ? (Vector2)player.transform.position : Vector2.zero;
            endPoint = startPoint;
            return;
        }

        Vector2 a = currentZone.EntryPoint;
        Vector2 b = currentZone.ExitPoint;
        Vector2 pos = player.transform.position;

        bool nearA = (pos - a).sqrMagnitude <= (pos - b).sqrMagnitude;
        startPoint = nearA ? a : b;
        endPoint = nearA ? b : a;
    }

    // 当前进度对应的产道内位置（0 → 起点，1 → 终点）
    private Vector2 ProgressPos()
    {
        if (currentZone == null)
            return player != null ? (Vector2)player.transform.position : Vector2.zero;

        float ratio = TotalProgress <= 0 ? 1f : (float)Progress / TotalProgress;
        return Vector2.Lerp(startPoint, endPoint, ratio);
    }

    // 结束时把玩家送出判定区：成功从终点外离开，失败从起点外退出
    private Vector2 LeavePos(bool success)
    {
        if (currentZone == null) return ProgressPos();

        Vector2 dir = (endPoint - startPoint).sqrMagnitude > 0.0001f
            ? (endPoint - startPoint).normalized
            : Vector2.right;
        Vector2 off = dir * currentZone.ExitOffset;
        return success ? endPoint + off : startPoint - off;
    }

    private void End(bool success)
    {
        if (!IsActive) return;

        if (player != null)
        {
            player.ControlLocked = false;
            player.ForcedFacingDir = null;
        }
        if (currentZone != null) currentZone.OnQTEEnded(success);

        ShowCanvas(false);
        EndZoom();

        IsActive = false;
        QteState.End();
        if (verboseLog)
            Debug.Log($"[产道QTE] 结束：{(success ? "穿越成功，离开产道" : "穿越失败，从入口离开产道")}");

        // 先清引用，再抛事件，避免监听方在回调里再次操作
        Action<bool> ended = OnQTEEnded;
        currentZone = null;
        player = null;
        rb = null;
        session = null;

        ended?.Invoke(success);
    }

    private void ShowCanvas(bool show)
    {
        if (canvasGo != null) canvasGo.SetActive(show);
    }

    private string BuildHint()
    {
        return acceptMouseClick ? $"鼠标左键 / {pressKey}" : pressKey.ToString();
    }

    // ---- 镜头拉近 / 归位 ----
    private Camera ResolveZoomCamera()
    {
        if (zoomCamera != null) return zoomCamera;
        if (player != null && player.worldCamera != null) return player.worldCamera;
        return Camera.main;
    }

    private void BeginZoom()
    {
        if (zoomRoutine != null) StopCoroutine(zoomRoutine);
        if (zoomCam == null) zoomCam = ResolveZoomCamera();
        if (zoomCam == null) return;

        if (!originalFovCaptured)
        {
            originalFov = zoomCam.fieldOfView;
            originalFovCaptured = true;
        }
        zoomRoutine = StartCoroutine(ZoomRoutine(originalFov * zoomFovFactor, zoomTime));
    }

    private void EndZoom()
    {
        if (zoomRoutine != null) StopCoroutine(zoomRoutine);
        if (zoomCam == null || !originalFovCaptured) return;
        zoomRoutine = StartCoroutine(ZoomRoutine(originalFov, zoomTime));
    }

    private IEnumerator ZoomRoutine(float targetFov, float duration)
    {
        if (zoomCam == null) { zoomRoutine = null; yield break; }

        float start = zoomCam.fieldOfView;
        if (duration <= 0f)
        {
            zoomCam.fieldOfView = targetFov;
            zoomRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            zoomCam.fieldOfView = Mathf.Lerp(start, targetFov, k);
            yield return null;
        }
        zoomCam.fieldOfView = targetFov;
        zoomRoutine = null;
    }

    private void SetYellowSize(float size)
    {
        if (yellowRt == null) return;
        yellowRt.sizeDelta = new Vector2(size, size);
        yellowRt.gameObject.SetActive(size > 0f);
    }

    private void UpdateDots()
    {
        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i] == null) continue;
            dots[i].color = i < Progress ? successColor : new Color(1f, 1f, 1f, 0.25f);
        }
    }

    // ---- UI 搭建 ----
    private void BuildUi()
    {
        ringSprite = SpriteFrom(MakeRingTexture(256, 0.72f, 1f));

        var canvasGoObj = new GameObject("CanalQTECanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGoObj.transform.SetParent(transform, false);
        canvasGo = canvasGoObj;

        Canvas canvas = canvasGoObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 8; // 结算(-10)/效果(-15) 之上，调试 HUD(max) 之下

        CanvasScaler scaler = canvasGoObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 半透明遮罩，让圆环更清楚
        dim = NewImage(canvasGoObj.transform, "Dim", dimColor);
        Stretch(dim.rectTransform);

        // 左侧竖排布局：标题 / 圆环 / 进度点 / 按键提示
        float leftX = 380f;                  // 距左边缘
        float centerY = 30f;                 // 圆环中心（相对屏幕竖直中心）
        float radius = grayRingSize * 0.5f;  // 灰圈半径

        // 圆环容器
        RectTransform ringRoot = NewRect(canvasGoObj.transform, "RingRoot");
        ringRoot.anchorMin = ringRoot.anchorMax = new Vector2(0f, 0.5f);
        ringRoot.pivot = new Vector2(0.5f, 0.5f);
        ringRoot.sizeDelta = new Vector2(grayRingSize, grayRingSize);
        ringRoot.anchoredPosition = new Vector2(leftX, centerY);

        // 灰色校准圈（静止）
        Image grayImg = NewImage(ringRoot, "GrayCalibrateRing", grayRingColor);
        grayImg.sprite = ringSprite;
        Stretch(grayImg.rectTransform);

        // 黄色增长圈（从中心放大）
        yellowImg = NewImage(ringRoot, "YellowRing", yellowRingColor);
        yellowImg.sprite = ringSprite;
        yellowRt = yellowImg.rectTransform;
        yellowRt.anchorMin = yellowRt.anchorMax = new Vector2(0.5f, 0.5f);
        yellowRt.pivot = new Vector2(0.5f, 0.5f);
        yellowRt.sizeDelta = new Vector2(minRingSize, minRingSize);

        // 上方：标题
        Text title = CreateText(canvasGoObj.transform, "Title", 40, TextAnchor.MiddleCenter, Color.white);
        title.text = "穿越产道中";
        title.fontStyle = FontStyle.Bold;
        SetLeftAnchored(title.rectTransform, leftX, centerY + radius + 52f, 720f, 60f);

        // 下方：按键提示（简短）
        hintText = CreateText(canvasGoObj.transform, "Hint", 30, TextAnchor.MiddleCenter,
            new Color(0.92f, 0.94f, 0.97f, 1f));
        SetLeftAnchored(hintText.rectTransform, leftX, centerY - radius - 50f, 720f, 50f);

        // 进度点（按键提示下方）
        dotsRow = NewRect(canvasGoObj.transform, "ProgressDots");
        SetLeftAnchored(dotsRow, leftX, centerY - radius - 104f, 720f, 40f);

        // 成功 / 失败反馈（叠在圆环中心，只显示 +1 / -1）
        feedbackText = CreateText(canvasGoObj.transform, "Feedback", 48, TextAnchor.MiddleCenter, Color.white);
        feedbackText.fontStyle = FontStyle.Bold;
        SetLeftAnchored(feedbackText.rectTransform, leftX, centerY, 320f, 70f);
        feedbackText.gameObject.SetActive(false);
    }

    // 按总进度重建进度点
    private void BuildDots()
    {
        for (int i = 0; i < dots.Count; i++)
            if (dots[i] != null) Destroy(dots[i].gameObject);
        dots.Clear();

        const float spacing = 34f;
        float half = (TotalProgress - 1) * spacing * 0.5f;

        for (int i = 0; i < TotalProgress; i++)
        {
            Image dot = NewImage(dotsRow, "Dot" + i, new Color(1f, 1f, 1f, 0.25f));
            RectTransform rt = dot.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(22f, 22f);
            rt.anchoredPosition = new Vector2(-half + i * spacing, 0f);
            dots.Add(dot);
        }
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
        t.font = GetBuiltinFont();
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

    // 锚到屏幕左侧竖直中心：x 为距左边距离，y 为相对竖直中心的偏移
    private static void SetLeftAnchored(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
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

    private static Sprite SpriteFrom(Texture2D tex)
    {
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    // 圆环贴图：内半径 innerRatio、外半径 outerRatio 之间的白色环带，中间与外部透明
    private static Texture2D MakeRingTexture(int size, float innerRatio, float outerRatio)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        var px = new Color[size * size];
        float c = (size - 1) * 0.5f;
        float rIn = innerRatio * c;
        float rOut = outerRatio * c;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                // 1.5px 边缘抗锯齿
                float a = Mathf.Clamp01((rOut - d) / 1.5f) * Mathf.Clamp01((d - rIn) / 1.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
