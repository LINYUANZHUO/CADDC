using System.IO;
using System.Reflection;
#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif

namespace YiZhiMao.Dc.Plugin;

internal static class PluginDependencyResolver
{
    private static readonly string PluginDirectory =
        Path.GetDirectoryName(typeof(PluginDependencyResolver).Assembly.Location) ?? string.Empty;
    private static bool initialized;
#if !NETFRAMEWORK
    private static AssemblyLoadContext? loadContext;
#endif

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

#if NETFRAMEWORK
        AppDomain.CurrentDomain.AssemblyResolve += ResolveFrameworkAssembly;
#else
        loadContext = AssemblyLoadContext.GetLoadContext(typeof(PluginDependencyResolver).Assembly);
        if (loadContext is not null)
        {
            loadContext.Resolving += ResolveCoreAssembly;
        }
#endif
        initialized = true;
    }

    public static void Dispose()
    {
        if (!initialized)
        {
            return;
        }

#if NETFRAMEWORK
        AppDomain.CurrentDomain.AssemblyResolve -= ResolveFrameworkAssembly;
#else
        if (loadContext is not null)
        {
            loadContext.Resolving -= ResolveCoreAssembly;
            loadContext = null;
        }
#endif
        initialized = false;
    }

#if NETFRAMEWORK
    private static Assembly? ResolveFrameworkAssembly(object? sender, ResolveEventArgs args) =>
        TryLoad(new AssemblyName(args.Name));
#else
    private static Assembly? ResolveCoreAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        var path = GetAssemblyPath(assemblyName);
        return path is null ? null : context.LoadFromAssemblyPath(path);
    }
#endif

    private static Assembly? TryLoad(AssemblyName assemblyName)
    {
        var path = GetAssemblyPath(assemblyName);
        return path is null ? null : Assembly.LoadFrom(path);
    }

    private static string? GetAssemblyPath(AssemblyName assemblyName)
    {
        if (string.IsNullOrWhiteSpace(PluginDirectory) || string.IsNullOrWhiteSpace(assemblyName.Name))
        {
            return null;
        }

        var path = Path.Combine(PluginDirectory, assemblyName.Name + ".dll");
        return File.Exists(path) ? Path.GetFullPath(path) : null;
    }
}
