using System.Net;

namespace CatholicCompanion.Api.Services
{
    // Shared by readings and calendar requests so visitors do not create bursts at USCCB.
    public sealed class UsccbRequestGate
    {
        private readonly SemaphoreSlim _requests = new(1, 1);
        private DateTimeOffset _nextRequest;
        private DateTimeOffset _blockedUntil;
        private HttpStatusCode _blockedStatus;

        public Task EnterAsync(CancellationToken cancellationToken) => _requests.WaitAsync(cancellationToken);

        public void Exit() => _requests.Release();

        public async Task WaitForSourceAsync(CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            if (now < _blockedUntil)
                throw new HttpRequestException("The readings source is temporarily paused.", null, _blockedStatus);

            var delay = _nextRequest - now;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);

            _nextRequest = DateTimeOffset.UtcNow.AddSeconds(1);
        }

        public void Observe(HttpResponseMessage response)
        {
            if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests))
                return;

            var now = DateTimeOffset.UtcNow;
            var retryAfter = response.Headers.RetryAfter;
            var delay = retryAfter?.Delta ?? (retryAfter?.Date - now) ?? TimeSpan.FromMinutes(1);
            if (delay < TimeSpan.FromMinutes(1)) delay = TimeSpan.FromMinutes(1);
            _blockedUntil = now.Add(delay);
            _blockedStatus = response.StatusCode;
        }
    }
}
