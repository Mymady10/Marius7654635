namespace SmartAuto.Common.Utilities;

/// <summary>Converts between logical (DIP) and physical pixels.</summary>
public static class DpiHelper
{
    public const double DefaultDpi = 96.0;

    public static int ToPhysicalX(double logicalX, double dpiX) =>
        (int)Math.Round(logicalX * dpiX / DefaultDpi);

    public static int ToPhysicalY(double logicalY, double dpiY) =>
        (int)Math.Round(logicalY * dpiY / DefaultDpi);

    public static double ToLogicalX(int physicalX, double dpiX) =>
        physicalX * DefaultDpi / dpiX;

    public static double ToLogicalY(int physicalY, double dpiY) =>
        physicalY * DefaultDpi / dpiY;

    public static (int x, int y) ToPhysical(double logicalX, double logicalY, double dpiX, double dpiY) =>
        (ToPhysicalX(logicalX, dpiX), ToPhysicalY(logicalY, dpiY));
}
