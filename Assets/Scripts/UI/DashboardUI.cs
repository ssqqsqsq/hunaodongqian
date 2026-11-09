using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
// 仪表盘 HUD（运行时自动创建，无需改场景）
//   · compass 打底（左下角，最底层）
//   · pointer 根据当前气瓶余量从 3 点钟方向逆时针转到 9 点钟（满→空）
//   · 灰色扇形：余量占比，从指针位置向 9 点钟方向铺满
//   · HR 图标 + 心率文字（下半区左侧，已含氮醉等 debuff 影响）
//   · 气瓶图标（5 态）+ bottle_shift（2 号瓶时旋转 180°）
//   · 深度：decrease/increase 随上升/下降切换 + 深度文字（正下方）
//   · 当前瓶气体 <1000 时左上显示 Warning
// ============================================================
public class DashboardUI : MonoBehaviour
{
    [Header("布局")]
    [SerializeField] private float gaugeSize = 560f;        // 仪表盘区域尺寸（参考 1920x1080）
    [SerializeField] private float gaugeMarginX = 40f;      // 距左下角
    [SerializeField] private float gaugeMarginY = 36f;
    [SerializeField, Range(0f, 1f)] private float ringScale = 0.84f;   // 灰色扇形外径占仪表盘半径比例
    [SerializeField, Range(0f, 1.2f)] private float pointerScale = 0.86f; // 指针长度占直径比例

    [Header("指针")]
    [Tooltip("指针图片在 0° 时朝向的补偿角：图片朝上填 -90，朝右填 0")]
    [SerializeField] private float pointerAngleOffsetDeg = -90f;

    [Header("指针位置")]
    [Tooltip("指针中心相对仪表盘中心的水平偏移（占仪表盘尺寸比例，0.5 = 一个半径）")]
    [SerializeField] private float pointerOffsetRadius = 0.5f;
    [Tooltip("指针旋转中心（相对指针图片）：0.5,0=图片底边（绕尾巴转，推荐）；0.5,0.5=图片中心")]
    [SerializeField] private Vector2 pointerPivot = new Vector2(0.5f, 0f);

    [Header("心率/气瓶行")]
    [SerializeField, Range(0.3f, 1.2f)] private float infoRowScale = 0.75f; // 该行图标/文字缩放
    [SerializeField] private float infoRowY = 0.30f;                         // 行中心距底边（占仪表盘尺寸比例）

    [Header("灰色扇形")]
    [Tooltip("false=灰色表示剩余量（从指针铺到 9 点钟）；true=灰色表示已消耗（从 3 点钟铺到指针）")]
    [SerializeField] private bool sectorShowsConsumed;

    [Header("颜色")]
    [SerializeField] private Color sectorColor = new Color(0.45f, 0.47f, 0.50f, 0.55f);

    [Header("警告阈值")]
    [SerializeField] private float warningGasThreshold = 1000f;

    // ---- 运行时引用 ----
    private OxygenComponent oxygen;
    private HeartRateComponent heart;
    private DepthComponent depth;

    [SerializeField] private RectTransform gaugeRt;        // 仪表盘容器
    [SerializeField] private RectTransform pointerRt;
    [SerializeField] private RadialSectorGraphic sector;
    [SerializeField] private Image hrIcon;
    [SerializeField] private Text hrText;
    [SerializeField] private Image bottleIcon;
    [SerializeField] private RectTransform shiftRt;
    [SerializeField] private Image depthUpIcon;            // 上升（深度减小）
    [SerializeField] private Image depthDownIcon;          // 下降（深度增大）
    [SerializeField] private Text depthText;
    [SerializeField] private Image warningIcon;

    [SerializeField] private Sprite spriteFull, spritePlenty, spriteLittle, spriteEmpty, spriteLose;
    private Sprite spriteHR, spriteShift, spriteCompass, spritePointer;
    private Sprite spriteDecrease, spriteIncrease, spriteWarning;

