using System.Data;
using Contracts.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Contracts.Api.Tests;

public sealed class ContractsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresqlContainer;

    public string ConnectionString => _postgresqlContainer.GetConnectionString();

    public ContractsApiFactory()
    {
        _postgresqlContainer = new PostgreSqlBuilder()
            .WithDatabase("contracts")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(IDbConnection));
            services.RemoveAll(typeof(NpgsqlConnection));

            services.AddScoped<NpgsqlConnection>(_ => new NpgsqlConnection(ConnectionString));
            services.AddScoped<IDbConnection>(_ => new NpgsqlConnection(ConnectionString));
        });
    }

    public async Task InitializeAsync()
    {
        await _postgresqlContainer.StartAsync();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var root = GetSolutionRootDirectory();
        var migrationPath = Path.Combine(root, "migration_contracts_v1.sql");
        var script = await File.ReadAllTextAsync(migrationPath);

        await using var command = new NpgsqlCommand(script, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresqlContainer.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var truncateSql = @"TRUNCATE TABLE public.contracts RESTART IDENTITY CASCADE;
TRUNCATE TABLE public.outbox_events RESTART IDENTITY CASCADE;
TRUNCATE TABLE public.projects RESTART IDENTITY CASCADE;
TRUNCATE TABLE public.bid_packages RESTART IDENTITY CASCADE;";
        await using var command = new NpgsqlCommand(truncateSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> InsertProjectAsync(string code, string name)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var sql = @"INSERT INTO public.projects(code, name) VALUES (@code, @name) RETURNING id;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("code", code);
        command.Parameters.AddWithValue("name", name);
        var result = await command.ExecuteScalarAsync();
        return (Guid)result!;
    }

    public async Task<Guid> InsertBidPackageAsync(Guid projectId, string code, string name)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var sql = @"INSERT INTO public.bid_packages(project_id, code, name) VALUES (@project_id, @code, @name) RETURNING id;";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("code", code);
        command.Parameters.AddWithValue("name", name);
        var result = await command.ExecuteScalarAsync();
        return (Guid)result!;
    }

    private static string GetSolutionRootDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        var directory = new DirectoryInfo(baseDir);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ContractService.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Unable to locate solution root directory.");
        }

        return directory.FullName;
    }
}
