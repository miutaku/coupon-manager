using CouponManager.Api;
using CouponManager.Application;
using CouponManager.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCouponInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHostedService<InactiveCouponCleanupService>();
builder.Services.AddHostedService<NotificationDispatchService>();
builder.Services.AddScoped<INotificationEventSource, CouponExpirationNotificationSource>();
builder.Services.AddHttpClient<INotificationChannel, DiscordNotificationChannel>().RemoveAllLoggers();
builder.Services.AddScoped<INotificationChannel, ShellScriptNotificationChannel>();
builder.Services.AddHttpClient<OpenAiCouponImageAnalyzer>(client => client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:8080"])
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler(handler => handler.Run(WriteProblemDetailsAsync));
app.UseCors();
app.MapOpenApi();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapCouponEndpoints();
app.MapSettingsEndpoints();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<CouponDbContext>().Database.MigrateAsync();
}

app.Run();

static async Task WriteProblemDetailsAsync(HttpContext context)
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var isValidationError = exception is ArgumentException;
    var statusCode = isValidationError
        ? StatusCodes.Status400BadRequest
        : StatusCodes.Status500InternalServerError;
    var message = isValidationError
        ? exception!.Message
        : "予期しないエラーが発生しました。";

    context.Response.StatusCode = statusCode;
    await Results.Problem(message, statusCode: statusCode).ExecuteAsync(context);
}
