import React from 'react';
import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';
import CodeBlock from '@theme/CodeBlock';
import styles from './landing.module.css';

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

# In your C# code:
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
    <section className={`${styles.section} ${styles.sectionAlt}`}>
      <div className={styles.sectionNarrow}>
        <div className={styles.sectionHead}>
          <h2 className={styles.sectionTitle}>Configure your way</h2>
          <p className={styles.sectionLede}>
            Register a provider, choose a configuration source, and build. Every
            source produces the same settings dictionary.
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
