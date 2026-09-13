namespace ChaosInbox.Services;

public static class UserContext
{
    public static string GetUserId(HttpContext context)
    {
        var value = context.Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value)) return "demo";

        var cleaned = new string(value.Trim()
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
            .Take(80)
            .ToArray());

        return string.IsNullOrWhiteSpace(cleaned) ? "demo" : cleaned;
    }
}
