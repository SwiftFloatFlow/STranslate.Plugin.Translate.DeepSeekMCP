using STranslate.Plugin;
using System.Collections.Concurrent;

namespace STranslate.Plugin.Translate.DeepSeek;

/// <summary>
/// Prompt扩展类 - 为Prompt添加Tag支持（SDK暂不支持时的兼容方案）
/// </summary>
public static class PromptExtensions
{
    /// <summary>
    /// 存储Prompt到Tag的映射
    /// </summary>
    private static readonly ConcurrentDictionary<Prompt, object?> _tagMap = new();

    /// <summary>
    /// 获取Prompt的Tag
    /// </summary>
    public static object? GetTag(this Prompt prompt)
    {
        // 首先尝试从SDK的Tag属性获取（如果SDK已更新）
        var sdkTag = prompt.GetType().GetProperty("Tag")?.GetValue(prompt);
        if (sdkTag != null) return sdkTag;
        
        // 否则从扩展映射获取
        return _tagMap.TryGetValue(prompt, out var tag) ? tag : null;
    }

    /// <summary>
    /// 设置Prompt的Tag
    /// </summary>
    public static void SetTag(this Prompt prompt, object? tag)
    {
        // 首先尝试设置到SDK的Tag属性（如果SDK已更新）
        var tagProperty = prompt.GetType().GetProperty("Tag");
        if (tagProperty != null && tagProperty.CanWrite)
        {
            tagProperty.SetValue(prompt, tag);
            return;
        }
        
        // 否则设置到扩展映射
        _tagMap[prompt] = tag;
    }

    /// <summary>
    /// 判断Prompt是否为全局提示词
    /// </summary>
    public static bool IsGlobalPrompt(this Prompt prompt)
    {
        var tag = prompt.GetTag()?.ToString();
        return tag?.StartsWith("Global:") == true;
    }

    /// <summary>
    /// 获取Prompt的显示名称（带来源标识）
    /// </summary>
    public static string GetPromptDisplayName(this Prompt prompt)
    {
        if (prompt.IsGlobalPrompt())
        {
            return $"{prompt.Name} [全局]";
        }
        return prompt.Name;
    }
}
