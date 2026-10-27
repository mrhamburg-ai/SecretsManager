import React, {useCallback, useEffect, useRef, useState} from 'react';
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
        options.TenantId = "your-tenant-id";
        options.ClientId = "your-client-id";
        options.ClientSecret = Environment
            .GetEnvironmentVariable("AZURE_CLIENT_SECRET");
    })
    .Build();`,
  },
  {
    id: 'scaleway',
    name: 'Scaleway',
    package: 'SecretsManager.Scaleway',
    to: '/docs/providers/scaleway',
    snippet: `var provider = new SecretProviderBuilder()
    .WithScaleway(options =>
    {
        options.Region = "fr-par";
        options.ProjectId = "your-project-uuid";
        options.AccessKey = "your-access-key";
        options.SecretKey = Environment
            .GetEnvironmentVariable("SECRETS_SCALEWAY_SECRETKEY");
    })
    .Build();`,
  },
  {
    id: 'ovh',
    name: 'OVH',
    package: 'SecretsManager.OVH',
    to: '/docs/providers/ovh',
    snippet: `var provider = new SecretProviderBuilder()
    .WithOVH(options =>
    {
        options.Endpoint = "ovh-eu";
        options.ApplicationKey = "your-application-key";
        options.ApplicationSecret = Environment
            .GetEnvironmentVariable("SECRETS_OVH_APPLICATIONSECRET");
        options.ConsumerKey = "your-consumer-key";
        options.OkmsId = "your-okms-id";
    })
    .Build();`,
  },
  {
    id: 'postgresql',
    name: 'PostgreSQL',
    package: 'SecretsManager.PostgreSql',
    to: '/docs/providers/postgresql',
    snippet: `var provider = new SecretProviderBuilder()
    .WithPostgreSql(options =>
    {
        options.ConnectionString = "Host=localhost;Database=secrets;Username=app;Password=secret";
        options.Schema = "public";
        options.EncryptionKey = "base64-encoded-32-byte-key";
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
    id: 'oracle',
    name: 'Oracle Vault',
    package: 'SecretsManager.OracleVault',
    to: '/docs/providers/oracle-vault',
    snippet: `var provider = new SecretProviderBuilder()
    .WithOracleVault(options =>
    {
        options.Region = "us-phoenix-1";
        options.AuthenticationType = "configfile";
        options.ProfileName = "DEFAULT";
        options.VaultId = "ocid1.vault.oc1..xxxxx";
        options.CompartmentId = "ocid1.compartment.oc1..xxxxx";
    })
    .Build();`,
  },
  {
    id: 'gcp',
    name: 'Google Cloud Secret Manager',
    package: 'SecretsManager.GoogleSecretManager',
    to: '/docs/providers/google-secret-manager',
    snippet: `var provider = new SecretProviderBuilder()
    .WithGoogleSecretManager(options =>
    {
        options.ProjectId = "my-gcp-project";
        options.CredentialsPath = "/path/to/service-account-key.json";
    })
    .Build();`,
  },
  {
    id: 'passbolt',
    name: 'Passbolt',
    package: 'SecretsManager.Passbolt',
    to: '/docs/providers/passbolt',
    snippet: `var provider = new SecretProviderBuilder()
    .WithPassbolt(options =>
    {
        options.BaseUrl = "https://passbolt.example.com";
        options.UserPrivateKey = Environment
            .GetEnvironmentVariable("SECRETS_PASSBOLT_USERPRIVATEKEY");
        options.UserPrivateKeyPassphrase = Environment
            .GetEnvironmentVariable("SECRETS_PASSBOLT_USERPRIVATEPASSPHRASE");
        options.UserKeyFingerprint = "ABCDEF1234567890";
    })
    .Build();`,
  },
  {
    id: 'aliyun',
    name: 'Aliyun KMS',
    package: 'SecretsManager.AliyunKms',
    to: '/docs/providers/aliyun-kms',
    snippet: `var provider = new SecretProviderBuilder()
    .WithAliyunKms(options =>
    {
        options.Region = "cn-hangzhou";
        options.AccessKeyId = "LTAI5t...";
        options.AccessKeySecret = Environment
            .GetEnvironmentVariable("SECRETS_ALIYUNKMS_SECRETKEY");
    })
    .Build();`,
  },
  {
    id: 'tencent',
    name: 'Tencent Cloud',
    package: 'SecretsManager.TencentCloud',
    to: '/docs/providers/tencent-cloud',
    snippet: `var provider = new SecretProviderBuilder()
    .WithTencentCloud(options =>
    {
        options.Region = "ap-beijing";
        options.SecretId = "AKID123...";
        options.SecretKey = Environment
            .GetEnvironmentVariable("SECRETS_TENCENTCLOUD_SECRETKEY");
    })
    .Build();`,
  },
  {
    id: 'ibm',
    name: 'IBM Cloud Secrets Manager',
    package: 'SecretsManager.IBMCloudSecretsManager',
    to: '/docs/providers/ibm-cloud-secrets-manager',
    snippet: `var provider = new SecretProviderBuilder()
    .WithIBMCloudSecretsManager(options =>
    {
        options.Region = "us-south";
        options.InstanceId = "your-instance-id";
        options.ApiKey = Environment
            .GetEnvironmentVariable("SECRETS_IBMCLOUD_SECRETS_MANAGER_API_KEY");
    })
    .Build();`,
  },
  {
    id: 'infisical',
    name: 'Infisical',
    package: 'SecretsManager.Infisical',
    to: '/docs/providers/infisical',
    snippet: `var provider = new SecretProviderBuilder()
    .WithInfisical(options =>
    {
        options.HostUri = "https://infisical.example.com";
        options.ClientId = "your-machine-identity-client-id";
        options.ClientSecret = Environment
            .GetEnvironmentVariable("SECRETS_INFISICAL_AUTH_CLIENT_SECRET");
        options.ProjectId = "your-project-id";
        options.EnvironmentSlug = "dev";
        options.SecretPath = "/";
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
  {
    id: 'conjur',
    name: 'CyberArk Conjur',
    package: 'SecretsManager.Conjur',
    to: '/docs/providers/conjur',
    snippet: `var provider = new SecretProviderBuilder()
    .WithConjur(options =>
    {
        options.Url = "https://conjur.example.com:443";
        options.Account = "myorg";
        options.Login = "host/ci-app";
        options.ApiKey = Environment
            .GetEnvironmentVariable("SECRETS_CONJUR_APIKEY");
        options.PolicyPath = "root";
    })
    .Build();`,
  },
];

const TAB_GAP = 4; // 0.25rem flex gap between tabs
const MORE_RESERVE = 48; // px kept aside for the "…" overflow button

function ProviderPanel(): React.JSX.Element {
  const [active, setActive] = useState(0);
  const [overflowIds, setOverflowIds] = useState<string[]>([]);
  const [menuOpen, setMenuOpen] = useState(false);
  const tabsRef = useRef<HTMLDivElement>(null);
  const shellRef = useRef<HTMLDivElement>(null);
  const moreRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLUListElement>(null);
  const tabRefs = useRef<Record<string, HTMLButtonElement | null>>({});
  const widths = useRef<Record<string, number>>({});

  const visibleIds = providers.filter((p) => !overflowIds.includes(p.id)).map((p) => p.id);

  const measure = useCallback(() => {
    const container = tabsRef.current;
    if (!container) {
      return;
    }

    // Cache each provider's width from the laid-out tab bar.
    for (const btn of Array.from(
      container.querySelectorAll<HTMLButtonElement>('button[data-provider]'),
    )) {
      const id = btn.dataset.provider;
      if (id && btn.offsetWidth > 0) {
        widths.current[id] = btn.offsetWidth;
      }
    }

    const total = providers.length;
    const contentWidth = container.clientWidth - 32; // 2 × 1rem padding
    const available = contentWidth - MORE_RESERVE;
    const widthAt = (index: number) => widths.current[providers[index].id] ?? 0;

    // Longest leading run of tabs that fits on its own.
    let used = 0;
    let prefix = 0;
    for (; prefix < total; prefix++) {
      const w = widthAt(prefix);
      if (!w || used + w > available) {
        break;
      }
      used += w + TAB_GAP;
    }

    let visible: string[];
    if (active < prefix) {
      visible = providers.slice(0, prefix).map((p) => p.id);
    } else {
      // Pin the active tab, then pack as many leading tabs beside it.
      const prefixAvailable = available - (widthAt(active) + TAB_GAP);
      let packed = 0;
      let usedPrefix = 0;
      for (; packed < total; packed++) {
        if (packed === active) {
          continue;
        }
        const w = widthAt(packed);
        if (!w || usedPrefix + w > prefixAvailable) {
          break;
        }
        usedPrefix += w + TAB_GAP;
      }
      visible = [
        ...providers
          .slice(0, packed)
          .filter((_, i) => i !== active)
          .map((p) => p.id),
        providers[active].id,
      ];
    }

    const hidden = providers.filter((p) => !visible.includes(p.id)).map((p) => p.id);
    setOverflowIds((prev) =>
      prev.length === hidden.length && prev.every((id, i) => id === hidden[i]) ? prev : hidden,
    );
  }, [active]);

  useEffect(() => {
    measure();
    const container = tabsRef.current;
    if (!container) {
      return;
    }
    let observer: ResizeObserver | undefined;
    if (typeof ResizeObserver !== 'undefined') {
      observer = new ResizeObserver(() => measure());
      observer.observe(container);
    }
    const onLoad = () => measure();
    window.addEventListener('load', onLoad);
    document.fonts?.ready.then(() => measure()).catch(() => {});
    return () => {
      observer?.disconnect();
      window.removeEventListener('load', onLoad);
    };
  }, [measure]);

  useEffect(() => {
    if (menuOpen) {
      menuRef.current?.querySelector<HTMLButtonElement>('button')?.focus();
    }
  }, [menuOpen]);

  useEffect(() => {
    if (!menuOpen) {
      return;
    }
    const onPointerDown = (event: PointerEvent) => {
      if (!shellRef.current?.contains(event.target as Node)) {
        setMenuOpen(false);
      }
    };
    document.addEventListener('pointerdown', onPointerDown);
    return () => document.removeEventListener('pointerdown', onPointerDown);
  }, [menuOpen]);

  const onTabsKeyDown = (event: React.KeyboardEvent) => {
    const step = {ArrowRight: 1, ArrowLeft: -1}[event.key];
    if (!step) {
      return;
    }
    event.preventDefault();
    const position = visibleIds.indexOf(providers[active].id);
    const next = (position + step + visibleIds.length) % visibleIds.length;
    const target = providers.findIndex((p) => p.id === visibleIds[next]);
    setActive(target);
    tabRefs.current[visibleIds[next]]?.focus();
  };

  const onMenuKeyDown = (event: React.KeyboardEvent) => {
    const items = Array.from(menuRef.current?.querySelectorAll<HTMLButtonElement>('li > button') ?? []);
    const position = items.indexOf(document.activeElement as HTMLButtonElement);
    switch (event.key) {
      case 'Escape':
        event.preventDefault();
        setMenuOpen(false);
        moreRef.current?.focus();
        break;
      case 'ArrowDown':
        event.preventDefault();
        items[(position + 1) % items.length]?.focus();
        break;
      case 'ArrowUp':
        event.preventDefault();
        items[(position - 1 + items.length) % items.length]?.focus();
        break;
      case 'Home':
        event.preventDefault();
        items[0]?.focus();
        break;
      case 'End':
        event.preventDefault();
        items[items.length - 1]?.focus();
        break;
    }
  };

  const provider = providers[active];

  return (
    <figure className={styles.panel} aria-label="Provider configuration example">
      <div className={styles.panelHead}>
        <span className={styles.panelTitle}>Program.cs</span>
        <span className={styles.panelBadge}>{provider.package}</span>
      </div>

      <div
        className={styles.tabsShell}
        ref={shellRef}
        onBlur={(event) => {
          if (!event.currentTarget.contains(event.relatedTarget as Node)) {
            setMenuOpen(false);
          }
        }}>
        <div
          className={styles.tabs}
          role="tablist"
          aria-label="Provider"
          ref={tabsRef}
          onKeyDown={onTabsKeyDown}>
          {providers.map((item, index) => (
            <button
              key={item.id}
              ref={(element) => {
                tabRefs.current[item.id] = element;
              }}
              data-provider={item.id}
              type="button"
              role="tab"
              id={`sm-tab-${item.id}`}
              aria-selected={index === active}
              aria-controls="sm-provider-panel"
              tabIndex={index === active ? 0 : -1}
              className={styles.tab}
              hidden={overflowIds.includes(item.id)}
              onClick={() => setActive(index)}>
              <span className={`sm-logo sm-logo--${item.id} ${styles.logo}`} aria-hidden="true" />
              {item.name}
            </button>
          ))}
          {overflowIds.length > 0 && (
            <button
              ref={moreRef}
              type="button"
              className={styles.more}
              aria-label="More providers"
              aria-haspopup="menu"
              aria-expanded={menuOpen}
              onClick={() => setMenuOpen((open) => !open)}>
              <span aria-hidden="true">…</span>
            </button>
          )}
        </div>

        {menuOpen && overflowIds.length > 0 && (
          <ul
            className={styles.moreMenu}
            role="menu"
            aria-label="More providers"
            ref={menuRef}
            onKeyDown={onMenuKeyDown}>
            {overflowIds.map((id) => {
              const item = providers.find((p) => p.id === id)!;
              return (
                <li key={item.id} role="menuitem" className={styles.moreItem}>
                  <button
                    type="button"
                    onClick={() => {
                      setActive(providers.findIndex((p) => p.id === item.id));
                      setMenuOpen(false);
                      moreRef.current?.focus();
                    }}>
                    <span className={`sm-logo sm-logo--${item.id} ${styles.logo}`} aria-hidden="true" />
                    {item.name}
                  </button>
                </li>
              );
            })}
          </ul>
        )}
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
