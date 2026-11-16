using System.Text.Json.Serialization;
using Habits.Api.ErrorHandling;
using Habits.Application.Abstractions;
using Habits.Application.Services;
using Habits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Controllers + JSON: enums kā teksts ("Weekly", "Monday") nevis cipari
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// Kļūdas -> ProblemDetails (ApiExceptionHandler).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// ---------- Glabāšana (appsettings.json: "Storage") ----------
var useInMemoryStorage = string.Equals(
    builder.Configuration["Storage"], "InMemory", StringComparison.OrdinalIgnoreCase);

if (useInMemoryStorage)
{
    //Dati "dzīvi" tikmēr strādā API
    builder.Services.AddSingleton<IHabitRepository, InMemoryHabitRepository>();
}
else
{
    //SQL serveris caur EF Core; dati izdzīve pēc restarta
    builder.Services.AddDbContext<HabitsDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("HabitsDb")));

    builder.Services.AddScoped<IHabitRepository, EfHabitRepository>();
}

//Aplikācijas slānis: viena servisa instance pret katru HTTP pieprasījumam.
builder.Services.AddScoped<HabitService>();

var app = builder.Build();

// Create / migrācija / ģenerēšana.
if (!useInMemoryStorage)
{
    await DatabaseInitializer.InitializeAsync(app.Services);
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();