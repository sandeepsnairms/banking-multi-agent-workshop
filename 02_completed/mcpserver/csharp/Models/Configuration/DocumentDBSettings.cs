namespace MCPServer.Models.Configuration;

public record DocumentDBSettings
{
    public required string ClusterName { get; init; }
    public required string Database { get; init; }
    public required string UserDataCollection { get; init; }
    public required string AccountsCollection { get; init; }
    public required string TransactionsCollection { get; init; }
    public required string RequestDataCollection { get; init; }
    public required string OfferDataCollection { get; init; }
    public string? UserAssignedIdentityClientID { get; init; }
}