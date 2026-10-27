import React from 'react';
import {Puzzle, CloudCog, Feather, Clock, FileJson, Sliders} from 'lucide-react';

const features = [
  {
    icon: Puzzle,
    title: 'Unified API',
    description: 'Get, put, version, and delete secrets with one interface, regardless of the backend.',
    color: '#00D4FF',
  },
  {
    icon: CloudCog,
    title: 'Multi-Provider',
    description: 'Swap between Azure, AWS, GCP, Scaleway, PostgreSQL, OVH, Passbolt, Tencent Cloud, Infisical, HashiCorp Vault, CyberArk Conjur, or encrypted local files without changing your business logic.',
    color: '#a78bfa',
  },
  {
    icon: Feather,
    title: 'Zero Bloat',
    description: 'Modular design. Only install the NuGet packages for the providers you actually use.',
    color: '#34d399',
  },
  {
    icon: Clock,
    title: 'Versioning',
    description: 'First-class support for secret history across all providers.',
    color: '#60a5fa',
  },
  {
    icon: FileJson,
    title: 'JSON Extraction',
    description: 'Query nested JSON values directly via dot-path syntax.',
    color: '#fb923c',
  },
  {
    icon: Sliders,
    title: 'Flexible Config',
    description: 'Configure via Fluent API, Environment Variables, or ESO-style YAML.',
    color: '#f472b6',
  },
];

export default function FeaturesSection(): React.JSX.Element {
  return (
    <section className="section-dark" style={{padding: '6rem 0'}}>
      <div style={{maxWidth: '1200px', margin: '0 auto', padding: '0 1.5rem'}}>
        <div style={{textAlign: 'center', marginBottom: '4rem'}}>
          <h2 style={{fontSize: '2.5rem', fontWeight: 700, marginBottom: '1rem'}}>
            Why SecretsManager?
          </h2>
          <p style={{fontSize: '1.25rem', color: 'var(--ifm-color-emphasis-600)'}}>
            A modern approach to secret management in .NET
          </p>
        </div>

        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 320px), 1fr))',
          gap: '1.5rem',
        }}>
          {features.map((feature, i) => {
            const Icon = feature.icon;
            return (
              <div key={i} className="feature-card">
                <Icon style={{width: '2.5rem', height: '2.5rem', marginBottom: '1rem', color: feature.color}} />
                <h3 style={{fontSize: '1.25rem', fontWeight: 600, marginBottom: '0.5rem'}}>
                  {feature.title}
                </h3>
                <p style={{color: 'var(--ifm-color-emphasis-600)', margin: 0}}>
                  {feature.description}
                </p>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}
