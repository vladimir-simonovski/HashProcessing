using MySqlConnector;
using Testcontainers.MariaDb;

namespace HashProcessing.Benchmarks.Infrastructure;

public sealed class MariaDbFixture : IAsyncDisposable
{
    private const string RootPassword = "root";

    private readonly MariaDbContainer _container = new MariaDbBuilder()
        .WithImage("mariadb:11")
        .WithEnvironment("MARIADB_ROOT_PASSWORD", RootPassword)
        .Build();

    private string _connectionString = null!;
    public string WorkerConnectionString { get; private set; } = null!;
    public string ApiConnectionString { get; private set; } = null!;

    public async Task StartAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        var rootConnectionString = BuildRootConnectionString();
        await CreateDatabaseAsync(rootConnectionString, "worker");
        await CreateDatabaseAsync(rootConnectionString, "api");

        WorkerConnectionString = ReplaceDatabase(rootConnectionString, "worker");
        ApiConnectionString = ReplaceDatabase(rootConnectionString, "api");
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private string BuildRootConnectionString()
    {
        var builder = new MySqlConnectionStringBuilder(_connectionString)
        {
            UserID = "root",
            Password = RootPassword
        };
        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{databaseName}`";
        await command.ExecuteNonQueryAsync();
    }

    private static string ReplaceDatabase(string connectionString, string databaseName)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString) { Database = databaseName };
        return builder.ConnectionString;
    }
}
