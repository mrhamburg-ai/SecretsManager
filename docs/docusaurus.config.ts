import {themes as prismThemes} from 'prism-react-renderer';
import type {PrismTheme} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

/**
 * One code surface in both colour modes, on the site's graphite (#11141a).
 * The light Prism themes do not keep comments and strings readable on a light
 * ground, so documentation samples use the same dark surface throughout.
 */
const codeTheme: PrismTheme = {
  plain: {color: '#d5d9e1', backgroundColor: '#11141a'},
  styles: [
    ...prismThemes.oneDark.styles,
    {types: ['comment', 'prolog', 'cdata'], style: {color: '#8a93a4', fontStyle: 'italic'}},
  ],
};

type ThemeConfig = Preset.ThemeConfig & {
  zoom?: {
    selector?: string;
    background?: {light?: string; dark?: string};
  };
};

const config: Config = {
  title: 'SecretsManager',
  tagline: 'Unified .NET secret management',
  favicon: 'img/favicon.svg',

  url: 'https://mrhamburg.github.io',
  baseUrl: '/SecretsManager/',

  organizationName: 'mrhamburg',
  projectName: 'SecretsManager',

  onBrokenLinks: 'throw',
  onBrokenAnchors: 'throw',
  markdown: {
    format: 'detect',
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },

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
          numberPrefixParser: false,
          editUrl: 'https://github.com/mrhamburg/SecretsManager/tree/main/docs/docs/',
        },
        blog: false,
        theme: {
          customCss: [
            './src/css/custom.css',
            './src/css/provider-logos.css',
          ],
        },
      } satisfies Preset.Options,
    ],
  ],

  clientModules: ['./src/fonts.js'],

  plugins: ['docusaurus-plugin-image-zoom'],

  themes: [
    [
      '@easyops-cn/docusaurus-search-local',
      {
        hashed: true,
        docsDir: 'docs',
        docsRouteBasePath: '/docs',
        indexBlog: false,
      },
    ],
  ],

  themeConfig: {
    zoom: {
      selector: '.markdown img:not(a img):not(.no-frame)',
      background: {
        light: 'rgba(248, 250, 252, 0.95)',
        dark: 'rgba(11, 18, 32, 0.95)',
      },
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
    colorMode: {
      defaultMode: 'dark',
      disableSwitch: false,
      respectPrefersColorScheme: true,
    },
    footer: {
      style: 'light',
      links: [
        {
          title: 'Links',
          items: [
            {label: 'Documentation', to: '/docs/intro'},
            {label: 'GitHub', href: 'https://github.com/mrhamburg/SecretsManager'},
            {label: 'NuGet Packages', href: 'https://www.nuget.org/'},
            {label: 'Issues', href: 'https://github.com/mrhamburg/SecretsManager/issues'},
          ],
        },
      ],
      copyright: `Copyright \u00A9 ${new Date().getFullYear()} SecretsManager. Built with Docusaurus.`,
    },
    prism: {
      theme: codeTheme,
      darkTheme: codeTheme,
      additionalLanguages: ['csharp', 'bash', 'yaml', 'json'],
    },
  } satisfies ThemeConfig,
};

export default config;
