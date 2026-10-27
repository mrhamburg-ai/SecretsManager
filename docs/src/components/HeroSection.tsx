import React, {useRef, useState} from 'react';
import Link from '@docusaurus/Link';
import styles from './landing.module.css';

const providers = [
  {
    id: 'filesystem',
    name: 'FileSystem',
    package: 'SecretsManager.FileSystem',
    to: '/docs/providers/filesystem',
    snippet: `var provider = new SecretProviderBuilder()
    .WithFileSystem(options =>
    {
        options.BasePath = "/var/secrets";
        options.EncryptionKey = Environment
            .GetEnvironmentVariable("SECRETS_FS_KEY");
    })
    .Build();`,
  },
  {
    id: 'azure',
    name: 'Azure Key Vault',
    package: 'SecretsManager.AzureKeyVault',
    to: '/docs/providers/azure-key-vault',
    snippet: `var provider = new SecretProviderBuilder()
    .WithAzureKeyVault(options =>
    {
        options.VaultUrl = "https://my-vault.vault.azure.net";
        options.AuthenticationType = "serviceprincipal";
        options.TenantId = tenantId;
        options.ClientId = clientId;
        options.ClientSecret = Environment
            .GetEnvironmentVariable("AZURE_CLIENT_SECRET");
    })
    .Build();`,
  },
  {
    id: 'aws',
    name: 'AWS Secrets Manager',
    package: 'SecretsManager.AwsSecretsManager',
    to: '/docs/providers/aws-secrets-manager',
    snippet: `var provider = new SecretProviderBuilder()
    .WithAwsSecretsManager(options =>
    {
        options.Region = "us-east-1";
        options.AccessKey = Environment
            .GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        options.SecretKey = Environment
            .GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
    })
    .Build();`,
  },
  {
    id: 'vault',
    name: 'HashiCorp Vault',
    package: 'SecretsManager.Vault',
    to: '/docs/providers/vault',
    snippet: `var provider = new SecretProviderBuilder()
    .WithVault(options =>
    {
        options.Url = "http://localhost:8200";
        options.Token = Environment
            .GetEnvironmentVariable("VAULT_TOKEN");
        options.MountPath = "secret";
    })
    .Build();`,
  },
];

function ProviderPanel(): React.JSX.Element {
  const [active, setActive] = useState(0);
  const tabs = useRef<(HTMLButtonElement | null)[]>([]);
  const provider = providers[active];

  const onKeyDown = (event: React.KeyboardEvent) => {
    const step = {ArrowRight: 1, ArrowLeft: -1}[event.key];
    if (!step) {
      return;
    }

    event.preventDefault();
    const next = (active + step + providers.length) % providers.length;
    setActive(next);
    tabs.current[next]?.focus();
  };

  return (
    <figure className={styles.panel} aria-label="Provider configuration example">
      <div className={styles.panelHead}>
        <span className={styles.panelTitle}>Program.cs</span>
        <span className={styles.panelBadge}>{provider.package}</span>
      </div>

      <div className={styles.tabs} role="tablist" aria-label="Provider" onKeyDown={onKeyDown}>
        {providers.map((item, index) => (
          <button
            key={item.id}
            ref={(element) => {
              tabs.current[index] = element;
            }}
            type="button"
            role="tab"
            id={`sm-tab-${item.id}`}
            aria-selected={index === active}
            aria-controls="sm-provider-panel"
            tabIndex={index === active ? 0 : -1}
            className={styles.tab}
            onClick={() => setActive(index)}>
            <span className={`sm-logo sm-logo--${item.id} ${styles.logo}`} aria-hidden="true" />
            {item.name}
          </button>
        ))}
      </div>

      <div
        className={styles.panelBody}
        role="tabpanel"
        id="sm-provider-panel"
        aria-labelledby={`sm-tab-${provider.id}`}>
        <div className={styles.panelMeta}>
          <span>Configure</span>
          <code>{provider.name}</code>
          <span>with one builder</span>
        </div>
        <pre className={styles.code}>
          <span className={styles.codeComment}>{`// ${provider.name}`}</span>
          {'\n'}
          {provider.snippet}
        </pre>
      </div>

      <Link className={styles.panelFoot} to={provider.to}>
        Read the {provider.name} guide →
      </Link>
    </figure>
  );
}

export default function HeroSection(): React.JSX.Element {
  return (
    <section className={styles.hero}>
      <div className={styles.heroInner}>
        <div className={styles.intro}>
          <span className={styles.eyebrow}>SecretsManager · .NET 10</span>
          <h1 className={styles.title}>
            One interface.
            <br />
            <span className={styles.titleAccent}>Any secret backend.</span>
          </h1>
          <p className={styles.lede}>
            A unified .NET abstraction for fifteen secret providers. Read, write,
            version, and delete secrets through one contract, then add retries,
            timeouts, logging, and telemetry without changing your application
            code.
          </p>
          <div className={styles.actions}>
            <Link className={styles.primary} to="/docs/getting-started/installation">
              Get started
            </Link>
            <a className={styles.secondary} href="https://github.com/mrhamburg/SecretsManager">
              View on GitHub
            </a>
          </div>
          <div className={styles.providers}>
            <span className={styles.providersLabel}>Works with</span>
            <ul className={styles.providerList}>
              {providers.map((provider) => (
                <li key={provider.id}>
                  <span
                    className={`sm-logo sm-logo--${provider.id} ${styles.logo}`}
                    aria-hidden="true"
                  />
                  {provider.name}
                </li>
              ))}
            </ul>
          </div>
        </div>

        <ProviderPanel />
      </div>
    </section>
  );
}
