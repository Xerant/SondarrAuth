using Microsoft.AspNetCore.Http;

namespace Sondarr.Auth.Shared.Tests;

// Microsoft.AspNetCore.Http.HttpContextAccessor stores its value in a static
// AsyncLocal shared across every instance, which makes it unsafe for isolated
// unit tests (state can leak between tests on the same logical call context).
// This test double is a plain, per-instance implementation instead.
internal sealed class TestHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
