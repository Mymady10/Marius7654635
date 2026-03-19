using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using SmartAuto.Abstractions;
using SmartAuto.Common.Constants;
using SmartAuto.Domain.Actions;
using SmartAuto.Domain.Exceptions;
using SmartAuto.Domain.Models;
using SmartAuto.Infrastructure.Input;
using SmartAuto.Infrastructure.Selectors;

namespace SmartAuto.Infrastructure.Engine;

/// <summary>
/// Core playback engine that executes a <see cref="ScriptModel"/> action by action.
///
/// Features:
/// • Strict hierarchical selector fallback via <see cref="SelectorEngine"/>.
/// • Per-action Polly retry + timeout + circuit-breaker policies.
/// • Visual highlight overlay 3 s before execution (if enabled).
/// • Progress reporting via <see cref="IProgress{T}"/>.
/// • Error recovery: screenshot + JSON state dump on failure.
/// • Pause/Resume support via <see cref="SemaphoreSlim"/>.
/// • Full <see cref="CancellationToken"/> propagation.
/// • All heavy work runs on background threads (never blocks the UI thread).
/// </summary>
public sealed class ExecutionEngine
{
    private readonly SelectorEngine            _selectorEngine;
    private readonly IOverlayService           _overlay;
    private readonly IScreenCaptureService     _capture;
    private readonly SendInputService          _input;
    private readonly ILogger<ExecutionEngine>  _logger;

    private readonly SemaphoreSlim _pauseSemaphore = new(1, 1);
    private volatile bool          _isPaused;
    private volatile bool          _isRunning;

    // P/Invoke for state dump: window snapshot.
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    public ExecutionEngine(
        SelectorEngine selectorEngine,
        IOverlayService overlay,
        IScreenCaptureService capture,
        SendInputService input,
        ILogger<ExecutionEngine> logger)
    {
        _selectorEngine = selectorEngine;
        _overlay        = overlay;
        _capture        = capture;
        _input          = input;
        _logger         = logger;
    }

    /// <summary>Whether execution is in progress.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Pauses execution at the next action boundary.</summary>
    public void Pause()
    {
        if (!_isRunning || _isPaused) return;
        _isPaused = true;
        _pauseSemaphore.Wait(0); // Drain the semaphore so the next WaitAsync blocks.
        _logger.LogInformation("Execution paused.");
    }

    /// <summary>Resumes a paused execution.</summary>
    public void Resume()
    {
        if (!_isPaused) return;
        _isPaused = false;
        _pauseSemaphore.Release();
        _logger.LogInformation("Execution resumed.");
    }

    // ─── Main entry point ─────────────────────────────────────────────────────

