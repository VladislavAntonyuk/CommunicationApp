namespace CommunicationApi.Services.Teams;

public interface ITeamsService
{
    Task Send(string recipient, string text);
}
