import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  docs: [
    {
      type: 'doc',
      id: 'quickstart',
      label: 'Quickstart',
    },
    'user-guide',
    {
      type: 'doc',
      id: 'ai-tools',
      label: 'AI tools',
    },
    {
      type: 'doc',
      id: 'features',
      label: 'Features',
    }
  ],
};

export default sidebars;
