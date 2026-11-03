using Mono.Cecil;

namespace WawiCallPro.Tools.StripDxLicenseInformation;

internal static class Program
{
    private const string LogPrefix = "[StripDxLicenseInformation]";

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args.Any(IsHelp))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            var options = Options.Parse(args);
            var files = ResolveInputFiles(options).ToArray();

            if (files.Length == 0)
            {
                Console.Error.WriteLine($"{LogPrefix} No input assemblies found.");
                return 1;
            }

            var summary = new StripSummary();

            foreach (var file in files)
            {
                var result = StripFile(file, options);
                summary.Add(result);

                if (result.Status == StripStatus.Modified)
                {
                    Console.WriteLine($"{LogPrefix} Removed {result.RemovedAttributeCount} attribute(s): {file}");
                }
                else if (options.Verbose)
                {
                    Console.WriteLine($"{LogPrefix} {result.Status}: {file}");
                }
            }

            Console.WriteLine(
                $"{LogPrefix} Checked {summary.CheckedCount} file(s), modified {summary.ModifiedCount}, removed {summary.RemovedAttributeCount} attribute(s), skipped {summary.SkippedCount}.");

            return summary.ErrorCount == 0 ? 0 : 2;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"{LogPrefix} {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{LogPrefix} Unexpected failure: {ex}");
            return 2;
        }
    }

    private static bool IsHelp(string value)
        => value.Equals("-h", StringComparison.OrdinalIgnoreCase)
           || value.Equals("--help", StringComparison.OrdinalIgnoreCase)
           || value.Equals("/?", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> ResolveInputFiles(Options options)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (var input in options.InputPaths)
        {
            var fullPath = Path.GetFullPath(input);

            if (File.Exists(fullPath))
            {
                if (IsCandidateAssemblyPath(fullPath) && seen.Add(fullPath))
                {
                    yield return fullPath;
                }

                continue;
            }

            if (!Directory.Exists(fullPath))
            {
                throw new ArgumentException($"Input path does not exist: {input}");
            }

            foreach (var pattern in options.Patterns)
            {
                foreach (var file in Directory.EnumerateFiles(fullPath, pattern, searchOption))
                {
                    var candidate = Path.GetFullPath(file);
                    if (IsCandidateAssemblyPath(candidate) && seen.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
        }
    }

    private static bool IsCandidateAssemblyPath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static StripResult StripFile(string file, Options options)
    {
        AssemblyDefinition assembly;
        using var resolver = new DefaultAssemblyResolver();
        var assemblyDirectory = Path.GetDirectoryName(file);
        if (!string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            resolver.AddSearchDirectory(assemblyDirectory);
        }

        try
        {
            assembly = AssemblyDefinition.ReadAssembly(file, new ReaderParameters
            {
                AssemblyResolver = resolver,
                InMemory = true
            });
        }
        catch (BadImageFormatException)
        {
            return StripResult.Skipped();
        }

        using (assembly)
        {
            var removed = StripAssembly(assembly);
            if (removed == 0)
            {
                return StripResult.Unchanged();
            }

            if (options.DryRun)
            {
                return StripResult.Modified(removed);
            }

            var keyFile = options.StrongNameKeyFile;
            if (assembly.Name.HasPublicKey && string.IsNullOrWhiteSpace(keyFile))
            {
                Console.Error.WriteLine(
                    $"{LogPrefix} Strong-named assembly requires --key-file before rewriting: {file}");
                return StripResult.Error();
            }

            var writerParameters = new WriterParameters();
            if (assembly.Name.HasPublicKey && !string.IsNullOrWhiteSpace(keyFile))
            {
                writerParameters.StrongNameKeyBlob = File.ReadAllBytes(keyFile);
            }

            var tempFile = Path.Combine(
                Path.GetDirectoryName(file) ?? Directory.GetCurrentDirectory(),
                $"{Path.GetFileName(file)}.{Guid.NewGuid():N}.tmp");

            try
            {
                assembly.Write(tempFile, writerParameters);
                File.Move(tempFile, file, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }

            return StripResult.Modified(removed);
        }
    }

    private static int StripAssembly(AssemblyDefinition assembly)
    {
        var removed = 0;

        removed += RemoveMatchingAttributes(assembly);

        foreach (var module in assembly.Modules)
        {
            removed += RemoveMatchingAttributes(module);

            foreach (var type in module.Types)
            {
                removed += StripType(type);
            }
        }

        return removed;
    }

    private static int StripType(TypeDefinition type)
    {
        var removed = RemoveMatchingAttributes(type);

        foreach (var genericParameter in type.GenericParameters)
        {
            removed += RemoveMatchingAttributes(genericParameter);
        }

        foreach (var interfaceImplementation in type.Interfaces)
        {
            removed += RemoveMatchingAttributes(interfaceImplementation);
        }

        foreach (var field in type.Fields)
        {
            removed += RemoveMatchingAttributes(field);
        }

        foreach (var property in type.Properties)
        {
            removed += RemoveMatchingAttributes(property);
        }

        foreach (var @event in type.Events)
        {
            removed += RemoveMatchingAttributes(@event);
        }

        foreach (var method in type.Methods)
        {
            removed += RemoveMatchingAttributes(method);
            removed += RemoveMatchingAttributes(method.MethodReturnType);

            foreach (var genericParameter in method.GenericParameters)
            {
                removed += RemoveMatchingAttributes(genericParameter);
            }

            foreach (var parameter in method.Parameters)
            {
                removed += RemoveMatchingAttributes(parameter);
            }
        }

        foreach (var nestedType in type.NestedTypes)
        {
            removed += StripType(nestedType);
        }

        return removed;
    }

    private static int RemoveMatchingAttributes(ICustomAttributeProvider provider)
    {
        if (!provider.HasCustomAttributes)
        {
            return 0;
        }

        var removed = 0;
        for (var i = provider.CustomAttributes.Count - 1; i >= 0; i--)
        {
            if (!IsDxLicenseInformationAttribute(provider.CustomAttributes[i]))
            {
                continue;
            }

            provider.CustomAttributes.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    private static bool IsDxLicenseInformationAttribute(CustomAttribute attribute)
    {
        var type = attribute.AttributeType;
        var name = type.Name;
        if (name.Equals("DXLicenseInformation", StringComparison.Ordinal)
            || name.Equals("DXLicenseInformationAttribute", StringComparison.Ordinal))
        {
            return true;
        }

        var fullName = type.FullName.Replace('/', '.');
        return fullName.EndsWith(".DXLicenseInformation", StringComparison.Ordinal)
               || fullName.EndsWith(".DXLicenseInformationAttribute", StringComparison.Ordinal);
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Removes DevExpress DXLicenseInformation custom attributes from managed assemblies.

            Usage:
              WawiCallPro.StripDxLicenseInformation --input <file-or-dir> [--recursive] [--pattern <glob>] [--key-file <snk>] [--dry-run] [--verbose]

            Options:
              --input <path>      Assembly file or directory. Can be specified multiple times.
              --recursive         Recurse into input directories.
              --pattern <glob>    Directory glob. Can be specified multiple times. Default: *.dll and *.exe.
              --key-file <path>   Strong-name key used to re-sign modified signed assemblies.
              --dry-run           Scan only, do not write files.
              --verbose           Log unchanged and skipped files.
            """);
    }

    private sealed class Options
    {
        private static readonly string[] DefaultPatterns = ["*.dll", "*.exe"];
        private readonly List<string> _inputPaths = [];
        private readonly List<string> _patterns = [];

        public IReadOnlyList<string> InputPaths => _inputPaths;

        public IReadOnlyList<string> Patterns => _patterns.Count == 0 ? DefaultPatterns : _patterns;

        public bool Recursive { get; private set; }

        public bool DryRun { get; private set; }

        public bool Verbose { get; private set; }

        public string? StrongNameKeyFile { get; private set; }

        public static Options Parse(string[] args)
        {
            var options = new Options();

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "--input":
                    case "-i":
                        options._inputPaths.Add(RequireValue(args, ref i, arg));
                        break;

                    case "--recursive":
                    case "-r":
                        options.Recursive = true;
                        break;

                    case "--pattern":
                    case "-p":
                        options._patterns.Add(RequireValue(args, ref i, arg));
                        break;

                    case "--key-file":
                    case "-k":
                        options.StrongNameKeyFile = Path.GetFullPath(RequireValue(args, ref i, arg));
                        if (!File.Exists(options.StrongNameKeyFile))
                        {
                            throw new ArgumentException(
                                $"Strong-name key file does not exist: {options.StrongNameKeyFile}");
                        }

                        break;

                    case "--dry-run":
                        options.DryRun = true;
                        break;

                    case "--verbose":
                    case "-v":
                        options.Verbose = true;
                        break;

                    default:
                        if (arg.StartsWith("-", StringComparison.Ordinal))
                        {
                            throw new ArgumentException($"Unknown argument: {arg}");
                        }

                        options._inputPaths.Add(arg);
                        break;
                }
            }

            if (options._inputPaths.Count == 0)
            {
                throw new ArgumentException("At least one --input path is required.");
            }

            return options;
        }

        private static string RequireValue(string[] args, ref int index, string optionName)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for {optionName}.");
            }

            index++;
            return args[index];
        }
    }

    private enum StripStatus
    {
        Unchanged,
        Modified,
        Skipped,
        Error
    }

    private readonly record struct StripResult(StripStatus Status, int RemovedAttributeCount)
    {
        public static StripResult Unchanged() => new(StripStatus.Unchanged, 0);

        public static StripResult Modified(int removedAttributeCount) =>
            new(StripStatus.Modified, removedAttributeCount);

        public static StripResult Skipped() => new(StripStatus.Skipped, 0);

        public static StripResult Error() => new(StripStatus.Error, 0);
    }

    private sealed class StripSummary
    {
        public int CheckedCount { get; private set; }

        public int ModifiedCount { get; private set; }

        public int SkippedCount { get; private set; }

        public int ErrorCount { get; private set; }

        public int RemovedAttributeCount { get; private set; }

        public void Add(StripResult result)
        {
            CheckedCount++;
            RemovedAttributeCount += result.RemovedAttributeCount;

            switch (result.Status)
            {
                case StripStatus.Modified:
                    ModifiedCount++;
                    break;
                case StripStatus.Skipped:
                    SkippedCount++;
                    break;
                case StripStatus.Error:
                    ErrorCount++;
                    break;
            }
        }
    }
}
