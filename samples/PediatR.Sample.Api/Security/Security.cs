namespace PediatR.Sample.Api.Security;

/// <summary>
/// The template-style authorization attribute. Placed directly on classic request classes, or on a
/// <c>[Command]</c>/<c>[Query]</c> method — where the generator forwards it onto the generated request.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class AuthorizeAttribute : Attribute
{
    public string Roles { get; set; } = string.Empty;
}

public sealed class ForbiddenAccessException : Exception
{
}

public interface ICurrentUser
{
    string? Id { get; }

    bool IsInRole(string role);
}

/// <summary>Derives identity from request headers (<c>X-User</c>, <c>X-Roles</c>) so the demo is drivable with curl.</summary>
public sealed class HeaderCurrentUser : ICurrentUser
{
    private readonly HashSet<string> _roles;

    public HeaderCurrentUser(IHttpContextAccessor accessor)
    {
        var request = accessor.HttpContext?.Request;
        Id = request?.Headers["X-User"].FirstOrDefault();
        _roles = (request?.Headers["X-Roles"].FirstOrDefault() ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public string? Id { get; }

    public bool IsInRole(string role) => _roles.Contains(role);
}
