# explain-exception-mcp

一个用于解释 C#/.NET 异常的 stdio MCP server。

它只做三件事：

1. 从异常文本或 stack trace 中解析源码文件、行号、方法名。
2. 在本地工作区读取对应源码上下文。
3. 构建异常分析提示词，交给调用 MCP 的客户端 LLM 继续分析。

## Tools

- `ParseStackTrace`: 从异常文本或 stack trace 解析源码文件、行号、方法名。
- `GetCodeContext`: 读取目标文件目标行附近的代码上下文。
- `AnalyzeException`: 将异常和代码上下文组装成分析提示词；不调用 LLM。
- `ExplainException`: 端到端执行解析、读取代码上下文、构建提示词。

## 运行

```powershell
dotnet run --project .\ExplainExceptionMCP\ExplainExceptionMCP.csproj
```

运行测试项目：

```powershell
dotnet test .\ExplainExceptionMCP.Tests\ExplainExceptionMCP.Tests.csproj
```

## MCP client 配置示例

```json
{
  "mcpServers": {
    "explain-exception-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/personal/projects/.net/ExplainExceptionMCP/ExplainExceptionMCP/ExplainExceptionMCP.csproj"
      ]
    }
  }
}
```
