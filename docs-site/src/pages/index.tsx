import type {ReactNode} from 'react';
import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import styles from './index.module.css';

export default function Home(): ReactNode {
  return (
    <Layout
      title="Prosa Documentation"
      description="Learn how to use Prosa to write, organize, and export your stories.">
      <header className={styles.heroBanner}>
        <div className="container">
          <Heading as="h1" className={styles.heroTitle}>
            Prosa Documentation
          </Heading>
          <p className={styles.heroSubtitle}>
            Learn how to use Prosa to write, organize, and export your stories.
          </p>
        </div>
      </header>

      <main className={styles.mainSection}>
        <div className="container">
          <div className={styles.linkGrid}>
            <article className={styles.linkCard}>
              <Heading as="h2" className={styles.cardTitle}>
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
              <Heading as="h2" className={styles.cardTitle}>
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
              <Heading as="h2" className={styles.cardTitle}>
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
