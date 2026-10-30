using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Pathway.Api.Application.Sessions;
using Pathway.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// MVC view services register the built-in antiforgery authorization filters; no views are mapped.
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-pathway_antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("planning", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
            }));
    options.AddPolicy("session-creation", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddHttpClient<Pathway.Api.Application.Planning.IPlanGenerator, Pathway.Api.Infrastructure.Planning.OllamaPlanGenerator>(client =>
    client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<AnonymousSessionPolicy>();
builder.Services.AddScoped<AnonymousSessionService>();
builder.Services.AddScoped<Pathway.Api.Application.Goals.GoalService>();
builder.Services.AddScoped<Pathway.Api.Application.Planning.PlanningService>();
builder.Services.AddScoped<Pathway.Api.Application.Planning.TaskService>();
builder.Services.AddDbContext<PathwayDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Pathway")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:Pathway before using persistence.")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    try { await next(); }
    catch (Pathway.Api.Application.UseCaseException exception)
    {
        context.Response.StatusCode = exception.Status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        { Status = exception.Status, Detail = exception.Message });
    }
    catch (DbUpdateConcurrencyException)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        { Status = 409, Detail = "The goal changed. Refresh and try again." });
    }
});
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();

public partial class Program { }
