using System.Drawing;
using FluentAssertions;
using SmartAuto.Domain.Actions;
using SmartAuto.Domain.Models;
using Xunit;

namespace SmartAuto.Tests.Unit.Domain;

/// <summary>
/// Unit tests for ActionBase, concrete action records, and ActionFactory.
/// Validates JSON polymorphic serialization / deserialization, property defaults,
/// and exhaustive ActionFactory discriminator matching.
/// </summary>
public sealed class ActionBaseTests
{
    private readonly ActionFactory _factory = new();

    // ─── ActionBase defaults ─────────────────────────────────────────────────

    [Fact]
    public void FindTextAndClickAction_HasSensibleDefaults()
    {
        var action = new FindTextAndClickAction { SearchText = "Submit" };

        action.Id.Should().NotBe(Guid.Empty);
        action.IsEnabled.Should().BeTrue();
        action.OnError.Should().Be(OnErrorBehavior.Stop);
        action.Timeout.Should().BeNull();
        action.RetryCount.Should().BeNull();
        action.MinConfidence.Should().Be(70);
        action.CaseSensitive.Should().BeFalse();
    }

    [Fact]
    public void FindColorAndClickAction_HasSensibleDefaults()
    {
        var action = new FindColorAndClickAction { TargetColor = Color.Red };

        action.Tolerance.H.Should().BeApproximately(15.0, 0.001);
        action.Tolerance.S.Should().BeApproximately(15.0, 0.001);
        action.Tolerance.V.Should().BeApproximately(15.0, 0.001);
    }

    [Fact]
    public void DelayAction_DelayMs_IsRequired()
    {
        var action = new DelayAction { DelayMs = 500 };
        action.DelayMs.Should().Be(500);
    }

    [Fact]
    public void LoopAction_DefaultMaxIterations_Is100()
    {
        var action = new LoopAction { IterationCount = 5, BodyActions = [] };
        action.MaxIterations.Should().Be(100);
    }

    [Fact]
    public void MouseClickAction_DefaultButton_IsLeft()
    {
        var action = new MouseClickAction { LogicalCoords = new Point(100, 200) };
        action.Button.Should().Be(MouseButton.Left);
        action.IsDoubleClick.Should().BeFalse();
    }

    // ─── ActionFactory serialization round-trip ───────────────────────────────

    [Theory]
    [InlineData("FindTextAndClick",  typeof(FindTextAndClickAction))]
    [InlineData("FindColorAndClick", typeof(FindColorAndClickAction))]
    [InlineData("FindImageAndClick", typeof(FindImageAndClickAction))]
    [InlineData("TypeText",          typeof(TypeTextAction))]
    [InlineData("SendInput",         typeof(SendInputAction))]
    [InlineData("Delay",             typeof(DelayAction))]
    [InlineData("MouseClick",        typeof(MouseClickAction))]
    public void ActionFactory_SerializeDeserialize_RoundTrip(string discriminator, Type expectedType)
    {
        ActionBase action = discriminator switch
        {
            "FindTextAndClick"  => new FindTextAndClickAction  { SearchText          = "Test" },
            "FindColorAndClick" => new FindColorAndClickAction { TargetColor         = Color.Blue },
            "FindImageAndClick" => new FindImageAndClickAction { ReferenceImageBase64 = "AAAA" },
            "TypeText"          => new TypeTextAction          { Text                = "Hello" },
            "SendInput"         => new SendInputAction         { VirtualKey          = 0x41 },
            "Delay"             => new DelayAction             { DelayMs             = 100 },
            "MouseClick"        => new MouseClickAction        { LogicalCoords       = new Point(10, 20) },
            _                   => throw new InvalidOperationException($"Unknown discriminator: {discriminator}"),
        };

        var json         = _factory.Serialize(action);
        var deserialized = _factory.Deserialize(json);

        deserialized.Should().BeOfType(expectedType);
        deserialized.Id.Should().Be(action.Id);
    }

    [Fact]
    public void ActionFactory_Deserialize_UnknownType_ThrowsInvalidOperation()
    {
        const string json = """{"$type":"UnknownAction","Id":"00000000-0000-0000-0000-000000000001"}""";
        var act = () => _factory.Deserialize(json);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown action type discriminator*");
    }

    [Fact]
    public void ActionFactory_Deserialize_MissingTypeDiscriminator_ThrowsInvalidOperation()
    {
        const string json = """{"Description":"No type","DelayMs":100}""";
        var act = () => _factory.Deserialize(json);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing the required '$type' discriminator*");
    }

    [Fact]
    public void ActionFactory_DeserializeList_EmptyJson_ReturnsEmptyList()
    {
        var result = _factory.DeserializeList("[]");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ActionBase_RecordWith_CreatesNewInstance_WithUpdatedProperty()
    {
        var original = new FindTextAndClickAction { SearchText = "Submit" };
        var modified = original with { IsEnabled = false };

        modified.IsEnabled.Should().BeFalse();
        original.IsEnabled.Should().BeTrue();
        modified.Id.Should().Be(original.Id);
    }
}