    private bool isTankTwo;              // 当前是否 2 号瓶（换瓶一次切换一次）
    private float lastDepth;
    private float depthTimer;
    private bool depthUp;

    private static bool _sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (!_sceneHooked)
        {
            _sceneHooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded; // 场景重载后重建
        }
        if (FindFirstObjectByType<DashboardUI>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("~DashboardUI");
        go.AddComponent<DashboardUI>();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
    }

    private void Awake()
    {
        // 场景里已手动放置（编辑器一键生成）→ 只更新不重建；
        // 否则运行时自动构建（其他场景的兜底）
        if (pointerRt == null || sector == null)
        {
            BuildUi();
        }
        else
        {
            EnsureBottleSprites();
        }
    }

    private void EnsureBottleSprites()
    {
        if (spriteFull == null) spriteFull = LoadSprite("bottle_full.png");
        if (spritePlenty == null) spritePlenty = LoadSprite("bottle_plenty.png");
        if (spriteLittle == null) spriteLittle = LoadSprite("bottle_little.png");
        if (spriteEmpty == null) spriteEmpty = LoadSprite("bottle_empty.png");
        if (spriteLose == null) spriteLose = LoadSprite("bottle_lose.png");
    }

    private void OnDestroy()
    {
        if (oxygen != null) oxygen.OnTankSwapped -= OnTankSwapped;
    }

    // ============ 构建 UI ============
    private void BuildUi()
    {
        Canvas canvas = QteUi.CreateOverlay(transform, "DashboardCanvas", short.MaxValue - 6);
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 仪表盘根容器（左下角）
        gaugeRt = QteUi.NewRect(canvas.transform, "GaugeRoot");
        QteUi.BottomLeft(gaugeRt, gaugeMarginX, gaugeMarginY, gaugeSize, gaugeSize);

        // 1) compass 打底
        Image compass = QteUi.NewImage(gaugeRt, "Compass", Color.white);
        compass.sprite = spriteCompass = LoadSprite("compass.png");
        QteUi.Stretch(compass.rectTransform);

        // 2) 灰色余量扇形
        var sectorGo = new GameObject("GasSector", typeof(RectTransform), typeof(CanvasRenderer), typeof(RadialSectorGraphic));
        sectorGo.transform.SetParent(gaugeRt, false);
        sector = sectorGo.GetComponent<RadialSectorGraphic>();
        sector.color = sectorColor;
        sector.raycastTarget = false;
        sector.InnerRadius = 0f;
        RectTransform sectorRt = sectorGo.GetComponent<RectTransform>();
        QteUi.Stretch(sectorRt);
        sectorRt.localScale = Vector3.one * ringScale;

        // 3) 指针（细长条：宽 1/20、高 1/2；中心右移一个仪表盘半径）
        Image pointer = QteUi.NewImage(gaugeRt, "Pointer", Color.white);
        pointer.sprite = spritePointer = LoadSprite("pointer.png");
        pointerRt = pointer.rectTransform;
        float basePtr = gaugeSize * pointerScale;
        QteUi.Place(pointerRt, new Vector2(0.5f, 0.5f), pointerPivot,
            new Vector2(gaugeSize * pointerOffsetRadius, 0f),
            new Vector2(basePtr / 20f, basePtr / 2f));

        // 4) 心率：图标 + 文字（下半区左侧，缩小并上移）
        float rowY = gaugeSize * infoRowY;
        float hrSize = gaugeSize * 0.16f * infoRowScale;
        int hrFont = Mathf.RoundToInt(34f * infoRowScale);
        hrIcon = QteUi.NewImage(gaugeRt, "HR", Color.white);
        hrIcon.sprite = spriteHR = LoadSprite("HR.png");
        QteUi.Place(hrIcon.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(gaugeSize * 0.16f, rowY), new Vector2(hrSize, hrSize));

        hrText = QteUi.NewText(gaugeRt, "HRText", hrFont, TextAnchor.MiddleLeft, Color.white);
        QteUi.Place(hrText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f),
            new Vector2(gaugeSize * 0.16f + hrSize * 0.5f + 8f, rowY),
            new Vector2(gaugeSize * 0.22f, 44f * infoRowScale));

