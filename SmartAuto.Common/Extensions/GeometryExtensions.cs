using System.Drawing;

namespace SmartAuto.Common.Extensions;

/// <summary>Extension methods for <see cref="Rectangle"/> and <see cref="Point"/> with DPI handling.</summary>
public static class GeometryExtensions
{
    /// <summary>Scales a <see cref="Point"/> from logical to physical pixels.</summary>
    public static Point ToPhysical(this Point logical, double dpiScale)
        => new((int)Math.Round(logical.X * dpiScale), (int)Math.Round(logical.Y * dpiScale));

    /// <summary>Scales a <see cref="Point"/> from physical to logical pixels.</summary>
    public static Point ToLogical(this Point physical, double dpiScale)
        => dpiScale == 0 ? physical
         : new((int)Math.Round(physical.X / dpiScale), (int)Math.Round(physical.Y / dpiScale));

    /// <summary>Scales a <see cref="Rectangle"/> from logical to physical pixels.</summary>
    public static Rectangle ToPhysical(this Rectangle rect, double dpiScale)
        => new(
            (int)Math.Round(rect.X * dpiScale),
            (int)Math.Round(rect.Y * dpiScale),
            (int)Math.Round(rect.Width * dpiScale),
            (int)Math.Round(rect.Height * dpiScale));

    /// <summary>Scales a <see cref="Rectangle"/> from physical to logical pixels.</summary>
    public static Rectangle ToLogical(this Rectangle rect, double dpiScale)
        => dpiScale == 0 ? rect
         : new(
               (int)Math.Round(rect.X / dpiScale),
               (int)Math.Round(rect.Y / dpiScale),
               (int)Math.Round(rect.Width / dpiScale),
               (int)Math.Round(rect.Height / dpiScale));

    /// <summary>Returns the center <see cref="Point"/> of a <see cref="Rectangle"/>.</summary>
    public static Point Center(this Rectangle rect)
        => new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

    /// <summary>Clamps a rectangle so it lies within the given boundary.</summary>
    public static Rectangle ClampTo(this Rectangle rect, Rectangle boundary)
    {
        int x = Math.Max(boundary.Left, Math.Min(rect.X, boundary.Right));
        int y = Math.Max(boundary.Top,  Math.Min(rect.Y, boundary.Bottom));
        int w = Math.Min(rect.Width,  boundary.Right  - x);
        int h = Math.Min(rect.Height, boundary.Bottom - y);
        return new Rectangle(x, y, Math.Max(0, w), Math.Max(0, h));
    }
}
