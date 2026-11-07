using UnityEngine;
using UnityEngine.SceneManagement;

// 冲刺时镜头轻微拉近，增强速度感；结束平滑还原。
// 与产道 QTE 的镜头拉近互斥（QTE 忙时本组件不干预 FOV）。
public class DashCameraZoom : MonoBehaviour
{
    [Tooltip("冲刺时 FOV 倍率（<1 拉近，1 不缩放）")]
    [SerializeField] private float dashFovFactor = 0.9f;
    [Tooltip("缩放平滑速度（越大越快）")]
    [SerializeField] private float lerpSpeed = 8f;
    [Tooltip("相机，留空自动取 玩家相机 / Camera.main")]
    [SerializeField] private Camera targetCamera;

    private PlayerSM player;
    private Camera cam;
    private float baseFov;
    private bool captured;
    private bool dashing;
    private bool subscribed;

    private static bool _sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (!_sceneHooked)
        {
            _sceneHooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        if (FindFirstObjectByType<DashCameraZoom>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("~DashCameraZoom");
        go.AddComponent<DashCameraZoom>();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
    }

    private void Start()
    {
        Resolve();
    }

    private void OnDestroy()
    {
        if (player != null && subscribed) player.OnStateChanged -= OnStateChanged;
    }

    private void Resolve()
    {
        if (player == null) player = FindFirstObjectByType<PlayerSM>();
        if (cam == null)
        {
            cam = targetCamera != null ? targetCamera
                : (player != null && player.worldCamera != null ? player.worldCamera : Camera.main);
        }
    }

    private void OnStateChanged(BaseState state)
    {
        dashing = player != null && state == player.dashState;
    }

    private void Update()
    {
        Resolve();
        if (player == null || cam == null) return;

        if (!subscribed)
        {
            player.OnStateChanged += OnStateChanged;
            subscribed = true;
        }

        // 其它 QTE（产道等）在控制 FOV 时让出，避免互相拉扯
        if (QteState.Busy) return;

        if (!captured)
        {
            baseFov = cam.fieldOfView;
            captured = true;
        }

        float target = dashing ? baseFov * dashFovFactor : baseFov;
        float k = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, k);
    }
}
