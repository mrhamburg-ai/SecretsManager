using System.Reflection;
using System.Text;

// API Compatibility Checker
// Compares the public API surface between two build directories.
// Usage: ApiCompatChecker <base-dir> <current-dir>
// Returns 0 if no breaking changes, 1 if breaking changes detected.

string baseDir = Environment.GetCommandLineArgs().Length > 1 ? Environment.GetCommandLineArgs()[1] : "base";
string currentDir = Environment.GetCommandLineArgs().Length > 2 ? Environment.GetCommandLineArgs()[2] : "current";

Console.WriteLine($"=== API Compatibility Check ===");
Console.WriteLine($"  Base:    {baseDir}");
Console.WriteLine($"  Current: {currentDir}");
Console.WriteLine();

// Get all DLLs in both directories
var baseDlls = Directory.GetFiles(baseDir, "*.dll").ToList();
var currentDlls = Directory.GetFiles(currentDir, "*.dll").ToList();

Console.WriteLine($"  Base DLLs: {baseDlls.Count}");
Console.WriteLine($"  Current DLLs: {currentDlls.Count}");
Console.WriteLine();

// Extract API surface for each DLL
var baseApis = new Dictionary<string, HashSet<string>>();
var currentApis = new Dictionary<string, HashSet<string>>();

foreach (var dll in baseDlls)
{
    var name = Path.GetFileNameWithoutExtension(dll);
    baseApis[name] = ExtractApiSurface(dll);
}

foreach (var dll in currentDlls)
{
    var name = Path.GetFileNameWithoutExtension(dll);
    currentApis[name] = ExtractApiSurface(dll);
}

// Compare
var breakingChanges = new List<string>();
var additions = new List<string>();

foreach (var kvp in baseApis)
{
    var name = kvp.Key;
    var baseApi = kvp.Value;
    
    if (!currentApis.ContainsKey(name))
    {
        breakingChanges.Add($"  ASSEMBLY REMOVED: {name}");
        continue;
    }
    
    var currentApi = currentApis[name];
    
    // Check for removed members
    var removed = baseApi.Except(currentApi).ToList();
    foreach (var member in removed)
    {
        breakingChanges.Add($"  REMOVED: {name}.{member}");
    }
    
    // Check for added members (informational)
    var added = currentApi.Except(baseApi).ToList();
    foreach (var member in added)
    {
        additions.Add($"  ADDED: {name}.{member}");
    }
}

// Report
if (additions.Count > 0)
{
    Console.WriteLine($"  Additions ({additions.Count}):");
    foreach (var a in additions.Take(20))
    {
        Console.WriteLine(a);
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
    foreach (var b in breakingChanges.Take(20))
    {
        Console.WriteLine(b);
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

static HashSet<string> ExtractApiSurface(string assemblyPath)
{
    var api = new HashSet<string>();
    
    try
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        
        foreach (var type in assembly.GetTypes())
        {
            // Only public and protected types
            if (!type.IsPublic && !type.IsNestedPublic && !type.IsNestedFamily)
                continue;
            
            var typeName = type.FullName ?? type.Name;
            api.Add($"TYPE:{typeName}");
            
            foreach (var member in type.GetMembers(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly))
            {
                if (member is MethodInfo mi && (mi.IsPrivate || mi.IsAssembly))
                    continue;
                if (member is FieldInfo fi && (fi.IsPrivate || fi.IsAssembly))
                    continue;
                
                var signature = member switch
                {
                    MethodInfo m => $"{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})",
                    PropertyInfo p => $"{p.PropertyType.Name} {p.Name}",
                    FieldInfo f => $"{f.FieldType.Name} {f.Name}",
                    ConstructorInfo c => $"ctor({string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name))})",
                    _ => member.Name
                };

                api.Add($"MEMBER:{typeName}.{signature}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  WARNING: Could not load {Path.GetFileName(assemblyPath)}: {ex.Message}");
    }

    return api;
}
