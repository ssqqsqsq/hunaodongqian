using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 切换气瓶 QTE（按钮1 触发）：
//   指针从正下方开始顺时针旋转（1s 一圈），转到金色判定区时按键即校准。
//   前 3 次为大区域（45°，上半圆随机位置）；3 次成功后第 4 次为小区域（20°）。
//   第 4 次成功 → QTE 成功；第 4 次失败 → QTE 失败（判定区变红）。
//   失败不累计进度，只重置判定区继续校准。
// 视觉风格与穿越产道一致，放在屏幕左侧，不做镜头缩放。
public class TankSwitchQTE : MonoBehaviour
{
    public static TankSwitchQTE Instance { get; private set; }

    [Header("节奏")]
    [Tooltip("指针转一圈的秒数")]
    [SerializeField] private float revSeconds = 1.5f;
    [SerializeField] private float largeZoneDeg = 45f;
    [SerializeField] private float smallZoneDeg = 20f;
    [Tooltip("前几次是大区域校准")]
    [SerializeField] private int largeZoneCount = 3;
    [Tooltip("每次校准后的间隔（秒）")]
    [SerializeField] private float betweenDelay = 0.5f;

    [Header("按键")]
    public KeyCode pressKey = KeyCode.Space;
    public bool acceptMouseClick = true;

