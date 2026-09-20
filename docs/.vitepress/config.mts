import { defineConfig } from 'vitepress';
import { withMermaid } from 'vitepress-plugin-mermaid';

export default withMermaid(
  defineConfig({
    title: 'RigSwitch',
    description: 'High-Performance Windows Workstation & Sim Rig Hardware Orchestrator',
    base: '/RigSwitch/',
    cleanUrls: true,
    srcExclude: [
      'superpowers/**'
    ],

    head: [
      ['link', { rel: 'icon', href: '/RigSwitch/favicon.ico' }],
      ['meta', { name: 'theme-color', content: '#00C853' }]
    ],

    themeConfig: {
      logo: '/images/logo.png',
      siteTitle: 'RigSwitch',

      nav: [
        { text: 'Getting Started', link: '/getting-started/' },
        { text: 'Features', link: '/guide/display-switching' },
        { text: 'Architecture', link: '/technical/architecture' },
        { text: 'GitHub Releases', link: 'https://github.com/spelech/RigSwitch/releases' }
      ],

      sidebar: {
        '/getting-started/': [
          {
            text: 'Getting Started',
            items: [
              { text: 'Overview & Requirements', link: '/getting-started/' },
              { text: 'Installation Guide', link: '/getting-started/installation' },
              { text: 'Quickstart & First Switch', link: '/getting-started/quickstart' }
            ]
          }
        ],
        '/guide/': [
          {
            text: 'Core Features & Guides',
            items: [
              { text: 'Display Topology Switching', link: '/guide/display-switching' },
              { text: 'Multi-Preset Workstation Engine', link: '/guide/presets-and-hooks' },
              { text: 'Interactive Hotkey Recorder', link: '/guide/hotkey-recorder' },
              { text: 'CoreAudio Routing & Filtering', link: '/guide/audio-routing' }
            ]
          }
        ],
        '/technical/': [
          {
            text: 'Technical Reference',
            items: [
              { text: 'System Architecture & Sequence', link: '/technical/architecture' },
              { text: 'Simulation Test Harness', link: '/technical/test-harness' }
            ]
          }
        ]
      },

      socialLinks: [
        { icon: 'github', link: 'https://github.com/spelech/RigSwitch' }
      ],

      search: {
        provider: 'local'
      },

      footer: {
        message: 'Released under the MIT License. Built with AgenticEngineeringToolbelt discipline.',
        copyright: 'Copyright © Spelech'
      }
    }
  })
);
