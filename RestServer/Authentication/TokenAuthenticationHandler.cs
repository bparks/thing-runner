using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThingRunner.Models;

namespace ThingRunner.RestServer.Authentication;

class TokenAuthenticationHandler : AuthenticationHandler<TokenAuthenticationOptions>
{
    private ThingsDbContext Database { get; init; }

    public TokenAuthenticationHandler(
        IOptionsMonitor<TokenAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        ThingsDbContext db
    ) : base(options, logger, encoder, clock)
    {
        Database = db;
    }

    protected async override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var matchingHeader = Request.Headers.Authorization
            .FirstOrDefault(h => h?.StartsWith("Token ", true, System.Globalization.CultureInfo.InvariantCulture) ?? false);
        if (matchingHeader is not null)
        {
            var token = matchingHeader.Substring(6);
            string hashedToken = Convert.ToBase64String(SHA512.Create().ComputeHash(Encoding.UTF8.GetBytes(token)));

            // Find it in the database
            var tokenRecord = Database.Tokens.SingleOrDefault(t => t.TokenValue == hashedToken);
            if (tokenRecord is not null)
            {
                Database.Audit.Add(new AuditRecord
                {
                    Id = Guid.NewGuid(),
                    Type = "auth",
                    OccurredAt = DateTime.UtcNow,
                    RequestId = Context.TraceIdentifier,
                    Data = JsonSerializer.Serialize(new
                    {
                        tokenName = tokenRecord.Name,
                        result = TokenIsValid(tokenRecord) ? "success" : "revoked"
                    })
                });
                Database.SaveChanges();

                if (TokenIsValid(tokenRecord))
                {
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.Name, tokenRecord.Name)
                    };
                    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, this.Scheme.Name));
                    var ticket = new AuthenticationTicket(principal, this.Scheme.Name);

                    return AuthenticateResult.Success(ticket);
                }
            }
        }
        return AuthenticateResult.NoResult();
    }

    private bool TokenIsValid(Token token)
    {
        return (token.RevokedAt is null);
    }
}