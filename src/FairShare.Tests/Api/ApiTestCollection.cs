namespace FairShare.Tests.Api;

// The API integration test classes share this collection so xUnit runs them sequentially.
// Two WebApplicationFactory hosts cold-starting in parallel inside one test process
// intermittently 500 on their first requests (shared native SQLite/DataProtection
// initialization), which flaked CI. Real deployments run in separate processes and
// don't share that state, so this is purely a test-harness concern.
[CollectionDefinition("Api")]
public class ApiTestCollection;
