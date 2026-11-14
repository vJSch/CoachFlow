namespace CoachFlow.Application.Ai;

public enum GateDenial
{
    None,
    AlreadyRunning,
    RateLimited
}

/// <summary>
/// Entscheidung R4: je Nutzer eine Anfrage gleichzeitig und höchstens fünf pro Minute.
/// Der Zustand liegt im Speicher, was für die eine Serverinstanz aus Annahme A8 genügt.
/// </summary>
public sealed class AiRequestGate(AiOptions options, TimeProvider timeProvider)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly Lock _lock = new();
    private readonly HashSet<Guid> _running = [];
    private readonly Dictionary<Guid, Queue<DateTimeOffset>> _recent = [];

    public GateDenial TryEnter(Guid userId)
    {
        lock (_lock)
        {
            if (_running.Contains(userId)) return GateDenial.AlreadyRunning;

            var now = timeProvider.GetUtcNow();
            if (!_recent.TryGetValue(userId, out var timestamps))
                _recent[userId] = timestamps = new Queue<DateTimeOffset>();

            while (timestamps.TryPeek(out var oldest) && now - oldest >= Window)
                timestamps.Dequeue();

            if (timestamps.Count >= options.MaxRequestsPerMinutePerUser) return GateDenial.RateLimited;

            timestamps.Enqueue(now);
            _running.Add(userId);
            return GateDenial.None;
        }
    }

    public void Leave(Guid userId)
    {
        lock (_lock) _running.Remove(userId);
    }
}
