import type {ReactNode} from 'react';
import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import styles from './index.module.css';

export default function Home(): ReactNode {
  return (
    <Layout
      title="Documentation"
      description="Learn how to use Prosa to write, organize, and export your stories.">
      <header className={styles.heroBanner}>
        <div className="container">
          <div className={styles.heroShell}>
            <div className={styles.heroCopy}>
              <Heading as="h1" className={styles.heroTitle}>
                Documentation
              </Heading>
              <p className={styles.heroSubtitle}>
                Learn how to write, organize, and export your stories with the same
                language, structure, and workflow as the Prosa app.
              </p>
              <div className={styles.heroActions}>
                <Link className="button button--primary" to="/docs/quickstart">
                  Get Started
                </Link>
                <Link className="button button--secondary" to="/docs/user-guide">
                  Writing Guide
                </Link>
              </div>
            </div>

            <aside className={styles.heroPanel} aria-label="Docs overview">
              <div className={styles.heroPanelLabel}>Documentation</div>
              <div className={styles.heroPanelValue}>3 core paths</div>
              <p className={styles.heroPanelText}>
                Start with setup, then move into writing workflows and AI tools.
              </p>
            </aside>
          </div>
        </div>
      </header>

      <main className={styles.mainSection}>
        <div className="container">
          <div className={styles.sectionHeader}>
            <p className={styles.sectionEyebrow}>Explore the docs</p>
            <Heading as="h2" className={styles.sectionTitle}>
              Find the Prosa guide that matches your task
            </Heading>
          </div>

          <div className={styles.linkGrid}>
            <article className={styles.linkCard}>
              <div className={styles.cardMeta}>01</div>
              <Heading as="h3" className={styles.cardTitle}>
                Getting Started
              </Heading>
              <p className={styles.cardText}>
                Set up your workspace and create your first writing project.
              </p>
              <Link className="button button--primary" to="/docs/quickstart">
                Open Quickstart
              </Link>
            </article>

            <article className={styles.linkCard}>
              <div className={styles.cardMeta}>02</div>
              <Heading as="h3" className={styles.cardTitle}>
                Writing
              </Heading>
              <p className={styles.cardText}>
                Learn projects, editor workflows, organization, and export.
              </p>
              <Link className="button button--primary" to="/docs/user-guide">
                Open User Guide
              </Link>
            </article>

            <article className={styles.linkCard}>
              <div className={styles.cardMeta}>03</div>
              <Heading as="h3" className={styles.cardTitle}>
                AI Writing Tools
              </Heading>
              <p className={styles.cardText}>
                Understand AI features and how to use them in your drafting flow.
              </p>
              <Link className="button button--primary" to="/docs/ai-tools">
                Open AI Tools
              </Link>
            </article>
          </div>
        </div>
      </main>
    </Layout>
  );
}
