import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const config: Config = {
  title: 'SecretsManager',
  tagline: 'Unified .NET Secret Management',
  favicon: 'img/favicon.svg',

  url: 'https://mrhamburg.github.io',
  baseUrl: '/SecretsManager/',

  organizationName: 'yourorg',
  projectName: 'SecretsManager',

  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    colorMode: {
      defaultMode: 'dark',
      disableSwitch: false,
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'SecretsManager',
      logo: {
        alt: 'SecretsManager Logo',
        src: 'img/logo.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docsSidebar',
          position: 'left',
          label: 'Docs',
        },
        {
          href: 'https://github.com/mrhamburg/SecretsManager',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Documentation',
          items: [
            {label: 'Introduction', to: '/docs/intro'},
            {label: 'Quick Start', to: '/docs/getting-started/quick-start'},
            {label: 'Providers', to: '/docs/providers/overview'},
          ],
        },
        {
          title: 'Resources',
          items: [
            {label: 'GitHub', href: 'https://github.com/mrhamburg/SecretsManager'},
            {label: 'NuGet Packages', href: 'https://www.nuget.org/'},
          ],
        },
        {
          title: 'Community',
          items: [
            {label: 'GitHub Discussions', href: 'https://github.com/mrhamburg/SecretsManager/discussions'},
            {label: 'Stack Overflow', href: 'https://stackoverflow.com/questions/tagged/secretsmanager'},
          ],
        },
        {
          title: 'Legal',
          items: [
            {label: 'MIT License', href: 'https://github.com/mrhamburg/SecretsManager/blob/main/LICENSE'},
          ],
        },
      ],
      copyright: `Copyright \u00A9 ${new Date().getFullYear()} SecretsManager. Built with Docusaurus.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['csharp', 'bash', 'yaml', 'json'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
