---
id: passbolt
title: Passbolt Provider
sidebar_label: Passbolt
sidebar_position: 5
---

# Passbolt Provider

<span className="provider-badge provider-passbolt">Passbolt</span>
<span className="badge-pill badge-dotnet">.NET 10.0</span>

## Overview

The Passbolt provider integrates with [Passbolt](https://www.passbolt.com/), an open-source password and credential manager built for teams. It communicates via Passbolt's REST API using `HttpClient` with JWT-based authentication and OpenPGP end-to-end encryption.

**Key Benefits:**
- Open-source and self-hosted
- End-to-end encryption using OpenPGP
- Team collaboration with granular sharing
- No vendor lock-in — you control your infrastructure
- JWT-based authentication (no session management needed)

## Installation

```bash title="Terminal"
dotnet add package SecretsManager
dotnet add package SecretsManager.Passbolt
```

## Configuration

### Fluent API

```csharp title="Program.cs"
var provider = new SecretProviderBuilder()
    .WithPassbolt(options =>
    {
        options.BaseUrl = "https://passbolt.example.com";
        options.UserPrivateKey = "-----BEGIN PGP PRIVATE KEY BLOCK-----\n...";
        options.UserPrivateKeyPassphrase = "your-passphrase";
        options.UserKeyFingerprint = "ABCDEF1234567890";
    })
    .Build();
```

### Environment Variables

```bash title="Terminal"
export SECRETS_PROVIDER=passbolt
export SECRETS_PASSBOLT_BASEURL=https://passbolt.example.com
export SECRETS_PASSBOLT_USERPRIVATEKEY="-----BEGIN PGP PRIVATE KEY BLOCK-----..."
export SECRETS_PASSBOLT_USERPRIVATEPASSPHRASE=your-passphrase
export SECRETS_PASSBOLT_USERKEYFINGERPRINT=ABCDEF1234567890
```

### YAML Configuration

```yaml title="secretstore.yaml"
provider:
  passbolt:
    base:
      url: https://passbolt.example.com
    user:
      private:
        key: |
          -----BEGIN PGP PRIVATE KEY BLOCK-----
          <your-armored-private-key>
          -----END PGP PRIVATE KEY BLOCK-----
        passphrase: your-passphrase
      key:
        fingerprint: ABCDEF1234567890
```

## Authentication

Passbolt uses JWT-based authentication with OpenPGP end-to-end encryption. You'll need:

1. **Passbolt instance URL** — the base URL of your Passbolt server
2. **User PGP private key** — your armored OpenPGP private key (exported from the Passbolt extension or GnuPG)
3. **Private key passphrase** — the passphrase that protects your private key
4. **Key fingerprint** — your PGP key fingerprint (uppercase, no spaces)

### Setting Up Your PGP Key

1. Install the Passbolt browser extension and complete the setup
2. Export your private key from the extension settings (Account > Export)
3. Note your key fingerprint from the extension or Passbolt admin panel
4. Use the exported key and fingerprint in your configuration

:::warning
Never commit your PGP private key or passphrase to version control. Use environment variables or a secrets manager for production deployments.
:::

## How It Works

The provider maps Passbolt concepts to the `ISecretProvider` interface:

| ISecretProvider | Passbolt |
|----------------|----------|
| Secret key | Resource name |
| Secret value | Encrypted `password` field within the resource secret |
| Metadata | Resource metadata (username, uri, description) |
| Version | Not natively supported (single version returned) |

### Resource Types

Passbolt uses resource types to define the structure of secrets. The provider automatically discovers the `password-and-description` resource type on first use. This type stores the secret value in an encrypted `password` field.

### Encryption

All secrets are encrypted client-side using your PGP key before being sent to the Passbolt server. The server never sees the plaintext value. When retrieving secrets, the encrypted data is decrypted locally using your private key.

## Usage

```csharp
// Standard ISecretProvider operations work identically
var secret = await provider.GetSecretAsync("my-secret");
await provider.PutSecretAsync("new-secret", "secret-value");
var versions = await provider.GetSecretVersionsAsync("my-secret");
await provider.DeleteSecretAsync("old-secret");
```

:::info[Versioning]
Passbolt does not support native secret versioning. Each update replaces the previous secret value. `GetSecretVersionsAsync` always returns a single version marked as current.
:::

### Configuration via IOptions&lt;T&gt;

You can also bind configuration from `IConfiguration`:

```csharp
var provider = new SecretProviderBuilder()
    .ConfigurePassbolt(configuration)
    .Build();
```

This reads from the `SecretsManager:Passbolt` section and validates all options.
