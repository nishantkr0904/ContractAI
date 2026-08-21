using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContractAI.API.Auth;
using ContractAI.API.Middleware;
using ContractAI.Data;
using ContractAI.Services;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog(config => config
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

builder.Services.AddContractAiData(connectionString);
builder.Services.AddContractAiServices(builder.Configuration);

// ICurrentTenant reads the request principal, so it needs the accessor and must be
// scoped to the request.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();

builder.Services
    .AddContractAiAuthentication(builder.Configuration)
    .AddContractAiAuthorization();

// The wire contract in API_REFERENCE.md is snake_case properties with
// UPPER_SNAKE_CASE enum values ("file_name", "PARSED_SUCCESS"). SnakeCaseUpper on
// the enum converter turns ContractStatus.ParsedSuccess into PARSED_SUCCESS, which
// is also exactly how the PostgreSQL enum labels read.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();

// Exposed so the integration test host (WebApplicationFactory) has a program type
// to bootstrap; without this the top-level-statements entry point is internal.
public partial class Program;
