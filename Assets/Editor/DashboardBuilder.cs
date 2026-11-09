#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

// 一键把仪表盘 UI 生成到当前场景的 Canvas 下（真实场景物体，可自由拖动/缩放）。
// 生成后由 DashboardUI 组件在运行时只负责更新数值/旋转，不再运行时重建。
public static class DashboardBuilder
{
    private const string UI_DIR = "Assets/Scenes/UI/";

    // 与 DashboardUI 默认布局保持一致
    private const float GaugeSize = 560f;
    private const float MarginX = 40f;
    private const float MarginY = 36f;

    [MenuItem("Tools/仪表盘/生成到当前场景", false, 0)]
    public static void Build()
    {
        if (Object.FindFirstObjectByType<DashboardUI>(FindObjectsInactive.Include) != null)
        {
            EditorUtility.DisplayDialog("仪表盘",
                "当前场景里已经存在仪表盘了。\n\n· 想保留现有布局：用「修复场景引用（保留布局）」\n· 想重做：先用「删除当前场景仪表盘」再生成",
                "好");
            return;
        }

        // ---- 加载/配置精灵 ----
        Sprite sCompass = LoadSprite("compass.png");
        Sprite sPointer = LoadSprite("pointer.png");
        Sprite sHR = LoadSprite("HR.png");
        Sprite sShift = LoadSprite("bottle_shift.png");
        Sprite sFull = LoadSprite("bottle_full.png");
        Sprite sPlenty = LoadSprite("bottle_plenty.png");
        Sprite sLittle = LoadSprite("bottle_little.png");
        Sprite sEmpty = LoadSprite("bottle_empty.png");
        Sprite sLose = LoadSprite("bottle_lose.png");
        Sprite sDecrease = LoadSprite("decrease.png");
        Sprite sIncrease = LoadSprite("increase.png");
        Sprite sWarning = LoadSprite("Warning.png");

        // ---- Canvas ----
        var canvasGo = new GameObject("DashboardCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 6;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // ---- 仪表盘根（左下角） ----
        RectTransform gauge = NewRect(canvasGo.transform, "GaugeRoot");
        gauge.anchorMin = gauge.anchorMax = Vector2.zero;
        gauge.pivot = Vector2.zero;
        gauge.sizeDelta = new Vector2(GaugeSize, GaugeSize);
        gauge.anchoredPosition = new Vector2(MarginX, MarginY);

        // 1) compass
        Image compass = NewImage(gauge, "Compass", sCompass);
        Stretch(compass.rectTransform);

        // 2) 灰色扇形
        RectTransform sectorRt = NewRect(gauge, "GasSector");
        Stretch(sectorRt);
        sectorRt.localScale = Vector3.one * 0.84f;
        var sector = sectorRt.gameObject.AddComponent<RadialSectorGraphic>();
        sector.color = new Color(0.45f, 0.47f, 0.50f, 0.55f);
        sector.raycastTarget = false;
        sector.InnerRadius = 0f;
        sector.StartAngle = 0f;
        sector.SweepAngle = 180f;

        // 3) 指针（宽 1/20、高 1/2；绕尾部旋转，尾部在仪表盘中心右移一个半径处）
        float basePtr = GaugeSize * 0.86f;
        Image pointer = NewImage(gauge, "Pointer", sPointer);
        RectTransform pointerRt = pointer.rectTransform;
        Place(pointerRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f),
            new Vector2(GaugeSize * 0.5f, 0f),
            new Vector2(basePtr / 20f, basePtr / 2f));

        // 4) 心率行
        float rowY = GaugeSize * 0.30f;
        float infoScale = 0.75f;
        float hrSize = GaugeSize * 0.16f * infoScale;

        Image hrIcon = NewImage(gauge, "HR", sHR);
        Place(hrIcon.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f),
            new Vector2(GaugeSize * 0.16f, rowY), new Vector2(hrSize, hrSize));

        Text hrText = NewText(gauge, "HRText", "0", Mathf.RoundToInt(34f * infoScale), TextAnchor.MiddleLeft);
        Place(hrText.rectTransform, Vector2.zero, new Vector2(0f, 0.5f),
            new Vector2(GaugeSize * 0.16f + hrSize * 0.5f + 8f, rowY),
            new Vector2(GaugeSize * 0.22f, 44f * infoScale));

