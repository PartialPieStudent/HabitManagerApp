namespace Habits.Mobile.Services.Api;

public static class ApiSettings
{
#if ANDROID
    public const string BaseUrl = "http://10.0.2.2:5080/";
#else
    public const string BaseUrl = "http://localhost:5080/";
#endif

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
}
