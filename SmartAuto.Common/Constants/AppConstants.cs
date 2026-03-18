namespace SmartAuto.Common.Constants;

/// <summary>Application-wide constant values.</summary>
public static class AppConstants
{
    // ─── Application identity ────────────────────────────────────────────────
    public const string AppName              = "SmartAuto";
    public const string AppVersion           = "1.0.0";
    public const string AppDisplayName       = "SmartAuto – Desktop Automation";
    public const string MutexName            = "SmartAuto_SingleInstance_Mutex";

    // ─── Script file format ──────────────────────────────────────────────────
    public const string ScriptFileExtension  = ".smartauto";
    public const string ScriptSchemaVersion  = "1.1";
    public const string ScriptMimeType       = "application/json";

    // ─── Selector engine ─────────────────────────────────────────────────────
    public const int    LruCacheTtlSeconds        = 5;
    public const int    LruCacheMaxEntries         = 64;
    public const int    DefaultMinConfidence        = 50;
    public const int    HighConfidenceThreshold     = 80;
    public const int    UiaMaxSearchDepth           = 20;
    public const double TemplateMatchThreshold      = 0.95;
    public const int    OcrMinConfidence            = 70;

    // ─── Hook / recording ────────────────────────────────────────────────────
    public const int    MaxHookEventsPerSecond      = 60;
    public const int    MinInterActionDelayMs       = 100;

    // ─── Input simulation ───────────────────────────────────────────────────
    public const int    SendInputDefaultDelayMs     = 50;

    // ─── Overlay ─────────────────────────────────────────────────────────────
    public const int    OverlayDefaultDurationMs    = 3000;

    // ─── Logging ─────────────────────────────────────────────────────────────
    public const string LogFileName              = "smartauto-.log";
    public const string SensitivePlaceholder     = "<masked>";

    // ─── Performance budgets ─────────────────────────────────────────────────
    public const double IdleCpuTargetPercent     = 1.5;
    public const int    PeakRamTargetMb          = 250;

    // ─── Global hotkeys (Windows VK codes) ──────────────────────────────────
    // Ctrl+Alt+R = Record, Ctrl+Alt+P = Play
    public const int    HotkeyIdRecord           = 0x9001;
    public const int    HotkeyIdPlay             = 0x9002;
    public const uint   HotkeyModAlt             = 0x0001;
    public const uint   HotkeyModCtrl            = 0x0002;
    public const uint   VkR                      = 0x52;
    public const uint   VkP                      = 0x50;

    // ─── Loop guard ──────────────────────────────────────────────────────────
    public const int    DefaultMaxLoopIterations = 100;
}
