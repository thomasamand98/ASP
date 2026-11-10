using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Api.Common;
using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.Auth;

// LOGIN_THROTTLE_LIMIT au lieu de la limite globale
[AttributeUsage(AttributeTargets.Method)]
public sealed class LoginThrottleAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SkipThrottleAttribute : Attribute;

// en mémoire, par (route, IP) : au-delà de la limite, 429 pendant 1 min
public class ThrottleMiddleware(RequestDelegate next, IOptions<AuthOptions> options, TimeProvider time)
{
    public const string ErrorMessage = "Trop de requêtes, veuillez réessayer dans quelques instants";
    private const long TtlMs = 60_000;

    private sealed class Record
    {
        // échéance de chaque requête (arrivée + 1 min)
        public readonly Queue<long> Hits = new();
        public long ExpiresAt;
        public long BlockExpiresAt;
        public bool IsBlocked;
    }

    private readonly ConcurrentDictionary<string, Record> _storage = new();
    private readonly AuthOptions _options = options.Value;
    private long _lastSweep;

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null || endpoint.Metadata.GetMetadata<SkipThrottleAttribute>() is not null)
        {
            await next(context);
            return;
        }

        var limit = endpoint.Metadata.GetMetadata<LoginThrottleAttribute>() is not null ? _options.LoginThrottleLimitValue : _options.ThrottleLimitValue;
        var key = $"{endpoint.DisplayName}-default-{NormalizeIp(context.Connection.RemoteIpAddress)}";
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        Sweep(now);

        var (totalHits, timeToExpire, isBlocked, timeToBlockExpire) = Increment(key, limit, now);
        if (isBlocked)
        {
            context.Response.Headers["Retry-After"] = timeToBlockExpire.ToString(CultureInfo.InvariantCulture);
            throw new TooManyRequestsException(ErrorMessage);
        }
        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, limit - totalHits).ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["X-RateLimit-Reset"] = timeToExpire.ToString(CultureInfo.InvariantCulture);
        await next(context);
    }

    private (int TotalHits, long TimeToExpire, bool IsBlocked, long TimeToBlockExpire) Increment(string key, int limit, long now)
    {
        var record = _storage.GetOrAdd(key, _ => new Record { ExpiresAt = now + TtlMs });
        lock (record)
        {
            while (record.Hits.Count > 0 && record.Hits.Peek() <= now) record.Hits.Dequeue();

            var timeToExpire = SecondsUntil(record.ExpiresAt, now);
            if (timeToExpire <= 0)
            {
                record.ExpiresAt = now + TtlMs;
                timeToExpire = SecondsUntil(record.ExpiresAt, now);
            }
            if (!record.IsBlocked) record.Hits.Enqueue(now + TtlMs);
            if (record.Hits.Count > limit && !record.IsBlocked)
            {
                record.IsBlocked = true;
                record.BlockExpiresAt = now + TtlMs;
            }
            var timeToBlockExpire = SecondsUntil(record.BlockExpiresAt, now);
            if (timeToBlockExpire <= 0 && record.IsBlocked)
            {
                record.IsBlocked = false;
                record.Hits.Clear();
                record.Hits.Enqueue(now + TtlMs);
            }
            return (record.Hits.Count, timeToExpire, record.IsBlocked, timeToBlockExpire);
        }
    }

    private static long SecondsUntil(long instant, long now) => (long)Math.Ceiling((instant - now) / 1000d);

    // compteurs inactifs, une fois par minute max
    private void Sweep(long now)
    {
        var last = Interlocked.Read(ref _lastSweep);
        if (now - last < TtlMs || Interlocked.CompareExchange(ref _lastSweep, now, last) != last) return;
        foreach (var (key, record) in _storage)
        {
            lock (record)
            {
                while (record.Hits.Count > 0 && record.Hits.Peek() <= now) record.Hits.Dequeue();
                if (record.Hits.Count > 0 || (record.IsBlocked && record.BlockExpiresAt > now) || record.ExpiresAt > now) continue;
                _storage.TryRemove(key, out _);
            }
        }
    }

    // IPv6 regroupées par /56 (un client a souvent tout le préfixe)
    private static string NormalizeIp(IPAddress? address)
    {
        if (address is null) return "";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return address.ToString();
        var bytes = address.GetAddressBytes();
        for (var i = 7; i < bytes.Length; i++) bytes[i] = 0;
        return $"{new IPAddress(bytes)}/56";
    }
}
