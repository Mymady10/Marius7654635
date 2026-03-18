using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using SmartAuto.Abstractions.Interfaces;
using SmartAuto.Abstractions.Models;
using SmartAuto.Application.Interfaces;
using SmartAuto.Common.Constants;
using SmartAuto.Common.Exceptions;
using SmartAuto.Domain.Actions;
using SmartAuto.Domain.Factories;
using SmartAuto.Domain.Models;
using ExecutionContext = SmartAuto.Domain.Models.ExecutionContext;

namespace SmartAuto.Application.Services;

/// <summary>
/// Core execution engine that runs a ScriptModel action-by-action.
/// All execution happens on background threads. Zero UI-thread blocking.
/// </summary>
public sealed class ExecutionEngine : IPlaybackEngine, IAsyncDisposable
{
    private readonly IEnumerable<ISelectorStrategy> _strategies;
    private readonly IInputSimulator _input;
    private readonly ILogger<ExecutionEngine> _logger;
    private readonly IVariableEvaluator _evaluator;

    private ExecutionContext? _activeContext;
    private bool _disposed;

    public bool IsRunning => _activeContext is { CancellationToken: { IsCancellationRequested: false } };

    public ExecutionEngine(
        IEnumerable<ISelectorStrategy> strategies,
        IInputSimulator input,
        IVariableEvaluator evaluator,
        ILogger<ExecutionEngine> logger)
    {
        _strategies = strategies.OrderBy(s => s.Priority).ToList();
        _input = input;
        _evaluator = evaluator;
        _logger = logger;
    }

    public async Task<PlaybackResult> ExecuteAsync(
        ScriptDefinition script,
        IProgress<PlaybackProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        using var ctx = new ExecutionContext();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, ctx.CancellationToken);
        _activeContext = ctx;

        foreach (var (k, v) in script.Variables)
            ctx.SetVariable(k, v);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var executed = 0;
        var total = script.Actions.Count;

        _logger.LogInformation("[{CorrelationId}] Starting execution of script '{Name}' ({Total} actions)",
            ctx.CorrelationId, script.Name, total);

