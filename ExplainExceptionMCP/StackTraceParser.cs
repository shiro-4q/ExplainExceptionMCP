using System.Text.RegularExpressions;

namespace ExplainExceptionMCP;

/// <summary>
/// 解析 .NET/C# stack trace 中源码文件和行号的工具类。
/// </summary>
internal static class StackTraceParser
{
    /// <summary>
    /// 匹配标准 stack frame，例如：at Namespace.Type.Method() in C:\A\B.cs:line 42。
    /// </summary>
    private static readonly Regex StackFrameWithMethodRegex = new(
        @"^\s*(?:at|在)\s+(?<method>.*?)(?:\s+in\s+|\s+位置\s+|\s+在\s+)(?<file>.+?\.(?:cs|razor|cshtml|fs|vb)):(?:line|行号|行)\s*(?<line>\d+)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// 匹配只出现源码位置的文本，作为非标准 stack trace 的兜底解析。
    /// </summary>
    private static readonly Regex SourceLocationRegex = new(
        @"(?<file>(?:[A-Za-z]:\\|\\\\|/|\.{1,2}[\\/]|[^\s:]+[\\/]).+?\.(?:cs|razor|cshtml|fs|vb)):(?:line|行号|行)\s*(?<line>\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// 从异常文本中解析所有包含源码文件和行号的 stack frame。
    /// </summary>
    /// <param name="stack">完整异常文本或 stack trace。</param>
    /// <returns>源码位置解析结果。</returns>
    public static ParseStackTraceResult Parse(string stack)
    {
        if (string.IsNullOrWhiteSpace(stack))
        {
            return new ParseStackTraceResult(false, null, [], "stack trace 为空。");
        }

        var frames = new List<StackFrameLocation>();
        var lines = stack.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var frame = TryParseFrame(line, frames.Count);
            if (frame is not null)
            {
                frames.Add(frame);
            }
        }

        if (frames.Count == 0)
        {
            return new ParseStackTraceResult(false, null, [], "未找到包含源码文件和行号的 stack frame。请确认异常包含 PDB 调试信息。");
        }

        return new ParseStackTraceResult(true, frames[0], frames, null);
    }

    /// <summary>
    /// 尝试从单行 stack trace 文本中解析一个源码位置。
    /// </summary>
    /// <param name="line">待解析的单行文本。</param>
    /// <param name="index">该 frame 在解析结果中的顺序。</param>
    /// <returns>解析成功时返回源码位置，否则返回空。</returns>
    private static StackFrameLocation? TryParseFrame(string line, int index)
    {
        var match = StackFrameWithMethodRegex.Match(line);
        if (match.Success)
        {
            return CreateFrame(
                index,
                method: match.Groups["method"].Value,
                filePath: match.Groups["file"].Value,
                lineText: match.Groups["line"].Value,
                rawFrame: line);
        }

        // 某些日志会截断方法名前缀，只保留 "文件:line 42" 形式；这里做兜底匹配。
        match = SourceLocationRegex.Match(line);
        if (!match.Success)
        {
            return null;
        }

        return CreateFrame(
            index,
            method: ExtractMethodPrefix(line, match.Index),
            filePath: match.Groups["file"].Value,
            lineText: match.Groups["line"].Value,
            rawFrame: line);
    }

    /// <summary>
    /// 将正则捕获值转换为结构化 frame，同时过滤非法行号。
    /// </summary>
    /// <param name="index">frame 顺序。</param>
    /// <param name="method">方法名文本。</param>
    /// <param name="filePath">源码文件路径文本。</param>
    /// <param name="lineText">行号文本。</param>
    /// <param name="rawFrame">原始 frame 文本。</param>
    /// <returns>合法源码位置，或空。</returns>
    private static StackFrameLocation? CreateFrame(
        int index,
        string method,
        string filePath,
        string lineText,
        string rawFrame)
    {
        if (!int.TryParse(lineText, out var line) || line <= 0)
        {
            return null;
        }

        return new StackFrameLocation(
            index,
            NormalizeMethod(method),
            NormalizeFilePath(filePath),
            line,
            rawFrame);
    }

    /// <summary>
    /// 标准化方法名，去掉中英文 stack trace 前缀。
    /// </summary>
    /// <param name="method">原始方法名文本。</param>
    /// <returns>标准化后的方法名。</returns>
    private static string NormalizeMethod(string method)
    {
        method = method.Trim();

        if (method.StartsWith("at ", StringComparison.OrdinalIgnoreCase))
        {
            method = method[3..].TrimStart();
        }

        if (method.StartsWith("在 ", StringComparison.Ordinal))
        {
            method = method[2..].TrimStart();
        }

        return string.IsNullOrWhiteSpace(method) ? "<unknown>" : method;
    }

    /// <summary>
    /// 去掉路径两侧空白和引号，保留原始路径分隔符。
    /// </summary>
    /// <param name="filePath">原始路径文本。</param>
    /// <returns>标准化后的路径文本。</returns>
    private static string NormalizeFilePath(string filePath)
    {
        return filePath.Trim().Trim('"', '\'');
    }

    /// <summary>
    /// 从非标准行中尝试截取源码位置前面的方法名部分。
    /// </summary>
    /// <param name="line">原始 stack trace 行。</param>
    /// <param name="sourceLocationIndex">源码位置在原始行中的起始索引。</param>
    /// <returns>推断出的方法名前缀，无法推断时返回源码位置前的全部文本。</returns>
    private static string ExtractMethodPrefix(string line, int sourceLocationIndex)
    {
        var prefix = line[..sourceLocationIndex].Trim();

        foreach (var marker in new[] { " in ", " 位置 ", " 在 " })
        {
            var markerIndex = prefix.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                return prefix[..markerIndex];
            }
        }

        return prefix;
    }
}
