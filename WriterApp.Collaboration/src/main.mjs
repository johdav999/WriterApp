import { createCollaborationServer } from './server.mjs';

const server = createCollaborationServer({
  backendUrl: process.env.COLLABORATION_BACKEND_URL,
  serviceKey: process.env.COLLABORATION_SERVICE_KEY,
  environment: process.env.COLLABORATION_ENVIRONMENT,
  allowedOrigins: (process.env.COLLABORATION_ALLOWED_ORIGINS ?? '').split(',').filter(Boolean),
  port: Number(process.env.PORT ?? 1234),
  address: process.env.COLLABORATION_ADDRESS ?? '127.0.0.1',
  commitsEnabled: process.env.COLLABORATION_COMMITS_ENABLED === 'true',
  interactionsEnabled: process.env.COLLABORATION_INTERACTIONS_ENABLED === 'true',
});
await server.listen();
console.log(`Collaboration read-only service listening on port ${server.address.port}`);
let stopping = false;
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, async () => {
  if (stopping) return; stopping = true; await server.destroy();
});
