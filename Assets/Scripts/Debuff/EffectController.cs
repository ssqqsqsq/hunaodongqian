using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 视觉 / 操作类 Debuff 效果主控（效果细则 1/2/3/5/6/7/8/9/11）。
// 监听 DebuffManager 的 OnEffectApplied / OnEffectRemoved，按 EffectTag 做引用计数：
// 多条 Debuff 施加同一种效果时，只要还有一条生效就保持，全部解除才关闭。
// 4 数值变化 / 10 强制疾跑 / 12 死亡 由 DebuffManager 与结算界面处理，不在这里。
//
// 覆盖效果：
//   1  屏幕昏暗       边框向中间变暗（暗角）
//   2  屏幕更加昏暗   更深的暗角
//   3  屏幕扭曲       低透明度噪声层滚动 + 轻微晃动（模拟反胃感）
//   5  数值显示错误   暴露 IsValueGlitchActive，由 HUD 层把 气体/心率 显示值乱跳 ±20
//   6  画面暂留       定时抓一帧，冻结时间 0.5s，暂留画面透明度 0→100 再恢复
//   7  操作延迟       给 PlayerSM.InputDelayTime 设 0.5s（按键延迟响应）
//   8  操作反向       交给 PlayerSM.InputInvert（移动 / 鼠标 180°）
//   9  随机偏移       交给 PlayerSM.RandomDrift（每 3~6s 朝向随机偏 45°，最高优先级）
//   11 视野闪烁       全屏黑闪 0.5s 循环
public class EffectController : MonoBehaviour
{
    [Header("屏幕效果")]
    [Tooltip("效果1 屏幕昏暗：整屏变暗强度")]
    [SerializeField] private float darkenIntensity = 0.5f;
    [Tooltip("效果2 屏幕更加昏暗：更深变暗")]
    [SerializeField] private float darkerIntensity = 0.85f;
    [Tooltip("效果3 相机滚动摇摆幅度（度）")]
    [SerializeField] private float distortRollDeg = 3.6f;
    [Tooltip("效果3 相机俯仰抖动幅度（度）")]
    [SerializeField] private float distortPitchDeg = 1.9f;

    [Header("画面暂留（效果6）")]
    [SerializeField] private float freezeIntervalMin = 3f;
    [SerializeField] private float freezeIntervalMax = 6f;
    [Tooltip("冻结时长（秒），暂留画面透明度 0→100 在这段时间走完")]
    [SerializeField] private float freezeHoldTime = 0.5f;
    [SerializeField] private float freezeFadeOut = 0.3f;

    [Header("视野明暗（效果11）")]
    [Tooltip("明暗渐变周期（秒）")]
    [SerializeField] private float flickerCycle = 1.0f;
    [Tooltip("最暗时的黑幕透明度（0~1，避免完全黑屏）")]
    [SerializeField] private float flickerMaxAlpha = 0.8f;

    [Header("操作延迟（效果7）")]
    [SerializeField] private float inputDelaySeconds = 0.5f;

    [Header("调试")]
    [SerializeField] private bool logChanges = false;

    // ---- 运行时状态 ----
    private DebuffManager manager;
    private PlayerSM player;

    private readonly Dictionary<EffectTag, int> counts = new Dictionary<EffectTag, int>();

    private Image darkenImg;    // 变暗（效果1/2）
    private Image flickerImg;   // 黑闪（效果11）
    private RawImage freezeImg; // 画面暂留（效果6）

    private bool distorted;
    private Vector3 savedCamEuler;

    private RenderTexture freezeRt;
    private Camera freezeCam;

    public bool IsValueGlitchActive => IsActive(EffectTag.ValueDisplayGlitch);

    // 运行时自动补一个，方便直接测试（不需要手动挂到场景）
    private static bool _sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (!_sceneHooked)
        {
            _sceneHooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded; // 场景重载（R 重开）后重建
        }
        if (FindFirstObjectByType<EffectController>(FindObjectsInactive.Include) != null) return;

        var go = new GameObject("~EffectController");
        go.AddComponent<EffectController>();
        Debug.Log("[效果] 场景里没有 EffectController，已自动创建一个（处理屏幕/操作类 Debuff 效果）");
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
        manager = FindFirstObjectByType<DebuffManager>();
        player = FindFirstObjectByType<PlayerSM>();

        if (manager == null)
        {
            Debug.LogWarning("EffectController：场景里没有 DebuffManager，视觉/操作类效果不会生效");
            return;
        }

