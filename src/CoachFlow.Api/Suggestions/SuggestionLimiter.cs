using Microsoft.Extensions.Options;

namespace CoachFlow.Api.Suggestions;

public enum LimitRejection
{
    None,
    AlreadyRunning,
    PerMinute
}

// Je Person eine laufende Anfrage und höchstens N Anfragen pro Minute. Gestartete Anfragen zählen auch dann,
// wenn sie abgebrochen werden, sonst ließe sich das Limit durch Abbrechen umgehen.
// Der Zustand liegt im Speicher. Das passt zum Betrieb mit genau einer Instanz.
public sealed class SuggestionLimiter(TimeProvider time, IOptions<AiOptions> options)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly Lock _lock = new();
    private readonly Dictionary<string, UserState> _users = [];

    public LimitRejection TryStart(string userId, out IDisposable? lease, out TimeSpan retryAfter)
    {
        lease = null;
        retryAfter = TimeSpan.Zero;
        var now = time.GetUtcNow();

        lock (_lock)
        {
            if (!_users.TryGetValue(userId, out var state))
            {
                state = new UserState();
                _users[userId] = state;
            }

            if (state.Running)
            {
                return LimitRejection.AlreadyRunning;
            }

            while (state.Starts.Count > 0 && now - state.Starts.Peek() >= Window)
            {
                state.Starts.Dequeue();
            }

            if (state.Starts.Count >= options.Value.RequestsPerMinute)
            {
                retryAfter = state.Starts.Peek() + Window - now;
                return LimitRejection.PerMinute;
            }

            state.Starts.Enqueue(now);
            state.Running = true;
            lease = new Lease(this, state);
            return LimitRejection.None;
        }
    }

    private void Finish(UserState state)
    {
        lock (_lock)
        {
            state.Running = false;
        }
    }

    private sealed class UserState
    {
        public bool Running { get; set; }

        public Queue<DateTimeOffset> Starts { get; } = new();
    }

    private sealed class Lease(SuggestionLimiter limiter, UserState state) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            limiter.Finish(state);
        }
    }
}
