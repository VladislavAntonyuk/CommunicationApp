using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace CommunicationApi.Tests;

public sealed class ProvidersTests : ApiTest
{
    [Fact]
    public async Task Get_NothingSaved_ReturnsReceivingProvidersEnabled()
    {
        var providers = await Get(Client);

        Assert.Equal(new[] { Target.Telegram, Target.Slack }, providers.Select(x => x.Target).ToArray());
        Assert.All(providers, x => Assert.True(x.Enabled));
    }

    [Fact]
    public async Task Get_ReturnsTargetAsName()
    {
        var json = await Client.GetStringAsync("/providers", TestContext.Current.CancellationToken);

        Assert.Contains("\"target\":\"Telegram\"", json);
    }

    [Fact]
    public async Task Put_SwitchOff_OnlyThatProviderIsDisabled()
    {
        await Put("Telegram", false);

        var providers = await Get(Client);
        Assert.False(providers.Single(x => x.Target == Target.Telegram).Enabled);
        Assert.True(providers.Single(x => x.Target == Target.Slack).Enabled);
    }

    [Fact]
    public async Task Put_SwitchOffThenOn_ProviderIsEnabled()
    {
        await Put("Slack", false);

        await Put("Slack", true);

        Assert.True((await Get(Client)).Single(x => x.Target == Target.Slack).Enabled);
    }

    [Fact]
    public async Task Put_SameValueTwice_Succeeds()
    {
        await Put("Slack", false);

        await Put("Slack", false);

        Assert.False((await Get(Client)).Single(x => x.Target == Target.Slack).Enabled);
    }

    [Theory]
    [InlineData("Email", "Email does not receive messages")]
    [InlineData("Teams", "Teams does not receive messages")]
    [InlineData("99", "99 does not receive messages")]
    public async Task Put_ProviderWithoutListener_ReturnsBadRequest(string target, string expectedDetail)
    {
        var response = await Client.PutAsJsonAsync($"/providers/{target}", new ProviderSwitch(false), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Equal(expectedDetail, problem?.Detail);
    }

    [Fact]
    public async Task Put_UnknownTarget_ReturnsBadRequest()
    {
        var response = await Client.PutAsJsonAsync("/providers/Viber", new ProviderSwitch(false), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("12345", "hash", true)]
    [InlineData("0", "hash", false)]
    [InlineData("", "hash", false)]
    [InlineData("12345", "", false)]
    public async Task Get_TelegramConfigured_DependsOnSettings(string appId, string appHash, bool expected)
    {
        var client = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Telegram:AppId", appId);
            builder.UseSetting("Telegram:AppHash", appHash);
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
        var client = Factory.WithWebHostBuilder(builder =>
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
        await Put("Telegram", false);

        await Notify("Telegram", "Telegram|1|hello");

        Assert.Empty(await Receive());
    }

    [Fact]
    public async Task Notify_OtherProviderSwitchedOff_MessageIsStored()
    {
        await Put("Telegram", false);

        await Notify("Slack", "Slack|U1|hello");

        Assert.Equal(new[] { "Slack|U1|hello" }, (await Receive()).Select(x => x.Text).ToArray());
    }

    [Fact]
    public async Task Notify_ProviderSwitchedBackOn_NewMessagesAreStored()
    {
        await Put("Telegram", false);
        await Notify("Telegram", "Telegram|1|while off");
        await Put("Telegram", true);

        await Notify("Telegram", "Telegram|1|after on");

        Assert.Equal(new[] { "Telegram|1|after on" }, (await Receive()).Select(x => x.Text).ToArray());
    }

    [Fact]
    public async Task SwitchOff_MessagesAlreadyStoredAreStillReturned()
    {
        await Notify("Telegram", "Telegram|1|before off");

        await Put("Telegram", false);

        Assert.Equal(new[] { "Telegram|1|before off" }, (await Receive()).Select(x => x.Text).ToArray());
    }

    private async Task Notify(string target, string text)
    {
        var response = await Client.PostAsJsonAsync($"/notify/{target}", new Message(text), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<List<ReceivedMessage>> Receive()
    {
        return await Client.GetFromJsonAsync<List<ReceivedMessage>>("/receive", TestContext.Current.CancellationToken) ?? [];
    }

    private static async Task<List<ProviderStatus>> Get(HttpClient client)
    {
        return await client.GetFromJsonAsync<List<ProviderStatus>>("/providers", TestContext.Current.CancellationToken) ?? [];
    }

    private async Task Put(string target, bool enabled)
    {
        var response = await Client.PutAsJsonAsync($"/providers/{target}", new ProviderSwitch(enabled), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