    [Header("视觉")]
    [SerializeField] private float ringSize = 300f;
    [SerializeField] private Color grayRingColor = new Color(0.5f, 0.55f, 0.6f, 0.85f);
    [SerializeField] private Color goldColor = new Color(1f, 0.82f, 0.12f, 0.95f);
    [SerializeField] private Color failRedColor = new Color(1f, 0.32f, 0.26f, 0.95f);
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.25f);

    [Header("调试")]
    [SerializeField] private bool verboseLog = true;

    public bool IsActive { get; private set; }
    public event Action<bool> OnFinished; // true = QTE 成功

    private GameObject canvasGo;
    private RectTransform goldArcRt;
    private Image goldArcImg;
    private RectTransform pointerRt;
    private Text statusText;
    private Sprite spriteLarge;
    private Sprite spriteSmall;

    private float zoneMid;   // 当前判定区中心（顺时针、自正下方起）
    private float zoneWidth; // 当前判定区宽度
    private Action<bool> currentCallback;
    private Coroutine session;

    private static bool _sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (!_sceneHooked)
        {
            _sceneHooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded; // 场景重载（R 重开）后重建
        }
        if (FindFirstObjectByType<TankSwitchQTE>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("~TankSwitchQTE");
        go.AddComponent<TankSwitchQTE>();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
    }

    private void Awake()
    {
        Instance = this;
        BuildUi();
        canvasGo.SetActive(false);
    }

    private void OnDestroy()
    {
        if (session != null) StopCoroutine(session);
        if (IsActive) { IsActive = false; QteState.End(); }
        if (Instance == this) Instance = null;
    }

    // 外部调用：开始一次完整 QTE（成功 / 失败 回调）
    public bool Play(Action<bool> onFinished)
    {
        if (IsActive) return false;

        currentCallback = onFinished;
        IsActive = true;
        QteState.Begin();
        session = StartCoroutine(Run());
        return true;
    }

    public void ForceStop()
    {
        if (!IsActive) return;
        if (session != null) StopCoroutine(session);
        Finish(false);
    }

    private IEnumerator Run()
    {
        canvasGo.SetActive(true);
        statusText.text = "指针转到金色区域时按键校准";

        int successCount = 0;

        while (IsActive)
        {
            bool isSmall = successCount >= largeZoneCount;
            zoneWidth = isSmall ? smallZoneDeg : largeZoneDeg;
            SetupZone(isSmall);

            // 指针从正下方开始顺时针转
            float a = 0f;
            bool resolved = false;

            while (!resolved && IsActive)
            {
                a = (a + 360f / revSeconds * Time.unscaledDeltaTime) % 360f;
                pointerRt.localRotation = Quaternion.Euler(0f, 0f, 180f - a);

                if (Input.GetKeyDown(pressKey) || (acceptMouseClick && Input.GetMouseButtonDown(0)))
                {
                    resolved = true;
                    bool hit = InZone(a, zoneMid, zoneWidth);

                    if (hit)
                    {
                        if (isSmall)
                        {
                            statusText.text = "QTE 成功！";
                            Finish(true);
                            yield break;
                        }

                        successCount++;
                        statusText.text = $"校准成功 {successCount}/{largeZoneCount}，准备下一次";
                        if (verboseLog) Debug.Log($"[换瓶QTE] 大区校准成功 {successCount}/{largeZoneCount}");
                    }
                    else
                    {
                        if (isSmall)
                        {
                            statusText.text = "QTE 失败";
                            goldArcImg.color = failRedColor;
                            if (verboseLog) Debug.Log("[换瓶QTE] 小区校准失败 → QTE 失败");
                            Finish(false);
                            yield break;
                        }

                        statusText.text = "校准失败，重置判定区再来";
                        if (verboseLog) Debug.Log("[换瓶QTE] 大区校准失败，继续");
                    }
                }

                yield return null;
            }

            if (!IsActive) break;
            yield return new WaitForSecondsRealtime(betweenDelay);
        }
    }

    private void SetupZone(bool isSmall)
    {
        float margin = zoneWidth * 0.5f + 12f;
        zoneMid = UnityEngine.Random.Range(90f + margin, 270f - margin);

        goldArcImg.sprite = isSmall ? spriteSmall : spriteLarge;
        goldArcImg.color = goldColor;
        goldArcRt.localRotation = Quaternion.Euler(0f, 0f, 180f - zoneMid);
    }

    private static bool InZone(float a, float mid, float width)
    {
        return Mathf.Abs(Mathf.DeltaAngle(a, mid)) <= width * 0.5f;
    }

    private void Finish(bool success)
    {
        if (!IsActive) return;

        IsActive = false;
        canvasGo.SetActive(false);
        QteState.End();

        Action<bool> cb = currentCallback;
        currentCallback = null;
        cb?.Invoke(success);
    }

    // ---- UI ----
    private void BuildUi()
    {
        spriteLarge = QteUi.SpriteFrom(QteUi.MakeArcTexture(256, 0.72f, 1f, largeZoneDeg * 0.5f));
        spriteSmall = QteUi.SpriteFrom(QteUi.MakeArcTexture(256, 0.72f, 1f, smallZoneDeg * 0.5f));

        Canvas canvas = QteUi.CreateOverlay(transform, "TankSwitchQteCanvas", short.MaxValue - 7);
        canvasGo = canvas.gameObject;

        // 半透明遮罩
        var dim = QteUi.NewImage(canvas.transform, "Dim", dimColor);
        QteUi.Stretch(dim.rectTransform);

        // 圆环容器（左侧）
        var ringRoot = QteUi.NewRect(canvas.transform, "RingRoot");
        QteUi.LeftMiddle(ringRoot, 380f, 40f, ringSize, ringSize);

        // 灰色底环
        Image gray = QteUi.NewImage(ringRoot, "GrayRing", grayRingColor);
        gray.sprite = QteUi.SpriteFrom(QteUi.MakeRingTexture(256, 0.72f, 1f));
        QteUi.Stretch(gray.rectTransform);

        // 金色判定区（圆弧）
        goldArcImg = QteUi.NewImage(ringRoot, "GoldZone", goldColor);
        goldArcImg.sprite = spriteLarge;
        goldArcRt = goldArcImg.rectTransform;
        QteUi.Stretch(goldArcRt);

        // 指针（从中心指向外）
        Image pointer = QteUi.NewImage(ringRoot, "Pointer", goldColor);
        pointerRt = pointer.rectTransform;
        QteUi.Place(pointerRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(8f, ringSize * 0.5f));

        // 中心点
        Image centerDot = QteUi.NewImage(ringRoot, "CenterDot", grayRingColor);
        centerDot.sprite = QteUi.SpriteFrom(QteUi.MakeCircleTexture(48));
        QteUi.Place(centerDot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));

        // 状态文字
        statusText = QteUi.NewText(canvas.transform, "Status", 30, TextAnchor.MiddleCenter, Color.white);
        QteUi.LeftMiddle(statusText.rectTransform, 380f, -180f, 640f, 50f);
    }
}
