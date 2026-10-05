using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SaqerAccountingSystem.Infrastructure;

public sealed record LegacySession(
    LegacyUserDto User,
    LegacyGroupDto Group,
    IReadOnlyList<LegacyScreenAccessDto> Screens,
    HashSet<string> Permissions,
    DateTime ExpiresAt);

public sealed class LegacySessionStore
{
    private readonly ConcurrentDictionary<string, LegacySession> _sessions = new(StringComparer.Ordinal);

    public string Create(LegacySession session)
    {
        CleanupExpired();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _sessions[token] = session with { ExpiresAt = DateTime.UtcNow.AddHours(12) };
        return token;
    }

    public bool TryGet(string token, out LegacySession session)
    {
        if (_sessions.TryGetValue(token, out session!) && session.ExpiresAt > DateTime.UtcNow)
            return true;

        _sessions.TryRemove(token, out _);
        session = null!;
        return false;
    }

    public void Remove(string token) => _sessions.TryRemove(token, out _);

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _sessions)
            if (pair.Value.ExpiresAt <= now)
                _sessions.TryRemove(pair.Key, out _);
    }
}
