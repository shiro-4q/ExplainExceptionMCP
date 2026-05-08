using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace ExplainExceptionMCP;

/// <summary>
/// MCP server 应用入口。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 配置并启动基于 stdio transport 的 MCP server。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>异步启动任务。</returns>
    public static async Task Main(string[] args)
    {
        // 使用 Microsoft.Extensions.Hosting 管理服务生命周期，和官方 MCP C# SDK 示例保持一致。
        var builder = Host.CreateApplicationBuilder(args);

        // stdio MCP 的 stdout 必须只承载 JSON-RPC 消息；默认控制台日志可能污染协议输出。
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        builder.Services
            .AddMcpServer(options =>// 注册MCP服务，并配置服务器信息和使用的工具。
            {
                options.ServerInfo = new Implementation
                {
                    Name = "explain-exception-mcp",
                    Title = "Explain Exception MCP",
                    Version = "0.1.0",
                    Description = "从 C#/.NET 异常中定位源码上下文，并构建可交给调用方 LLM 的分析提示词。"
                };
                options.ServerInstructions = """
                    使用 ParseStackTrace 从 .NET/C# stack trace 中解析源码文件、行号和方法名。
                    使用 GetCodeContext 读取指定源码位置附近的代码上下文。
                    使用 AnalyzeException 将异常和代码上下文组装成分析提示词。
                    使用 ExplainException 执行解析、读取代码和构建提示词的端到端流程。
                    """;// 服务器说明里可以告诉调用方 LLM 这个 MCP 的功能和使用的工具，帮助它更好地利用这些工具。
            })
            .WithStdioServerTransport()// 配置基于 stdio 的 MCP 服务器传输。
            .WithToolsFromAssembly();// 自动注册当前程序集中的所有 MCP 工具，会自动扫描[McpServerToolType]下面所有标记了[McpServerTool] 的方法。

        await builder.Build().RunAsync();// 构建Host，启动应用
    }
}
