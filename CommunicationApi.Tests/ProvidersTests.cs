using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace CommunicationApi.Tests;

public sealed class ProvidersTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Get_NothingSaved_ReturnsReceivingProvidersEnabled()
    {
        var client = _factory.CreateClient();

        var providers = await Get(client);

        Assert.Equal(new[] { Target.Telegram, Target.Slack }, providers.Select(x => x.Target).ToArray());
        Assert.All(providers, x => Assert.True(x.Enabled));
    }

    [Fact]
    public async Task Get_ReturnsTargetAsName()
    {
        var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/providers", TestContext.Current.CancellationToken);

        Assert.Contains("\"target\":\"Telegram\"", json);
    }

    [Fact]
    public async Task Put_SwitchOff_OnlyThatProviderIsDisabled()
    {
        var client = _factory.CreateClient();

        await Put(client, "Telegram", false);

        var providers = await Get(client);
        Assert.False(providers.Single(x => x.Target == Target.Telegram).Enabled);
        Assert.True(providers.Single(x => x.Target == Target.Slack).Enabled);
    }

    [Fact]
    public async Task Put_SwitchOffThenOn_ProviderIsEnabled()
    {
        var client = _factory.CreateClient();
        await Put(client, "Slack", false);

        await Put(client, "Slack", true);

        Assert.True((await Get(client)).Single(x => x.Target == Target.Slack).Enabled);
    }

    [Fact]
    public async Task Put_SameValueTwice_Succeeds()
    {
        var client = _factory.CreateClient();
        await Put(client, "Slack", false);

        await Put(client, "Slack", false);

        Assert.False((await Get(client)).Single(x => x.Target == Target.Slack).Enabled);
    }

    [Theory]
    [InlineData("Email", "Email does not receive messages")]
    [InlineData("Teams", "Teams does not receive messages")]
    [InlineData("99", "99 does not receive messages")]
    public async Task Put_ProviderWithoutListener_ReturnsBadRequest(string target, string expectedDetail)
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/providers/{target}", new ProviderSwitch(false), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(expectedDetail, problem?.Detail);
    }

    [Fact]
    public async Task Put_UnknownTarget_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync("/providers/Viber", new ProviderSwitch(false), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("12345", "hash", "+380987654321", true)]
    [InlineData("0", "hash", "+380987654321", false)]
    [InlineData("12345", "", "+380987654321", false)]
    [InlineData("12345", "hash", "", false)]
    public async Task Get_TelegramConfigured_DependsOnSettings(string appId, string appHash, string phone, bool expected)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Telegram:AppId", appId);
            builder.UseSetting("Telegram:AppHash", appHash);
            builder.UseSetting("Telegram:Phone", phone);
        }).CreateClient();

        var providers = await Get(client);

        Assert.Equal(expected, providers.Single(x => x.Target == Target.Telegram).Configured);
    }

    [Theory]
    [InlineData("xapp-1-token", true)]
    [InlineData("<your app-level token here>", false)]
    [InlineData("", false)]
    public async Task Get_SlackConfigured_DependsOnSettings(string appLevelToken, bool expected)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Slack:ApiToken", "token");
            builder.UseSetting("Slack:AppLevelToken", appLevelToken);
        }).CreateClient();

        var providers = await Get(client);

        Assert.Equal(expected, providers.Single(x => x.Target == Target.Slack).Configured);
    }

    [Fact]
    public async Task Notify_ProviderSwitchedOff_MessageIsNotStored()
    {
        var client = _factory.CreateClient();
        await Put(client, "Telegram", false);

        await Notify(client, "Telegram", "Telegram|1|hello");

        Assert.Empty(await Receive(client));
    }

    [Fact]
    public async Task Notify_OtherProviderSwitchedOff_MessageIsStored()
    {
        var client = _factory.CreateClient();
        await Put(client, "Telegram", false);

        await Notify(client, "Slack", "Slack|U1|hello");

        Assert.Equal(new[] { "Slack|U1|hello" }, (await Receive(client)).Select(x => x.Text).ToArray());
    }

    [Fact]
    public async Task Notify_ProviderSwitchedBackOn_NewMessagesAreStored()
    {
        var client = _factory.CreateClient();
        await Put(client, "Telegram", false);
        await Notify(client, "Telegram", "Telegram|1|while off");
        await Put(client, "Telegram", true);

        await Notify(client, "Telegram", "Telegram|1|after on");

        Assert.Equal(new[] { "Telegram|1|after on" }, (await Receive(client)).Select(x => x.Text).ToArray());
    }

    [Fact]
    public async Task SwitchOff_MessagesAlreadyStoredAreStillReturned()
    {
        var client = _factory.CreateClient();
        await Notify(client, "Telegram", "Telegram|1|before off");

        await Put(client, "Telegram", false);

        Assert.Equal(new[] { "Telegram|1|before off" }, (await Receive(client)).Select(x => x.Text).ToArray());
    }

    private static async Task Notify(HttpClient client, string target, string text)
    {
        var response = await client.PostAsJsonAsync($"/notify/{target}", new Message(text), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<List<ReceivedMessage>> Receive(HttpClient client)
    {
        return await client.GetFromJsonAsync<List<ReceivedMessage>>("/receive", TestContext.Current.CancellationToken) ?? [];
    }

    private static async Task<List<ProviderStatus>> Get(HttpClient client)
    {
        return await client.GetFromJsonAsync<List<ProviderStatus>>("/providers", TestContext.Current.CancellationToken) ?? [];
    }

    private static async Task Put(HttpClient client, string target, bool enabled)
    {
        var response = await client.PutAsJsonAsync($"/providers/{target}", new ProviderSwitch(enabled), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