        try
        {
            var actions = script.Actions.OfType<ActionBase>().ToList();

            foreach (var action in actions)
            {
                linked.Token.ThrowIfCancellationRequested();

                while (ctx.IsPaused && !linked.Token.IsCancellationRequested)
                    await Task.Delay(100, linked.Token).ConfigureAwait(false);

                if (ctx.Breakpoints.Contains(action.Id))
                    ctx.Pause();

                progress?.Report(new PlaybackProgress
                {
                    TotalActions = total,
                    CompletedActions = executed,
                    CurrentActionName = ActionFactory.Describe(action),
                    Status = PlaybackStatus.Running
                });

                _logger.LogDebug("[{CorrelationId}] Executing action {ActionType} [{ActionId}]",
                    ctx.CorrelationId, action.ActionType, action.Id);

                if (!action.IsEnabled)
                {
                    _logger.LogDebug("[{CorrelationId}] Skipping disabled action [{ActionId}]",
                        ctx.CorrelationId, action.Id);
                    continue;
                }

                await ExecuteActionWithRetryAsync(action, ctx, linked.Token).ConfigureAwait(false);
                executed++;

                var delay = AppConstants.DefaultPlaybackDelayMs;
                if (delay > 0)
                    await Task.Delay(delay, linked.Token).ConfigureAwait(false);
            }

            sw.Stop();
            _logger.LogInformation(
                "[{CorrelationId}] Execution completed: {Executed}/{Total} actions in {Elapsed}ms",
                ctx.CorrelationId, executed, total, sw.ElapsedMilliseconds);

            return new PlaybackResult
            {
                Success = true,
                ActionsExecuted = executed,
                Duration = sw.Elapsed,
                Variables = ctx.GetSnapshot().ToDictionary(p => p.Key, p => p.Value ?? (object)string.Empty)
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _logger.LogWarning("[{CorrelationId}] Execution cancelled after {Elapsed}ms",
                ctx.CorrelationId, sw.ElapsedMilliseconds);
            return new PlaybackResult
            {
                Success = false,
                ActionsExecuted = executed,
                Duration = sw.Elapsed,
                ErrorMessage = "Cancelled"
            };
        }
        catch (PlaybackFailedException ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[{CorrelationId}] Execution failed at action [{ActionId}]",
                ctx.CorrelationId, ex.ActionId);
            return new PlaybackResult
            {
                Success = false,
                ActionsExecuted = executed,
                Duration = sw.Elapsed,
                ErrorMessage = ex.Message,
                ErrorActionId = ex.ActionId
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[{CorrelationId}] Unexpected error during execution", ctx.CorrelationId);
            return new PlaybackResult
            {
                Success = false,
                ActionsExecuted = executed,
                Duration = sw.Elapsed,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            _activeContext = null;
        }
    }

    private async Task ExecuteActionWithRetryAsync(ActionBase action, ExecutionContext ctx, CancellationToken ct)
    {
        var pipeline = CreateResiliencePipeline(action);
        await pipeline.ExecuteAsync(async cancellationToken =>
        {
            await DispatchActionAsync(action, ctx, cancellationToken).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    private ResiliencePipeline CreateResiliencePipeline(ActionBase action) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<SelectorNotFoundException>()
                    .Handle<CaptureException>(),
                MaxRetryAttempts = action.RetryCount,
                DelayGenerator = args => new ValueTask<TimeSpan?>(
                    TimeSpan.FromMilliseconds(action.RetryDelayMs * (args.AttemptNumber + 1))),
                OnRetry = args =>
                {
                    _logger.LogWarning(args.Outcome.Exception,
                        "Retry {Attempt}/{Max} for action [{ActionId}] after {Delay}ms",
                        args.AttemptNumber + 1, action.RetryCount, action.Id,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

    private async Task DispatchActionAsync(ActionBase action, ExecutionContext ctx, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(action.TimeoutMs);

        try
        {
            await (action switch
            {
                ClickAction click               => ExecuteClickAsync(click, ctx, timeout.Token),
                TypeTextAction type             => ExecuteTypeTextAsync(type, ctx, timeout.Token),
                SendInputAction send            => ExecuteSendInputAsync(send, ctx, timeout.Token),
                FindTextAndClickAction ftc      => ExecuteFindTextAndClickAsync(ftc, ctx, timeout.Token),
                FindColorAndClickAction fcc     => ExecuteFindColorAndClickAsync(fcc, ctx, timeout.Token),
                FindImageAndClickAction fic     => ExecuteFindImageAndClickAsync(fic, ctx, timeout.Token),
                WaitForConditionAction wait     => ExecuteWaitForConditionAsync(wait, ctx, timeout.Token),
                DelayAction delay               => ExecuteDelayAsync(delay, timeout.Token),
                IfConditionAction ifCond        => ExecuteIfConditionAsync(ifCond, ctx, timeout.Token),
                LoopAction loop                 => ExecuteLoopAsync(loop, ctx, timeout.Token),
                SetVariableAction setVar        => ExecuteSetVariableAsync(setVar, ctx, timeout.Token),
                _                               => Task.CompletedTask
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new PlaybackFailedException(
                $"Action [{action.Id}] timed out after {action.TimeoutMs}ms", action.Id);
        }
    }

    private async Task ExecuteClickAsync(ClickAction action, ExecutionContext ctx, CancellationToken ct)
    {
        var point = action.Selector is not null
            ? (await FindElementAsync(action.Selector, ct).ConfigureAwait(false))?.ClickPoint ?? action.Position
            : action.Position;

        if (action.IsDoubleClick)
            await _input.DoubleClickAsync(point, ct).ConfigureAwait(false);
        else
            await _input.ClickAsync(point, action.Button, ct).ConfigureAwait(false);
    }

    private async Task ExecuteTypeTextAsync(TypeTextAction action, ExecutionContext ctx, CancellationToken ct)
    {
        var text = _evaluator.Evaluate(action.Text, ctx.GetSnapshot());
        await _input.TypeTextAsync(text, action.CharDelayMs, ct).ConfigureAwait(false);
    }

    private async Task ExecuteSendInputAsync(SendInputAction action, ExecutionContext ctx, CancellationToken ct)
    {
        var combo = new KeyCombo(action.Keys, action.Ctrl, action.Alt, action.Shift, action.Win);
        await _input.SendKeysAsync(combo, ct).ConfigureAwait(false);
    }

    private async Task ExecuteFindTextAndClickAsync(FindTextAndClickAction action, ExecutionContext ctx, CancellationToken ct)
    {
        var selector = new ElementSelector
        {
            TextToFind = action.TextToFind,
            SearchRegion = action.SearchRegion
        };
        var result = await FindElementAsync(selector, ct).ConfigureAwait(false)
            ?? throw new SelectorNotFoundException($"Text '{action.TextToFind}' not found on screen");
        await _input.ClickAsync(result.ClickPoint, action.Button, ct).ConfigureAwait(false);
    }

    private async Task ExecuteFindColorAndClickAsync(FindColorAndClickAction action, ExecutionContext ctx, CancellationToken ct)
    {
        var selector = new ElementSelector
        {
            ColorRange = new ColorRange { R = action.R, G = action.G, B = action.B, Tolerance = action.Tolerance },
            SearchRegion = action.SearchRegion
        };
        var result = await FindElementAsync(selector, ct).ConfigureAwait(false)
            ?? throw new SelectorNotFoundException(
                $"Color RGB({action.R},{action.G},{action.B}) not found on screen");
        await _input.ClickAsync(result.ClickPoint, action.Button, ct).ConfigureAwait(false);
    }

    private async Task ExecuteFindImageAndClickAsync(FindImageAndClickAction action, ExecutionContext ctx, CancellationToken ct)
    {
        var selector = new ElementSelector
        {
            ImageBase64 = action.ImageBase64,
            SearchRegion = action.SearchRegion
        };
        var result = await FindElementAsync(selector, ct).ConfigureAwait(false)
            ?? throw new SelectorNotFoundException("Image template not found on screen");
        await _input.ClickAsync(result.ClickPoint, action.Button, ct).ConfigureAwait(false);
    }

    private async Task ExecuteWaitForConditionAsync(WaitForConditionAction action, ExecutionContext ctx, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_evaluator.EvaluateBool(action.Condition, ctx.GetSnapshot())) return;
            await Task.Delay(action.PollIntervalMs, ct).ConfigureAwait(false);
        }
    }

    private static Task ExecuteDelayAsync(DelayAction action, CancellationToken ct) =>
        Task.Delay(action.DelayMs, ct);

    private async Task ExecuteIfConditionAsync(IfConditionAction action, ExecutionContext ctx, CancellationToken ct)
    {
        var branch = _evaluator.EvaluateBool(action.Condition, ctx.GetSnapshot())
            ? action.ThenActions
            : action.ElseActions;

        foreach (var child in branch)
        {
            ct.ThrowIfCancellationRequested();
            await ExecuteActionWithRetryAsync(child, ctx, ct).ConfigureAwait(false);
        }
    }

    private async Task ExecuteLoopAsync(LoopAction action, ExecutionContext ctx, CancellationToken ct)
    {
        var iter = 0;
        while (!ct.IsCancellationRequested && iter < action.MaxIterations)
        {
            if (action.FixedCount.HasValue && iter >= action.FixedCount.Value) break;
            if (action.WhileCondition is not null
                && !_evaluator.EvaluateBool(action.WhileCondition, ctx.GetSnapshot())) break;

            foreach (var child in action.Body)
            {
                ct.ThrowIfCancellationRequested();
                await ExecuteActionWithRetryAsync(child, ctx, ct).ConfigureAwait(false);
            }
            iter++;
        }

        if (iter >= action.MaxIterations)
            _logger.LogWarning("Loop [{ActionId}] reached max iterations ({Max})", action.Id, action.MaxIterations);
    }

    private Task ExecuteSetVariableAsync(SetVariableAction action, ExecutionContext ctx, CancellationToken ct)
    {
        var value = _evaluator.Evaluate(action.Expression, ctx.GetSnapshot());
        ctx.SetVariable(action.VariableName, value);
        return Task.CompletedTask;
    }

    private async Task<SelectorResult?> FindElementAsync(ElementSelector selector, CancellationToken ct)
    {
        foreach (var strategy in _strategies)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await strategy.FindAsync(selector, ct).ConfigureAwait(false);
                if (result is not null)
                {
                    _logger.LogDebug("Selector found via {Strategy} (confidence={Confidence})",
                        strategy.Name, result.Confidence);
                    return result;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Strategy {Strategy} failed: {Message}", strategy.Name, ex.Message);
            }
        }
        return null;
    }

    public Task PauseAsync()
    {
        _activeContext?.Pause();
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        _activeContext?.Resume();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _activeContext?.Dispose();
        return ValueTask.CompletedTask;
    }
}
