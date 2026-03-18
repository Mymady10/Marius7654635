using DynamicExpresso;
using Microsoft.Extensions.Logging;
using SmartAuto.Application.Interfaces;

namespace SmartAuto.Application.Services;

public sealed class DynamicExpressionEvaluator : IVariableEvaluator
{
    private readonly ILogger<DynamicExpressionEvaluator> _logger;

    public DynamicExpressionEvaluator(ILogger<DynamicExpressionEvaluator> logger)
    {
        _logger = logger;
    }

    public string Evaluate(string expression, IReadOnlyDictionary<string, object?> variables)
    {
        // Simple {{VarName}} interpolation first
        var result = expression;
        foreach (var (key, value) in variables)
            result = result.Replace($"{{{{{key}}}}}", value?.ToString() ?? string.Empty, StringComparison.Ordinal);

        // If it still looks like a plain string (no operators), return it as-is
        if (!result.Contains(' ') && !result.Contains('+') && !result.Contains('('))
            return result;

        try
        {
            var interpreter = CreateInterpreter(variables);
            var val = interpreter.Eval(result);
            return val?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Expression evaluation failed for: {Expression}", expression);
            return result;
        }
    }

    public bool EvaluateBool(string expression, IReadOnlyDictionary<string, object?> variables)
    {
        try
        {
            var interpreter = CreateInterpreter(variables);
            return interpreter.Eval<bool>(expression);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Boolean expression evaluation failed for: {Expression}", expression);
            return false;
        }
    }

    private static Interpreter CreateInterpreter(IReadOnlyDictionary<string, object?> variables)
    {
        var interpreter = new Interpreter();
        foreach (var (key, value) in variables)
            interpreter.SetVariable(key, value);
        return interpreter;
    }
}
