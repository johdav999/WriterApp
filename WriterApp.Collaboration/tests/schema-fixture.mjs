import { createSeed, getSupport } from '../src/generated/schema.mjs';
const state = createSeed({ type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'Seed' }] }] });
console.log(JSON.stringify({ support: await getSupport(), state: Buffer.from(state).toString('base64') }));
