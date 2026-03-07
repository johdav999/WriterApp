import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const config: Config = {
  title: 'Prosa Docs',
  tagline: 'Learn how to use Prosa to write, organize, and export your stories.',
  favicon: 'img/favicon.ico',

  future: {
    v4: true,
  },

  url: 'https://docs.prosa-app.com',
  baseUrl: '/',

  organizationName: 'johdav999',
  projectName: 'WriterApp',

  onBrokenLinks: 'throw',

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
    image: 'img/docusaurus-social-card.jpg',
    colorMode: {
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'Prosa Docs',
      logo: {
        alt: 'Prosa Docs',
        src: 'img/logo.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docs',
          position: 'left',
          label: 'Documentation',
        },
        {
          href: 'https://github.com/johdav999/WriterApp',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            {
              label: 'Quickstart',
              to: '/docs/quickstart',
            },
            {
              label: 'User Guide',
              to: '/docs/user-guide',
            },
            {
              label: 'AI Tools',
              to: '/docs/ai-tools',
            },
          ],
        },
        {
          title: 'Links',
          items: [
            {
              label: 'Prosa App',
              href: 'https://app.prosa-app.com',
            },
            {
              label: 'Main Website',
              href: 'https://www.prosa-app.com',
            },
          ],
        },
      ],
      copyright: `Copyright ${new Date().getFullYear()} Prosa`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
