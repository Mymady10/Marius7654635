using FluentAssertions;
using SmartAuto.Domain.Exceptions;
using Xunit;

namespace SmartAuto.Tests.Unit.Domain;

/// <summary>
/// Unit tests for SmartAuto exception hierarchy.
/// Validates message formatting and property population.
/// </summary>
public sealed class SmartAutoExceptionTests
{
    [Fact]
    public void ElementNotFoundException_ContainsStrategyAndAttemptCount()
    {
        var ex = new ElementNotFoundException("Could not find element", "UIAutomation", 3);

        ex.Message.Should().Be("Could not find element");
        ex.StrategyAttempted.Should().Be("UIAutomation");
        ex.AttemptsCount.Should().Be(3);
    }

    [Fact]
    public void LowConfidenceException_FormatsMessageCorrectly()
    {
        var ex = new LowConfidenceException(actual: 45, required: 70, "OCR scan result");

        ex.ActualConfidence.Should().Be(45);
        ex.RequiredConfidence.Should().Be(70);
        ex.Message.Should().Contain("45");
        ex.Message.Should().Contain("70");
    }

    [Fact]
    public void ActionTimeoutException_FormatsElapsedAndLimit()
    {
        var ex = new ActionTimeoutException(
            elapsed: TimeSpan.FromSeconds(12.5),
            limit:   TimeSpan.FromSeconds(10.0),
            actionDescription: "Click OK button");

        ex.Elapsed.Should().Be(TimeSpan.FromSeconds(12.5));
        ex.Limit.Should().Be(TimeSpan.FromSeconds(10.0));
        ex.Message.Should().Contain("Click OK button");
        ex.Message.Should().Contain("12.5");
    }

    [Fact]
    public void ExpressionEvaluationException_ContainsExpression()
    {
        var inner = new InvalidOperationException("Bad expression");
        var ex    = new ExpressionEvaluationException("{{Counter}} + 1", inner);

        ex.Expression.Should().Be("{{Counter}} + 1");
        ex.InnerException.Should().BeSameAs(inner);
        ex.Message.Should().Contain("{{Counter}} + 1");
    }

    [Fact]
    public void ScriptMigrationException_ContainsSchemaVersion()
    {
        var ex = new ScriptMigrationException("0.9");
        ex.SchemaVersion.Should().Be("0.9");
        ex.Message.Should().Contain("0.9");
    }

    [Fact]
    public void ExecutionCancelledException_DefaultMessage_IsNotNull()
    {
        var ex = new ExecutionCancelledException();
        ex.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ExecutionCancelledException_CustomMessage_IsPreserved()
    {
        var ex = new ExecutionCancelledException("User pressed Ctrl+Alt+P");
        ex.Message.Should().Be("User pressed Ctrl+Alt+P");
    }

    [Fact]
    public void AllExceptions_InheritFromSmartAutoException()
    {
        new ElementNotFoundException("x", "y", 1).Should().BeAssignableTo<SmartAutoException>();
        new LowConfidenceException(0, 50).Should().BeAssignableTo<SmartAutoException>();
        new ActionTimeoutException(TimeSpan.Zero, TimeSpan.Zero, "a").Should().BeAssignableTo<SmartAutoException>();
        new ExecutionCancelledException().Should().BeAssignableTo<SmartAutoException>();
        new ExpressionEvaluationException("x").Should().BeAssignableTo<SmartAutoException>();
        new ScriptMigrationException("1.0").Should().BeAssignableTo<SmartAutoException>();
    }
}
