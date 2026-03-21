import React, {type ReactNode} from 'react';
import clsx from 'clsx';
import {
  ErrorCauseBoundary,
  useThemeConfig,
} from '@docusaurus/theme-common';
import {
  splitNavbarItems,
  useNavbarMobileSidebar,
} from '@docusaurus/theme-common/internal';
import NavbarItem, {type Props as NavbarItemConfig} from '@theme/NavbarItem';
import NavbarLogo from '@theme/Navbar/Logo';
import NavbarMobileSidebarToggle from '@theme/Navbar/MobileSidebar/Toggle';

import styles from './styles.module.css';

function useNavbarItems() {
  return useThemeConfig().navbar.items as NavbarItemConfig[];
}

function NavbarItems({items}: {items: NavbarItemConfig[]}): ReactNode {
  return (
    <>
      {items.map((item, i) => (
        <ErrorCauseBoundary
          key={i}
          onError={(error) =>
            new Error(
              `A theme navbar item failed to render.
Please double-check the following navbar item (themeConfig.navbar.items) of your Docusaurus config:
${JSON.stringify(item, null, 2)}`,
              {cause: error},
            )
          }>
          <NavbarItem {...item} />
        </ErrorCauseBoundary>
      ))}
    </>
  );
}

export default function NavbarContent(): ReactNode {
  const mobileSidebar = useNavbarMobileSidebar();
  const items = useNavbarItems();
  const [leftItems, rightItems] = splitNavbarItems(items);

  return (
    <div className={styles.shell}>
      <div className={styles.headerGrid}>
        <div className={styles.brandRegion}>
          <NavbarLogo />
          <span className={styles.headerLabel}>Prosa Docs</span>
        </div>

        <div className={styles.actionsRegion}>
          <NavbarItems items={rightItems} />
        </div>

        {!mobileSidebar.disabled && (
          <div className={styles.mobileToggle}>
            <NavbarMobileSidebarToggle />
          </div>
        )}

        <nav className={clsx('navbar__items', styles.menuRegion)} aria-label="Primary">
          <NavbarItems items={leftItems} />
        </nav>
      </div>
    </div>
  );
}
