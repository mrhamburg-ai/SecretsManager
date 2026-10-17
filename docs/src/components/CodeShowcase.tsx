import React from 'react';
import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';
import CodeBlock from '@theme/CodeBlock';

const fluentCode = `var provider = new SecretProviderBuilder()
    .WithFileSystem(options =>
    {
        options.BasePath = "/etc/secrets";
        options.EncryptionKey = "my-encryption-key";
    })
    .Build();

var secret = await provider.GetSecretAsync("db-password");
Console.WriteLine(secret.Value);`;

const ioptionsJson = `{
  "SecretsManager": {
    "FileSystem": {
      "BasePath": "/etc/secrets",
      "EncryptionKey": "base64-encoded-key"
    }
  }
}`;

const ioptionsCode = `var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSecretsManager();
builder.Services.Configure<FileSystemOptions>(
    builder.Configuration.GetSection("SecretsManager:FileSystem"));

// Or bind and build in one step
var provider = new SecretProviderBuilder()
    .ConfigureFileSystem(builder.Configuration)
    .Build();

var secret = await provider.GetSecretAsync("db-password");`;

const envCode = `export SECRETS_PROVIDER=filesystem
export SECRETS_FILESYSTEM_PATH=/etc/secrets
export SECRETS_FILESYSTEM_ENCRYPTION_KEY=my-key

// In your C# code:
var provider = new SecretProviderBuilder()
    .WithFileSystem()
    .FromEnvironment()
    .Build();

var secret = await provider.GetSecretAsync("db-password");`;

const yamlCode = `# secretstore.yaml
provider:
  filesystem:
    path: /etc/secrets
    encryption:
      key: base64encodedkey`;

const yamlCsharpCode = `var provider = new SecretProviderBuilder()
    .WithFileSystem()
    .FromYamlFile("secretstore.yaml")
    .Build();`;

export default function CodeShowcase(): React.JSX.Element {
  return (
    <section className="section-main" style={{padding: '6rem 0'}}>
      <div style={{maxWidth: '900px', margin: '0 auto', padding: '0 1.5rem'}}>
        <div style={{textAlign: 'center', marginBottom: '3rem'}}>
          <h2 style={{fontSize: '2.5rem', fontWeight: 700, marginBottom: '1rem'}}>
            Configure Your Way
          </h2>
          <p style={{fontSize: '1.25rem', color: 'var(--ifm-color-emphasis-600)'}}>
            Choose the configuration style that fits your workflow
          </p>
        </div>

        <Tabs>
          <TabItem value="fluent" label="Fluent API" default>
            <CodeBlock language="csharp" title="Program.cs">
              {fluentCode}
            </CodeBlock>
          </TabItem>
          <TabItem value="ioptions" label={'IOptions<T>'}>
            <CodeBlock language="json" title="appsettings.json">
              {ioptionsJson}
            </CodeBlock>
            <CodeBlock language="csharp" title="Program.cs">
              {ioptionsCode}
            </CodeBlock>
          </TabItem>
          <TabItem value="env" label="Environment">
            <CodeBlock language="bash" title="Terminal">
              {envCode}
            </CodeBlock>
          </TabItem>
          <TabItem value="yaml" label="YAML">
            <CodeBlock language="yaml" title="secretstore.yaml">
              {yamlCode}
            </CodeBlock>
            <CodeBlock language="csharp" title="Program.cs">
              {yamlCsharpCode}
            </CodeBlock>
          </TabItem>
        </Tabs>
      </div>
    </section>
  );
}
