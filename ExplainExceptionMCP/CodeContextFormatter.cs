using System.Text;

namespace ExplainExceptionMCP;

/// <summary>
/// 将多个 stack frame 的代码上下文合并为适合放进提示词的文本。
/// </summary>
internal static class CodeContextFormatter
{
    /// <summary>
    /// 按 stack frame 顺序合并代码上下文。
    /// </summary>
    /// <param name="frames">已选中的 stack frame 列表。</param>
    /// <param name="contexts">与 frame 对应的代码上下文列表。</param>
    /// <returns>带 frame 标题、路径、警告和代码块的合并文本。</returns>
    public static string Combine(IReadOnlyList<StackFrameLocation> frames, IReadOnlyList<CodeContextResult> contexts)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < contexts.Count; i++)
        {
            var context = contexts[i];
            var frame = i < frames.Count ? frames[i] : null;

            builder.Append("【Stack Frame ")
                .Append(i)
                .AppendLine("】");

            if (frame is not null)
            {
                builder.Append("Method: ").AppendLine(frame.Method);
                builder.Append("Stack Path: ").Append(frame.FilePath).Append(':').AppendLine(frame.Line.ToString());
            }

            builder.Append("Resolved Path: ").AppendLine(context.ResolvedFilePath ?? "<not found>");
            if (!string.IsNullOrWhiteSpace(context.Warning))
            {
                builder.Append("Warning: ").AppendLine(context.Warning);
            }

            if (!context.Success)
            {
                // 即使某一帧源码读取失败，也把失败原因放入提示词，便于调用方 LLM 说明限制。
                builder.Append("Error: ").AppendLine(context.Error);
                builder.AppendLine();
                continue;
            }

            builder.AppendLine("```csharp");
            builder.AppendLine(context.Code);
            builder.AppendLine("```");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}
