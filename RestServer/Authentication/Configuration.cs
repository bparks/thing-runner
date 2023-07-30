using Microsoft.AspNetCore.Authentication;

namespace ThingRunner.RestServer.Authentication;

static class Configuration
{
    public static void AddToken(this AuthenticationBuilder auth, Action<TokenAuthenticationOptions>? configureOptions = null)
    {
        auth.AddScheme<TokenAuthenticationOptions, TokenAuthenticationHandler>("Token", configureOptions);
    }
}