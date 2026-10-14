import React, {useState} from 'react';
import Link from '@docusaurus/Link';
import {Copy, Check} from 'lucide-react';
import {AzureLogo, ScalewayLogo, FileSystemLogo, PostgreSqlLogo, OVHLogo, AwsLogo, OracleLogo, GcpLogo, PassboltLogo, TencentCloudLogo, AliyunLogo, IBMCloudLogo, VaultLogo, ConjurLogo} from './ProviderLogos';

const providers = [
  {id: 'filesystem', name: 'FileSystem', short: 'FS', package: 'SecretsManager.FileSystem'},
  {id: 'azure', name: 'Azure Key Vault', short: 'Azure', package: 'SecretsManager.AzureKeyVault'},
  {id: 'scaleway', name: 'Scaleway', short: 'Scaleway', package: 'SecretsManager.Scaleway'},
  {id: 'ibmcloud', name: 'IBM Cloud', short: 'IBM', package: 'SecretsManager.IBMCloudSecretsManager'},
  {id: 'postgresql', name: 'PostgreSQL', short: 'PG', package: 'SecretsManager.PostgreSql'},
  {id: 'ovh', name: 'OVH', short: 'OVH', package: 'SecretsManager.OVH'},
  {id: 'aws', name: 'AWS Secrets Manager', short: 'AWS', package: 'SecretsManager.AwsSecretsManager'},
  {id: 'oracle', name: 'Oracle Vault', short: 'Oracle', package: 'SecretsManager.OracleVault'},
  {id: 'gcp', name: 'Google Cloud', short: 'GCP', package: 'SecretsManager.GoogleSecretManager'},
  {id: 'passbolt', name: 'Passbolt', short: 'Passbolt', package: 'SecretsManager.Passbolt'},
  {id: 'aliyun', name: 'Aliyun KMS', short: 'Aliyun', package: 'SecretsManager.AliyunKms'},
  {id: 'tencent', name: 'Tencent Cloud', short: 'Tencent', package: 'SecretsManager.TencentCloud'},
  {id: 'vault', name: 'HashiCorp Vault', short: 'Vault', package: 'SecretsManager.Vault'},
  {id: 'conjur', name: 'CyberArk Conjur', short: 'Conjur', package: 'SecretsManager.Conjur'},
];

const providerLogos: Record<string, React.ReactNode> = {
  filesystem: <FileSystemLogo style={{width: '1.5rem', height: '1.5rem'}} />,
  azure: <AzureLogo style={{width: '1.5rem', height: '1.5rem'}} />,
  scaleway: <ScalewayLogo style={{width: '1.5rem', height: '1.5rem'}} />,
  ibmcloud: <IBMCloudLogo style={{width: '1.5rem', height: '1.5rem'}} />,
  postgresql: <PostgreSqlLogo style={{width: '1.5rem', height: '1.5rem'}} />,
  ovh: <OVHLogo style={{width: '1.5rem', height: '1.5rem'}} />,
  aws: <AwsLogo style={{width: '1.5rem', height: '1.5rem'}} />,
  oracle: <OracleLogo style={{width: '1.5rem', height: '1.5rem'}} />,
  gcp: <GcpLogo style={{width: '1.5rem', height: '1.5rem'}} />,
  passbolt: <PassboltLogo style={{width: '1.5rem', height: '1.5rem'}} />,
  aliyun: <AliyunLogo style={{width: '1.5rem', height: '1.5rem'}} />,
  tencent: <TencentCloudLogo style={{width: '1.5rem', height: '1.5rem'}} />,
  vault: <VaultLogo style={{width: '1.5rem', height: '1.5rem'}} />,
  conjur: <ConjurLogo style={{width: '1.5rem', height: '1.5rem'}} />,
};