        // 5) 气瓶
        float bottleSize = GaugeSize * 0.17f * infoScale;
        Image bottle = NewImage(gauge, "Bottle", sFull);
        Place(bottle.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f),
            new Vector2(GaugeSize * 0.60f, rowY), new Vector2(bottleSize, bottleSize));

        // 6) bottle_shift
        Image shift = NewImage(gauge, "BottleShift", sShift);
        Place(shift.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f),
            new Vector2(GaugeSize * 0.80f, rowY), new Vector2(bottleSize * 0.9f, bottleSize * 0.9f));

        // 7) 深度行
        float depthY = GaugeSize * 0.12f;
        float depthIconSize = GaugeSize * 0.13f;

        Image depthDown = NewImage(gauge, "DepthIncrease", sIncrease); // 深度增大
        Place(depthDown.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-GaugeSize * 0.13f, depthY), new Vector2(depthIconSize, depthIconSize));

        Image depthUp = NewImage(gauge, "DepthDecrease", sDecrease); // 深度减小
        Place(depthUp.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-GaugeSize * 0.13f, depthY), new Vector2(depthIconSize, depthIconSize));
        depthUp.gameObject.SetActive(false);

        Text depthText = NewText(gauge, "DepthText", "0.0 m", 30, TextAnchor.MiddleLeft);
        Place(depthText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0.5f),
            new Vector2(-GaugeSize * 0.03f, depthY), new Vector2(GaugeSize * 0.32f, 44f));

        // 8) 警告（左上偏上）
        Image warning = NewImage(gauge, "Warning", sWarning);
        Place(warning.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(GaugeSize * 0.18f, -GaugeSize * 0.20f), new Vector2(GaugeSize * 0.18f, GaugeSize * 0.18f));
        warning.gameObject.SetActive(false);

        // ---- 挂控制器并接线（控制器挂在 Canvas 上，隐藏 GaugeRoot 时不会自禁） ----
        var controller = canvasGo.AddComponent<DashboardUI>();
        var so = new SerializedObject(controller);
        SetRef(so, "gaugeRt", gauge);
        SetRef(so, "pointerRt", pointerRt);
        SetRef(so, "sector", sector);
        SetRef(so, "hrIcon", hrIcon);
        SetRef(so, "hrText", hrText);
        SetRef(so, "bottleIcon", bottle);
        SetRef(so, "shiftRt", shift.rectTransform);
        SetRef(so, "depthUpIcon", depthUp);
        SetRef(so, "depthDownIcon", depthDown);
        SetRef(so, "depthText", depthText);
        SetRef(so, "warningIcon", warning);
        SetRef(so, "spriteFull", sFull);
        SetRef(so, "spritePlenty", sPlenty);
        SetRef(so, "spriteLittle", sLittle);
        SetRef(so, "spriteEmpty", sEmpty);
        SetRef(so, "spriteLose", sLose);
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(canvasGo, "生成仪表盘");
        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        Selection.activeGameObject = gauge.gameObject;
        EditorGUIUtility.PingObject(gauge.gameObject);

        Debug.Log("[仪表盘] 已生成到当前场景的 DashboardCanvas/GaugeRoot 下，可直接在场景/Inspector 里微调位置尺寸。");
    }

    // 生成 3 个技能按钮（替换原功能键）+ 第一关简图（右上角）到当前场景 Canvas
    [MenuItem("Tools/HUD/生成技能按钮与地图到当前场景", false, 0)]
    public static void BuildSkillsAndMap()
    {
        if (Object.FindFirstObjectByType<FunctionButtons>(FindObjectsInactive.Include) != null)
        {
            EditorUtility.DisplayDialog("HUD",
                "当前场景里已经存在 FunctionButtons。\n请先删除旧的（Hierarchy 里删掉旧的功能按钮 Canvas 和挂有 FunctionButtons 的物体）再生成。",
                "好");
            return;
        }

        Sprite sShift = LoadSprite("skill_shift.png");     // 换瓶
        Sprite sCalm = LoadSprite("skill_calm.png");       // 平复
        Sprite sAbandon = LoadSprite("skill_abandon.png"); // 卸瓶
        Sprite sMap = LoadSprite("map_01.png");            // 第一关简图

        // Canvas（需要 GraphicRaycaster 才能接收点击）
        var canvasGo = new GameObject("SkillMapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 11; // 与原功能按钮同层
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // ---- 技能按钮（右下角，从右到左：卸瓶 / 平复 / 换瓶） ----
        float size = 110f, gap = 16f, right = 30f, baseY = 236f;
        Button btn3 = NewSkillButton(canvasGo.transform, "Skill_Abandon", sAbandon, right, baseY, size);
        Button btn2 = NewSkillButton(canvasGo.transform, "Skill_Calm", sCalm, right + size + gap, baseY, size);
        Button btn1 = NewSkillButton(canvasGo.transform, "Skill_Shift", sShift, right + 2f * (size + gap), baseY, size);

        // ---- 第一关简图（右上角） ----
        Image map = NewImage(canvasGo.transform, "Map01", sMap);
        map.preserveAspect = true;
        float mapW = 380f;
        float mapH = (sMap != null && sMap.rect.width > 0f) ? mapW * (sMap.rect.height / sMap.rect.width) : mapW;
        Place(map.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-20f, -20f), new Vector2(mapW, mapH));

        // ---- 控制器接线 ----
        var fb = canvasGo.AddComponent<FunctionButtons>();
        var so = new SerializedObject(fb);
        SetRef(so, "btn1", btn1);
        SetRef(so, "btn2", btn2);
        SetRef(so, "btn3", btn3);
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(canvasGo, "生成技能按钮与地图");
        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        Selection.activeGameObject = canvasGo;
        EditorGUIUtility.PingObject(canvasGo);

        Debug.Log("[HUD] 已生成技能按钮 + 地图（SkillMapCanvas）。按钮/地图位置尺寸可直接微调；FunctionButtons 已接好线。");
    }

    // 生成"内心独白"文字到当前场景（可选；不生成也会运行时自动创建）
    [MenuItem("Tools/HUD/生成内心独白到当前场景", false, 0)]
    public static void BuildSelfTalk()
    {
        if (Object.FindFirstObjectByType<SelfTalkSystem>(FindObjectsInactive.Include) != null)
        {
            EditorUtility.DisplayDialog("内心独白", "当前场景已有 SelfTalkSystem，请先删除再生成。", "好");
            return;
        }

        var canvasGo = new GameObject("SelfTalkCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 8;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var textGo = new GameObject("SelfTalkText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(CanvasGroup));
        textGo.transform.SetParent(canvasGo.transform, false);

        Text text = textGo.GetComponent<Text>();
        text.font = BuiltinFont();
        text.text = "Self-talk placeholder"; // 运行时会替换为中文台词（并用系统中文字体）
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        CanvasGroup cg = textGo.GetComponent<CanvasGroup>();
        cg.alpha = 1f; // 编辑时可见便于摆位；运行时 Awake 会置 0

        Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 220f), new Vector2(1000f, 200f));

        var sys = canvasGo.AddComponent<SelfTalkSystem>();
        var so = new SerializedObject(sys);
        SetRef(so, "group", cg);
        SetRef(so, "text", text);
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(canvasGo, "生成内心独白");
        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        Selection.activeGameObject = canvasGo;
        EditorGUIUtility.PingObject(canvasGo);

        Debug.Log("[内心独白] 已生成到当前场景（SelfTalkCanvas）。位置/字号可在场景里调；Play 后显示并淡入淡出。");
    }

    // 把现有场景里未接线的仪表盘重新接线（保留手工调整过的布局）
    [MenuItem("Tools/仪表盘/修复场景引用（保留布局）", false, 0)]
    public static void Repair()
    {
        var controller = Object.FindFirstObjectByType<DashboardUI>(FindObjectsInactive.Include);
        if (controller == null)
        {
            EditorUtility.DisplayDialog("仪表盘", "当前场景里没有 DashboardUI 控制器，请先「生成到当前场景」。", "好");
            return;
        }

        RectTransform gauge = FindComp<RectTransform>(controller.transform, "GaugeRoot");
        if (gauge == null)
        {
            EditorUtility.DisplayDialog("仪表盘", "找不到 GaugeRoot（可能被改名了），无法修复。", "好");
            return;
        }

        var so = new SerializedObject(controller);
        SetRef(so, "gaugeRt", gauge);
        SetRef(so, "pointerRt", FindComp<RectTransform>(gauge, "Pointer"));
        SetRef(so, "sector", FindComp<RadialSectorGraphic>(gauge, "GasSector"));
        SetRef(so, "hrIcon", FindComp<Image>(gauge, "HR"));
        SetRef(so, "hrText", FindComp<Text>(gauge, "HRText"));
        SetRef(so, "bottleIcon", FindComp<Image>(gauge, "Bottle"));
        Image shift = FindComp<Image>(gauge, "BottleShift");
        SetRef(so, "shiftRt", shift != null ? shift.rectTransform : null);
        SetRef(so, "depthUpIcon", FindComp<Image>(gauge, "DepthDecrease"));
        SetRef(so, "depthDownIcon", FindComp<Image>(gauge, "DepthIncrease"));
        SetRef(so, "depthText", FindComp<Text>(gauge, "DepthText"));
        SetRef(so, "warningIcon", FindComp<Image>(gauge, "Warning"));
        SetRef(so, "spriteFull", LoadSprite("bottle_full.png"));
        SetRef(so, "spritePlenty", LoadSprite("bottle_plenty.png"));
        SetRef(so, "spriteLittle", LoadSprite("bottle_little.png"));
        SetRef(so, "spriteEmpty", LoadSprite("bottle_empty.png"));
        SetRef(so, "spriteLose", LoadSprite("bottle_lose.png"));
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        Debug.Log("[仪表盘] 已重新接线（保留布局）。现在 Play 后只有这一套仪表盘，并且会正常更新。");
    }

    [MenuItem("Tools/仪表盘/修正指针旋转中心（绕尾部）", false, 0)]
    public static void FixPointerPivot()
    {
        var controller = Object.FindFirstObjectByType<DashboardUI>(FindObjectsInactive.Include);
        RectTransform gauge = controller != null
            ? FindComp<RectTransform>(controller.transform, "GaugeRoot") : null;
        if (gauge == null)
        {
            EditorUtility.DisplayDialog("仪表盘", "当前场景里没有仪表盘，请先「生成到当前场景」。", "好");
            return;
        }

        RectTransform pointer = FindComp<RectTransform>(gauge, "Pointer");
        if (pointer == null)
        {
            EditorUtility.DisplayDialog("仪表盘", "找不到 Pointer（可能被改名了）。", "好");
            return;
        }

        float h = pointer.rect.height;
        if (pointer.pivot.y > 0.001f)
        {
            Vector2 pos = pointer.anchoredPosition;
            pos.y -= h * 0.5f;   // 保持指针视觉位置不变，仅把旋转中心下移半个长度
            pointer.pivot = new Vector2(pointer.pivot.x, 0f);
            pointer.anchoredPosition = pos;

            EditorSceneManager.MarkSceneDirty(pointer.gameObject.scene);
            Debug.Log("[仪表盘] 指针旋转中心已改到尾部（视觉位置不变，旋转中心下移 " + (h * 0.5f) + "px）。");
        }
        else
        {
            Debug.Log("[仪表盘] 指针已经是绕尾部旋转，无需修改。");
        }
    }

    [MenuItem("Tools/仪表盘/删除当前场景仪表盘", false, 1)]
    public static void Remove()
    {
        var all = Object.FindObjectsByType<DashboardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all.Length == 0)
        {
            EditorUtility.DisplayDialog("仪表盘", "当前场景里没有仪表盘。", "好");
            return;
        }
        foreach (var c in all)
        {
            var canvas = c.GetComponentInParent<Canvas>();
            Object.DestroyImmediate(canvas != null ? canvas.gameObject : c.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // ---------- 工具 ----------
    private static Sprite LoadSprite(string file)
    {
        string path = UI_DIR + file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null) Debug.LogWarning("[仪表盘] 找不到或无法作为 Sprite 导入：" + path);
        return sp;
    }

    private static Font BuiltinFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static Image NewImage(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.raycastTarget = false;
        return img;
    }

    private static Text NewText(Transform parent, string name, string content, int fontSize, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = BuiltinFont();
        t.text = content;
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private static Button NewSkillButton(Transform parent, string name, Sprite sprite, float xFromRight, float yFromBottom, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = true; // 需要接收点击

        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f); // 右下角
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(-xFromRight, yFromBottom);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        return btn;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) Debug.LogWarning("[仪表盘] 找不到字段：" + field);
        else p.objectReferenceValue = value;
    }

    private static T FindComp<T>(Transform root, string name) where T : Component
    {
        foreach (T c in root.GetComponentsInChildren<T>(true))
            if (c.gameObject.name == name) return c;
        return null;
    }
}
#endif