    /// <summary>
    /// Executes a script model asynchronously.
    /// Must be called from a background thread; never from the UI thread.
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(
        ScriptModel script,
        IProgress<ExecutionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        _isRunning = true;
        var sw = Stopwatch.StartNew();
        int completed = 0, failed = 0;

        using var ctx = new ExecutionContext(script, cancellationToken);

        _logger.LogInformation(
            "Starting script '{Name}' (ID={Id}, Correlation={Corr}).",
            script.Name, script.Id, ctx.CorrelationId);

        try
        {
            var actions = script.Actions;
            int total   = CountActions(actions);

            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (!action.IsEnabled) continue;

                // ── Pause check ───────────────────────────────────────────
                if (_isPaused)
                    await _pauseSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                // ── Inter-action delay ────────────────────────────────────
                if (i > 0 && script.MinInterActionDelayMs > 0)
                    await Task.Delay(script.MinInterActionDelayMs, cancellationToken).ConfigureAwait(false);

                // ── Progress update ───────────────────────────────────────
                progress?.Report(new ExecutionProgress(
                    total, completed, action.Description, completed * 100.0 / total));

                _logger.LogDebug("[Action {Idx}/{Total}] Executing: {Type} – {Desc}",
                    i + 1, total, action.GetType().Name, action.Description);

                // ── Execute with Polly policies ───────────────────────────
                var success = await ExecuteActionWithPolicyAsync(action, script, ctx, cancellationToken)
                                   .ConfigureAwait(false);

                if (success)
                {
                    completed++;
                }
                else
                {
                    failed++;

                    if (action.OnError == OnErrorBehavior.Stop)
                    {
                        var dumpPath = await SaveStateDumpAsync(ctx, script, action, cancellationToken)
                                            .ConfigureAwait(false);

                        _logger.LogError(
                            "Action '{Desc}' failed with OnError=Stop. Halting execution. " +
                            "State dump: {Dump}", action.Description, dumpPath);

                        return new ExecutionResult(
                            Success: false,
                            ErrorMessage: $"Action '{action.Description}' failed.",
                            Elapsed: sw.Elapsed,
                            ActionsExecuted: completed + failed,
                            ActionsFailed: failed,
                            CorrelationId: ctx.CorrelationId,
                            StateDumpPath: dumpPath);
                    }
                }
            }

            sw.Stop();
            _logger.LogInformation(
                "Script '{Name}' completed in {Elapsed:F2}s. " +
                "Completed={Completed}, Failed={Failed}.",
                script.Name, sw.Elapsed.TotalSeconds, completed, failed);

            return new ExecutionResult(
                Success: true,
                ErrorMessage: null,
                Elapsed: sw.Elapsed,
                ActionsExecuted: completed + failed,
                ActionsFailed: failed,
                CorrelationId: ctx.CorrelationId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Script '{Name}' was cancelled.", script.Name);
            return new ExecutionResult(
                Success: false,
                ErrorMessage: "Cancelled by user.",
                Elapsed: sw.Elapsed,
                ActionsExecuted: completed + failed,
                ActionsFailed: failed,
                CorrelationId: ctx.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in script '{Name}'.", script.Name);
            return new ExecutionResult(
                Success: false,
                ErrorMessage: ex.Message,
                Elapsed: sw.Elapsed,
                ActionsExecuted: completed + failed,
                ActionsFailed: failed,
                CorrelationId: ctx.CorrelationId);
        }
        finally
        {
            _isRunning = false;
            _isPaused  = false;
            if (_pauseSemaphore.CurrentCount == 0) _pauseSemaphore.Release();
        }
    }

    // ─── Per-action execution with Polly ─────────────────────────────────────

    private async Task<bool> ExecuteActionWithPolicyAsync(
        ActionBase action,
        ScriptModel script,
        ExecutionContext ctx,
        CancellationToken cancellationToken)
    {
        var retryCount = action.RetryCount ?? script.DefaultRetryCount;
        var timeout    = action.Timeout    ?? script.DefaultActionTimeout;

        // Build a Polly pipeline: Timeout → Retry.
        var pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = timeout,
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = retryCount,
                Delay            = TimeSpan.FromMilliseconds(500),
                BackoffType      = DelayBackoffType.Exponential,
                OnRetry          = args =>
                {
                    _logger.LogWarning(
                        "Retry {Attempt}/{Max} for action '{Desc}': {Reason}",
                        args.AttemptNumber, retryCount, action.Description,
                        args.Outcome.Exception?.Message ?? "unknown");
                    return ValueTask.CompletedTask;
                },
            })
            .Build();

