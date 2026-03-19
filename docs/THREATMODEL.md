# SmartAuto – STRIDE Threat Model

**Document Version:** 1.0  
**Date:** 2026-03-18  
**Author:** SmartAuto Security Team  
**Classification:** Internal  

---

## 1. Overview

SmartAuto is a Windows desktop RPA tool that installs global keyboard/mouse hooks, captures screen content, and replays automation actions. This document applies the **STRIDE** threat model (Spoofing, Tampering, Repudiation, Information Disclosure, Denial of Service, Elevation of Privilege) to all trust boundaries and data flows.

---

## 2. Architecture & Trust Boundaries

```
┌─────────────────────────────────────────────────────────┐
│  User Session (Current User DACL)                        │
│  ┌─────────────────┐   IPC   ┌────────────────────────┐ │
│  │  SmartAuto.exe  │◄───────►│  Target Application(s) │ │
│  │  (WinUI 3, x64) │         │  (any desktop app)     │ │
│  └────────┬────────┘         └────────────────────────┘ │
│           │ P/Invoke                                      │
│  ┌────────▼──────────────────────────────────────────┐  │
│  │  Windows OS (user32.dll, kernel32.dll, dwmapi.dll)│  │
│  │  WinRT APIs (Windows.Graphics.Capture, Media.Ocr) │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
              │ DPAPI                │ File I/O
      ┌───────▼──────┐      ┌────────▼────────┐
      │ Credential   │      │ Script Files     │
      │ Store (DPAPI │      │ %LOCALAPPDATA%\  │
      │ CurrentUser) │      │ SmartAuto\       │
      └──────────────┘      └─────────────────┘
```

### Trust Boundaries
| Boundary | Description |
|----------|-------------|
| TB-01 | SmartAuto process ↔ Windows kernel (P/Invoke hooks) |
| TB-02 | SmartAuto process ↔ File system (script files, logs) |
| TB-03 | SmartAuto process ↔ Target applications (UIA/SendInput) |
| TB-04 | SmartAuto process ↔ DPAPI (credential encryption) |
| TB-05 | SmartAuto process ↔ Network (optional telemetry, AppInstaller updates) |

---

## 3. STRIDE Analysis

### 3.1 Spoofing

| ID | Asset | Threat | Mitigation | Status |
|----|-------|--------|------------|--------|
| S-01 | Global keyboard hook | Malicious process installs competing hook and intercepts SmartAuto's hook chain | Global hooks are registered with `GetModuleHandle(null)`. SmartAuto validates `CallNextHookEx` always called to maintain chain integrity. Cannot fully prevent adversarial hooks from OS perspective. | Accepted – OS-level risk |
| S-02 | Script files | Attacker replaces a `.smartauto` script with a malicious version | Scripts are loaded from `%LOCALAPPDATA%` (DACL protects against other users). JSON schema validation (JsonSchema.Net) + ActionFactory discriminator whitelist reject unknown action types. | Mitigated |
| S-03 | AppInstaller update feed | MITM replaces the update package | Mandatory EV code-signing; AppInstaller verifies signature before installation. Update channel is HTTPS only. | Mitigated |

### 3.2 Tampering

| ID | Asset | Threat | Mitigation | Status |
|----|-------|--------|------------|--------|
| T-01 | Script files | Attacker modifies a script on disk to inject malicious actions | JSON schema validation on load; ActionFactory whitelist; only registered `[JsonDerivedType]` actions accepted. | Mitigated |
| T-02 | Log files | Attacker tampers with logs to cover tracks | Logs are append-only (Serilog file sink). Structured JSON export can be checksummed externally. | Partially mitigated |
| T-03 | In-memory actions | Code injection into SmartAuto process modifies action list before execution | Standard Windows ASLR/DEP/CFG apply. SmartAuto does not expose a COM interface or named pipe. | Accepted – process-level risk |
| T-04 | Encrypted credentials | Attacker modifies DPAPI-encrypted variable bytes on disk | DPAPI cipher-text includes MAC; any tampering causes decryption failure (returns empty string; caller handles). | Mitigated |

### 3.3 Repudiation

| ID | Asset | Threat | Mitigation | Status |
|----|-------|--------|------------|--------|
| R-01 | Script execution | User denies running a script that caused damage | Structured JSON execution log with correlation ID, timestamps, and action details written per-run. Log rotation keeps 30 days by default. | Mitigated |
| R-02 | Key presses during recording | User denies sensitive input was typed | Raw key values are **never** logged (always `<masked>`). Only key-down/key-up events with VK category are captured. | Mitigated |

### 3.4 Information Disclosure

| ID | Asset | Threat | Mitigation | Status |
|----|-------|--------|------------|--------|
| I-01 | Keystrokes | Global hook records passwords, PINs, banking details | Raw keystroke values are **never** stored or transmitted. `SensitivePlaceholder = "<masked>"` used in all log/script output. `TypeTextAction.IsSensitive` masks text in script editor and logs. | Mitigated |
| I-02 | Screenshots | Captured screen images contain sensitive content | Screenshots are held in memory only during the relevant action. Deleted immediately after use. **No persistent screenshot cache** unless "Debug Mode" is explicitly enabled with user consent (shown in UI). Temp files use `FileOptions.DeleteOnClose`. | Mitigated |
| I-03 | DPAPI-encrypted credentials | Script file contains encrypted credential variables | DPAPI encryption scoped to `CurrentUser`; cipher-text is stored in the script file and is useless on another machine or user account. | Mitigated |
| I-04 | Log files | Logs contain action descriptions that reveal UI structure | Action descriptions are generic ("Click at X,Y"; never include window content). Sensitive `TypeTextAction` descriptions are `<masked>`. | Mitigated |
| I-05 | Telemetry | Usage data transmitted without consent | Telemetry is **opt-in only** (disabled by default). No data collected unless user explicitly enables it in Settings. | Mitigated |

