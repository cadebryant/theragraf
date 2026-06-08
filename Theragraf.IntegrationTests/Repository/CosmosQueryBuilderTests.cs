namespace Theragraf.IntegrationTests.Repository;

using FluentAssertions;
using Theragraf.Core.Models;
using Theragraf.Functions.Services;

/// <summary>
/// Pure unit tests for <see cref="CosmosSessionRepository.BuildQuery"/>.
/// No emulator required — exercises the SQL generation logic directly.
/// </summary>
[Trait("Category", "Unit")]
public class CosmosQueryBuilderTests
{
    private static (string Sql, List<(string Name, object Value)> Parameters) Build(
        SessionQueryOptions options, string clientId = "client-001") =>
        CosmosSessionRepository.BuildQuery(clientId, options);

    // ── Base query ────────────────────────────────────────────────────────────

    [Fact]
    public void BuildQuery_NoOptions_Returns_BaseQuery()
    {
        var (sql, parameters) = Build(new SessionQueryOptions());

        sql.Should().Contain("SELECT * FROM c WHERE c.clientId = @clientId");
        sql.Should().Contain("ORDER BY c.id DESC");
        parameters.Should().ContainSingle(p => p.Name == "@clientId" && (string)p.Value == "client-001");
    }

    // ── Discipline filter ─────────────────────────────────────────────────────

    [Fact]
    public void BuildQuery_WithDiscipline_Adds_DisciplineClause()
    {
        var (sql, parameters) = Build(new SessionQueryOptions { Discipline = "OT" });

        sql.Should().Contain("c.discipline = @discipline");
        parameters.Should().Contain(p => p.Name == "@discipline" && (string)p.Value == "OT");
    }

    [Fact]
    public void BuildQuery_EmptyDiscipline_Omits_DisciplineClause()
    {
        var (sql, _) = Build(new SessionQueryOptions { Discipline = "  " });
        sql.Should().NotContain("@discipline");
    }

    // ── Therapist filter ──────────────────────────────────────────────────────

    [Fact]
    public void BuildQuery_WithTherapist_Adds_TherapistClause()
    {
        var (sql, parameters) = Build(new SessionQueryOptions { Therapist = "Dr. Jones" });

        sql.Should().Contain("c.therapistName = @therapist");
        parameters.Should().Contain(p => p.Name == "@therapist" && (string)p.Value == "Dr. Jones");
    }

    // ── Payer filter ──────────────────────────────────────────────────────────

    [Fact]
    public void BuildQuery_WithPayer_Adds_PayerClause()
    {
        var (sql, parameters) = Build(new SessionQueryOptions { Payer = "Medicaid" });

        sql.Should().Contain("c.payer = @payer");
        parameters.Should().Contain(p => p.Name == "@payer" && (string)p.Value == "Medicaid");
    }

    // ── Date range filters ────────────────────────────────────────────────────

    [Fact]
    public void BuildQuery_WithDateFrom_Adds_DateFromClause()
    {
        var from  = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var (sql, parameters) = Build(new SessionQueryOptions { DateFrom = from });

        sql.Should().Contain("c.id >= @dateFrom");
        parameters.Should().Contain(p => p.Name == "@dateFrom");
    }

    [Fact]
    public void BuildQuery_WithDateTo_Adds_DateToClause()
    {
        var to    = new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero);
        var (sql, parameters) = Build(new SessionQueryOptions { DateTo = to });

        sql.Should().Contain("c.id <= @dateTo");
        parameters.Should().Contain(p => p.Name == "@dateTo");
    }

    [Fact]
    public void BuildQuery_WithBothDates_Adds_BothDateClauses()
    {
        var options = new SessionQueryOptions
        {
            DateFrom = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            DateTo   = new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)
        };
        var (sql, _) = Build(options);

        sql.Should().Contain("c.id >= @dateFrom");
        sql.Should().Contain("c.id <= @dateTo");
    }

    // ── Sort options ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("therapist",     "c.therapistName")]
    [InlineData("therapistname", "c.therapistName")]
    [InlineData("discipline",    "c.discipline")]
    [InlineData("setting",       "c.setting")]
    [InlineData("payer",         "c.payer")]
    [InlineData("duration",      "c.sessionDurationMinutes")]
    [InlineData("createdat",     "c.createdAt")]
    [InlineData("sessiondate",   "c.id")]
    [InlineData(null,            "c.id")]          // default
    [InlineData("unknown",       "c.id")]          // unknown falls back to default
    public void BuildQuery_SortBy_Maps_CorrectField(string? sortBy, string expectedField)
    {
        var (sql, _) = Build(new SessionQueryOptions { SortBy = sortBy });
        sql.Should().Contain($"ORDER BY {expectedField}");
    }

    [Fact]
    public void BuildQuery_SortOrder_Asc_Uses_ASC()
    {
        var (sql, _) = Build(new SessionQueryOptions { SortOrder = "asc" });
        sql.Should().Contain("ASC");
        sql.Should().NotContain("DESC");
    }

    [Fact]
    public void BuildQuery_SortOrder_Default_Uses_DESC()
    {
        var (sql, _) = Build(new SessionQueryOptions { SortOrder = null });
        sql.Should().Contain("DESC");
    }

    [Fact]
    public void BuildQuery_SortOrder_CaseInsensitive()
    {
        var (sql, _) = Build(new SessionQueryOptions { SortOrder = "ASC" });
        sql.Should().Contain("ASC");
    }

    // ── Combined filters ──────────────────────────────────────────────────────

    [Fact]
    public void BuildQuery_MultipleFilters_AllPresent_InCorrectOrder()
    {
        var options = new SessionQueryOptions
        {
            Discipline = "PT",
            Therapist  = "Dr. Smith",
            Payer      = "Medicare",
            SortBy     = "discipline",
            SortOrder  = "asc"
        };

        var (sql, parameters) = Build(options, "client-abc");

        sql.Should().Contain("c.discipline = @discipline");
        sql.Should().Contain("c.therapistName = @therapist");
        sql.Should().Contain("c.payer = @payer");
        sql.Should().Contain("ORDER BY c.discipline ASC");

        parameters.Should().HaveCount(4); // clientId + discipline + therapist + payer
    }
}