export default function HeroSection(): React.JSX.Element {
  const [selectedProvider, setSelectedProvider] = useState('filesystem');
  const [copiedStep, setCopiedStep] = useState<number | null>(null);

  const handleCopy = (text: string, step: number) => {
    navigator.clipboard.writeText(text);
    setCopiedStep(step);
    setTimeout(() => setCopiedStep(null), 2000);
  };

  const selectedPackage = providers.find(p => p.id === selectedProvider)?.package;

  return (
    <section className="hero-gradient">
      <div style={{maxWidth: '1200px', margin: '0 auto', padding: '4rem 1.5rem'}}>
        {/* Hero content */}
        <div style={{display: 'grid', gridTemplateColumns: '1fr', gap: '3rem', alignItems: 'center'}}>
          <div style={{display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 450px), 1fr))', gap: '3rem', alignItems: 'center'}}>
            {/* Left: Text */}
            <div>
              <div style={{display: 'flex', flexWrap: 'wrap', gap: '0.5rem', marginBottom: '1.5rem'}}>
                <span className="badge-pill badge-dotnet">.NET 10.0</span>
                <span className="badge-pill badge-license">MIT / Apache 2.0</span>
              </div>

              <h1 style={{fontSize: 'clamp(2.5rem, 5vw, 3.5rem)', fontWeight: 700, marginBottom: '1.5rem', lineHeight: 1.1}}>
                One Interface.{' '}
                <span style={{
                  background: 'linear-gradient(135deg, var(--ifm-color-primary), var(--sm-color-secondary))',
                  WebkitBackgroundClip: 'text',
                  WebkitTextFillColor: 'transparent',
                  backgroundClip: 'text',
                }}>
                  Any Secret Backend.
                </span>
              </h1>

                <p style={{fontSize: '1.25rem', color: 'var(--ifm-color-emphasis-700)', marginBottom: '2rem', lineHeight: 1.6}}>
                A unified .NET 10.0 abstraction layer for 14 secret backends including Azure Key Vault, AWS, Google Cloud, Oracle Vault, PostgreSQL, Scaleway, OVH, IBM Cloud, Aliyun KMS, Passbolt, Tencent Cloud, HashiCorp Vault, CyberArk Conjur, and local encrypted storage. Inspired by Kubernetes External Secrets.
                </p>

              <div style={{display: 'flex', flexWrap: 'wrap', gap: '1rem'}}>
                <Link className="btn-primary" to="/docs/intro">
                  Get Started
                </Link>
                <a className="btn-outline" href="https://github.com/mrhamburg/SecretsManager">
                  View on GitHub
                </a>
              </div>
            </div>

            {/* Right: Visual */}
            <div className="diagram-box">
              <div style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '0.75rem',
                padding: '0.5rem 1rem',
                borderRadius: '0.5rem',
                background: 'rgba(81, 43, 212, 0.2)',
                border: '1px solid rgba(81, 43, 212, 0.3)',
                marginBottom: '1rem',
              }}>
                <code style={{color: 'var(--sm-color-secondary)', fontFamily: 'var(--ifm-font-family-monospace)', fontSize: '0.9rem'}}>
                  ISecretProvider
                </code>
              </div>

              <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(6, 1fr)',
                gap: '0.5rem',
                padding: '1rem',
                borderRadius: '0.5rem',
                background: 'rgba(81, 43, 212, 0.05)',
                border: '1px solid rgba(81, 43, 212, 0.15)',
              }}>
                {providers.map(p => (
                  <div
                    key={p.id}
                    style={{
                      display: 'flex',
                      flexDirection: 'column',
                      alignItems: 'center',
                      gap: '0.25rem',
                      padding: '0.5rem',
                      borderRadius: '0.375rem',
                      background: 'rgba(255, 255, 255, 0.5)',
                      border: '1px solid rgba(81, 43, 212, 0.1)',
                    }}
                    title={p.name}
                  >
                    {providerLogos[p.id]}
                    <span style={{fontSize: '0.625rem', color: 'var(--ifm-color-emphasis-600)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', maxWidth: '100%'}}>
                      {p.short}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>

        {/* Installation Section */}
        <div style={{marginTop: '3rem', display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 350px), 1fr))', gap: '1rem'}}>
          {/* Step 1: Base package */}
          <div className="install-box">
            <div style={{display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem'}}>
              <span style={{
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                width: '1.5rem', height: '1.5rem', borderRadius: '50%',
                background: 'var(--ifm-color-primary)', color: '#fff',
                fontSize: '0.75rem', fontWeight: 700,
              }}>1</span>
              <span style={{fontSize: '0.875rem', fontWeight: 600}}>Install Base Package</span>
            </div>
            <code style={{color: 'var(--sm-color-secondary)', fontFamily: 'var(--ifm-font-family-monospace)', fontSize: '0.875rem'}}>
              dotnet add package SecretsManager.Core
            </code>
            <button
              className="copy-btn"
              onClick={() => handleCopy('dotnet add package SecretsManager', 1)}
              aria-label="Copy command"
            >
              {copiedStep === 1
                ? <Check style={{width: '1rem', height: '1rem', color: '#34d399'}} />
                : <Copy style={{width: '1rem', height: '1rem'}} />}
            </button>
          </div>

          {/* Step 2: Provider */}
          <div className="install-box">
            <div style={{display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem'}}>
              <span style={{
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                width: '1.5rem', height: '1.5rem', borderRadius: '50%',
                background: 'var(--ifm-color-primary)', color: '#fff',
                fontSize: '0.75rem', fontWeight: 700,
              }}>2</span>
              <span style={{fontSize: '0.875rem', fontWeight: 600}}>Select a Provider</span>
            </div>
            <div style={{display: 'flex', flexWrap: 'wrap', gap: '0.5rem', marginBottom: '0.75rem'}}>
              {providers.map(p => (
                <button
                  key={p.id}
                  className={`provider-selector-btn ${selectedProvider === p.id ? 'provider-selector-btn--active' : 'provider-selector-btn--inactive'}`}
                  onClick={() => setSelectedProvider(p.id)}
                >
                  {p.name}
                </button>
              ))}
            </div>
            <code style={{color: 'var(--sm-color-secondary)', fontFamily: 'var(--ifm-font-family-monospace)', fontSize: '0.875rem'}}>
              dotnet add package {selectedPackage}
            </code>
            <button
              className="copy-btn"
              onClick={() => handleCopy(`dotnet add package ${selectedPackage}`, 2)}
              aria-label="Copy command"
            >
              {copiedStep === 2
                ? <Check style={{width: '1rem', height: '1rem', color: '#34d399'}} />
                : <Copy style={{width: '1rem', height: '1rem'}} />}
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}
