namespace SecretsManager.AwsSecretsManager;

/// <summary>
/// Options for the AWS Secrets Manager secret provider.
/// </summary>
public sealed class AwsSecretsManagerOptions
{
    /// <summary>
    /// AWS region (e.g. "us-east-1", "eu-west-1").
    /// </summary>
    public string Region { get; set; } = "";

    /// <summary>
    /// AWS access key ID. When null, the default credential chain is used.
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>
    /// AWS secret access key.
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Optional session token (for temporary credentials).
    /// </summary>
    public string? SessionToken { get; set; }

    /// <summary>
    /// Validates the options and returns error messages for any missing required fields.
    /// </summary>
    /// <returns>An enumerable of validation error messages; empty if the options are valid.</returns>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Region))
            yield return "Region is required";
    }
}
