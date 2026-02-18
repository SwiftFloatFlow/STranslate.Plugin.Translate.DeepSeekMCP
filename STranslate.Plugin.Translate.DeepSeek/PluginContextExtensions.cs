using STranslate.Plugin;
using System.Reflection;

namespace STranslate.Plugin.Translate.DeepSeek;

/// <summary>
/// IPluginContext扩展类 - 为IPluginContext添加GetGlobalPrompts支持（SDK暂不支持时的兼容方案）
/// </summary>
public static class PluginContextExtensions
{
    /// <summary>
    /// 获取所有已启用的全局提示词列表
    /// </summary>
    public static IReadOnlyList<GlobalPrompt> GetGlobalPrompts(this IPluginContext context)
    {
        // 尝试通过反射调用SDK的方法（如果SDK已更新）
        var method = context.GetType().GetMethod("GetGlobalPrompts");
        if (method != null)
        {
            var result = method.Invoke(context, null);
            if (result is IReadOnlyList<GlobalPrompt> prompts)
            {
                return prompts;
            }
        }
        
        // SDK暂不支持，返回空列表
        return Array.Empty<GlobalPrompt>();
    }

    /// <summary>
    /// 根据ID获取特定的全局提示词
    /// </summary>
    public static GlobalPrompt? GetGlobalPromptById(this IPluginContext context, string id)
    {
        // 尝试通过反射调用SDK的方法（如果SDK已更新）
        var method = context.GetType().GetMethod("GetGlobalPromptById");
        if (method != null)
        {
            var result = method.Invoke(context, new object[] { id });
            return result as GlobalPrompt;
        }
        
        // SDK暂不支持，返回null
        return null;
    }
}

/// <summary>
/// 全局提示词定义（SDK暂不支持时的兼容方案）
/// </summary>
public class GlobalPrompt
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "新全局提示词";
    public bool IsEnabled { get; set; } = true;
    public List<PromptItem> Items { get; set; } = [];

    /// <summary>
    /// 转换为普通 Prompt（供插件使用）
    /// </summary>
    public Prompt ToPrompt(bool isEnabled = false)
    {
        var prompt = new Prompt
        {
            Name = $"[Global:{Id}] {Name}",
            IsEnabled = isEnabled
        };
        
        foreach (var item in Items)
        {
            prompt.Items.Add(item);
        }
        
        // 设置Tag标识
        prompt.SetTag($"Global:{Id}");
        
        return prompt;
    }
}
