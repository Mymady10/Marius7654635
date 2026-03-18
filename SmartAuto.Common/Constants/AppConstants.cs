namespace SmartAuto.Common.Constants;

public static class AppConstants
{
    public const string AppName = "SmartAuto";
    public const string AppVersion = "1.0.0";
    public const string ScriptSchemaVersion = "1.1";
    public const string ScriptFileExtension = ".smartauto";

    // Hotkeys
    public const int HotkeyRecordId = 1;
    public const int HotkeyPlayId = 2;
    public const int HotkeyRecordModifiers = 0x0002 | 0x0001; // Ctrl+Alt
    public const int HotkeyRecordVk = 0x52; // R
    public const int HotkeyPlayVk = 0x50; // P

    // Rate limiting
    public const int MaxHookEventsPerSecond = 60;
    public const int DefaultActionDelayMs = 100;
    public const int DefaultPlaybackDelayMs = 200;

    // Selector
    public const int SelectorCacheTtlSeconds = 5;
    public const int SelectorCacheCapacity = 100;
    public const double ImageMatchThreshold = 0.95;
    public const int ColorTolerance = 15;

    // Performance
    public const int MaxLoopIterations = 100;
    public const int DefaultTimeoutMs = 30_000;
    public const long PeakRamLimitBytes = 250L * 1024 * 1024; // 250 MB
}
