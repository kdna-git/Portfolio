import { dotnet } from './_framework/dotnet.js'

const isBrowser = typeof window !== "undefined";
if (!isBrowser) throw new Error("Expected to be running in a browser");

// Storage helpers the app imports with [JSImport]. Attached to globalThis before the runtime starts
// so the import resolves, and wrapped so a browser that blocks site data degrades to "nothing saved"
// instead of throwing on every write.
globalThis.portfolioStorage = {
    read: (key) => {
        try {
            return globalThis.localStorage.getItem(key);
        } catch {
            return null;
        }
    },
    write: (key, value) => {
        globalThis.localStorage.setItem(key, value);
    },
};

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
