using System.Diagnostics;
using PharmaGo.UsersService.Factory;
using PharmaGo.UsersService.Filters;
using System.Diagnostics.CodeAnalysis;
using Instrumentation;

Activity.DefaultIdFormat = ActivityIdFormat.W3C;
Activity.ForceDefaultIdFormat = true;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.SuppressEfAndSqlClientLogs();

builder.Services.AddHttpContextAccessor();
builder.Services.RegisterBusinessLogicServices(builder.Configuration);
builder.Services.RegisterDataAccessServices(builder.Configuration);
builder.Services.AddControllers(options => options.Filters.Add(typeof(ExceptionFilter)));

builder.Services.AddControllers();

builder.Services.AddHealthChecks();

builder.Services.AddPharmaGoOpenTelemetryMetrics("PharmaGo.UsersService");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("MyAllowedOrigins",
        policy =>
        {
            policy.WithOrigins("*")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var app = builder.Build();

app.MigrateDatabase();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("MyAllowedOrigins");
app.UseServiceCorrelationScope();
app.UseStructuredRequestLogging();

app.UseAuthorization();

app.MapHealthChecks("/health");
app.UseMetricsMiddleware();

app.MapControllers();

app.Run();

[ExcludeFromCodeCoverage]
public partial class Program { }

