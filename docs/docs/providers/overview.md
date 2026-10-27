---
id: overview
title: Providers Overview
sidebar_label: Overview
sidebar_position: 1
---

# Providers Overview

SecretsManager supports fifteen production-ready providers, each designed for different use cases and environments.

## Available Providers

<div style={{display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 280px), 1fr))', gap: '1.5rem', margin: '2rem 0'}}>
  <div className="feature-card provider-card-fs" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{color: '#10B981', fontWeight: 600, fontSize: '1.125rem', marginBottom: '0.5rem'}}>FileSystem</div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      Local encrypted storage using AES-256-GCM. Perfect for development, edge devices, and air-gapped environments.
    </p>
    <a href="/docs/providers/filesystem" style={{color: '#10B981', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-azure" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem'}}>
      <img src="/img/azure-logo.svg" alt="Azure" style={{width: '1.25rem', height: '1.25rem'}} />
      <span style={{color: '#0078D4', fontWeight: 600, fontSize: '1.125rem'}}>Azure Key Vault</span>
    </div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      Enterprise-grade secret management with Microsoft Azure. Supports HSM-backed keys and advanced access policies.
    </p>
    <a href="/docs/providers/azure-key-vault" style={{color: '#0078D4', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-scaleway" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem'}}>
      <img src="/img/scaleway-logo.svg" alt="Scaleway" style={{width: '1.25rem', height: '1.25rem'}} />
      <span style={{color: '#4F0599', fontWeight: 600, fontSize: '1.125rem'}}>Scaleway</span>
    </div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      European cloud provider with GDPR-compliant storage. Competitive pricing and excellent performance.
    </p>
    <a href="/docs/providers/scaleway" style={{color: '#FF5900', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-ovh" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem'}}>
      <img src="/img/ovh-logo.svg" alt="OVH" style={{width: '1.25rem', height: '1.25rem'}} />
      <span style={{color: '#10B981', fontWeight: 600, fontSize: '1.125rem'}}>OVH</span>
    </div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      European cloud provider with GDPR-compliant storage. Secure OAuth-based authentication.
    </p>
    <a href="/docs/providers/ovh" style={{color: '#10B981', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-pg" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{color: '#336791', fontWeight: 600, fontSize: '1.125rem', marginBottom: '0.5rem'}}>PostgreSQL</div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      Store secrets in your existing PostgreSQL database with optional AES-256-GCM encryption at rest. Simple, self-hosted, and version-tracked.
    </p>
    <a href="/docs/providers/postgresql" style={{color: '#336791', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-aws" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem'}}>
      <img src="/img/aws-logo.svg" alt="AWS" style={{width: '1.25rem', height: '1.25rem'}} />
      <span style={{color: '#FF9900', fontWeight: 600, fontSize: '1.125rem'}}>AWS Secrets Manager</span>
    </div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      Fully managed secret management with IAM integration, automatic rotation, and multi-Region replication.
    </p>
    <a href="/docs/providers/aws-secrets-manager" style={{color: '#FF9900', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-oracle" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem'}}>
      <img src="/img/oracle-logo.svg" alt="Oracle" style={{width: '1.25rem', height: '1.25rem'}} />
      <span style={{color: '#F80000', fontWeight: 600, fontSize: '1.125rem'}}>Oracle Vault</span>
    </div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      Enterprise secret management on Oracle Cloud Infrastructure with native SDK integration and multiple auth modes.
    </p>
    <a href="/docs/providers/oracle-vault" style={{color: '#F80000', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-gcp" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{color: '#4285F4', fontWeight: 600, fontSize: '1.125rem', marginBottom: '0.5rem'}}>Google Cloud Secret Manager</div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      Fully managed secret storage with IAM integration, automatic versioning, and seamless ADC authentication.
    </p>
    <a href="/docs/providers/google-secret-manager" style={{color: '#4285F4', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-passbolt" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{color: '#F58220', fontWeight: 600, fontSize: '1.125rem', marginBottom: '0.5rem'}}>Passbolt</div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      Open-source password manager with end-to-end OpenPGP encryption. Self-hosted, team-oriented, and fully auditable.
    </p>
    <a href="/docs/providers/passbolt" style={{color: '#F58220', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-aliyun" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{color: '#FF6A00', fontWeight: 600, fontSize: '1.125rem', marginBottom: '0.5rem'}}>Aliyun KMS</div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      Alibaba Cloud's fully managed secret management with server-side encryption, automatic rotation, and global regional availability.
    </p>
    <a href="/docs/providers/aliyun-kms" style={{color: '#FF6A00', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-tencent" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{color: '#007AFF', fontWeight: 600, fontSize: '1.125rem', marginBottom: '0.5rem'}}>Tencent Cloud</div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      Tencent Cloud's fully managed secret management with server-side encryption, automatic versioning, and regional availability.
    </p>
    <a href="/docs/providers/tencent-cloud" style={{color: '#007AFF', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-ibm" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{color: '#000000', fontWeight: 600, fontSize: '1.125rem', marginBottom: '0.5rem'}}>IBM Cloud Secrets Manager</div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      Fully managed secret storage with IAM integration, automatic versioning, and seamless authentication.
    </p>
    <a href="/docs/providers/ibm-cloud-secrets-manager" style={{color: '#000000', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-infisical" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{color: '#EAB308', fontWeight: 600, fontSize: '1.125rem', marginBottom: '0.5rem'}}>Infisical</div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      Open-source secrets management with self-hosting, machine identity authentication, and secret version history.
    </p>
    <a href="/docs/providers/infisical" style={{color: '#EAB308', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-vault" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem'}}>
      <img src="/img/vault-logo.svg" alt="Vault" style={{width: '1.25rem', height: '1.25rem'}} />
      <span style={{color: '#000000', fontWeight: 600, fontSize: '1.125rem'}}>HashiCorp Vault</span>
    </div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      Industry-standard secret management with KV v2 versioning, fine-grained policies, and audit logging.
    </p>
    <a href="/docs/providers/vault" style={{color: '#000000', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>

  <div className="feature-card provider-card-conjur" style={{padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid var(--sm-line)'}}>
    <div style={{color: '#007CFF', fontWeight: 600, fontSize: '1.125rem', marginBottom: '0.5rem'}}>CyberArk Conjur</div>
    <p style={{fontSize: '0.875rem', marginBottom: '0.75rem'}}>
      Open-source machine identity secrets management with policy-based access control and built-in versioning.
    </p>
    <a href="/docs/providers/conjur" style={{color: '#007CFF', fontSize: '0.875rem'}}>Learn more &rarr;</a>
  </div>
</div>

## Feature Comparison

All providers implement the `ISecretProvider` interface, but each has unique capabilities:

| Feature | FileSystem | Azure Key Vault | Scaleway | OVH | PostgreSQL | AWS Secrets Manager | Oracle Vault | Google Cloud Secret Manager | Passbolt | Aliyun KMS | Tencent Cloud | IBM Cloud Secrets Manager | Infisical | HashiCorp Vault | CyberArk Conjur |
|---------|:----------:|:---------------:|:--------:|:---:|:----------:|:-------------------:|:------------:|:---------------------------:|:--------:|:----------:|:-------------:|:-------------------------:|:---------:|:--------------:|:--------------:|
| Versioning | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | No | Yes | Yes | Yes | Yes | Yes | Yes |
| Encryption | AES-256-GCM | HSM-backed | Managed | Managed | AES-256-GCM | AWS KMS | OCI KMS | Google Cloud KMS | OpenPGP | KMS-managed | KMS-managed | Managed | Managed | Storage-backed | Managed |
| Auth Types | File Permissions | Managed Identity, Service Principal, Azure CLI | API Key, IAM | OAuth | Connection String | IAM, Access Keys, Instance Profile | Config File, Instance Principal, Security Token | ADC, Service Account Key | JWT + PGP | AccessKey | AccessKey | IAM | Universal Auth | Token, AppRole, OIDC, K8s | API Key |
| Pricing | Free | $0.03 / 10K ops | Free (included) | Free (included) | Self-hosted | $0.40 / secret / month | Pay-per-use | $0.06 / 10K ops | Free (self-hosted) | Pay-per-use | Pay-per-use | $0.40 / secret / month | Free (self-hosted) / Cloud plans | Free (self-hosted) | Free (self-hosted) |
| Region Control | Local | Azure Regions | EU (fr-par, nl-ams) | EU (fr-par, nl-ams) | Your infrastructure | AWS Global Regions | OCI Regions | Google Cloud Regions | Your server | Alibaba Cloud Global | Tencent Cloud Global | IBM Cloud Global | Your infrastructure / Infisical Cloud | Your infrastructure | Your infrastructure |

:::info[Provider Selection]
Choose your provider based on your deployment environment, compliance requirements, and budget. You can also use multiple providers in the same application for different secret types.
:::

## Choosing a Provider

### Use FileSystem when:

- Developing locally and need a simple, zero-configuration solution
- Deploying to edge devices or air-gapped environments
- You need full control over encryption keys and storage

### Use Azure Key Vault when:

- Your infrastructure is already on Azure
- You need enterprise features like HSM-backed keys or advanced RBAC
- Compliance requires Azure Government or similar certifications

### Use PostgreSQL when:

- You want to use your existing PostgreSQL infrastructure
- You need full control over where secrets are stored (self-hosted)
- You want encrypted secrets without a cloud dependency
- Your team already manages PostgreSQL databases

### Use Scaleway when:

- Your data must remain in Europe (GDPR compliance)
- You want competitive pricing compared to major cloud providers
- You're using Scaleway for other infrastructure services

### Use OVH when:

- Your data must remain in Europe (GDPR compliance)
- You prefer OAuth-based authentication
- You're using OVH for other infrastructure services

### Use AWS Secrets Manager when:

- Your infrastructure is already on AWS
- You need automatic secret rotation for database credentials
- You want fine-grained access control via IAM policies
- You need multi-Region secret replication

### Use Oracle Vault when:

- Your infrastructure is already on Oracle Cloud Infrastructure
- You need enterprise-grade secret management with OCI KMS integration
- You prefer config file or instance principal authentication
- You're using Oracle Cloud for other infrastructure services

### Use Google Cloud Secret Manager when:

- Your infrastructure is already on Google Cloud
- You want seamless authentication via Application Default Credentials
- You need fine-grained access control via Cloud IAM
- You prefer per-operation pricing over per-secret pricing

### Use Passbolt when:

- You want an open-source, self-hosted solution
- Your team needs collaborative password management
- You require end-to-end OpenPGP encryption
- You prefer full control over your secret infrastructure
- You're already using Passbolt for team credential management

### Use Aliyun KMS when:

- Your infrastructure is already on Alibaba Cloud
- You need a fully managed secret service with server-side encryption
- You operate in regions where Alibaba Cloud has data centers (Asia-Pacific, Middle East, Europe)
- You want native integration with other Alibaba Cloud services (RDS, ECS, RAM)

### Use Tencent Cloud when:

- Your infrastructure is already on Tencent Cloud
- You need a fully managed secret service with server-side encryption
- You operate in regions where Tencent Cloud has data centers (Asia-Pacific, Southeast Asia)
- You want native integration with other Tencent Cloud services (CVM, VPC, CAM)

### Use IBM Cloud Secrets Manager when:

- Your infrastructure is already on IBM Cloud
- You need enterprise features like IAM integration and audit logging
- You want fully managed encryption with built-in secret lifecycle management
- You prefer global availability across multiple regions
- You need integration with IBM Cloud Event Notifications

### Use Infisical when:

- You want an open-source secrets platform that you can self-host
- You need machine identity authentication with project-scoped roles
- You want secret version history and per-secret metadata
- Your team already uses Infisical for application secrets

### Use HashiCorp Vault when:

- You want a self-hosted, industry-standard secret management solution
- You need built-in KV versioning and dynamic secrets
- You require fine-grained access control policies and audit logging
- You need support for multiple authentication methods (tokens, AppRole, OIDC, Kubernetes)
- Your team already runs Vault in your infrastructure

### Use CyberArk Conjur when:

- You want an open-source, self-hosted solution for machine identities and CI/CD pipelines
- You need version-controlled secrets with policy-based privileges
- You want granular, policy-driven access control for hosts and applications
- Your team already runs Conjur in your infrastructure

:::tip[Multi-Provider Support]
You can configure multiple providers in the same application. For example, use FileSystem for local development and IBM Cloud Secrets Manager for production by switching based on environment variables.
:::
