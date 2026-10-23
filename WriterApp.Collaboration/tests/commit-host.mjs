// Test-only owned subprocess. Deterministic process death is never exposed by the service entry point.
import { createCollaborationServer } from '../src/server.mjs';
import { sha256 } from '../src/commits.mjs';
let armed;
const server = createCollaborationServer({ backendUrl: process.env.PROBE_BACKEND_URL, serviceKey: process.env.COLLABORATION_SERVICE_KEY,
  environment: 'test', port: 0, commitsEnabled: true, checkpointEvery: 2, revalidateMilliseconds: 100,
  async fault(point) {
    if (armed === point) {
      armed = null;
      if (point === 'afterPersist') process.exit(91);
      throw new Error('intentional test fault');
    }
  },
});
server.hocuspocus.configuration.extensions.push({ async connected({ context }) {
  process.send({ type: 'connection', ticketHash: sha256(Buffer.from(context.session.ticket)), connectionId: context.session.connectionId });
} });
process.on('message', async message => {
  if (message.type === 'arm') { armed = message.point; process.send({ type: 'armed', point: armed }); }
  if (message.type === 'stop') { await server.destroy(); process.exit(0); }
});
await server.listen(); process.send({ type: 'listening', port: server.address.port });
