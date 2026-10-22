using Habits.Domain.TestData;

namespace Habits.Mobile.Services;

public sealed class DemoUserService : ICurrentUserService
{
    public string UserId => TestDataGenerator.UserOne;
}