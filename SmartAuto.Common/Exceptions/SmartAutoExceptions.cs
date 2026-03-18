namespace SmartAuto.Common.Exceptions;

public class SmartAutoException(string message, Exception? inner = null)
    : Exception(message, inner);

public class SelectorNotFoundException(string message, Exception? inner = null)
    : SmartAutoException(message, inner);

public class PlaybackFailedException(string message, string? actionId = null, Exception? inner = null)
    : SmartAutoException(message, inner)
{
    public string? ActionId { get; } = actionId;
}

public class ScriptValidationException(string message, IEnumerable<string>? errors = null)
    : SmartAutoException(message)
{
    public IReadOnlyList<string> Errors { get; } = errors?.ToList() ?? [];
}

public class HookException(string message, Exception? inner = null)
    : SmartAutoException(message, inner);

public class CaptureException(string message, Exception? inner = null)
    : SmartAutoException(message, inner);