        // 5) 气瓶图标（5 态）
        spriteFull = LoadSprite("bottle_full.png");
        spritePlenty = LoadSprite("bottle_plenty.png");
        spriteLittle = LoadSprite("bottle_little.png");
        spriteEmpty = LoadSprite("bottle_empty.png");
        spriteLose = LoadSprite("bottle_lose.png");
        float bottleSize = gaugeSize * 0.17f * infoRowScale;
        bottleIcon = QteUi.NewImage(gaugeRt, "Bottle", Color.white);
        bottleIcon.sprite = spriteFull;
        QteUi.Place(bottleIcon.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(gaugeSize * 0.60f, rowY), new Vector2(bottleSize, bottleSize));

        // 6) bottle_shift
        shiftRt = QteUi.NewImage(gaugeRt, "BottleShift", Color.white).rectTransform;
        shiftRt.GetComponent<Image>().sprite = spriteShift = LoadSprite("bottle_shift.png");
        QteUi.Place(shiftRt, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(gaugeSize * 0.80f, rowY), new Vector2(bottleSize * 0.9f, bottleSize * 0.9f));
        // 7) 深度：decrease/increase + 文字（正下方）
        float depthY = gaugeSize * 0.12f;
        float depthIconSize = gaugeSize * 0.13f;
        depthDownIcon = QteUi.NewImage(gaugeRt, "DepthDown", Color.white);
        depthDownIcon.sprite = spriteIncrease = LoadSprite("increase.png"); // 深度增大
        QteUi.Place(depthDownIcon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-gaugeSize * 0.13f, depthY), new Vector2(depthIconSize, depthIconSize));

        depthUpIcon = QteUi.NewImage(gaugeRt, "DepthUp", Color.white);
        depthUpIcon.sprite = spriteDecrease = LoadSprite("decrease.png"); // 深度减小
        QteUi.Place(depthUpIcon.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-gaugeSize * 0.13f, depthY), new Vector2(depthIconSize, depthIconSize));

        depthText = QteUi.NewText(gaugeRt, "DepthText", 30, TextAnchor.MiddleLeft, Color.white);
        QteUi.Place(depthText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0.5f),
            new Vector2(-gaugeSize * 0.03f, depthY), new Vector2(gaugeSize * 0.32f, 44f));

        // 8) 警告（左上偏上）
        warningIcon = QteUi.NewImage(gaugeRt, "Warning", Color.white);
        warningIcon.sprite = spriteWarning = LoadSprite("Warning.png");
        QteUi.Place(warningIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(gaugeSize * 0.18f, -gaugeSize * 0.20f), new Vector2(gaugeSize * 0.18f, gaugeSize * 0.18f));
        warningIcon.gameObject.SetActive(false);

        // 初始：扇形=满，指针=满（3 点钟）
        sector.StartAngle = 0f;
        sector.SweepAngle = 180f;
        pointerRt.localRotation = Quaternion.Euler(0f, 0f, pointerAngleOffsetDeg);
    }

    // ============ 更新 ============
    private void Update()
    {
        ResolveRefs();

        // 没有玩家（如菜单场景）时隐藏整块仪表盘
        bool hasPlayer = oxygen != null;
        if (gaugeRt != null && gaugeRt.gameObject.activeSelf != hasPlayer)
            gaugeRt.gameObject.SetActive(hasPlayer);
        if (!hasPlayer) return;

        {
            float max = oxygen.MaxOxygen;
            float current = oxygen.CurrentOxygen;
            float frac = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            // 指针：满=3 点钟(0°)，空=9 点钟(180°)，逆时针
            float geomAngle = (1f - frac) * 180f;
            pointerRt.localRotation = Quaternion.Euler(0f, 0f, geomAngle + pointerAngleOffsetDeg);

            // 灰色扇形：默认 = 剩余量（从指针到 9 点钟方向）
            if (sectorShowsConsumed)
            {
                sector.StartAngle = 0f;
                sector.SweepAngle = geomAngle;
            }
            else
            {
                sector.StartAngle = geomAngle;
                sector.SweepAngle = 180f - geomAngle;
            }

            UpdateBottle(frac);
            UpdateWarning(current);
        }

        if (heart != null)
        {
            hrText.text = Mathf.RoundToInt(heart.CurrentHeartRate).ToString();
        }

        UpdateDepth();
    }

    private void ResolveRefs()
    {
        if (oxygen != null) return;

        OxygenComponent o = FindFirstObjectByType<OxygenComponent>();
        if (o == null) return;
        oxygen = o;
        oxygen.OnTankSwapped += OnTankSwapped;
        isTankTwo = false;

        heart = FindFirstObjectByType<HeartRateComponent>();
        depth = FindFirstObjectByType<DepthComponent>();
        if (depth != null) lastDepth = depth.CurrentDepth;
    }

    private void OnTankSwapped()
    {
        isTankTwo = !isTankTwo;
        if (shiftRt != null)
            shiftRt.localRotation = Quaternion.Euler(0f, 0f, isTankTwo ? 180f : 0f);
    }

    private void UpdateBottle(float frac)
    {
        // 没有气瓶（当前瓶没气且无备用瓶）
        if (frac <= 0.001f && !oxygen.HasAirTank)
        {
            bottleIcon.sprite = spriteLose;
            return;
        }
        if (frac >= 0.75f) bottleIcon.sprite = spriteFull;
        else if (frac >= 0.5f) bottleIcon.sprite = spritePlenty;
        else if (frac >= 0.25f) bottleIcon.sprite = spriteLittle;
        else bottleIcon.sprite = spriteEmpty;
    }

    private void UpdateWarning(float currentGas)
    {
        bool show = currentGas < warningGasThreshold;
        if (warningIcon.gameObject.activeSelf != show) warningIcon.gameObject.SetActive(show);
    }

    private void UpdateDepth()
    {
        if (depth == null) return;

        float d = depth.CurrentDepth;
        depthTimer += Time.deltaTime;
        if (depthTimer < 0.2f) return;
        float diff = d - lastDepth;
        depthTimer = 0f;
        lastDepth = d;

        bool up; // true = 上升（深度减小）
        if (Mathf.Abs(diff) < 0.05f) up = depthUp;   // 稳定时保持上一次
        else up = diff < 0f;

        depthUp = up;
        depthUpIcon.gameObject.SetActive(up);
        depthDownIcon.gameObject.SetActive(!up);
        depthText.text = $"{d:0.0} m";
    }

    // ============ 精灵加载 ============
    private Sprite LoadSprite(string fileName)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(fileName);

        // 1) 若资源被放在 Resources/UI 下（打包可用）
        Sprite s = Resources.Load<Sprite>("UI/" + name);
        if (s != null) return s;
        Texture2D t = Resources.Load<Texture2D>("UI/" + name);
        if (t != null) return MakeSprite(t);

#if UNITY_EDITOR
        // 2) 编辑器内直接从 Assets/Scenes/UI 读取
        string[] dirs = { "Assets/Scenes/UI", "Assets/UI" };
        foreach (string dir in dirs)
        {
            string path = dir + "/" + fileName;
            Sprite sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) return sp;
            Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return MakeSprite(tex);
        }
#endif
        Debug.LogWarning("[DashboardUI] 找不到 UI 贴图：" + fileName);
        return null;
    }

    private static Sprite MakeSprite(Texture2D tex)
    {
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }
}
