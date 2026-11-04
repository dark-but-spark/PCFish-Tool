using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Serialized;
using Il2CppInterop.Generator;
using Il2CppInterop.Generator.Runners;

namespace InteropGen;

internal sealed class Resolver : INetModuleResolver
{
    private readonly Dictionary<string, ModuleDefinition> _cache = new(StringComparer.Ordinal);

    public void Add(ModuleDefinition module)
    {
        if (module.Name is not null)
        {
            _cache[module.Name] = module;
        }
    }

    public ModuleDefinition? Resolve(string name)
    {
        return _cache.TryGetValue(name, out ModuleDefinition? module) ? module : null;
    }
}

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: InteropGen <gameDir> <cpp2ilOutDir> <unityDepsDir> <outputDir>");
            return 1;
        }

        string gameDir = args[0];
        string cpp2ilOut = args[1];
        string unityDeps = args[2];
        string outputDir = args[3];

        Directory.CreateDirectory(outputDir);

        var resolver = new Resolver();
        var files = Directory.GetFiles(cpp2ilOut, "*.dll");
        Console.WriteLine($"Dump DLLs: {files.Length}");
        var modules = files
            .Select(f => ModuleDefinition.FromFile(f, new ModuleReaderParameters { ModuleResolver = resolver }))
            .Select(m => { resolver.Add(m); return m; })
            .ToList();

        Console.WriteLine($"Modules: {modules.Count}, assemblies: {modules.Count(m => m.Assembly != null)}");
        var options = new GeneratorOptions
        {
            GameAssemblyPath = Path.Combine(gameDir, "GameAssembly.dll"),
            Source = modules.Select(m => m.Assembly).ToList(),
            OutputDir = outputDir,
            UnityBaseLibsDir = unityDeps,
            Parallel = true,
            Il2CppPrefixMode = (GeneratorOptions.PrefixMode)1,
        };

        string tpa = (string?)AppDomain.CurrentDomain.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        string extra = string.Join(
            Path.PathSeparator,
            Directory.GetFiles(unityDeps, "*.dll", SearchOption.TopDirectoryOnly)
        );
        AppDomain.CurrentDomain.SetData(
            "TRUSTED_PLATFORM_ASSEMBLIES",
            tpa + Path.PathSeparator + extra
        );

        Console.WriteLine("Generating interop assemblies...");
        Il2CppInteropGenerator generator = Il2CppInteropGenerator.Create(options);
        generator.AddInteropAssemblyGenerator();
        generator.Run();
        Console.WriteLine("Done.");
        return 0;
    }
}




