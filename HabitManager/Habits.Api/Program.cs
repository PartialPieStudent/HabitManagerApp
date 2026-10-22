using System.Text.Json.Serialization;
using Habits.Api.ErrorHandling;
using Habits.Application.Abstractions;
using Habits.Application.Services;
using Habits.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Controllers + JSON: enums kā teksts ("Weekly", "Monday")
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// Errors -> ProblemDetails (ApiExceptionHandler).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Storage: glabājas atmiņā lietotnes dzīves laikā
// 4. md būs db repozitorijs
builder.Services.AddSingleton<IHabitRepository, InMemoryHabitRepository>();

// Aplikācijas servisi:
builder.Services.AddScoped<HabitService>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); 
}

app.MapControllers();

app.Run();