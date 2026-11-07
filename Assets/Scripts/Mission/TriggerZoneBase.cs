using System.Collections.Generic;
using UnityEngine;

// 矩形判定区基类：用 BoxCollider(isTrigger) 划定区域，只对 PlayerSM 生效。
// 玩家身上有多个碰撞体（布娃娃四肢各自带 Rigidbody），这里用集合做引用计数，
// 保证"进入 / 离开"各只触发一次，不会被多个碰撞体重复触发。
[RequireComponent(typeof(BoxCollider))]
public abstract class TriggerZoneBase : MonoBehaviour
{
    [Header("判定设置")]
    [Tooltip("勾选后，死亡状态的玩家进入不算数；玩家在区域内死亡时倒计时也会停止")]
    [SerializeField] protected bool ignoreDeadPlayer = true;

    [Header("调试")]
    [Tooltip("打印玩家进出区域日志")]
    [SerializeField] protected bool verboseLog = true;

    // 玩家当前是否在区域内
    public bool PlayerInside { get; protected set; }

    // 区域内的玩家（不在区域时为 null）
    public PlayerSM Player { get; protected set; }

    // 玩家身上与区域重叠的碰撞体，用于多碰撞体引用计数
    private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();

    protected BoxCollider Box { get; private set; }

    protected virtual void Awake()
    {
        Box = GetComponent<BoxCollider>();
        if (Box == null) Box = gameObject.AddComponent<BoxCollider>();
        Box.isTrigger = true;
    }

    // 玩家进入区域（区域内第一个碰撞体进入时触发）
    protected virtual void OnPlayerEnter(PlayerSM player) { }

    // 玩家离开区域（区域内最后一个碰撞体离开时触发）
    protected virtual void OnPlayerExit(PlayerSM player) { }

    private void OnTriggerEnter(Collider other)
    {
        PlayerSM p = ResolvePlayer(other);
        if (p == null) return;
        if (ignoreDeadPlayer && p.IsDead) return;

        bool wasInside = PlayerInside;
        playerColliders.Add(other);

        if (!wasInside && playerColliders.Count > 0)
        {
            PlayerInside = true;
            Player = p;
            if (verboseLog) Debug.Log($"[结算][区域] {GetType().Name}：玩家进入区域");
            OnPlayerEnter(p);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!playerColliders.Remove(other)) return;
        if (playerColliders.Count > 0) return;

        PlayerSM p = Player;
        PlayerInside = false;
        Player = null;
        if (verboseLog) Debug.Log($"[结算][区域] {GetType().Name}：玩家离开区域");
        OnPlayerExit(p);
    }

    // 玩家是否在区域内且活着（派生类做倒计时前统一判断）
    protected bool HasLivePlayerInside()
    {
        return PlayerInside && Player != null && !Player.IsDead;
    }

    protected static PlayerSM ResolvePlayer(Collider other)
    {
        return other.GetComponentInParent<PlayerSM>();
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = Box != null ? Box : GetComponent<BoxCollider>();
        if (box == null) return;

        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        // 玩家在区域内时变橙，否则青色
        bool active = PlayerInside;
        Gizmos.color = active
            ? new Color(1f, 0.55f, 0.1f, 0.25f)
            : new Color(0.2f, 0.85f, 1f, 0.15f);
        Gizmos.DrawCube(box.center, box.size);

        Gizmos.color = active
            ? new Color(1f, 0.55f, 0.1f, 1f)
            : new Color(0.2f, 0.85f, 1f, 1f);
        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = old;
    }
}
