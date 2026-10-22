using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Habits.Shared;

namespace Habits.Mobile.Services.Api;

public sealed class ApiHabitService : IHabitService
{
    // JSON formātā kā API
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly ICurrentUserService _currentUser;

    public ApiHabitService(HttpClient http, ICurrentUserService currentUser)
    {
        _http = http;
        _currentUser = currentUser;
    }

    // ---------------- Queries ----------------

    public async Task<IReadOnlyList<HabitDto>> GetHabitsAsync(bool includeArchived = false)
    {

        var query = includeArchived ? "true" : "false";
        return await GetAsync<List<HabitDto>>($"api/habits?includeArchived={query}");
    }

    public async Task<HabitDto?> GetHabitAsync(Guid habitId)
    {
        try
        {
            return await GetAsync<HabitDto>($"api/habits/{habitId}");
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null; 
        }
    }

    public async Task<IReadOnlyList<HabitCompletionDto>> GetCompletionsAsync(Guid habitId) =>
        await GetAsync<List<HabitCompletionDto>>($"api/habits/{habitId}/completions");

    // ---------------- Commands ----------------

    public async Task<HabitDto> CreateAsync(CreateHabitDto dto)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/habits", dto);
        return await ReadAsync<HabitDto>(response);
    }

    public async Task<HabitDto> UpdateAsync(UpdateHabitDto dto)
    {
        using var response = await SendAsync(HttpMethod.Put, $"api/habits/{dto.Id}", dto);
        return await ReadAsync<HabitDto>(response);
    }

    public async Task SetArchivedAsync(Guid habitId, bool archived)
    {
        var action = archived ? "archive" : "restore";
        using var _ = await SendAsync(HttpMethod.Post, $"api/habits/{habitId}/{action}");
    }

    public async Task MarkCompletedAsync(MarkHabitCompletedDto dto)
    {
        using var _ = await SendAsync(HttpMethod.Post, $"api/habits/{dto.HabitId}/completions", dto);
    }

    public async Task UnmarkCompletedAsync(MarkHabitCompletedDto dto)
    {
        using var _ = await SendAsync(
            HttpMethod.Delete, $"api/habits/{dto.HabitId}/completions/{dto.Date:yyyy-MM-dd}");
    }

    public async Task DeleteAsync(Guid habitId)
    {
        using var _ = await SendAsync(HttpMethod.Delete, $"api/habits/{habitId}");
    }

    // ---------------- HTTP helpers ----------------

    private async Task<T> GetAsync<T>(string uri)
    {
        using var response = await SendAsync(HttpMethod.Get, uri);
        return await ReadAsync<T>(response);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, object? body = null)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(ApiHeaders.UserId, _currentUser.UserId);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (HttpRequestException ex)   // server not running / no network
        {
            throw new ApiUnavailableException(ex);
        }
        catch (TaskCanceledException ex)  // timeout
        {
            throw new ApiUnavailableException(ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response);
            var status = response.StatusCode;
            response.Dispose();
            throw new ApiException(status, message);
        }

        return response;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions)
        ?? throw new ApiException(response.StatusCode, "Serveris atgrieza tukšu atbildi.");

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOptions);
            if (problem is not null)
            {
                if (!string.IsNullOrWhiteSpace(problem.Detail)) return problem.Detail;

                // Validācijas kļūdas no  [ApiController] nāk kā "errors".
                var firstError = problem.Errors?.Values.SelectMany(e => e).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstError)) return firstError;

                if (!string.IsNullOrWhiteSpace(problem.Title)) return problem.Title;
            }
        }
        catch (JsonException)
        {
        }
        catch (NotSupportedException)
        {
        }

        return $"Servera kļūda ({(int)response.StatusCode}).";
    }
}

internal sealed record ProblemResponse(
    string? Title,
    string? Detail,
    Dictionary<string, string[]>? Errors);
