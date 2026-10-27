import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  docsSidebar: [
    'intro',
    {
      type: 'category',
      collapsible: false,
      label: 'Getting Started',
      items: [
        'getting-started/installation',
        'getting-started/quick-start',
        'getting-started/configuration',
      ],
    },
    {
      type: 'category',
      collapsible: false,
      label: 'Providers',
      items: [
        'providers/overview',
        {type: 'doc', id: 'providers/filesystem', className: 'sm-side-provider sm-side-filesystem'},
        {type: 'doc', id: 'providers/azure-key-vault', className: 'sm-side-provider sm-side-azure'},
        {type: 'doc', id: 'providers/scaleway', className: 'sm-side-provider sm-side-scaleway'},
        {type: 'doc', id: 'providers/postgresql', className: 'sm-side-provider sm-side-postgresql'},
        {type: 'doc', id: 'providers/ovh', className: 'sm-side-provider sm-side-ovh'},
        {type: 'doc', id: 'providers/aws-secrets-manager', className: 'sm-side-provider sm-side-aws'},
        {type: 'doc', id: 'providers/google-secret-manager', className: 'sm-side-provider sm-side-gcp'},
        {type: 'doc', id: 'providers/oracle-vault', className: 'sm-side-provider sm-side-oracle'},
        {type: 'doc', id: 'providers/passbolt', className: 'sm-side-provider sm-side-passbolt'},
        {type: 'doc', id: 'providers/aliyun-kms', className: 'sm-side-provider sm-side-aliyun'},
        {type: 'doc', id: 'providers/tencent-cloud', className: 'sm-side-provider sm-side-tencent'},
        {type: 'doc', id: 'providers/ibm-cloud-secrets-manager', className: 'sm-side-provider sm-side-ibm'},
        {type: 'doc', id: 'providers/infisical', className: 'sm-side-provider sm-side-infisical'},
        {type: 'doc', id: 'providers/vault', className: 'sm-side-provider sm-side-vault'},
        {type: 'doc', id: 'providers/conjur', className: 'sm-side-provider sm-side-conjur'},
      ],
    },
    {
      type: 'category',
      collapsible: false,
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
