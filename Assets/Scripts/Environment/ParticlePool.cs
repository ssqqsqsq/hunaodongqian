using System.Collections.Generic;
using UnityEngine;

// 粒子对象池：预生成一批 ParticleSystem，按需取用、用完归还，
// 避免运行时频繁 Instantiate / Destroy 造成 GC 和卡顿。
//
// 用法：
//   var pool = new ParticlePool(template, prewarm);   // template 为已配置好的粒子系统（建议 inactive）
//   var ps = pool.Get();                              // 取一个，激活并清空
//   ps.Emit(count);                                   // 用完后调用
//   pool.Release(ps);                                 // 归还原池（停止并清空、隐藏）
//   pool.Dispose();                                   // 销毁全部池对象
public class ParticlePool
{
    private readonly ParticleSystem template;
    private readonly List<ParticleSystem> free = new List<ParticleSystem>();
    private readonly List<ParticleSystem> all = new List<ParticleSystem>();

    // 池里所有创建过的发射器（含使用中的），供需要时遍历
    public IReadOnlyList<ParticleSystem> All => all;
    public int Count => all.Count;
    public int FreeCount => free.Count;

    public ParticlePool(ParticleSystem template, int prewarm)
    {
        this.template = template;

        int n = Mathf.Max(0, prewarm);
        for (int i = 0; i < n; i++)
            free.Add(CreateOne());
    }

    // 从池中取一个可用的发射器；池空则自动扩容
    public ParticleSystem Get()
    {
        ParticleSystem instance;
        if (free.Count > 0)
        {
            int last = free.Count - 1;
            instance = free[last];
            free.RemoveAt(last);
        }
        else
        {
            instance = CreateOne();
        }

        instance.gameObject.SetActive(true);
        instance.Clear(true);
        instance.Play(true);
        return instance;
    }

    // 归还发射器：停止、清空粒子并隐藏，等待下次复用
    public void Release(ParticleSystem instance)
    {
        if (instance == null) return;
        if (free.Contains(instance)) return; // 防止重复归还

        instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        instance.Clear(true);
        instance.gameObject.SetActive(false);
        free.Add(instance);
    }

    // 销毁池里所有发射器（场景卸载 / 组件销毁时调用）
    public void Dispose()
    {
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null) Object.Destroy(all[i].gameObject);
        }

        all.Clear();
        free.Clear();
    }

    private ParticleSystem CreateOne()
    {
        ParticleSystem instance = Object.Instantiate(template, template.transform.parent);
        instance.gameObject.name = template.gameObject.name + "_Pooled" + all.Count;

        // 保持与模板一致（Instantiate 挂父级时位置处理因重载而异，这里显式对齐）
        instance.transform.localPosition = template.transform.localPosition;
        instance.transform.localRotation = template.transform.localRotation;
        instance.transform.localScale = template.transform.localScale;

        instance.gameObject.SetActive(false);
        all.Add(instance);
        return instance;
    }
}
