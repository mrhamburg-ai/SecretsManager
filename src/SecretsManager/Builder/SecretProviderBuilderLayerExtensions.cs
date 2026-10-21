using System;
using Microsoft.Extensions.Configuration;
using SecretsManager;

namespace SecretsManager.Builder;

/// <summary>
/// Extension methods for adding operational layers to the provider pipeline.
/// </summary>
public static class SecretProviderBuilderLayerExtensions
{
    /// <summary>
    /// Adds a concurrency-limiting layer.
    /// </summary>
    public static SecretProviderBuilder WithConcurrentLimiter(
        this SecretProviderBuilder builder,
        Action<ConcurrentLimiterLayerOptions>? configure = null)
    {
        var options = new ConcurrentLimiterLayerOptions();
        configure?.Invoke(options);

        builder.AddLayer(new ConcurrentLimiterLayer(options));
        return builder;
    }

    /// <summary>
    /// Adds a retry layer.
    /// </summary>
    public static SecretProviderBuilder WithRetry(
        this SecretProviderBuilder builder,
        Action<RetryLayerOptions>? configure = null)
    {
        var options = new RetryLayerOptions();
        configure?.Invoke(options);

        builder.AddLayer(new RetryLayer(options));
        return builder;
    }

    /// <summary>
    /// Adds a timeout layer.
    /// </summary>
    public static SecretProviderBuilder WithTimeout(
        this SecretProviderBuilder builder,
        Action<TimeoutLayerOptions>? configure = null)
    {
        var options = new TimeoutLayerOptions();
        configure?.Invoke(options);

        builder.AddLayer(new TimeoutLayer(options));
        return builder;
    }

    /// <summary>
    /// Adds a logging layer.
    /// </summary>
    public static SecretProviderBuilder WithLogging(
        this SecretProviderBuilder builder,
        Action<LoggingLayerOptions>? configure = null)
    {
        var options = new LoggingLayerOptions();
        configure?.Invoke(options);

        builder.AddLayer(new LoggingLayer(options));
        return builder;
    }

    /// <summary>
    /// Adds OpenTelemetry metrics layer.
    /// </summary>
    public static SecretProviderBuilder WithOtelMetrics(
        this SecretProviderBuilder builder,
        Action<OtelMetricsLayerOptions>? configure = null)
    {
        var options = new OtelMetricsLayerOptions();
        configure?.Invoke(options);

        builder.AddLayer(new OtelMetricsLayer(options));
        return builder;
    }

    /// <summary>
    /// Adds OpenTelemetry trace layer.
    /// </summary>
    public static SecretProviderBuilder WithOtelTrace(
        this SecretProviderBuilder builder,
        Action<OtelTraceLayerOptions>? configure = null)
    {
        var options = new OtelTraceLayerOptions();
        configure?.Invoke(options);

        builder.AddLayer(new OtelTraceLayer(options));
        return builder;
    }

    /// <summary>
    /// Adds a durable audit layer. The registered provider name is recorded automatically.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configure">Optional configuration action for <see cref="AuditLayerOptions"/>.</param>
    /// <returns>The same builder for method chaining.</returns>
    public static SecretProviderBuilder WithAudit(
        this SecretProviderBuilder builder,
        Action<AuditLayerOptions>? configure = null)
    {
        var layer = builder.GetOrAddLayer(static () => new AuditLayer(new AuditLayerOptions()));
        configure?.Invoke(layer.Options);
        return builder;
    }

    /// <summary>
    /// Registers an audit sink. May be called more than once to fan out to multiple sinks.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="sink">The sink to register.</param>
    /// <returns>The same builder for method chaining.</returns>
    public static SecretProviderBuilder WithAuditSink(
        this SecretProviderBuilder builder,
        ISecretAuditSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        var layer = builder.GetOrAddLayer(static () => new AuditLayer(new AuditLayerOptions()));
        layer.Options.Sinks.Add(sink);
        return builder;
    }

    /// <summary>
    /// Binds the <c>SecretsManager:Audit</c> configuration section. Sinks are objects and must be
    /// registered with <see cref="WithAuditSink"/>.
    /// </summary>
    /// <param name="builder">The secret provider builder.</param>
    /// <param name="configuration">The configuration to bind.</param>
    /// <returns>The same builder for method chaining.</returns>
    /// <exception cref="ArgumentException">The configuration is invalid.</exception>
    public static SecretProviderBuilder ConfigureAudit(
        this SecretProviderBuilder builder,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection("SecretsManager:Audit");
        if (!section.Exists())
            return builder;

        var errors = new List<string>();
        var enabled = true;

        var enabledText = section["Enabled"];
        if (!string.IsNullOrWhiteSpace(enabledText) && !bool.TryParse(enabledText, out enabled))
            errors.Add("Enabled must be true or false");

        var scope = SecretAuditScope.All;
        var onSinkFailure = AuditFailureMode.Ignore;

        if (enabled)
        {
            var scopeText = section["Scope"];
            if (!string.IsNullOrWhiteSpace(scopeText))
            {
                if (Enum.TryParse<SecretAuditScope>(scopeText, ignoreCase: true, out var parsedScope)
                    && (parsedScope & ~SecretAuditScope.All) == 0)
                {
                    scope = parsedScope;
                }
                else
                {
                    errors.Add("Scope must be None, Reads, Writes, Deletes, Enumeration, or All");
                }
            }

            var failureText = section["OnSinkFailure"];
            if (!string.IsNullOrWhiteSpace(failureText)
                && (!Enum.TryParse<AuditFailureMode>(failureText, ignoreCase: true, out onSinkFailure)
                    || !Enum.IsDefined(onSinkFailure)))
            {
                errors.Add("OnSinkFailure must be Ignore or Throw");
            }
        }

        if (errors.Count > 0)
            throw new ArgumentException(
                $"Configuration errors for Audit: {string.Join("; ", errors)}");

        // Always add the layer, even when disabled: a sink registered later must not turn audit
        // back on behind the configuration's back.
        var layer = builder.GetOrAddLayer(static () => new AuditLayer(new AuditLayerOptions()));
        layer.Options.Enabled = enabled;
        layer.Options.Scope = scope;
        layer.Options.OnSinkFailure = onSinkFailure;
        return builder;
    }
}
