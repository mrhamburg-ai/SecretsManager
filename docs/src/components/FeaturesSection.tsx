import React from 'react';
import Link from '@docusaurus/Link';
import styles from './landing.module.css';

const features = [
  {
    title: 'Unified API',
    description:
      'Read, write, version, and delete secrets through one contract, whichever backend holds the value.',
  },
  {
    title: 'Fifteen providers',
    description:
      'Use Azure, AWS, Google Cloud, Vault, PostgreSQL, or encrypted local files without changing your application logic.',
  },
  {
    title: 'Modular packages',
    description:
      'The core package has no provider dependencies. Install only the provider packages your application uses.',
  },
  {
    title: 'Versioning',
    description:
      'First-class secret history across providers, with a common version identifier and retrieval model.',
  },
  {
    title: 'JSON extraction',
    description:
      'Query nested JSON values directly with dot-path syntax instead of parsing the whole secret in every caller.',
  },
  {
    title: 'Operational layers',
    description:
      'Add retries, timeouts, concurrency limits, logging, and OpenTelemetry without modifying provider code.',
  },
];

export default function FeaturesSection(): React.JSX.Element {
  return (
    <section className={styles.section} id="features">
      <div className={styles.sectionInner}>
        <div className={styles.sectionHead}>
          <h2 className={styles.sectionTitle}>One contract, every provider</h2>
          <p className={styles.sectionLede}>
            Secret management that stays portable when the backend or the cloud
            changes.
          </p>
        </div>

        <div className={styles.grid}>
          {features.map((feature, index) => (
            <article key={feature.title} className={styles.card}>
              <span className={styles.cardIndex}>
                {String(index + 1).padStart(2, '0')}
              </span>
              <h3 className={styles.cardTitle}>{feature.title}</h3>
              <p className={styles.cardText}>{feature.description}</p>
            </article>
          ))}
        </div>

        <div className={styles.quickStart}>
          <span className={styles.quickLabel}>Start with the core package</span>
          <div className={styles.quickCommand}>
            <span className={styles.prompt}>$</span>
            <code>dotnet add package SecretsManager.Core</code>
          </div>
          <div>
            <Link to="/docs/getting-started/installation">Installation guide →</Link>
          </div>
        </div>
      </div>
    </section>
  );
}
