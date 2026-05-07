# explain-exception-mcp

A stdio MCP server for analyzing C#/.NET exceptions.

It helps MCP clients:

1. Parse source file paths, line numbers, and method names from exception text or .NET stack traces.
2. Read the matching source code context from a local workspace.
3. Build an exception analysis prompt that the client-side LLM can use to explain the likely root cause and suggest fixes.

## Tools

- `ParseStackTrace`: Parses source file paths, line numbers, and method names from exception text or a .NET stack trace.
- `GetCodeContext`: Reads code around a target line in a source file.
- `AnalyzeException`: Combines an exception and code context into an analysis prompt. It does not call an LLM directly.
- `ExplainException`: Runs the end-to-end flow: parse the stack trace, read code context, and build the analysis prompt.

## Installation

Install from NuGet as a .NET global tool:

```powershell
dotnet tool install -g ExplainExceptionMCP
```

Update an existing installation:

```powershell
dotnet tool update -g ExplainExceptionMCP
```

Verify that the command is available:

```powershell
explain-exception-mcp
```

This is a stdio MCP server, so it waits for MCP JSON-RPC messages on standard input. It is normal for the process to keep running without printing an HTTP URL.

## MCP Client Configuration

After installing the NuGet global tool, configure your MCP client to run the tool command directly.

```json
{
  "mcpServers": {
    "explain-exception-mcp": {
      "command": "explain-exception-mcp"
    }
  }
}
```

If your MCP client cannot find .NET global tools from `PATH`, use the full path to the generated shim.

Windows example:

```json
{
  "mcpServers": {
    "explain-exception-mcp": {
      "command": "C:\\Users\\YOUR_USER_NAME\\.dotnet\\tools\\explain-exception-mcp.exe"
    }
  }
}
```

macOS/Linux example:

```json
{
  "mcpServers": {
    "explain-exception-mcp": {
      "command": "/home/YOUR_USER_NAME/.dotnet/tools/explain-exception-mcp"
    }
  }
}
```

## Usage Example

Ask your MCP client to call `ExplainException` with a .NET stack trace and, when useful, the local workspace root:

```text
Use explain-exception-mcp to analyze this exception.
workspaceRoot: C:\path\to\your\repo

System.NullReferenceException: Object reference not set to an instance of an object.
   at Demo.Services.OrderService.CalculateTotal() in C:\path\to\your\repo\Services\OrderService.cs:line 42
```

## Local Development

Run the server from source:

```powershell
dotnet run --project .\ExplainExceptionMCP\ExplainExceptionMCP.csproj
```

Run tests:

```powershell
dotnet test .\ExplainExceptionMCP.Tests\ExplainExceptionMCP.Tests.csproj
```
