using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
// 内心独白 / 自言自语系统（运行时自动创建）
//   · 进入游戏随机说一句登场词（20% 通用池 / 80% 对应关卡池）
//   · 当 Debuff 发作时，从对应 buff 池抽一句
//   · 文字在屏幕中央偏上（不遮挡角色），淡入 1s → 停 2s → 淡出 1s（共 4s）
//   · 新的独白会打断当前独白并重新淡入
// ============================================================
public class SelfTalkSystem : MonoBehaviour
{
    [Header("显示")]
    [SerializeField] private float textOffsetY = 220f;   // 相对屏幕中心向上偏移（不挡角色）
    [SerializeField] private float textWidth = 1000f;
    [SerializeField] private int fontSize = 30;
    [SerializeField] private float fadeIn = 1f;
    [SerializeField] private float hold = 2f;
    [SerializeField] private float fadeOut = 1f;

    [SerializeField] private CanvasGroup group;
    [SerializeField] private Text text;
    private Coroutine fadeCo;

    private DebuffManager debuff;
    private bool entryPlayed;

    private static bool _sceneHooked;

    // ---------------- 文案池 ----------------

    // 登场：通用池
    private static readonly string[] GeneralEntry =
    {
        "感觉今天我运气贼好，说不定能发现宝藏呢",
        "头灯，气罐，面罩......我准备好了！",
        "兄弟们，耍起",
        "斯哈，这水比我想象的凉多了！",
        "这次我负责搅动泥沙......开玩笑的！",
    };

    // 登场：关卡池（按场景名匹配）
    private static readonly Dictionary<string, string[]> LevelEntry = new Dictionary<string, string[]>
    {
        {
            "diyiguan", new[]
            {
                "这次我一定要亲眼看到那样的美景",
                "百闻不如一见，水晶洞给我等好了",
                "要是带块水晶回去我不赚大了？",
            }
        },
    };

    // buff 发作：Debuff id → 台词池
    private static readonly Dictionary<int, string[]> BuffLines = new Dictionary<int, string[]>
    {
        { 1, new[] { "我感觉有点头痛......", "我感觉有点喘不过来气......" } },                       // 早期高碳酸血症
        { 2, new[] { "头晕恶心......好想吐......" } },                                             // 重度高碳酸血症
        { 4, new[] { "继续深入吧，我现在信心满满！", "我现在状态极佳，我们走吧！" } },               // 早期氮麻醉
        { 5, new[] { "这是什么地方？？我在干什么？！", "我周围都是什么东西？！不要过来！！" } },     // 重度氮麻醉
        { 7, new[] { "有......有一点累......", "我想需要休息一下......" } },                        // 轻度心率过快
        { 8, new[] { "完全搞不清方向了！我不要死在这里啊！！", "我刚刚转向了吗？！这是什么地方！！" } }, // 重度心率过快1
        { 9, new[] { "完蛋了，彻底完蛋了！我要离开这里！！", "快跑啊，我不想死在这儿！" } },           // 重度心率过快2
        { 10, new[] { "我能感受到水对我的压迫，不过我还可以接受" } },                               // 深度心率+1
        { 11, new[] { "会不会有点太深了？应该没事吧......" } },                                      // 深度心率+2
        { 12, new[] { "水压的我难受，控制不住心跳加速......" } },                                    // 深度心率+3
        { 14, new[] { "已经用掉1/3气体了，我觉得要考虑返回了" } },                                    // 气体余量心率+1
        { 15, new[] { "只剩下一半气体了，再不走要来不及了！" } },                                     // 气体余量心率+2
        { 16, new[] { "不能再耽搁了，我必须马上离开了！！" } },                                      // 气体余量心率+3
        { 17, new[] { "哎！咬嘴掉了！掉哪去了？！", "怎么连咬嘴都没拿住？没事我还能憋一会......" } }, // 突发1 咬嘴脱落
        { 18, new[] { "卡住了！！怎么会？！" } },                                                   // 突发2 缝隙卡住
        { 19, new[] { "什么都看不见了，我在哪？？", "冷静冷静！乱动只会让泥沙更多......" } },         // 突发3 搅动泥沙
        { 21, new[] { "我需要平复一下心情......", "我还以为这次要上神秘园了......", "呜啊......刚刚吓死我了......" } }, // 心有余悸
    };

    // ---------------- 生命周期 ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (!_sceneHooked)
        {
            _sceneHooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        if (FindFirstObjectByType<SelfTalkSystem>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("~SelfTalkSystem");
        go.AddComponent<SelfTalkSystem>();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
    }

    private void Awake()
    {
        if (group == null || text == null)
        {
            BuildUi();
        }
        else
        {
            // 场景里已有（编辑器一键生成）：运行时换成系统中文字体，初始全透明
            text.font = QteUi.GetFont();
            group.alpha = 0f;
        }
    }

    private void OnDestroy()
    {
        if (debuff != null) debuff.OnDebuffActivated -= OnDebuffActivated;
    }

    private void Update()
    {
        if (debuff == null) BindDebuff();

        // 等玩家出现后说一句登场词（菜单场景没有玩家就不说）
        if (!entryPlayed && FindFirstObjectByType<PlayerSM>() != null)
        {
            entryPlayed = true;
            PlayEntryLine();
        }
    }

    private void BindDebuff()
    {
        var d = FindFirstObjectByType<DebuffManager>();
        if (d == null) return;
        debuff = d;
        debuff.OnDebuffActivated += OnDebuffActivated;
    }

    // ---------------- 触发 ----------------

    private void PlayEntryLine()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        string[] pool = GeneralEntry;
        // 80% 关卡池（若该场景有关卡池），20% 通用池
        if (Random.value < 0.8f &&
            LevelEntry.TryGetValue(sceneName, out string[] levelPool) &&
            levelPool != null && levelPool.Length > 0)
        {
            pool = levelPool;
        }

        Say(Pick(pool));
    }

    private void OnDebuffActivated(DebuffConfig cfg)
    {
        if (cfg == null) return;
        if (!BuffLines.TryGetValue(cfg.id, out string[] pool) || pool == null || pool.Length == 0) return;
        Say(Pick(pool));
    }

    private static string Pick(string[] pool)
    {
        if (pool == null || pool.Length == 0) return null;
        return pool[Random.Range(0, pool.Length)];
    }

    // ---------------- 显示 ----------------

    private void Say(string line)
    {
        if (string.IsNullOrEmpty(line) || text == null) return;

        if (fadeCo != null) StopCoroutine(fadeCo);
        text.text = line;
        fadeCo = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        // 淡入
        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Clamp01(t / fadeIn);
            yield return null;
        }
        group.alpha = 1f;

        // 停留
        yield return new WaitForSeconds(hold);

        // 淡出
        t = 0f;
        while (t < fadeOut)
        {
            t += Time.deltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / fadeOut);
            yield return null;
        }
        group.alpha = 0f;
        fadeCo = null;
    }

    // ---------------- UI ----------------

    private void BuildUi()
    {
        Canvas canvas = QteUi.CreateOverlay(transform, "SelfTalkCanvas", short.MaxValue - 8);

        var textGo = new GameObject("SelfTalkText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(CanvasGroup));
        textGo.transform.SetParent(canvas.transform, false);

        text = textGo.GetComponent<Text>();
        text.font = QteUi.GetFont();           // 系统中文动态字体
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = "";

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        group = textGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        RectTransform rt = text.rectTransform;
        QteUi.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, textOffsetY), new Vector2(textWidth, 200f));
    }
}
