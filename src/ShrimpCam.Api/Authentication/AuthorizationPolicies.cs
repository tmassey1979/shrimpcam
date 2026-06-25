using Microsoft.AspNetCore.Authorization;
using ShrimpCam.Core.Authentication;

namespace ShrimpCam.Api.Authentication;

internal static class AuthorizationPolicies
{
    public const string Viewer = "Viewer";
    public const string Operator = "Operator";
    public const string Administrator = "Administrator";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(Viewer, policy => policy.RequireRole(AuthorizationRoles.Viewer, AuthorizationRoles.Operator, AuthorizationRoles.Administrator));
        options.AddPolicy(Operator, policy => policy.RequireRole(AuthorizationRoles.Operator, AuthorizationRoles.Administrator));
        options.AddPolicy(Administrator, policy => policy.RequireRole(AuthorizationRoles.Administrator));
    }
}
