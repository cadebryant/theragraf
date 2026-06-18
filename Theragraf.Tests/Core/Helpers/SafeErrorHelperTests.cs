using FluentAssertions;
using Theragraf.Core.Helpers;

namespace Theragraf.Tests.Core.Helpers;

public class SafeErrorHelperTests
{
    [Fact]
    public void GenerateCorrelationId_ReturnsValidFormat()
    {
        var correlationId = SafeErrorHelper.GenerateCorrelationId();

        correlationId.Should().NotBeNullOrEmpty();
        correlationId.Should().HaveLength(16);
        correlationId.Should().MatchRegex("^[0-9a-f]{16}$", "correlation ID should be 16-char hex string");
    }

    [Fact]
    public void GenerateCorrelationId_ReturnsUniqueValues()
    {
        var id1 = SafeErrorHelper.GenerateCorrelationId();
        var id2 = SafeErrorHelper.GenerateCorrelationId();

        id1.Should().NotBe(id2, "each correlation ID should be unique");
    }

    [Fact]
    public void GetSafeErrorMessage_WithOperation_IncludesOperationAndCorrelationId()
    {
        var correlationId = "abc123def4567890";
        var message = SafeErrorHelper.GetSafeErrorMessage("retrieving caseload", correlationId);

        message.Should().Contain("retrieving caseload");
        message.Should().Contain(correlationId);
        message.Should().Contain("contact support");
    }

    [Fact]
    public void GetSafeErrorMessage_WithoutCorrelationId_GeneratesOne()
    {
        var message = SafeErrorHelper.GetSafeErrorMessage("saving session");

        message.Should().Contain("saving session");
        message.Should().MatchRegex(@"[0-9a-f]{16}", "should contain a generated correlation ID");
    }

    [Fact]
    public void GetGenericErrorMessage_IncludesCorrelationId()
    {
        var correlationId = "1234567890abcdef";
        var message = SafeErrorHelper.GetGenericErrorMessage(correlationId);

        message.Should().Contain("unexpected error");
        message.Should().Contain(correlationId);
        message.Should().Contain("contact support");
    }

    [Fact]
    public void GetAuditLogDetail_IncludesCorrelationIdAndExceptionInfo()
    {
        var correlationId = "test123456789abc";
        var exception = new InvalidOperationException("Database connection failed");

        var detail = SafeErrorHelper.GetAuditLogDetail(exception, correlationId);

        detail.Should().Contain($"[{correlationId}]");
        detail.Should().Contain("InvalidOperationException");
        detail.Should().Contain("Database connection failed");
    }

    [Fact]
    public void GetAuditLogDetail_DoesNotIncludeSensitiveStackTrace()
    {
        var correlationId = "audit12345678901";
        var exception = new Exception("Test error");

        var detail = SafeErrorHelper.GetAuditLogDetail(exception, correlationId);

        // Audit log should have exception type and message but not full stack trace
        detail.Should().NotContain("at System.");
        detail.Should().NotContain("at Theragraf.");
    }

    [Fact]
    public void GetInternalLogDetail_IncludesStackTrace()
    {
        var correlationId = "internal1234567890";
        var exception = new InvalidOperationException("Test failure");

        var detail = SafeErrorHelper.GetInternalLogDetail(exception, correlationId);

        detail.Should().Contain($"[{correlationId}]");
        detail.Should().Contain("InvalidOperationException");
        detail.Should().Contain("Test failure");
        // Stack trace should be present for internal logging
        detail.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetSafeErrorMessage_NeverExposesExceptionDetails()
    {
        var message = SafeErrorHelper.GetSafeErrorMessage("processing request", "abc123");

        // User-facing message should be generic
        message.Should().NotContain("Exception");
        message.Should().NotContain("Stack");
        message.Should().NotContain("at ");
        message.Should().NotContain("Cosmos");
        message.Should().NotContain("SQL");
        message.Should().NotContain("connection string");
    }
}
