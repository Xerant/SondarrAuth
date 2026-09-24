using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Sondarr.Auth.Shared
{
    /// <summary>
    /// Fetches and caches a Supabase project's JWKS so tokens signed with an asymmetric key
    /// (ES256/RS256, identified by a <c>kid</c> header) can be verified without a shared secret.
    /// Instances are shared per JWKS URL via <see cref="For(string)"/> because
    /// <see cref="SupabaseAuthenticationExtensions.CreateTokenValidationParameters(Microsoft.Extensions.Configuration.IConfiguration, string)"/>
    /// is called per request by the validate-token and session endpoints.
    /// </summary>
    internal sealed class SupabaseJwksProvider
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

        // A token with an unknown kid forces a refetch (Supabase key rotation), but no more
        // often than this -- otherwise tokens with made-up kids could hammer the JWKS endpoint.
        private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromMinutes(5);

        private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly ConcurrentDictionary<string, SupabaseJwksProvider> Instances = new(StringComparer.OrdinalIgnoreCase);

        private readonly HttpClient _httpClient;
        private readonly string _jwksUrl;
        private readonly TimeProvider _timeProvider;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        private JsonWebKeySet? _cachedKeySet;
        private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;
        private DateTimeOffset _lastFetchAttempt = DateTimeOffset.MinValue;

        internal SupabaseJwksProvider(HttpClient httpClient, string jwksUrl, TimeProvider? timeProvider = null)
        {
            _httpClient = httpClient;
            _jwksUrl = jwksUrl;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Returns the process-wide provider for <paramref name="jwksUrl"/>, creating it on first use.
        /// </summary>
        public static SupabaseJwksProvider For(string jwksUrl) =>
            Instances.GetOrAdd(jwksUrl, url => new SupabaseJwksProvider(SharedHttpClient, url));

        /// <summary>
        /// Returns the signing keys matching <paramref name="kid"/>, or none if the JWKS has no such key
        /// or can't be fetched. Synchronous because <see cref="TokenValidationParameters.IssuerSigningKeyResolver"/>
        /// has no async overload; the fetch is cached for an hour so this only blocks on a cache miss.
        /// </summary>
        public IEnumerable<SecurityKey> GetSigningKeys(string kid)
        {
            var keys = FindKeys(GetKeySetAsync(forceRefresh: false).GetAwaiter().GetResult(), kid);
            if (keys.Count > 0)
            {
                return keys;
            }

            return FindKeys(GetKeySetAsync(forceRefresh: true).GetAwaiter().GetResult(), kid);
        }

        private static List<SecurityKey> FindKeys(JsonWebKeySet? keySet, string kid) =>
            keySet?.Keys.Where(k => k.Kid == kid).Cast<SecurityKey>().ToList() ?? new List<SecurityKey>();

        private async Task<JsonWebKeySet?> GetKeySetAsync(bool forceRefresh)
        {
            if (!NeedsFetch(forceRefresh))
            {
                return _cachedKeySet;
            }

            await _refreshLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!NeedsFetch(forceRefresh))
                {
                    return _cachedKeySet;
                }

                _lastFetchAttempt = _timeProvider.GetUtcNow();
                var json = await _httpClient.GetStringAsync(_jwksUrl).ConfigureAwait(false);
                _cachedKeySet = new JsonWebKeySet(json);
                _cachedAt = _lastFetchAttempt;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException)
            {
                // Keep serving the last good key set (if any). The token then fails validation
                // with "signing key not found" rather than surfacing as a 500.
            }
            finally
            {
                _refreshLock.Release();
            }

            return _cachedKeySet;
        }

        private bool NeedsFetch(bool forceRefresh)
        {
            var now = _timeProvider.GetUtcNow();
            if (now - _lastFetchAttempt < MinRefreshInterval)
            {
                return false;
            }

            return forceRefresh || _cachedKeySet == null || now - _cachedAt >= CacheDuration;
        }
    }
}
