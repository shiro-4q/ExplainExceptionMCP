namespace ExplainExceptionMCP;

/// <summary>
/// 构建异常分析提示词的工具类。
/// </summary>
internal static class ExceptionPromptBuilder
{
    /// <summary>
    /// 根据异常文本和代码上下文构建给调用方 LLM 使用的分析提示词。
    /// </summary>
    /// <param name="stack">完整异常文本或 stack trace。</param>
    /// <param name="code">与异常相关的代码上下文。</param>
    /// <returns>包含提示词或错误信息的结果。</returns>
    public static ExceptionPromptResult Build(string stack, string code)
    {
        var prompt = BuildPrompt(stack, code);

        if (string.IsNullOrWhiteSpace(stack))
        {
            return Failure(prompt, "stack 为空，无法构建有效的异常分析提示词。");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return Failure(prompt, "code 为空，无法结合源码构建异常分析提示词。");
        }

        return new ExceptionPromptResult(
            Success: true,
            Prompt: prompt,
            Error: null);
    }

    /// <summary>
    /// 只负责拼接提示词文本，不读取文件、不访问网络、不调用任何模型。
    /// </summary>
    /// <param name="stack">完整异常文本或 stack trace。</param>
    /// <param name="code">与异常相关的代码上下文。</param>
    /// <returns>可直接交给调用方 LLM 的提示词。</returns>
    public static string BuildPrompt(string stack, string code)
    {
        return $"""
            你是一个高级.NET工程师，请分析异常：

            【异常】
            {stack}

            【代码】
            {code}

            请输出：
            1. 根本原因
            2. 触发条件
            3. 影响范围
            4. 修复建议
            要求：
            - 给出具体代码修改建议
            - 如果可能，给出示例代码
            """;
    }

    /// <summary>
    /// 返回提示词构建失败结果，同时保留已拼接出的原始 prompt，方便调用方排查输入。
    /// </summary>
    /// <param name="prompt">已拼接出的提示词。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>提示词构建失败结果。</returns>
    private static ExceptionPromptResult Failure(string prompt, string error)
    {
        return new ExceptionPromptResult(
            Success: false,
            Prompt: prompt,
            Error: error);
    }
}
