using ModelContextProtocol.Client;

namespace ExplainExceptionMCP.Tests;

/// <summary>
/// 异常解释工具的测试集合。
/// </summary>
public sealed class ExceptionToolsTests
{
    /// <summary>
    /// 校验公开工具方法能解析 stack trace，并能构建分析提示词。
    /// </summary>
    [Fact]
    public void Tools_Should_ParseStackTrace_And_BuildPrompt()
    {
        const string sampleStack = """
            System.NullReferenceException: Object reference not set to an instance of an object.
               at Demo.Services.OrderService.CalculateTotal() in C:\repo\Demo\Services\OrderService.cs:line 42
               at Demo.Api.OrdersController.Create() in C:\repo\Demo\Api\OrdersController.cs:line 18
            """;

        var parseResult = ExceptionTools.ParseStackTrace(sampleStack);

        Assert.True(parseResult.Success, parseResult.Error);
        Assert.Equal(2, parseResult.Frames.Count);
        Assert.Equal("Demo.Services.OrderService.CalculateTotal()", parseResult.PrimaryFrame?.Method);

        var promptResult = ExceptionTools.AnalyzeException(
            sampleStack,
            """
             39: public decimal CalculateTotal()
             40: {
             41:     var order = _currentOrderProvider.Get();
            >42:     return order.Items.Sum(x => x.Price);
             43: }
            """);

        Assert.True(promptResult.Success, promptResult.Error);
        Assert.Contains("根本原因", promptResult.Prompt);
        Assert.Contains("修复建议", promptResult.Prompt);
    }

    /// <summary>
    /// 启动主项目 MCP server，并校验客户端发现工具。
    /// </summary>
    [Fact]
    public async Task McpServer_Should_Expose_ToolNames()
    {
        var repoRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "ExplainExceptionMCP", "ExplainExceptionMCP.csproj");
        var expectedTools = new[]
        {
            "AnalyzeException",
            "ExplainException",
            "GetCodeContext",
            "ParseStackTrace"
        };

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "explain-exception-mcp-test",
            Command = "dotnet",
            Arguments = ["run", "--project", projectPath, "--no-restore"],
            WorkingDirectory = repoRoot,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["DOTNET_CLI_HOME"] = Path.Combine(repoRoot, ".dotnet_home"),
                ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
            }
        }, loggerFactory: null);

        await using var client = await McpClient.CreateAsync(transport);
        var tools = await client.ListToolsAsync();
        var actualTools = tools.Select(tool => tool.Name).OrderBy(name => name).ToArray();

        Assert.Equal(expectedTools, actualTools);
    }

    /// <summary>
    /// 从测试进程目录向上查找解决方案文件所在目录。
    /// </summary>
    /// <returns>仓库根目录。</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ExplainExceptionMCP.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("未能找到 ExplainExceptionMCP.slnx 所在目录。");
    }
}
