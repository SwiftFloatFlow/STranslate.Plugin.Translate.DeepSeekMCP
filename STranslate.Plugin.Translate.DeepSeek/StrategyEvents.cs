using System.Runtime.CompilerServices;

namespace STranslate.Plugin.Translate.DeepSeek;

/// <summary>
/// 提示词策略变更事件参数
/// </summary>
public class PromptStrategyChangedEventArgs : EventArgs
{
    public string PromptName { get; }
    public McpToolStrategy NewStrategy { get; }

    public PromptStrategyChangedEventArgs(string promptName, McpToolStrategy newStrategy)
    {
        PromptName = promptName;
        NewStrategy = newStrategy;
    }
}

/// <summary>
/// 弱引用事件管理器 - 用于命令系统与UI之间的通信
/// 避免内存泄漏，当订阅者被GC回收时自动清理
/// </summary>
public static class StrategyEvents
{
    // 使用 ConditionalWeakTable 存储弱引用回调
    private static readonly ConditionalWeakTable<object, List<WeakReference<EventHandler<PromptStrategyChangedEventArgs>>>> _subscribers = new();
    
    // 全局事件（用于静态访问）
    private static event EventHandler<PromptStrategyChangedEventArgs>? _globalEvent;

    /// <summary>
    /// 订阅策略变更事件（弱引用）
    /// </summary>
    /// <param name="subscriber">订阅者对象（用于弱引用关联）</param>
    /// <param name="handler">事件处理器</param>
    public static void Subscribe(object subscriber, EventHandler<PromptStrategyChangedEventArgs> handler)
    {
        if (subscriber == null || handler == null) return;

        var list = _subscribers.GetOrCreateValue(subscriber);
        list.Add(new WeakReference<EventHandler<PromptStrategyChangedEventArgs>>(handler));
        _globalEvent += handler;
    }

    /// <summary>
    /// 取消订阅策略变更事件
    /// </summary>
    public static void Unsubscribe(object subscriber, EventHandler<PromptStrategyChangedEventArgs> handler)
    {
        if (subscriber == null || handler == null) return;

        if (_subscribers.TryGetValue(subscriber, out var list))
        {
            list.RemoveAll(wr =>
            {
                if (wr.TryGetTarget(out var target))
                {
                    return target == handler;
                }
                return true; // 清理已失效的引用
            });
        }
        _globalEvent -= handler;
    }

    /// <summary>
    /// 触发策略变更事件
    /// </summary>
    public static void RaisePromptStrategyChanged(string promptName, McpToolStrategy newStrategy)
    {
        var args = new PromptStrategyChangedEventArgs(promptName, newStrategy);
        _globalEvent?.Invoke(null, args);
    }

    /// <summary>
    /// 清理所有已失效的订阅
    /// </summary>
    public static void Cleanup()
    {
        foreach (var entry in _subscribers.ToList())
        {
            entry.Value.RemoveAll(wr => !wr.TryGetTarget(out _));
        }
    }
}
