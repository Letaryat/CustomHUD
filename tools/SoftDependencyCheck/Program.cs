// SwiftlyS2 finds a plugin's class with Assembly.GetTypes(), and that fails the whole load when any
// type needs an assembly the server lacks. A provider's contract is such an assembly wherever that
// provider is not installed, so one type implementing a contract interface, or holding a struct built
// over a contract type, makes the provider required -- an async method awaiting a provider's task
// compiles to exactly that struct. This loads CustomHUD.dll with only what the plugin ships and what
// SwiftlyS2 provides, and fails naming each type that would not load.
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

var dll = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine("build", "publish", "CustomHUD", "CustomHUD.dll"));
if (!File.Exists(dll))
{
    Console.Error.WriteLine($"{dll} not found; publish CustomHUD first.");
    return 2;
}

var context = new PluginContext(Path.GetDirectoryName(dll)!);
var assembly = context.LoadFromAssemblyPath(dll);
try
{
    Console.WriteLine($"OK: all {assembly.GetTypes().Length} types of {Path.GetFileName(dll)} load with no provider plugin installed.");
    return 0;
}
catch (ReflectionTypeLoadException)
{
    Console.Error.WriteLine($"{Path.GetFileName(dll)} needs these assemblies to load, so SwiftlyS2 refuses it on a server without them:");
    foreach (var name in TypeNames(dll))
    {
        try
        {
            assembly.GetType(name, throwOnError: true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  {name}: {(ex is FileNotFoundException { FileName: { } missing } ? missing : ex.Message)}");
        }
    }
    return 1;
}

// Every type definition's reflection name ("Ns.Outer+Inner"), compiler-generated ones included.
static IEnumerable<string> TypeNames(string path)
{
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    var metadata = pe.GetMetadataReader();
    var names = new List<string>();
    foreach (var handle in metadata.TypeDefinitions)
    {
        var type = metadata.GetTypeDefinition(handle);
        var name = metadata.GetString(type.Name);
        if (name == "<Module>") continue;
        while (type.GetDeclaringType() is { IsNil: false } outer)
        {
            type = metadata.GetTypeDefinition(outer);
            name = metadata.GetString(type.Name) + "+" + name;
        }
        var ns = metadata.GetString(type.Namespace);
        names.Add(ns.Length > 0 ? ns + "." + name : name);
    }
    return names;
}

// The plugin's folder and its resources/exports (CustomHUD.Contract); anything else falls through to
// this process, which holds what SwiftlyS2 provides.
sealed class PluginContext(string directory) : AssemblyLoadContext("CustomHUD")
{
    protected override Assembly? Load(AssemblyName name)
    {
        foreach (var dir in new[] { directory, Path.Combine(directory, "resources", "exports") })
        {
            var path = Path.Combine(dir, name.Name + ".dll");
            if (File.Exists(path)) return LoadFromAssemblyPath(path);
        }
        return null;
    }
}
