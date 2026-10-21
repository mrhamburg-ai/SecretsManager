using System.Linq;

namespace SecretsManager.PostgreSql.Tests;

public sealed class PostgreSqlAuditSinkOptionsTests
{
    [Fact]
    public void Validate_ValidOptions_ReturnsNoErrors()
    {
        var options = new PostgreSqlAuditSinkOptions
        {
            ConnectionString = "Host=localhost;Database=secrets;Username=app"
        };

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void Validate_RequiresConnectionString()
    {
        var errors = new PostgreSqlAuditSinkOptions().Validate().ToList();

        Assert.Contains("ConnectionString is required", errors);
    }

    [Fact]
    public void Validate_RejectsInvalidIdentifiers()
    {
        var options = new PostgreSqlAuditSinkOptions
        {
            ConnectionString = "Host=localhost",
            Schema = "public; DROP TABLE secrets; --",
            TableName = "audit log"
        };

        var errors = options.Validate().ToList();

        Assert.Contains(errors, e => e.Contains("Schema must contain"));
        Assert.Contains(errors, e => e.Contains("TableName must contain"));
    }

    [Fact]
    public void Validate_RejectsOverlongTableName()
    {
        var options = new PostgreSqlAuditSinkOptions
        {
            ConnectionString = "Host=localhost",
            TableName = new string('a', 64)
        };

        var errors = options.Validate().ToList();

        Assert.Contains(errors, e => e.Contains("63 characters or fewer"));
    }
}