        manager.OnEffectApplied += HandleApply;
        manager.OnEffectRemoved += HandleRemove;
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.OnEffectApplied -= HandleApply;
            manager.OnEffectRemoved -= HandleRemove;
        }
        if (freezeRt != null) freezeRt.Release();
    }

    private void HandleApply(DebuffConfig cfg, EffectSpec e)
    {
        AddCount(e.tag, +1);
        if (logChanges) Debug.Log($"[效果] 施加 {e.tag}（Debuff[{cfg.id}] {cfg.name}）");
    }

    private void HandleRemove(DebuffConfig cfg, EffectSpec e)
    {
        AddCount(e.tag, -1);
        if (logChanges) Debug.Log($"[效果] 解除 {e.tag}（Debuff[{cfg.id}] {cfg.name}）");
    }

    private void AddCount(EffectTag tag, int delta)
    {
        int n = counts.TryGetValue(tag, out int v) ? v : 0;
        n += delta;
        if (n <= 0) counts.Remove(tag);
        else counts[tag] = n;
    }

    private bool IsActive(EffectTag tag)
    {
        return counts.TryGetValue(tag, out int n) && n > 0;
    }

    private void Update()
    {
        if (manager == null) return;

        UpdateDarken();
        UpdateDistort();
        UpdateFlicker();
        UpdateInputEffects();
    }

    // ---- 效果1/2：暗角 ----
    private void UpdateDarken()
    {
        bool dark = IsActive(EffectTag.ScreenDarken);
        bool darker = IsActive(EffectTag.ScreenDarker);

        float level = darker ? darkerIntensity : (dark ? darkenIntensity : 0f);
        darkenImg.gameObject.SetActive(level > 0f);
        darkenImg.color = new Color(0f, 0f, 0f, level);
    }

    // ---- 效果3：屏幕扭曲（仅相机摇摆制造头晕感，已去掉噪点层） ----
    private void UpdateDistort()
    {
        bool on = IsActive(EffectTag.ScreenDistort);

        Camera cam = Camera.main;
        if (cam == null) return;

        if (on)
        {
            // 首次生效记录相机基准旋转；CameraFollowXY 只改位置不改旋转，这里叠加摇摆
            if (!distorted)
            {
                savedCamEuler = cam.transform.localEulerAngles;
                distorted = true;
            }
            Vector3 e = savedCamEuler;
            e.z = savedCamEuler.z + Mathf.Sin(Time.time * 2.2f) * distortRollDeg;
            e.x = savedCamEuler.x + Mathf.Sin(Time.time * 1.7f) * distortPitchDeg;
            cam.transform.localEulerAngles = e;
        }
        else if (distorted)
        {
            // 结束：恢复相机基准旋转
            cam.transform.localEulerAngles = savedCamEuler;
            distorted = false;
        }
    }

    // ---- 效果11：视野明暗渐变（亮度正弦起伏，而不是黑闪跳变） ----
    private void UpdateFlicker()
    {
        bool on = IsActive(EffectTag.ScreenFlicker);
        flickerImg.gameObject.SetActive(on);

        if (!on)
        {
            // 结束时清掉残留黑幕
            flickerImg.color = new Color(0f, 0f, 0f, 0f);
            return;
        }

        // 0 → 最暗 → 0 的平滑正弦，无突变
        float phase = Mathf.Repeat(Time.unscaledTime, flickerCycle);
        float wave = Mathf.Sin(phase / flickerCycle * Mathf.PI * 2f) * 0.5f + 0.5f;
        flickerImg.color = new Color(0f, 0f, 0f, wave * flickerMaxAlpha);
    }

    // ---- 效果7/8/9：操作类，交给 PlayerSM ----
    private void UpdateInputEffects()
    {
        if (player == null) return;

        player.InputDelayTime = IsActive(EffectTag.InputDelay) ? inputDelaySeconds : 0f;
        player.InputInvert = IsActive(EffectTag.InputInvert);
        player.RandomDrift = IsActive(EffectTag.RandomDrift);
    }

    // ---- 效果6：画面暂留（定时抓帧 + 停时淡入） ----
    // 常驻协程：效果未激活时只是空转等待，激活后才开始定时抓帧
    private IEnumerator FreezeLoop()
    {
        while (true)
        {
            // 空闲等待，直到画面暂留效果被激活
            if (!IsActive(EffectTag.FrameFreeze))
            {
                yield return null;
                continue;
            }

            // 随机间隔 3~6s；期间随时可能被移除
            float interval = Random.Range(freezeIntervalMin, freezeIntervalMax);
            float t = 0f;
            while (t < interval && IsActive(EffectTag.FrameFreeze))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!IsActive(EffectTag.FrameFreeze)) continue;

            // 抓当前帧；失败则退化为“黑幕淡入”，画面本身停时后也是静态的
            bool captured = CaptureFrame();

            float prevTimeScale = Time.timeScale;
            Time.timeScale = 0f; // 冻结

            freezeImg.gameObject.SetActive(true);

            // 暂留画面透明度 0 → 100
            float hold = 0f;
            while (hold < freezeHoldTime)
            {
                hold += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(hold / freezeHoldTime);
                freezeImg.color = captured
                    ? new Color(1f, 1f, 1f, a)
                    : new Color(0f, 0f, 0f, a);
                yield return null;
            }

            // 恢复：淡出
            float fade = 0f;
            while (fade < freezeFadeOut)
            {
                fade += Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(fade / freezeFadeOut);
                freezeImg.color = captured
                    ? new Color(1f, 1f, 1f, a)
                    : new Color(0f, 0f, 0f, a);
                yield return null;
            }

            freezeImg.gameObject.SetActive(false);
            Time.timeScale = prevTimeScale;
        }
    }

    // 抓当前画面到 RenderTexture，用独立相机渲染，避免打扰主相机。
    // 返回是否抓帧成功（失败时上层退化为黑幕效果）。
    private bool CaptureFrame()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        EnsureFreezeRt();
        EnsureFreezeCam();

        Transform f = freezeCam.transform;
        Transform m = cam.transform;
        f.position = m.position;
        f.rotation = m.rotation;

        freezeCam.orthographic = cam.orthographic;
        freezeCam.orthographicSize = cam.orthographicSize;
        freezeCam.fieldOfView = cam.fieldOfView;
        freezeCam.cullingMask = cam.cullingMask;
        freezeCam.clearFlags = cam.clearFlags;
        freezeCam.backgroundColor = cam.backgroundColor;
        freezeCam.nearClipPlane = cam.nearClipPlane;
        freezeCam.farClipPlane = cam.farClipPlane;

        try
        {
            freezeCam.targetTexture = freezeRt;
            freezeCam.Render();
            freezeCam.targetTexture = null;
            freezeImg.texture = freezeRt;
            return true;
        }
        catch (System.Exception ex)
        {
            freezeCam.targetTexture = null;
            freezeImg.texture = null;
            Debug.LogWarning($"EffectController：抓帧失败，画面暂留退化为黑幕 —— {ex.Message}");
            return false;
        }
    }

    private void EnsureFreezeRt()
    {
        if (freezeRt != null &&
            freezeRt.width == Screen.width &&
            freezeRt.height == Screen.height) return;

        if (freezeRt != null) freezeRt.Release();

        int w = Mathf.Max(1, Screen.width);
        int h = Mathf.Max(1, Screen.height);
        freezeRt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        freezeRt.Create();
    }

    private void EnsureFreezeCam()
    {
        if (freezeCam != null) return;

        var go = new GameObject("FreezeCaptureCamera");
        go.transform.SetParent(transform, false);
        freezeCam = go.AddComponent<Camera>();
        freezeCam.enabled = false; // 手动 Render()，不自动渲染
    }

    // ---- UI 搭建 ----
    private void BuildUi()
    {
        var canvasGo = new GameObject("EffectCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 15; // 结算界面（-10）之下、普通 UI 之上

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 变暗（效果1/2）：整屏黑色半透明遮罩，简单不花哨
        darkenImg = MakeImage(canvasGo.transform, "Darken", null, new Color(0f, 0f, 0f, 0f));
        Stretch(darkenImg.rectTransform);
        darkenImg.gameObject.SetActive(false);

        // 视野闪烁（效果11）
        flickerImg = MakeImage(canvasGo.transform, "Flicker", null, new Color(0f, 0f, 0f, 0f));
        Stretch(flickerImg.rectTransform);
        flickerImg.gameObject.SetActive(false);

        // 画面暂留（效果6）
        var freezeGo = new GameObject("FrameFreeze", typeof(RawImage));
        freezeGo.transform.SetParent(canvasGo.transform, false);
        freezeImg = freezeGo.GetComponent<RawImage>();
        freezeImg.color = Color.white;
        freezeImg.raycastTarget = false;
        Stretch(freezeImg.rectTransform);
        freezeImg.gameObject.SetActive(false);

        StartCoroutine(FreezeLoop()); // 空转等待，效果激活后开始跑
    }

    private static Image MakeImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Sprite SpriteFrom(Texture2D tex)
    {
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
