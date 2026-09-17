import { dotnet } from './_framework/dotnet.js';

// XHarness ends the run when it sees "WASM EXIT <code>"; print it on every
// path, including runtime start-up failures, so a broken app cannot hang the run.
let exitCode = 1;
try {
    const runtime = await dotnet.withDiagnosticTracing(false).create();
    exitCode = await runtime.runMain();
} catch (error) {
    const detail = error instanceof Error
        ? (error.stack ?? error.message)
        : JSON.stringify(error, Object.getOwnPropertyNames(error ?? {}));
    console.error(`Unhandled error: ${detail}`);
}
console.log(`WASM EXIT ${exitCode}`);
