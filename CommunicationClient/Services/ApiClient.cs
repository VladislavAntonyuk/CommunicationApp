using System.Net.Http.Json;
using Shared;

namespace CommunicationClient.Services;

public class ApiClient(HttpClient httpClient, IPreferences preferences)
{
    private string Backend => preferences.Get("backend", "");

    public async Task<List<ProviderStatus>> GetProviders() =>
        await Read<List<ProviderStatus>>(await httpClient.GetAsync($"{Backend}/providers")) ?? [];

    public async Task SetProvider(Target target, bool enabled) =>
        await Check(await httpClient.PutAsJsonAsync($"{Backend}/providers/{target}", new ProviderSwitch(enabled)));

    public async Task<List<ChatStatus>> GetChats(Target target) =>
        await Read<List<ChatStatus>>(await httpClient.GetAsync($"{Backend}/providers/{target}/chats")) ?? [];

    public async Task SetChat(Target target, string chatId, bool enabled) =>
        await Check(await httpClient.PutAsJsonAsync(
            $"{Backend}/providers/{target}/chats/{Uri.EscapeDataString(chatId)}", new ProviderSwitch(enabled)));

    public async Task<TelegramLoginStatus?> GetTelegramLogin() =>
        await Read<TelegramLoginStatus>(await httpClient.GetAsync($"{Backend}/telegram/login"));

    public async Task<TelegramLoginStatus?> SendTelegramLogin(string value) =>
        await Read<TelegramLoginStatus>(await httpClient.PostAsJsonAsync($"{Backend}/telegram/login", new TelegramLoginValue(value)));

    public async Task<TelegramLoginStatus?> TelegramLogout() =>
        await Read<TelegramLoginStatus>(await httpClient.PostAsync($"{Backend}/telegram/logout", null));

    private static async Task<T?> Read<T>(HttpResponseMessage response)
    {
        await Check(response);
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private static async Task Check(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ServerException(await ServerError.Read(response));
    }
}

public class ServerException(string message) : Exception(message);
