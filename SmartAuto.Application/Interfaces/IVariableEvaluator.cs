namespace SmartAuto.Application.Interfaces;

public interface IVariableEvaluator
{
    /// <summary>Evaluate an expression/template string returning its string result.</summary>
    string Evaluate(string expression, IReadOnlyDictionary<string, object?> variables);

    /// <summary>Evaluate an expression returning a boolean.</summary>
    bool EvaluateBool(string expression, IReadOnlyDictionary<string, object?> variables);
}
