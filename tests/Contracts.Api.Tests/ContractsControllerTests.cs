using System.Net;
using System.Net.Http.Json;
using Contracts.Api.Models;
using Dapper;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace Contracts.Api.Tests;

[Collection("contracts-api")]
public sealed class ContractsControllerTests
{
    private readonly ContractsApiFactory _factory;
    private readonly HttpClient _client;

    public ContractsControllerTests(ContractsApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());
        _client.DefaultRequestHeaders.Add("X-Roles", "Contract.Create");
    }

    [Fact]
    public async Task CreateContract_ReturnsCreated_AndPersistsOutbox()
    {
        await _factory.ResetDatabaseAsync();
        var projectId = await _factory.InsertProjectAsync("PRJ001", "Du an A");

        var request = new ContractRequest
        {
            ScopeType = "project",
            ProjectId = projectId,
            Code = "CTR001",
            Name = "Hop dong PCCC",
            ValueVnd = 1_250_000_000m,
            WarrantyValueVnd = 50_000_000m,
            StartDate = new DateOnly(2025, 11, 1),
            EndDate = new DateOnly(2026, 4, 30),
            Description = "Mo ta hop dong",
            PaymentSchedule =
            {
                new PaymentScheduleItem
                {
                    DueDate = new DateOnly(2025, 12, 15),
                    AmountVnd = 250_000_000m,
                    Note = "Dot 1"
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/contracts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.ETag.Should().NotBeNull();

        var payload = await response.Content.ReadFromJsonAsync<ContractCreateResponse>();
        payload.Should().NotBeNull();
        payload!.Code.Should().Be("CTR001");
        payload.State.Should().Be("draft");

        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();

        var inserted = await connection.QuerySingleAsync<(Guid Id, string Code, decimal Value)>(
            "SELECT id, code, value_vnd FROM public.contracts WHERE id = @id", new { id = payload.Id });

        inserted.Code.Should().Be("CTR001");
        inserted.Value.Should().Be(request.ValueVnd);

        var outboxCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.outbox_events WHERE aggregate_id = @id", new { id = payload.Id });
        outboxCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateContract_WithDuplicateCode_ReturnsConflict()
    {
        await _factory.ResetDatabaseAsync();
        var projectId = await _factory.InsertProjectAsync("PRJ002", "Du an B");

        await using (var connection = new NpgsqlConnection(_factory.ConnectionString))
        {
            await connection.OpenAsync();
            var sql = @"INSERT INTO public.contracts (
                    scope_type, project_id, package_id, code, name,
                    value_vnd, warranty_value_vnd, start_date, end_date,
                    description, payment_schedule, created_by)
                VALUES ('project', @ProjectId, NULL, 'CTR100', 'Hop dong test',
                    1000, 0, '2025-01-01', '2025-12-31', NULL, '[]'::jsonb, '00000000-0000-0000-0000-000000000001');";
            await connection.ExecuteAsync(sql, new { ProjectId = projectId });
        }

        var request = new ContractRequest
        {
            ScopeType = "project",
            ProjectId = projectId,
            Code = "CTR100",
            Name = "Hop dong trung",
            ValueVnd = 5_000_000m,
            WarrantyValueVnd = 1_000_000m,
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 6, 1)
        };

        var response = await _client.PostAsJsonAsync("/api/v1/contracts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("duplicate_code");
    }

    [Fact]
    public async Task CreateContract_InvalidDateRange_ReturnsUnprocessableEntity()
    {
        await _factory.ResetDatabaseAsync();
        var projectId = await _factory.InsertProjectAsync("PRJ003", "Du an C");

        var request = new ContractRequest
        {
            ScopeType = "project",
            ProjectId = projectId,
            Code = "CTR200",
            Name = "Hop dong sai ngay",
            ValueVnd = 5_000_000m,
            WarrantyValueVnd = 0,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2025, 12, 1)
        };

        var response = await _client.PostAsJsonAsync("/api/v1/contracts", request);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var validation = await response.Content.ReadFromJsonAsync<ContractValidationResponse>();
        validation.Should().NotBeNull();
        validation!.Valid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.Field == "start_date");
    }
}

[CollectionDefinition("contracts-api")]
public sealed class ContractsApiCollection : ICollectionFixture<ContractsApiFactory>
{
}
