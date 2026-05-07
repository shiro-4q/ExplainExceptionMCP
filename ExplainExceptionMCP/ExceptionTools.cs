using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ExplainExceptionMCP;

/// <summary>
/// 暴露给 MCP 客户端的异常解释工具集合。
/// </summary>
/// <remarks>
/// <see cref="McpServerToolTypeAttribute"/> 让 <c>WithToolsFromAssembly</c> 能发现这个类型；
/// 具体方法仍需要使用 <see cref="McpServerToolAttribute"/> 才会作为 MCP tool 暴露。
/// </remarks>
[McpServerToolType]
public static class ExceptionTools
{
    /// <summary>
    /// 从 .NET/C# stack trace 中解析源码文件路径、行号和方法名。
    /// </summary>
    /// <param name="stack">完整异常文本或 stack trace。</param>
    /// <returns>解析出的源码位置列表。</returns>
    [McpServerTool(Name = nameof(ParseStackTrace), ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("从 .NET/C# stack trace 中解析源码文件路径、行号和方法名。")]
    public static ParseStackTraceResult ParseStackTrace(
        [Description("完整异常文本或 stack trace。")] string stack)
    {
        return StackTraceParser.Parse(stack);
    }

    /// <summary>
    /// 读取指定文件指定行附近的代码上下文。
    /// </summary>
    /// <param name="filePath">源码文件路径，可以是绝对路径、相对路径，或 stack trace 中解析出的路径。</param>
    /// <param name="line">目标行号，使用 1-based 编号。</param>
    /// <param name="before">目标行之前读取的行数，最大 100。</param>
    /// <param name="after">目标行之后读取的行数，最大 100。</param>
    /// <param name="workspaceRoot">可选工作区根目录。文件路径不存在时会在该目录下按文件名和路径后缀尝试匹配。</param>
    /// <returns>代码上下文读取结果。</returns>
    [McpServerTool(Name = nameof(GetCodeContext), ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("读取指定文件指定行附近的代码上下文。")]
    public static CodeContextResult GetCodeContext(
        [Description("源码文件路径，可以是绝对路径、相对路径，或 stack trace 中解析出的路径。")] string filePath,
        [Description("目标行号，1-based。")] int line,
        [Description("目标行之前读取的行数，默认 10，最大 100。")] int before = 10,
        [Description("目标行之后读取的行数，默认 10，最大 100。")] int after = 10,
        [Description("可选工作区根目录。文件路径不存在时会在该目录下按文件名和路径后缀尝试匹配。")] string? workspaceRoot = null)
    {
        return CodeContextReader.Read(filePath, line, before, after, workspaceRoot);
    }

    /// <summary>
    /// 将异常文本和代码上下文组装成分析提示词。
    /// </summary>
    /// <param name="stack">完整异常文本或 stack trace。</param>
    /// <param name="code">与异常相关的代码上下文。</param>
    /// <returns>可交给调用方 LLM 的提示词构建结果。</returns>
    [McpServerTool(Name = nameof(AnalyzeException), ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("将异常和代码上下文组装成分析提示词。")]
    public static ExceptionPromptResult AnalyzeException(
        [Description("完整异常文本或 stack trace。")] string stack,
        [Description("与异常相关的代码上下文。")] string code)
    {
        return ExceptionPromptBuilder.Build(stack, code);
    }

    /// <summary>
    /// 端到端完成 stack trace 解析、代码上下文读取和异常分析提示词构建。
    /// </summary>
    /// <param name="stack">完整异常文本或 stack trace。</param>
    /// <param name="workspaceRoot">可选工作区根目录，用于处理 stack trace 路径与当前 checkout 路径不一致的情况。</param>
    /// <param name="before">每个命中行之前读取的行数，最大 100。</param>
    /// <param name="after">每个命中行之后读取的行数，最大 100。</param>
    /// <param name="maxFrames">最多读取几个 stack frame 的代码上下文，最大 10。</param>
    /// <returns>包含解析结果、代码上下文和提示词的端到端结果。</returns>
    [McpServerTool(Name = nameof(ExplainException), ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("端到端执行：解析 stack trace、读取代码上下文、构建异常分析提示词；")]
    public static ExplainExceptionResult ExplainException(
        [Description("完整异常文本或 stack trace。")] string stack,
        [Description("可选工作区根目录。用于读取源码并处理 stack trace 路径与当前 checkout 路径不一致的情况。")] string? workspaceRoot = null,
        [Description("每个命中行之前读取的行数，默认 10，最大 100。")] int before = 10,
        [Description("每个命中行之后读取的行数，默认 10，最大 100。")] int after = 10,
        [Description("最多读取几个 stack frame 的代码上下文，默认 3，最大 10。")] int maxFrames = 3)
    {
        var parseResult = StackTraceParser.Parse(stack);
        if (!parseResult.Success)
        {
            return new ExplainExceptionResult(
                Success: false,
                Parse: parseResult,
                CodeContexts: [],
                CombinedCode: "",
                Prompt: new ExceptionPromptResult(
                    Success: false,
                    Prompt: ExceptionPromptBuilder.BuildPrompt(stack, ""),
                    Error: parseResult.Error ?? "未能从 stack trace 中解析出源码位置。"));
        }

        // 只读取前几个最靠近异常顶部的 frame，避免一次性把过多源码塞进提示词。
        var framesToRead = parseResult.Frames.Take(Math.Clamp(maxFrames, 1, 10)).ToArray();
        var contexts = framesToRead
            .Select(frame => CodeContextReader.Read(frame.FilePath, frame.Line, before, after, workspaceRoot))
            .ToArray();

        var combinedCode = CodeContextFormatter.Combine(framesToRead, contexts);
        var prompt = ExceptionPromptBuilder.Build(stack, combinedCode);

        return new ExplainExceptionResult(
            Success: prompt.Success,
            Parse: parseResult,
            CodeContexts: contexts,
            CombinedCode: combinedCode,
            Prompt: prompt);
    }
}
