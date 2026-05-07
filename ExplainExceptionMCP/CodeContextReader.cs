using System.Text;

namespace ExplainExceptionMCP;

/// <summary>
/// 读取源码文件指定行附近上下文的工具类。
/// </summary>
internal static class CodeContextReader
{
    /// <summary>
    /// 搜索同名文件时跳过的目录，避免在构建产物或依赖目录中误命中。
    /// </summary>
    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        "bin",
        "obj",
        "node_modules",
        "packages"
    };

    /// <summary>
    /// 读取指定文件指定行附近的源码上下文。
    /// </summary>
    /// <param name="filePath">源码文件路径，可以是绝对路径、相对路径，或 stack trace 中解析出的路径。</param>
    /// <param name="line">目标行号，使用 1-based 编号。</param>
    /// <param name="before">目标行之前读取的行数，最大 100。</param>
    /// <param name="after">目标行之后读取的行数，最大 100。</param>
    /// <param name="workspaceRoot">可选工作区根目录，用于路径兜底匹配。</param>
    /// <returns>代码上下文读取结果。</returns>
    public static CodeContextResult Read(string filePath, int line, int before, int after, string? workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Error(filePath, line, "filePath 为空。");
        }

        if (line <= 0)
        {
            return Error(filePath, line, "line 必须大于 0。");
        }

        before = Math.Clamp(before, 0, 100);
        after = Math.Clamp(after, 0, 100);

        var resolution = ResolveFilePath(filePath, workspaceRoot);
        if (resolution.ResolvedPath is null)
        {
            return new CodeContextResult(
                Success: false,
                RequestedFilePath: filePath,
                ResolvedFilePath: null,
                RequestedLine: line,
                StartLine: 0,
                EndLine: 0,
                Code: "",
                TargetLine: null,
                Lines: [],
                Warning: resolution.Warning,
                Error: resolution.Error);
        }

        string[] sourceLines;
        try
        {
            sourceLines = File.ReadAllLines(resolution.ResolvedPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new CodeContextResult(
                Success: false,
                RequestedFilePath: filePath,
                ResolvedFilePath: resolution.ResolvedPath,
                RequestedLine: line,
                StartLine: 0,
                EndLine: 0,
                Code: "",
                TargetLine: null,
                Lines: [],
                Warning: resolution.Warning,
                Error: $"读取文件失败：{ex.Message}");
        }

        if (sourceLines.Length == 0)
        {
            return new CodeContextResult(
                Success: false,
                RequestedFilePath: filePath,
                ResolvedFilePath: resolution.ResolvedPath,
                RequestedLine: line,
                StartLine: 0,
                EndLine: 0,
                Code: "",
                TargetLine: null,
                Lines: [],
                Warning: resolution.Warning,
                Error: "文件为空。");
        }

        var warning = resolution.Warning;
        if (line > sourceLines.Length)
        {
            warning = AppendWarning(warning, $"请求行号 {line} 超过文件总行数 {sourceLines.Length}，上下文已定位到文件末尾。");
        }

        var clampedLine = Math.Clamp(line, 1, sourceLines.Length);
        var startLine = Math.Max(1, clampedLine - before);
        var endLine = Math.Min(sourceLines.Length, clampedLine + after);
        var contextLines = new List<CodeContextLine>(endLine - startLine + 1);
        var builder = new StringBuilder();
        var width = endLine.ToString().Length;

        for (var number = startLine; number <= endLine; number++)
        {
            var text = sourceLines[number - 1];
            var isTarget = number == clampedLine;
            contextLines.Add(new CodeContextLine(number, text, isTarget));

            // 使用 ">" 标记目标行，让 LLM 在纯文本代码块中也能快速定位报错行。
            var marker = isTarget ? ">" : " ";
            builder.Append(marker)
                .Append(' ')
                .Append(number.ToString().PadLeft(width))
                .Append(": ")
                .AppendLine(text);
        }

        return new CodeContextResult(
            Success: true,
            RequestedFilePath: filePath,
            ResolvedFilePath: resolution.ResolvedPath,
            RequestedLine: line,
            StartLine: startLine,
            EndLine: endLine,
            Code: builder.ToString().TrimEnd(),
            TargetLine: sourceLines[clampedLine - 1],
            Lines: contextLines,
            Warning: warning,
            Error: null);
    }

    /// <summary>
    /// 构造统一的代码上下文读取失败结果。
    /// </summary>
    /// <param name="filePath">请求的文件路径。</param>
    /// <param name="line">请求的目标行号。</param>
    /// <param name="message">错误说明。</param>
    /// <returns>读取失败结果。</returns>
    private static CodeContextResult Error(string filePath, int line, string message)
    {
        return new CodeContextResult(
            Success: false,
            RequestedFilePath: filePath,
            ResolvedFilePath: null,
            RequestedLine: line,
            StartLine: 0,
            EndLine: 0,
            Code: "",
            TargetLine: null,
            Lines: [],
            Warning: null,
            Error: message);
    }

    /// <summary>
    /// 将 stack trace 中的路径解析为当前机器上可读取的真实文件路径。
    /// </summary>
    /// <param name="requestedPath">调用方传入的原始路径。</param>
    /// <param name="workspaceRoot">可选工作区根目录。</param>
    /// <returns>路径解析结果。</returns>
    private static PathResolution ResolveFilePath(string requestedPath, string? workspaceRoot)
    {
        var expandedPath = Environment.ExpandEnvironmentVariables(requestedPath.Trim().Trim('"', '\''));

        if (File.Exists(expandedPath))
        {
            return new PathResolution(Path.GetFullPath(expandedPath), null, null);
        }

        var searchRoot = ResolveSearchRoot(workspaceRoot);
        if (!Path.IsPathFullyQualified(expandedPath))
        {
            var rootedCandidate = Path.GetFullPath(Path.Combine(searchRoot, expandedPath));
            if (File.Exists(rootedCandidate))
            {
                return new PathResolution(rootedCandidate, null, null);
            }
        }

        var fileName = Path.GetFileName(expandedPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new PathResolution(null, null, $"文件不存在：{requestedPath}");
        }

        // stack trace 通常来自另一台机器或容器，绝对路径未必存在；
        // 这里先找同名文件，再用路径后缀分数选择最像原始路径的候选。
        var candidates = FindCandidates(searchRoot, fileName)
            .Select(candidate => new
            {
                Path = candidate,
                Score = GetTrailingPathScore(expandedPath, candidate)
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Path.Length)
            .Take(5)
            .ToArray();

        if (candidates.Length == 0)
        {
            return new PathResolution(null, null, $"文件不存在，且未能在工作区 '{searchRoot}' 下找到同名文件：{requestedPath}");
        }

        var best = candidates[0];
        var warning = best.Score > 1
            ? $"原始路径不存在，已在工作区中按路径后缀匹配到：{best.Path}"
            : $"原始路径不存在，已按文件名匹配到：{best.Path}";

        if (candidates.Length > 1 && candidates[1].Score == best.Score)
        {
            warning = AppendWarning(warning, "存在多个相同分数的候选文件，当前选择路径最短的候选。");
        }

        return new PathResolution(best.Path, warning, null);
    }

    /// <summary>
    /// 解析搜索根目录；未传入或传入路径无效时使用当前工作目录。
    /// </summary>
    /// <param name="workspaceRoot">调用方传入的工作区根目录。</param>
    /// <returns>实际用于搜索的根目录。</returns>
    private static string ResolveSearchRoot(string? workspaceRoot)
    {
        var root = string.IsNullOrWhiteSpace(workspaceRoot)
            ? Directory.GetCurrentDirectory()
            : Environment.ExpandEnvironmentVariables(workspaceRoot.Trim().Trim('"', '\''));

        return Directory.Exists(root)
            ? Path.GetFullPath(root)
            : Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// 在工作区中查找同名候选文件。
    /// </summary>
    /// <param name="searchRoot">搜索根目录。</param>
    /// <param name="fileName">目标文件名。</param>
    /// <returns>候选文件路径集合。</returns>
    private static IEnumerable<string> FindCandidates(string searchRoot, string fileName)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive
        };

        try
        {
            return Directory.EnumerateFiles(searchRoot, fileName, options)
                .Where(path => !HasIgnoredDirectory(path))
                .Select(Path.GetFullPath)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return [];
        }
    }

    /// <summary>
    /// 判断路径是否位于应被忽略的构建、依赖或版本控制目录中。
    /// </summary>
    /// <param name="path">候选文件路径。</param>
    /// <returns>需要忽略时返回 true。</returns>
    private static bool HasIgnoredDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is null)
        {
            return false;
        }

        var parts = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => IgnoredDirectoryNames.Contains(part));
    }

    /// <summary>
    /// 计算请求路径和候选路径从末尾开始连续相同的路径片段数量。
    /// </summary>
    /// <param name="requestedPath">stack trace 中的原始路径。</param>
    /// <param name="candidatePath">工作区中找到的候选路径。</param>
    /// <returns>路径后缀匹配分数，分数越高越可信。</returns>
    private static int GetTrailingPathScore(string requestedPath, string candidatePath)
    {
        var requested = SplitPath(requestedPath);
        var candidate = SplitPath(candidatePath);
        var score = 0;

        for (int r = requested.Length - 1, c = candidate.Length - 1; r >= 0 && c >= 0; r--, c--)
        {
            if (!string.Equals(requested[r], candidate[c], StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            score++;
        }

        return score;
    }

    /// <summary>
    /// 将路径按目录分隔符拆分成片段，兼容 Windows 和 Unix 风格分隔符。
    /// </summary>
    /// <param name="path">待拆分的路径。</param>
    /// <returns>路径片段数组。</returns>
    private static string[] SplitPath(string path)
    {
        return path
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// 追加警告文本。
    /// </summary>
    /// <param name="current">已有警告。</param>
    /// <param name="next">需要追加的新警告。</param>
    /// <returns>合并后的警告。</returns>
    private static string AppendWarning(string? current, string next)
    {
        return string.IsNullOrWhiteSpace(current) ? next : $"{current} {next}";
    }

    /// <summary>
    /// 文件路径解析结果。
    /// </summary>
    /// <param name="ResolvedPath">实际可读取的本地文件路径。</param>
    /// <param name="Warning">解析过程中的非致命警告。</param>
    /// <param name="Error">解析失败时的错误说明。</param>
    private sealed record PathResolution(string? ResolvedPath, string? Warning, string? Error);
}
