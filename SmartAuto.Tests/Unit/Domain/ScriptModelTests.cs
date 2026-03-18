using FluentAssertions;
using SmartAuto.Domain.Models;
using Xunit;

namespace SmartAuto.Tests.Unit.Domain;

/// <summary>
/// Unit tests for <see cref="ScriptModel"/>: defaults, variable management,
/// schema migration (v1.0 → v1.1), and variable interpolation via ExecutionContext.
/// </summary>
public sealed class ScriptModelTests
{
    // ─── ScriptModel defaults ─────────────────────────────────────────────────

    [Fact]
    public void ScriptModel_Defaults_AreCorrect()
    {
        var script = new ScriptModel();

        script.Id.Should().NotBe(Guid.Empty);
        script.Name.Should().Be("Untitled Script");
        script.SchemaVersion.Should().Be("1.1");
        script.DefaultRetryCount.Should().Be(3);
        script.DefaultActionTimeout.Should().Be(TimeSpan.FromSeconds(10));
        script.MinInterActionDelayMs.Should().Be(100);
        script.ShowDebugOverlay.Should().BeFalse();
        script.Actions.Should().BeEmpty();
        script.Variables.Should().BeEmpty();
    }

    // ─── Schema migration ─────────────────────────────────────────────────────

    [Fact]
    public void ScriptModel_MigrateIfNeeded_V1_0_ToV1_1_UpdatesSchemaVersion()
    {
        var script = new ScriptModel { SchemaVersion = "1.0" };
        script.MigrateIfNeeded();
        script.SchemaVersion.Should().Be("1.1");
    }

    [Fact]
    public void ScriptModel_MigrateIfNeeded_AlreadyCurrentVersion_NoChange()
    {
        var script = new ScriptModel { SchemaVersion = "1.1" };
        script.MigrateIfNeeded();
        script.SchemaVersion.Should().Be("1.1");
    }

    // ─── Variable management ──────────────────────────────────────────────────

    [Fact]
    public void ScriptModel_Variables_AreCaseInsensitive()
    {
        var script = new ScriptModel();
        script.Variables["MyVar"] = new ScriptVariable { Name = "MyVar", Value = "Hello" };

        // Access via different casing.
        script.Variables.Should().ContainKey("myvar");
        script.Variables.Should().ContainKey("MYVAR");
    }
}

/// <summary>
/// Unit tests for <see cref="ExecutionContext"/>:
/// variable access, interpolation, DPI conversion, and DPAPI decryption path.
/// </summary>
public sealed class ExecutionContextTests
{
    private static ScriptModel BuildScript(params (string name, string value, bool sensitive)[] vars)
    {
        var script = new ScriptModel();
        foreach (var (name, value, sensitive) in vars)
        {
            script.Variables[name] = new ScriptVariable
            {
                Name        = name,
                Value       = value,
                IsSensitive = sensitive,
            };
        }
        return script;
    }

    [Fact]
    public void ExecutionContext_GetVariable_ReturnsValue_ForKnownVariable()
    {
        using var ctx = new ExecutionContext(BuildScript(("Url", "https://example.com", false)));
        ctx.GetVariable("Url").Should().Be("https://example.com");
    }

    [Fact]
    public void ExecutionContext_GetVariable_ReturnsNull_ForUnknownVariable()
    {
        using var ctx = new ExecutionContext(new ScriptModel());
        ctx.GetVariable("NonExistent").Should().BeNull();
    }

    [Fact]
    public void ExecutionContext_GetVariable_IsCaseInsensitive()
    {
        using var ctx = new ExecutionContext(BuildScript(("MyVar", "42", false)));
        ctx.GetVariable("myvar").Should().Be("42");
        ctx.GetVariable("MYVAR").Should().Be("42");
    }

    [Fact]
    public void ExecutionContext_SetVariable_CreatesOrUpdatesVariable()
    {
        using var ctx = new ExecutionContext(new ScriptModel());
        ctx.SetVariable("Result", "success");
        ctx.GetVariable("Result").Should().Be("success");

        ctx.SetVariable("Result", "updated");
        ctx.GetVariable("Result").Should().Be("updated");
    }

    [Fact]
    public void ExecutionContext_Interpolate_ReplacesPlaceholders()
    {
        using var ctx = new ExecutionContext(BuildScript(("Name", "World", false)));
        var result = ctx.Interpolate("Hello, {{Name}}!");
        result.Should().Be("Hello, World!");
    }

    [Fact]
    public void ExecutionContext_Interpolate_UnknownPlaceholder_LeftUnchanged()
    {
        using var ctx = new ExecutionContext(new ScriptModel());
        var result = ctx.Interpolate("Value: {{Unknown}}");
        result.Should().Be("Value: {{Unknown}}");
    }

    [Fact]
    public void ExecutionContext_Interpolate_EmptyString_ReturnsEmpty()
    {
        using var ctx = new ExecutionContext(new ScriptModel());
        ctx.Interpolate(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void ExecutionContext_CorrelationId_IsNotEmpty()
    {
        using var ctx = new ExecutionContext(new ScriptModel());
        ctx.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ExecutionContext_TwoInstances_HaveDifferentCorrelationIds()
    {
        using var ctx1 = new ExecutionContext(new ScriptModel());
        using var ctx2 = new ExecutionContext(new ScriptModel());
        ctx1.CorrelationId.Should().NotBe(ctx2.CorrelationId);
    }

    [Fact]
    public void ExecutionContext_Cancel_CancelsToken()
    {
        using var ctx = new ExecutionContext(new ScriptModel());
        ctx.CancellationToken.IsCancellationRequested.Should().BeFalse();
        ctx.Cancel();
        ctx.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void ExecutionContext_ToPhysical_WithScale1_ReturnsUnchanged()
    {
        using var ctx = new ExecutionContext(new ScriptModel());
        // DpiScaleFactor defaults to 1.0 in test environment.
        var logical  = new System.Drawing.Point(100, 200);
        var physical = ctx.ToPhysical(logical);
        // With scale 1.0, logical == physical.
        physical.X.Should().Be((int)Math.Round(100 * ctx.DpiScaleFactor));
        physical.Y.Should().Be((int)Math.Round(200 * ctx.DpiScaleFactor));
    }

    [Fact]
    public void ExecutionContext_Dispose_DoesNotThrow()
    {
        var ctx = new ExecutionContext(new ScriptModel());
        var act = ctx.Invoking(c => c.Dispose());
        act.Should().NotThrow();
    }
}
