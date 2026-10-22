import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  docsSidebar: [
    'intro',
    {
      type: 'category',
      label: 'Getting Started',
      items: [
        'getting-started/quick-start',
        'getting-started/configuration',
        'getting-started/installation',
      ],
    },
    {
      type: 'category',
      label: 'Providers',
      items: [
        'providers/overview',
        'providers/filesystem',
        'providers/azure-key-vault',
        'providers/scaleway',
        'providers/postgresql',
        'providers/ovh',
        'providers/aws-secrets-manager',
        'providers/google-secret-manager',
        'providers/oracle-vault',
        'providers/passbolt',
        'providers/aliyun-kms',
        'providers/tencent-cloud',
        'providers/ibm-cloud-secrets-manager',
        'providers/vault',
        'providers/conjur',
      ],
    },
    {
      type: 'category',
      label: 'Advanced Usage',
      items: [
        'advanced/versioning',
        'advanced/json-extraction',
        'advanced/layering',
        'advanced/logging',
        'advanced/audit-trail',
        'advanced/custom-providers',
      ],
    },
    'api-reference',
    {
      type: 'link',
      label: 'GitHub',
      href: 'https://github.com/mrhamburg/SecretsManager',
    },
  ],
};

export default sidebars;
