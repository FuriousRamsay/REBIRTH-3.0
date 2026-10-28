// AUTHORED_NOT_RUN. Add the actual original-resource native connection scope to
// the actual native dequeue/transport fixture. No game/native save proof mocked.
process.argv.push('--resources');
await import('./mp072_native_transport_scope.mjs');
