using System.Reflection;

// API Compatibility Checker
// Compares the public API surface between two published output directories.
// Usage: ApiCompatChecker <base-dir> <current-dir>
// Returns 0 if no breaking changes, 1 if breaking changes are detected, 2 on error.
//
// Each directory is inspected through its own MetadataLoadContext. That matters for two reasons:
//   1. Assembly.LoadFrom refuses a second assembly whose name is already loaded, so the base and
//      current copies of SecretsManager.dll could not both be read in one process.
//   2. Assembly.LoadFrom resolves dependencies from the probing paths rather than from the directory
//      being inspected, so a published output full of NuGet dependencies failed to load and every
//      member looked "removed".
// MetadataLoadContext reflects over the metadata in isolation and resolves dependencies from an
// explicit path list, which fixes both.

string[] arguments = Environment.GetCommandLineArgs();
string baseDir = arguments.Length > 1 ? arguments[1] : "base";
string currentDir = arguments.Length > 2 ? arguments[2] : "current";

Console.WriteLine("=== API Compatibility Check ===");
Console.WriteLine($"  Base:    {baseDir}");
Console.WriteLine($"  Current: {currentDir}");
Console.WriteLine();

if (!Directory.Exists(baseDir))
{
    Console.WriteLine($"ERROR: base directory not found: {baseDir}");
    return 2;
}

if (!Directory.Exists(currentDir))
{
    Console.WriteLine($"ERROR: current directory not found: {currentDir}");
    return 2;
}

using var baseContext = CreateLoadContext(baseDir);
using var currentContext = CreateLoadContext(currentDir);

if (!TryExtractAll(baseDir, baseContext, out Dictionary<string, HashSet<string>> baseApis))
{
    return 2;
}

if (!TryExtractAll(currentDir, currentContext, out Dictionary<string, HashSet<string>> currentApis))
{
    return 2;
}

Console.WriteLine($"  Base assemblies:    {baseApis.Count}");
Console.WriteLine($"  Current assemblies: {currentApis.Count}");
Console.WriteLine();

var breakingChanges = new List<string>();
var additions = new List<string>();

foreach (var (assemblyName, baseApi) in baseApis)
{
    if (!currentApis.TryGetValue(assemblyName, out HashSet<string>? currentApi))
    {
        breakingChanges.Add($"  ASSEMBLY REMOVED: {assemblyName}");
        continue;
    }

    foreach (var member in baseApi.Except(currentApi).OrderBy(member => member, StringComparer.Ordinal))
    {
        breakingChanges.Add($"  REMOVED: {assemblyName}.{member}");
    }

    foreach (var member in currentApi.Except(baseApi).OrderBy(member => member, StringComparer.Ordinal))
    {
        additions.Add($"  ADDED: {assemblyName}.{member}");
    }
}

if (additions.Count > 0)
{
    Console.WriteLine($"  Additions ({additions.Count}):");
    foreach (var addition in additions.Take(20))
    {
        Console.WriteLine(addition);
    }
    if (additions.Count > 20)
    {
        Console.WriteLine($"  ... and {additions.Count - 20} more");
    }
    Console.WriteLine();
}

if (breakingChanges.Count > 0)
{
    Console.WriteLine($"  BREAKING CHANGES ({breakingChanges.Count}):");
    foreach (var change in breakingChanges.Take(20))
    {
        Console.WriteLine(change);
    }
    if (breakingChanges.Count > 20)
    {
        Console.WriteLine($"  ... and {breakingChanges.Count - 20} more");
    }
    Console.WriteLine();
    Console.WriteLine("RESULT: FAIL - Breaking changes detected");
    return 1;
}

Console.WriteLine("RESULT: PASS - No breaking changes detected");
return 0;

static MetadataLoadContext CreateLoadContext(string directory)
{
    var paths = new List<string>(Directory.GetFiles(directory, "*.dll"));

    // The core library has to be in the resolver or nothing can be resolved.
    var coreDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
    if (!string.IsNullOrEmpty(coreDirectory))
    {
        paths.AddRange(Directory.GetFiles(coreDirectory, "*.dll"));
    }

    // PathAssemblyResolver throws when two files in the set share a name, so keep one per name.
    var distinct = paths
        .GroupBy(path => Path.GetFileName(path)!, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .ToList();

    return new MetadataLoadContext(new PathAssemblyResolver(distinct));
}

static bool TryExtractAll(
    string directory,
    MetadataLoadContext context,
    out Dictionary<string, HashSet<string>> apiSurfaces)
{
    apiSurfaces = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    foreach (var assemblyPath in Directory.GetFiles(directory, "*.dll").OrderBy(path => path, StringComparer.Ordinal))
    {
        var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        try
        {
            apiSurfaces[assemblyName] = ExtractApiSurface(assemblyPath, context);
        }
        catch (Exception ex)
        {
            // Report this as an error instead of letting the assembly contribute no members, which
            // would otherwise make every member look removed.
            Console.WriteLine($"  ERROR: could not inspect {assemblyName}: {ex.Message}");
            return false;
        }
    }

    return true;
}

static HashSet<string> ExtractApiSurface(string assemblyPath, MetadataLoadContext context)
{
    var api = new HashSet<string>(StringComparer.Ordinal);
    var assembly = context.LoadFromAssemblyPath(assemblyPath);

    foreach (var type in GetPublicTypes(assembly))
    {
        var typeName = type.FullName ?? type.Name;
        api.Add($"TYPE:{typeName}");

        foreach (var member in type.GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (member is MethodInfo method && (method.IsPrivate || method.IsAssembly))
            {
                continue;
            }

            if (member is FieldInfo field && (field.IsPrivate || field.IsAssembly))
            {
                continue;
            }

            var signature = member switch
            {
                MethodInfo m => $"{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})",
                PropertyInfo p => $"{p.PropertyType.Name} {p.Name}",
                FieldInfo f => $"{f.FieldType.Name} {f.Name}",
                ConstructorInfo c => $"ctor({string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name))})",
                _ => member.Name,
            };

            api.Add($"MEMBER:{typeName}.{signature}");
        }
    }

    return api;
}

static IEnumerable<Type> GetPublicTypes(Assembly assembly)
{
    IReadOnlyList<Type> types;
    try
    {
        types = assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException ex)
    {
        // Part of the surface may still be readable even if some types cannot be loaded.
        types = ex.Types.OfType<Type>().ToArray();
    }

    return types.Where(type => type.IsPublic || type.IsNestedPublic || type.IsNestedFamily);
}
