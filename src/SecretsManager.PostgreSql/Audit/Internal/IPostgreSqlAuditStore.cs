namespace SecretsManager.PostgreSql.Internal;

internal interface IPostgreSqlAuditStore
{
    ValueTask WriteAsync(SecretAuditEvent auditEvent, CancellationToken cancellationToken);
}
