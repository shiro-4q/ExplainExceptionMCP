using System.Text.Json;

namespace ExplainExceptionMCP;

/// <summary>
/// 表示从 stack trace 中解析出的一帧源码位置。
/// </summary>
/// <param name="Index">命中的顺序，从 0 开始，越小越接近异常顶部。</param>
/// <param name="Method">stack frame 中的方法名；无法识别时为 <c>&lt;unknown&gt;</c>。</param>
/// <param name="FilePath">stack trace 中记录的源码文件路径。</param>
/// <param name="Line">源码行号，使用 1-based 编号。</param>
/// <param name="RawFrame">原始 stack frame 文本，便于调用方回溯解析依据。</param>
public sealed record StackFrameLocation(
    int Index,
    string Method,
    string FilePath,
    int Line,
    string RawFrame);

/// <summary>
/// stack trace 解析结果。
/// </summary>
/// <param name="Success">是否成功解析到至少一个包含源码文件和行号的 frame。</param>
/// <param name="PrimaryFrame">首个命中的 frame，通常最接近根本报错位置。</param>
/// <param name="Frames">所有命中的源码位置列表。</param>
/// <param name="Error">解析失败时的错误说明。</param>
public sealed record ParseStackTraceResult(
    bool Success,
    StackFrameLocation? PrimaryFrame,
    IReadOnlyList<StackFrameLocation> Frames,
    string? Error);

/// <summary>
/// 代码上下文中的单行内容。
/// </summary>
/// <param name="Number">源文件中的实际行号，使用 1-based 编号。</param>
/// <param name="Text">该行源码文本。</param>
/// <param name="IsTarget">是否为 stack trace 指向的目标行。</param>
public sealed record CodeContextLine(
    int Number,
    string Text,
    bool IsTarget);

/// <summary>
/// 指定文件和行号附近的源码上下文读取结果。
/// </summary>
/// <param name="Success">是否成功读取到源码上下文。</param>
/// <param name="RequestedFilePath">调用方或 stack trace 传入的原始文件路径。</param>
/// <param name="ResolvedFilePath">实际读取到的本地文件路径；未找到文件时为空。</param>
/// <param name="RequestedLine">调用方请求的目标行号。</param>
/// <param name="StartLine">返回上下文的起始行号。</param>
/// <param name="EndLine">返回上下文的结束行号。</param>
/// <param name="Code">带行号和目标行标记的源码片段。</param>
/// <param name="TargetLine">目标行源码文本。</param>
/// <param name="Lines">结构化的源码行列表。</param>
/// <param name="Warning">读取成功但发生路径替换、行号越界等情况时的提示。</param>
/// <param name="Error">读取失败时的错误说明。</param>
public sealed record CodeContextResult(
    bool Success,
    string RequestedFilePath,
    string? ResolvedFilePath,
    int RequestedLine,
    int StartLine,
    int EndLine,
    string Code,
    string? TargetLine,
    IReadOnlyList<CodeContextLine> Lines,
    string? Warning,
    string? Error);

/// <summary>
/// 异常分析提示词构建结果。
/// </summary>
/// <param name="Success">是否成功构建出可用提示词。</param>
/// <param name="Prompt">可交给调用方 LLM 的完整提示词。</param>
/// <param name="Error">构建失败时的错误说明。</param>
public sealed record ExceptionPromptResult(
    bool Success,
    string Prompt,
    string? Error);

/// <summary>
/// 端到端异常解释工作流结果。
/// </summary>
/// <param name="Success">解析、读取代码并构建提示词的整体结果。</param>
/// <param name="Parse">stack trace 解析结果。</param>
/// <param name="CodeContexts">每个命中 frame 对应的代码上下文结果。</param>
/// <param name="CombinedCode">合并后的代码上下文文本，已按 stack frame 分组。</param>
/// <param name="Prompt">最终构建出的异常分析提示词。</param>
public sealed record ExplainExceptionResult(
    bool Success,
    ParseStackTraceResult Parse,
    IReadOnlyList<CodeContextResult> CodeContexts,
    string CombinedCode,
    ExceptionPromptResult Prompt);

/// <summary>
/// 项目内统一使用的 JSON 序列化配置。
/// </summary>
internal static class JsonOptions
{
    /// <summary>
    /// 使用 Web 默认命名策略，并开启缩进，方便自检输出阅读。
    /// </summary>
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
