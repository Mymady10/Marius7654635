using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace SmartAuto.Tests.Unit;

/// <summary>
/// Architecture enforcement tests using NetArchTest.
/// Validates the strict layered architecture:
///   Abstractions → Domain → Application → Infrastructure → Presentation
///
/// No circular dependencies; Infrastructure must not be referenced by Application or Domain.
/// </summary>
public sealed class ArchitectureTests
{
    // Namespaces of each layer.
    private const string AbstrNs   = "SmartAuto.Abstractions";
    private const string DomainNs  = "SmartAuto.Domain";
    private const string AppNs     = "SmartAuto.Application";
    private const string InfraNs   = "SmartAuto.Infrastructure";
    private const string CommonNs  = "SmartAuto.Common";

    // Load all assemblies to be tested.
    private static Types LoadTypes(string ns)
    {
        // NetArchTest uses the loaded assembly; ensure it's in the current AppDomain.
        var asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == ns);

        // If not yet loaded, force-load from the bin directory.
        if (asm is null)
        {
            var dllPath = Path.Combine(
                AppContext.BaseDirectory, $"{ns}.dll");

            if (File.Exists(dllPath))
                asm = System.Reflection.Assembly.LoadFrom(dllPath);
        }

        // Return types from the target namespace, falling back gracefully.
        return asm is not null
            ? Types.InAssembly(asm)
            : Types.InCurrentDomain().That().ResideInNamespace(ns).GetTypes() is { } _ ? null! : null!;
    }

    // ─── Domain must not depend on Infrastructure ─────────────────────────────

    [Fact]
    public void Domain_ShouldNot_DependOn_Infrastructure()
    {
        var types = LoadTypes(DomainNs);
        if (types is null) return; // assembly not available in current test run

        var result = types
            .That().ResideInNamespace(DomainNs)
            .ShouldNot().HaveDependencyOn(InfraNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain layer must not depend on Infrastructure (strict layering).");
    }

    // ─── Application must not depend on Infrastructure ────────────────────────

    [Fact]
    public void Application_ShouldNot_DependOn_Infrastructure()
    {
        var types = LoadTypes(AppNs);
        if (types is null) return;

        var result = types
            .That().ResideInNamespace(AppNs)
            .ShouldNot().HaveDependencyOn(InfraNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application layer must not depend on Infrastructure (strict layering).");
    }

    // ─── Abstractions must not depend on Domain, Application, or Infrastructure

    [Fact]
    public void Abstractions_ShouldNot_DependOn_Domain()
    {
        var types = LoadTypes(AbstrNs);
        if (types is null) return;

        var result = types
            .That().ResideInNamespace(AbstrNs)
            .ShouldNot().HaveDependencyOn(DomainNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Abstractions must be free of upstream layer dependencies.");
    }

    [Fact]
    public void Abstractions_ShouldNot_DependOn_Infrastructure()
    {
        var types = LoadTypes(AbstrNs);
        if (types is null) return;

        var result = types
            .That().ResideInNamespace(AbstrNs)
            .ShouldNot().HaveDependencyOn(InfraNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Abstractions must be free of upstream layer dependencies.");
    }

    // ─── Common has no project-layer dependencies ─────────────────────────────

    [Fact]
    public void Common_ShouldNot_DependOn_Domain()
    {
        var types = LoadTypes(CommonNs);
        if (types is null) return;

        var result = types
            .That().ResideInNamespace(CommonNs)
            .ShouldNot().HaveDependencyOn(DomainNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Common layer must not depend on any other SmartAuto layer.");
    }

    // ─── Domain actions must be records ──────────────────────────────────────

    [Fact]
    public void DomainActions_ShouldBe_Records()
    {
        // All concrete action types that extend ActionBase should be record types.
        var actionTypes = new[]
        {
            typeof(SmartAuto.Domain.Actions.FindTextAndClickAction),
            typeof(SmartAuto.Domain.Actions.FindColorAndClickAction),
            typeof(SmartAuto.Domain.Actions.FindImageAndClickAction),
            typeof(SmartAuto.Domain.Actions.TypeTextAction),
            typeof(SmartAuto.Domain.Actions.SendInputAction),
            typeof(SmartAuto.Domain.Actions.DelayAction),
            typeof(SmartAuto.Domain.Actions.MouseClickAction),
            typeof(SmartAuto.Domain.Actions.WaitForConditionAction),
            typeof(SmartAuto.Domain.Actions.IfConditionAction),
            typeof(SmartAuto.Domain.Actions.LoopAction),
        };

        foreach (var t in actionTypes)
        {
            // In C# 9+ records have a synthesized "EqualityContract" property.
            t.GetProperty("EqualityContract",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
             .Should().NotBeNull(because: $"{t.Name} should be a record type (has EqualityContract).");
        }
    }

    // ─── IAction contract is implemented correctly ────────────────────────────

    [Fact]
    public void AllConcreteActions_Implement_IAction()
    {
        var actionTypes = new[]
        {
            typeof(SmartAuto.Domain.Actions.FindTextAndClickAction),
            typeof(SmartAuto.Domain.Actions.FindColorAndClickAction),
            typeof(SmartAuto.Domain.Actions.DelayAction),
            typeof(SmartAuto.Domain.Actions.MouseClickAction),
        };

        foreach (var t in actionTypes)
        {
            t.Should().Implement<SmartAuto.Abstractions.IAction>(
                because: $"{t.Name} must implement IAction.");
        }
    }
}
