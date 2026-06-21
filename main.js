import { dotnet } from './_framework/dotnet.js'

const isBrowser = typeof window != "undefined";
if (!isBrowser) {
    throw new Error(`Expected to be running in a browser`);
}

const dotnetRuntime = await dotnet
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();
const getAssemblyName = asset => {
    const fileName = (asset.virtualPath ?? asset.name).split(/[\\/]/).pop();
    const name = fileName.endsWith(".dll") ? fileName.slice(0, -4) : fileName;
    const hashSeparator = name.lastIndexOf(".");
    if (hashSeparator > 0 && /^[a-z0-9]{10}$/.test(name.slice(hashSeparator + 1))) {
        return name.slice(0, hashSeparator);
    }

    return name;
};

const assemblyAssets = [
    ...(config.resources?.coreAssembly ?? []),
    ...(config.resources?.assembly ?? [])
]
    .filter(asset => typeof asset.name === "string" && asset.name.endsWith(".dll"))
    .map(asset => `${getAssemblyName(asset)}|${asset.name}`);

await dotnetRuntime.runMain(config.mainAssemblyName, [
    window.location.search,
    globalThis.document.baseURI,
    assemblyAssets.join(";")
]);
