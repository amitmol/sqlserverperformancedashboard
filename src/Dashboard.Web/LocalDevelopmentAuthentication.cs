using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Dashboard.Web;

// Explicit local-only identity for the synthetic demo, never a production login provider.
public sealed class LocalDevelopmentAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string InstanceId = "11111111-1111-1111-1111-111111111111";
    public LocalDevelopmentAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var ip = Context.Connection.RemoteIpAddress;
        if (ip == null || !IPAddress.IsLoopback(ip))
            return Task.FromResult(AuthenticateResult.Fail("Local development access only."));
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Local development user"),
            new Claim("monitoring-instance", InstanceId) }, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        return Response.WriteAsJsonAsync(new { contractVersion = 1, requestId = Context.TraceIdentifier,
            generatedAtUtc = DateTime.UtcNow, error = new { code = "authenticationRequired", message = "Local development access is required." } });
    }
}
