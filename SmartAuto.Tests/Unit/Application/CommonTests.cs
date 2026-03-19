using FluentAssertions;
using SmartAuto.Common.Constants;
using SmartAuto.Common.Extensions;
using System.Drawing;
using Xunit;

namespace SmartAuto.Tests.Unit.Application;

/// <summary>
/// Unit tests for <see cref="GeometryExtensions"/>.
/// Validates DPI-aware coordinate conversion and geometry helpers.
/// </summary>
public sealed class GeometryExtensionsTests
{
    // ─── Point ToPhysical ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(100, 200, 1.0,   100,  200)]
    [InlineData(100, 200, 1.25,  125,  250)]
    [InlineData(100, 200, 1.5,   150,  300)]
    [InlineData(100, 200, 2.0,   200,  400)]
    [InlineData(0,   0,   2.0,   0,    0)]
    public void Point_ToPhysical_ScalesCorrectly(int lx, int ly, double scale, int ex, int ey)
    {
        var logical  = new Point(lx, ly);
        var physical = logical.ToPhysical(scale);

        physical.X.Should().Be(ex);
        physical.Y.Should().Be(ey);
    }

    // ─── Point ToLogical ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(100, 200, 1.0,   100, 200)]
    [InlineData(200, 400, 2.0,   100, 200)]
    [InlineData(150, 300, 1.5,   100, 200)]
    public void Point_ToLogical_ScalesCorrectly(int px, int py, double scale, int ex, int ey)
    {
        var physical = new Point(px, py);
        var logical  = physical.ToLogical(scale);

        logical.X.Should().Be(ex);
        logical.Y.Should().Be(ey);
    }

    [Fact]
    public void Point_ToLogical_ZeroScale_ReturnsOriginal()
    {
        var pt = new Point(42, 84);
        pt.ToLogical(0).Should().Be(pt);
    }

    // ─── Rectangle ToPhysical ─────────────────────────────────────────────────

    [Fact]
    public void Rectangle_ToPhysical_ScalesAllComponents()
    {
        var rect    = new Rectangle(10, 20, 100, 50);
        var scaled  = rect.ToPhysical(2.0);

        scaled.X.Should().Be(20);
        scaled.Y.Should().Be(40);
        scaled.Width.Should().Be(200);
        scaled.Height.Should().Be(100);
    }

    // ─── Rectangle ToLogical ─────────────────────────────────────────────────

    [Fact]
    public void Rectangle_ToLogical_DividesAllComponents()
    {
        var rect   = new Rectangle(20, 40, 200, 100);
        var scaled = rect.ToLogical(2.0);

        scaled.X.Should().Be(10);
        scaled.Y.Should().Be(20);
        scaled.Width.Should().Be(100);
        scaled.Height.Should().Be(50);
    }

    [Fact]
    public void Rectangle_ToLogical_ZeroScale_ReturnsOriginal()
    {
        var rect = new Rectangle(10, 20, 300, 400);
        rect.ToLogical(0).Should().Be(rect);
    }

    // ─── Rectangle Center ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, 100, 100, 50, 50)]
    [InlineData(10, 20, 200, 100, 110, 70)]
    [InlineData(5, 5, 0, 0, 5, 5)]
    public void Rectangle_Center_ComputedCorrectly(int x, int y, int w, int h, int cx, int cy)
    {
        var rect   = new Rectangle(x, y, w, h);
        var center = rect.Center();

        center.X.Should().Be(cx);
        center.Y.Should().Be(cy);
    }

    // ─── Rectangle ClampTo ───────────────────────────────────────────────────

    [Fact]
    public void Rectangle_ClampTo_InsideBoundary_ReturnsSameRect()
    {
        var inner    = new Rectangle(10, 10, 50, 50);
        var boundary = new Rectangle(0, 0, 200, 200);

        inner.ClampTo(boundary).Should().Be(inner);
    }

    [Fact]
    public void Rectangle_ClampTo_PartiallyOutside_ClampsCorrectly()
    {
        var rect     = new Rectangle(190, 190, 50, 50);
        var boundary = new Rectangle(0, 0, 200, 200);
        var clamped  = rect.ClampTo(boundary);

        clamped.Right.Should().BeLessOrEqualTo(boundary.Right);
        clamped.Bottom.Should().BeLessOrEqualTo(boundary.Bottom);
    }
}

/// <summary>
/// Unit tests for <see cref="AppConstants"/> to ensure key constants have expected values
/// that match the specification requirements.
/// </summary>
public sealed class AppConstantsTests
{
    [Fact]
    public void AppConstants_MaxHookEventsPerSecond_Is60() =>
        AppConstants.MaxHookEventsPerSecond.Should().Be(60);

    [Fact]
    public void AppConstants_MinInterActionDelayMs_Is100() =>
        AppConstants.MinInterActionDelayMs.Should().Be(100);

    [Fact]
    public void AppConstants_DefaultMaxLoopIterations_Is100() =>
        AppConstants.DefaultMaxLoopIterations.Should().Be(100);

    [Fact]
    public void AppConstants_TemplateMatchThreshold_Is0_95() =>
        AppConstants.TemplateMatchThreshold.Should().BeApproximately(0.95, 0.001);

    [Fact]
    public void AppConstants_LruCacheTtlSeconds_Is5() =>
        AppConstants.LruCacheTtlSeconds.Should().Be(5);

    [Fact]
    public void AppConstants_SensitivePlaceholder_IsMasked() =>
        AppConstants.SensitivePlaceholder.Should().Be("<masked>");

    [Fact]
    public void AppConstants_MutexName_IsNotEmpty() =>
        AppConstants.MutexName.Should().NotBeNullOrWhiteSpace();

    [Fact]
    public void AppConstants_LogFileName_ContainsDotLog() =>
        AppConstants.LogFileName.Should().Contain(".log");
}