### 3.5 Denial of Service

| ID | Asset | Threat | Mitigation | Status |
|----|-------|--------|------------|--------|
| D-01 | Hook message pump | Flood of mouse/keyboard events overwhelms the hook callback | Rate limiter: max 60 events/second with `ConcurrentQueue` sliding-window counter. Events beyond limit are silently dropped (not blocked). Hook `CallNextHookEx` always called to prevent system degradation. | Mitigated |
| D-02 | Script execution | Infinite loop in `LoopAction` locks workstation | `MaxIterations` guard (configurable, default 100). Engine checks `CancellationToken` on each iteration. | Mitigated |
| D-03 | OCR / OpenCV | Large screenshot causes OOM | `ObjectPool<Mat>` for OpenCV Mat objects; Tesseract engine pooled. Peak RAM target < 250 MB enforced by monitoring and graceful fallback. | Mitigated |
| D-04 | UI thread | Heavy work blocks WinUI 3 UI thread | All recording, playback, and selector work runs on background `Task.Run` threads. `DispatcherQueue.TryEnqueue` used for all UI updates. Zero UI-thread blocking by design. | Mitigated |
| D-05 | Channel overflow | Hook channel full causes event loss | Channel capacity 512 with `BoundedChannelFullMode.DropOldest`. Oldest (least recently needed) events dropped gracefully. | Mitigated |

### 3.6 Elevation of Privilege

| ID | Asset | Threat | Mitigation | Status |
|----|-------|--------|------------|--------|
| E-01 | UAC | SmartAuto runs as standard user; target application requires elevation | `IsUserAnAdmin()` check on startup. If elevated target detected, user is prompted to restart SmartAuto elevated via `ShellExecute` with `runas` verb. SmartAuto never silently self-elevates. | Mitigated |
| E-02 | MSIX capabilities | Over-declared capabilities could be exploited | MSIX manifest declares **minimal capabilities only**: `graphicsCapture` (Windows.Graphics.Capture) and optionally `privateNetworkClientServer` (if update feed is private). No `broadFileSystemAccess`, no `userAccountInformation`. | Mitigated |
| E-03 | Hook injection | Hook installed from non-elevated context could interact with elevated windows | Standard Windows boundary: low-integrity hooks cannot inject into high-integrity windows. This is the expected OS security model. | Accepted – OS-level boundary |
| E-04 | Script execution as admin | Recorded script runs with elevated privileges and damages the system | Elevation state is clearly displayed in the Run & Debug page. User must confirm elevated execution. Scripts do not self-escalate privileges. | Mitigated |

---

## 4. Additional Security Controls

### 4.1 Single-Instance Enforcement
Named `Mutex` (`SmartAuto_SingleInstance_Mutex`) prevents multiple instances from competing for hooks or corrupting shared state.

### 4.2 Input Validation
- All script JSON validated against JsonSchema.Net schema v1.1 on load.
- ActionFactory discriminator whitelist rejects unknown action types.
- DynamicExpresso expression evaluator runs in a sandboxed interpreter (no `Assembly.Load`, no file system access).

### 4.3 Secure Credential Storage
- Sensitive script variables encrypted with `ProtectedData.Protect(DataProtectionScope.CurrentUser)`.
- Decryption failure returns empty string; execution continues with warning logged (no crash).

### 4.4 Code Signing
- Production builds require EV code signing certificate.
- MSIX packages verified by Windows before installation.
- AppInstaller update feed served over HTTPS with TLS 1.2+.

### 4.5 AppLocker Compatibility
- MSIX manifest follows AppLocker publisher rules.
- No DLL side-loading; all native dependencies included in the package.

### 4.6 Privacy by Default
| Control | Default | User Can Change? |
|---------|---------|-----------------|
| Raw keystroke logging | Off (never stored) | No |
| Screenshot persistence | Off | Yes (Debug Mode) |
| Telemetry | Off | Yes (opt-in) |
| Sensitive variable masking | On | No |

---

## 5. Residual Risks

| Risk | Rationale for Acceptance |
|------|--------------------------|
| Competing low-level hooks from other software | OS-level architecture; cannot prevent. Documented in user guide. |
| In-process code injection by malware | Standard process-level protection applies (ASLR, DEP, CFG). |
| Hook blind spot during UAC elevation | Expected Windows security model; user prompted to re-launch elevated. |

---

## 6. Review Cadence

This threat model will be reviewed:
- Before each major release (vX.0)
- After any change to authentication, serialization, hook management, or network communication
- After a security vulnerability report

---

*Generated as part of SmartAuto v1.0 security review. For questions, contact the security team.*
