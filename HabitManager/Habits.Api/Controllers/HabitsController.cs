using Habits.Application.Services;
using Habits.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Habits.Api.Controllers;

[ApiController]
[Route("api/habits")]
public sealed class HabitsController : ControllerBase
{
    private readonly HabitService _habits;

    public HabitsController(HabitService habits)
    {
        _habits = habits;
    }

    private string CurrentUserId => Request.Headers[ApiHeaders.UserId].ToString();
    //Datums UTC
    private static DateOnly ResolveToday(DateOnly? today) =>
        today ?? DateOnly.FromDateTime(DateTime.UtcNow);
    // GET api/habits?includeArchived=true
    // GET api/habits?includeArchived=true&today=2026-11-09
    [HttpGet]
    public Task<IReadOnlyList<HabitDto>> GetAll(
        [FromQuery] bool includeArchived, [FromQuery] DateOnly? today, CancellationToken ct) =>
        _habits.GetHabitsAsync(CurrentUserId, includeArchived, ResolveToday(today), ct);

    // GET api/habits/{id}?today=2026-11-09
    [HttpGet("{id:guid}")]
    public Task<HabitDto> GetById(Guid id, [FromQuery] DateOnly? today, CancellationToken ct) =>
        _habits.GetHabitAsync(CurrentUserId, id, ResolveToday(today), ct);

    // POST api/habits
    [HttpPost]
    public async Task<ActionResult<HabitDto>> Create(
        CreateHabitDto dto, [FromQuery] DateOnly? today, CancellationToken ct)
    {
        var created = await _habits.CreateAsync(CurrentUserId, dto, ResolveToday(today), ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT api/habits/{id}
    [HttpPut("{id:guid}")]
    public Task<HabitDto> Update(
        Guid id, UpdateHabitDto dto, [FromQuery] DateOnly? today, CancellationToken ct) =>
        _habits.UpdateAsync(CurrentUserId, id, dto, ResolveToday(today), ct);

    // POST api/habits/{id}/archive
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await _habits.SetArchivedAsync(CurrentUserId, id, archived: true, ct);
        return NoContent();
    }

    // POST api/habits/{id}/restore
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        await _habits.SetArchivedAsync(CurrentUserId, id, archived: false, ct);
        return NoContent();
    }

    // DELETE api/habits/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _habits.DeleteAsync(CurrentUserId, id, ct);
        return NoContent();
    }

    // GET api/habits/{id}/completions
    [HttpGet("{id:guid}/completions")]
    public Task<IReadOnlyList<HabitCompletionDto>> GetCompletions(Guid id, CancellationToken ct) =>
        _habits.GetCompletionsAsync(CurrentUserId, id, ct);

    // POST api/habits/{id}/completions   body: { "habitId": "...", "date": "2026-11-04" }
    [HttpPost("{id:guid}/completions")]
    public async Task<ActionResult<HabitCompletionDto>> MarkCompleted(
        Guid id, MarkHabitCompletedDto dto, CancellationToken ct)
    {
        // The habit id from the route is authoritative.
        var completion = await _habits.MarkCompletedAsync(CurrentUserId, id, dto.Date, ct);
        return CreatedAtAction(nameof(GetCompletions), new { id }, completion);
    }

    // DELETE api/habits/{id}/completions/2026-11-04
    [HttpDelete("{id:guid}/completions/{date}")]
    public async Task<IActionResult> UnmarkCompleted(Guid id, DateOnly date, CancellationToken ct)
    {
        await _habits.UnmarkCompletedAsync(CurrentUserId, id, date, ct);
        return NoContent();
    }
}