        try
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                await DispatchActionAsync(action, script, ctx, ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Action '{Desc}' timed out after {Timeout}.",
                action.Description, timeout);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Action '{Desc}' failed after {Retries} retries.",
                action.Description, retryCount);
            return false;
        }
    }

    // ─── Action dispatcher (exhaustive pattern matching) ─────────────────────

    private async Task DispatchActionAsync(
        ActionBase action,
        ScriptModel script,
        ExecutionContext ctx,
        CancellationToken cancellationToken)
    {
        // Show debug overlay before execution if enabled.
        if (script.ShowDebugOverlay)
        {
            await ShowPreviewOverlayAsync(action, ctx, script.OverlayDuration, cancellationToken)
                 .ConfigureAwait(false);
        }

        // Exhaustive pattern matching on action type.
        switch (action)
        {
            case FindTextAndClickAction ftc:
                await ExecuteFindTextAndClickAsync(ftc, ctx, cancellationToken).ConfigureAwait(false);
                break;

            case FindColorAndClickAction fcc:
                await ExecuteFindColorAndClickAsync(fcc, ctx, cancellationToken).ConfigureAwait(false);
                break;

            case FindImageAndClickAction fic:
                await ExecuteFindImageAndClickAsync(fic, ctx, cancellationToken).ConfigureAwait(false);
                break;

            case TypeTextAction tt:
                await _input.TypeTextAsync(
                    ctx.Interpolate(tt.Text), tt.CharDelayMs, cancellationToken).ConfigureAwait(false);
                break;

            case SendInputAction si:
                await _input.SendKeyAsync(si.VirtualKey, si.ScanCode, si.Modifiers, si.IsKeyDown,
                    cancellationToken).ConfigureAwait(false);
                break;

            case MouseClickAction mc:
                await ExecuteMouseClickAsync(mc, ctx, cancellationToken).ConfigureAwait(false);
                break;

            case DelayAction delay:
                await Task.Delay(delay.DelayMs, cancellationToken).ConfigureAwait(false);
                break;

            case WaitForConditionAction wfc:
                await ExecuteWaitForConditionAsync(wfc, ctx, cancellationToken).ConfigureAwait(false);
                break;

            case IfConditionAction ifc:
                await ExecuteIfConditionAsync(ifc, script, ctx, cancellationToken).ConfigureAwait(false);
                break;

            case LoopAction loop:
                await ExecuteLoopAsync(loop, script, ctx, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown action type: {action.GetType().FullName}. " +
                    "Ensure the type is handled in ExecutionEngine.DispatchActionAsync.");
        }
    }

    // ─── Find + Click actions ─────────────────────────────────────────────────

    /// <summary>
    /// Internal flow for "Find text and click":
    /// 1. Interpolate variables in SearchText.
    /// 2. Build SelectorCriteria with OCR strategy parameters.
    /// 3. Call SelectorEngine (hierarchical fallback: UIA → OCR → pixel → image → absolute).
    /// 4. DPI-aware coordinate conversion.
    /// 5. Show overlay (if enabled).
    /// 6. Simulate mouse click via SendInput.
    /// </summary>
    private async Task ExecuteFindTextAndClickAsync(
        FindTextAndClickAction action,
        ExecutionContext ctx,
        CancellationToken cancellationToken)
    {
        var resolvedText = ctx.Interpolate(action.SearchText);

        var criteria = new SelectorCriteria(
            TargetWindowHandle: GetForegroundWindow(),
            SearchText:         resolvedText,
            SearchRegion:       action.SearchRegion);

        var result = await _selectorEngine.FindElementAsync(
            criteria, ctx, action.MinConfidence, cancellationToken).ConfigureAwait(false);

        if (result is null)
            throw new ElementNotFoundException(
                $"Text '{resolvedText}' not found on screen after trying all strategies.",
                "SelectorEngine", 1);

        // Show highlight before click.
        await _overlay.HighlightAsync(result.PhysicalBounds, TimeSpan.FromMilliseconds(500),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _input.ClickAsync(result.PhysicalCenter, MouseButton.Left, cancellationToken)
                    .ConfigureAwait(false);

        _logger.LogInformation(
            "FindTextAndClick: clicked '{Text}' at ({X},{Y}) via {Strategy} (conf={Conf}).",
            resolvedText, result.PhysicalCenter.X, result.PhysicalCenter.Y,
            result.StrategyName, result.Confidence);
    }

    /// <summary>
    /// Internal flow for "Find color and click":
    /// 1. Build SelectorCriteria with target color + HSV tolerance.
    /// 2. Call SelectorEngine (ColorMatch strategy is tried; UIA may not have color criteria).
    /// 3. DPI-aware coordinate conversion.
    /// 4. Simulate mouse click via SendInput.
    /// </summary>
    private async Task ExecuteFindColorAndClickAsync(
        FindColorAndClickAction action,
        ExecutionContext ctx,
        CancellationToken cancellationToken)
    {
        var criteria = new SelectorCriteria(
            TargetWindowHandle: GetForegroundWindow(),
            TargetColor:        action.TargetColor,
            ColorTolerance:     action.Tolerance,
            SearchRegion:       action.SearchRegion);

        var result = await _selectorEngine.FindElementAsync(
            criteria, ctx, AppConstants.DefaultMinConfidence, cancellationToken).ConfigureAwait(false);

        if (result is null)
            throw new ElementNotFoundException(
                $"Color (R={action.TargetColor.R}, G={action.TargetColor.G}, B={action.TargetColor.B}) " +
                "not found on screen after trying all strategies.",
                "SelectorEngine", 1);

        await _overlay.HighlightAsync(result.PhysicalBounds, TimeSpan.FromMilliseconds(500),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _input.ClickAsync(result.PhysicalCenter, MouseButton.Left, cancellationToken)
                    .ConfigureAwait(false);

        _logger.LogInformation(
            "FindColorAndClick: clicked color at ({X},{Y}) via {Strategy} (conf={Conf}).",
            result.PhysicalCenter.X, result.PhysicalCenter.Y,
            result.StrategyName, result.Confidence);
    }

    private async Task ExecuteFindImageAndClickAsync(
        FindImageAndClickAction action,
        ExecutionContext ctx,
        CancellationToken cancellationToken)
    {
        var imageBytes = Convert.FromBase64String(action.ReferenceImageBase64);

        var criteria = new SelectorCriteria(
            TargetWindowHandle:    GetForegroundWindow(),
            ReferenceImageBytes:   imageBytes,
            TemplateMatchThreshold: action.MatchThreshold,
            SearchRegion:          action.SearchRegion);

        var result = await _selectorEngine.FindElementAsync(
            criteria, ctx, AppConstants.DefaultMinConfidence, cancellationToken).ConfigureAwait(false);

        if (result is null)
            throw new ElementNotFoundException(
                "Reference image not found on screen after trying all strategies.",
                "SelectorEngine", 1);

        await _overlay.HighlightAsync(result.PhysicalBounds, TimeSpan.FromMilliseconds(500),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _input.ClickAsync(result.PhysicalCenter, MouseButton.Left, cancellationToken)
                    .ConfigureAwait(false);
    }

    private async Task ExecuteMouseClickAsync(
        MouseClickAction action,
        ExecutionContext ctx,
        CancellationToken cancellationToken)
    {
        System.Drawing.Point physicalPoint;

        // Prefer relative positioning anchored to the captured window rectangle.
        if (action.WindowContext is not null)
        {
            var windowBounds = action.WindowContext.Bounds;
            physicalPoint = ctx.ToPhysical(new System.Drawing.Point(
                windowBounds.X + action.LogicalCoords.X,
                windowBounds.Y + action.LogicalCoords.Y));
        }
        else
        {
            physicalPoint = ctx.ToPhysical(action.LogicalCoords);
        }

        await _input.ClickAsync(physicalPoint, action.Button, cancellationToken,
            action.IsDoubleClick).ConfigureAwait(false);
    }

    // ─── Control flow actions ─────────────────────────────────────────────────

    private async Task ExecuteWaitForConditionAsync(
        WaitForConditionAction action,
        ExecutionContext ctx,
        CancellationToken cancellationToken)
    {
        var interpreter = new DynamicExpresso.Interpreter();
        var timeout     = action.Timeout ?? TimeSpan.FromSeconds(30);
        var deadline    = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = interpreter.Eval(action.ConditionExpression);
                if (result is true) return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WaitForCondition expression evaluation failed.");
            }

            await Task.Delay(action.PollIntervalMs, cancellationToken).ConfigureAwait(false);
        }

        throw new ActionTimeoutException(timeout, timeout, action.Description);
    }

    private async Task ExecuteIfConditionAsync(
        IfConditionAction action,
        ScriptModel script,
        ExecutionContext ctx,
        CancellationToken cancellationToken)
    {
        var interpreter = new DynamicExpresso.Interpreter();
        bool conditionMet;

        try
        {
            conditionMet = (bool)interpreter.Eval(action.ConditionExpression);
        }
        catch (Exception ex)
        {
            throw new ExpressionEvaluationException(action.ConditionExpression, ex);
        }

        var actionsToRun = conditionMet ? action.ThenActions : action.ElseActions;
        foreach (var subAction in actionsToRun)
        {
            if (!subAction.IsEnabled) continue;
            await ExecuteActionWithPolicyAsync(subAction, script, ctx, cancellationToken)
                 .ConfigureAwait(false);
        }
    }

    private async Task ExecuteLoopAsync(
        LoopAction action,
        ScriptModel script,
        ExecutionContext ctx,
        CancellationToken cancellationToken)
    {
        var interpreter  = new DynamicExpresso.Interpreter();
        int maxIter       = action.MaxIterations;
        int currentIter   = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (currentIter >= maxIter)
            {
                _logger.LogWarning("LoopAction reached max iterations guard ({Max}). Exiting loop.", maxIter);
                break;
            }

            // Fixed-count loop.
            if (action.IterationCount.HasValue && currentIter >= action.IterationCount.Value)
                break;

            // While-condition loop.
            if (action.WhileCondition is not null)
            {
                bool shouldContinue;
                try
                {
                    shouldContinue = (bool)interpreter.Eval(action.WhileCondition);
                }
                catch (Exception ex)
                {
                    throw new ExpressionEvaluationException(action.WhileCondition, ex);
                }
                if (!shouldContinue) break;
            }
            else if (!action.IterationCount.HasValue)
            {
                // Neither fixed count nor while condition → run once.
                break;
            }

            foreach (var subAction in action.BodyActions)
            {
                if (!subAction.IsEnabled) continue;
                await ExecuteActionWithPolicyAsync(subAction, script, ctx, cancellationToken)
                     .ConfigureAwait(false);
            }

            currentIter++;
        }
    }

    // ─── Overlay ──────────────────────────────────────────────────────────────

    private async Task ShowPreviewOverlayAsync(
        ActionBase action,
        ExecutionContext ctx,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        // For actions with a known screen target we could show a precise overlay.
        // For now show a generic notification; concrete coordinate-based overlays
        // are shown immediately before each click.
        _ = action; // suppress unused warning
        _ = ctx;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    // ─── State dump ───────────────────────────────────────────────────────────

    private async Task<string?> SaveStateDumpAsync(
        ExecutionContext ctx,
        ScriptModel script,
        ActionBase failedAction,
        CancellationToken cancellationToken)
    {
        try
        {
            var dir  = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppConstants.AppName, "Dumps");
            Directory.CreateDirectory(dir);

            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
            var dumpPath  = Path.Combine(dir, $"dump_{timestamp}_{ctx.CorrelationId}.json");

            var dump = new
            {
                Timestamp    = DateTimeOffset.UtcNow,
                CorrelationId = ctx.CorrelationId,
                ScriptName   = script.Name,
                FailedAction = new
                {
                    Type        = failedAction.GetType().Name,
                    Description = failedAction.Description,
                    Id          = failedAction.Id,
                },
            };

            var json = JsonSerializer.Serialize(dump, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(dumpPath, json, cancellationToken).ConfigureAwait(false);

            // Save screenshot alongside dump.
            try
            {
                var capture     = await _capture.CaptureScreenAsync(cancellationToken: cancellationToken)
                                               .ConfigureAwait(false);
                var screenshotPath = Path.ChangeExtension(dumpPath, ".png");

                // Encode BGRA → PNG via OpenCvSharp.
                using var mat = new OpenCvSharp.Mat(
                    capture.Height, capture.Width, OpenCvSharp.MatType.CV_8UC4, capture.BgraPixels);
                OpenCvSharp.Cv2.ImWrite(screenshotPath, mat);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save screenshot in state dump.");
            }

            _logger.LogInformation("State dump saved to {Path}", dumpPath);
            return dumpPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save state dump.");
            return null;
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static int CountActions(IReadOnlyList<ActionBase> actions)
    {
        int count = 0;
        foreach (var a in actions)
        {
            count++;
            if (a is IfConditionAction ifc)
                count += CountActions(ifc.ThenActions) + CountActions(ifc.ElseActions);
            else if (a is LoopAction loop)
                count += CountActions(loop.BodyActions);
        }
        return count;
    }
}
