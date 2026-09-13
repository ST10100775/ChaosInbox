using System.Text.Json.Serialization;
using ChaosInbox.Data;
using ChaosInbox.Models;
using ChaosInbox.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase))
{
    builder.Configuration.AddJsonFile("appsettings.Docker.json", optional: true, reloadOnChange: false);
}

var connectionString = builder.Configuration.GetConnectionString("ChaosInbox")
    ?? "Data Source=data/chaosinbox.db";

var dbPath = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;
if (!string.IsNullOrWhiteSpace(dbPath))
{
    var fullPath = Path.IsPathRooted(dbPath) ? dbPath : Path.Combine(builder.Environment.ContentRootPath, dbPath);
    var directory = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
}

builder.Services.AddDbContext<ChaosInboxDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<TicketProcessor>();
builder.Services.AddScoped<ChaosEngine>();
builder.Services.AddSingleton<DemoEmailService>();
builder.Services.AddSingleton<EmlEmailService>();
builder.Services.AddSingleton<GmailEmailService>();
builder.Services.AddSingleton<FallbackTicketAnalyzer>();

builder.Services.AddHttpClient<OllamaTicketAnalyzer>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434/");
    client.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChaosInboxDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new
{
    application = "Chaos Inbox",
    status = "alive",
    utc = DateTime.UtcNow
}));

app.MapGet("/api/tickets", async (HttpContext http, ChaosInboxDbContext db, string? status, CancellationToken ct) =>
{
    var userId = UserContext.GetUserId(http);
    var query = db.Tickets.AsNoTracking().Where(x => x.UserId == userId);

    if (Enum.TryParse<TicketStatus>(status, true, out var parsedStatus))
        query = query.Where(x => x.Status == parsedStatus);

    var items = await query
        .OrderBy(x => x.Status == TicketStatus.Completed || x.Status == TicketStatus.Archived)
        .ThenByDescending(x => x.Priority)
        .ThenBy(x => x.DeadlineUtc == null)
        .ThenBy(x => x.DeadlineUtc)
        .ThenByDescending(x => x.ReceivedAtUtc)
        .ToListAsync(ct);

    return Results.Ok(items);
});

app.MapGet("/api/tickets/{id:int}", async (int id, HttpContext http, ChaosInboxDbContext db, CancellationToken ct) =>
{
    var userId = UserContext.GetUserId(http);
    var ticket = await db.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
    return ticket is null ? Results.NotFound() : Results.Ok(ticket);
});

app.MapPost("/api/demo/import", async (HttpContext http, DemoEmailService demo, TicketProcessor processor, CancellationToken ct) =>
{
    var userId = UserContext.GetUserId(http);
    var created = 0;
    var tickets = new List<EmailTicket>();

    foreach (var email in demo.GetDemoEmails(DateTime.UtcNow))
    {
        var result = await processor.ProcessAsync(userId, email, ct);
        if (result.Created) created++;
        tickets.Add(result.Ticket);
    }

    return Results.Ok(new { created, total = tickets.Count, tickets });
});

app.MapPost("/api/eml/import", async (HttpContext http, EmlEmailService eml, TicketProcessor processor, CancellationToken ct) =>
{
    var userId = UserContext.GetUserId(http);
    if (!http.Request.HasFormContentType)
        return Results.BadRequest(new { error = "Use multipart/form-data with one or more .eml files in the 'files' field." });

    var form = await http.Request.ReadFormAsync(ct);
    var files = form.Files.Where(f => f.FileName.EndsWith(".eml", StringComparison.OrdinalIgnoreCase)).ToList();
    if (files.Count == 0)
        return Results.BadRequest(new { error = "No .eml files were supplied." });

    var results = new List<object>();
    foreach (var file in files.Take(25))
    {
        try
        {
            var email = await eml.ParseAsync(file, ct);
            var processed = await processor.ProcessAsync(userId, email, ct);
            results.Add(new { file = file.FileName, processed.Created, processed.Ticket.Id, processed.Ticket.Subject });
        }
        catch (Exception ex)
        {
            results.Add(new { file = file.FileName, created = false, error = ex.Message });
        }
    }

    return Results.Ok(results);
});

app.MapPost("/api/gmail/sync", async (HttpContext http, GmailEmailService gmail, TicketProcessor processor, int? max, CancellationToken ct) =>
{
    var userId = UserContext.GetUserId(http);
    try
    {
        var messages = await gmail.GetRecentAsync(userId, Math.Clamp(max ?? 20, 1, 50), ct);
        var created = 0;
        foreach (var message in messages)
        {
            var result = await processor.ProcessAsync(userId, message, ct);
            if (result.Created) created++;
        }
        return Results.Ok(new { fetched = messages.Count, created });
    }
    catch (FileNotFoundException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem(title: "Gmail sync failed", detail: ex.Message, statusCode: 502);
    }
});

app.MapPut("/api/tickets/{id:int}/status", async (int id, UpdateStatusRequest request, HttpContext http, ChaosInboxDbContext db, CancellationToken ct) =>
{
    var userId = UserContext.GetUserId(http);
    var ticket = await db.Tickets.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
    if (ticket is null) return Results.NotFound();

    ticket.Status = request.Status;
    ticket.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync(ct);
    return Results.Ok(ticket);
});

app.MapPost("/api/tickets/{id:int}/chaos", async (int id, string? mode, HttpContext http, ChaosInboxDbContext db, ChaosEngine chaos, CancellationToken ct) =>
{
    var userId = UserContext.GetUserId(http);
    var ticket = await db.Tickets.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
    if (ticket is null) return Results.NotFound();

    var evt = await chaos.InjectAsync(ticket, mode ?? "api", ct);
    return Results.Ok(new { ticket, chaosEvent = evt });
});

app.MapDelete("/api/tickets", async (HttpContext http, ChaosInboxDbContext db, CancellationToken ct) =>
{
    var userId = UserContext.GetUserId(http);
    var tickets = await db.Tickets.Where(x => x.UserId == userId).ToListAsync(ct);
    db.Tickets.RemoveRange(tickets);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

app.MapGet("/api/stats", async (HttpContext http, ChaosInboxDbContext db, CancellationToken ct) =>
{
    var userId = UserContext.GetUserId(http);
    var tickets = await db.Tickets.AsNoTracking().Where(x => x.UserId == userId).ToListAsync(ct);
    var now = DateTime.UtcNow;

    return Results.Ok(new
    {
        total = tickets.Count,
        open = tickets.Count(x => x.Status is not TicketStatus.Completed and not TicketStatus.Archived),
        completed = tickets.Count(x => x.Status == TicketStatus.Completed),
        overdue = tickets.Count(x => x.DeadlineUtc < now && x.Status is not TicketStatus.Completed and not TicketStatus.Archived),
        critical = tickets.Count(x => x.Priority == TicketPriority.Critical && x.Status is not TicketStatus.Completed and not TicketStatus.Archived),
        aiProcessed = tickets.Count(x => x.AiUsed),
        fallbackProcessed = tickets.Count(x => !x.AiUsed)
    });
});

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
