using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StripTool = WawiCallPro.Tools.StripDxLicenseInformation.Program;
using CecilMethodAttributes = Mono.Cecil.MethodAttributes;
using CecilTypeAttributes = Mono.Cecil.TypeAttributes;

namespace WawiCallPro.Tools.StripDxLicenseInformation.Tests;

[TestClass]
public sealed class StripTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-strip-tests-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [TestMethod]
    public void DryRunPreservesBytesAndActualRewriteRemovesOnlyLicenseAttributesAndRemainsExecutable()
    {
        var file = CreateFixture("WawiCallPro.Fixture.dll");
        var before = File.ReadAllBytes(file);
        Assert.AreEqual(0, StripTool.Main(["--input", file, "--dry-run"]));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(file));
        Assert.AreEqual(0, StripTool.Main(["--input", file]));
        AssertLicenseCount(file, 0);
        using (var assembly = AssemblyDefinition.ReadAssembly(file))
        {
            var retained = assembly.CustomAttributes.Single(attribute => attribute.AttributeType.Name == "AssemblyMetadataAttribute");
            Assert.AreEqual("LexwareProductLine", retained.ConstructorArguments[0].Value);
            Assert.AreEqual("Pro", retained.ConstructorArguments[1].Value);
        }
        var context = new AssemblyLoadContext(Guid.NewGuid().ToString("N"), isCollectible: true);
        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(file));
            var loaded = context.LoadFromStream(stream);
            Assert.AreEqual(42, loaded.GetType("Fixture.Sample")!.GetMethod("Answer")!.Invoke(null, null));
        }
        finally { context.Unload(); }
        var rewritten = File.ReadAllBytes(file);
        Assert.AreEqual(0, StripTool.Main(["--input", file]));
        CollectionAssert.AreEqual(rewritten, File.ReadAllBytes(file));
    }

    [TestMethod]
    public void DirectoryPatternsRecursionAndDuplicateInputsDoNotTouchOtherAssembliesOrNativeExecutables()
    {
        var directory = Path.Combine(_root, "nested");
        Directory.CreateDirectory(directory);
        var matched = CreateFixture("nested/WawiCallPro.Match.dll");
        var excluded = CreateFixture("nested/Other.dll");
        var native = Path.Combine(directory, "WawiCallPro.Native.exe");
        File.WriteAllBytes(native, [0x4D, 0x5A, 0, 0]);
        var before = File.ReadAllBytes(excluded);
        Assert.AreEqual(0, StripTool.Main(["--input", _root, "--input", matched, "--recursive", "--pattern", "WawiCallPro*.dll", "--pattern", "WawiCallPro*.exe"]));
        AssertLicenseCount(matched, 0);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(excluded));
        CollectionAssert.AreEqual(new byte[] { 0x4D, 0x5A, 0, 0 }, File.ReadAllBytes(native));
    }

    [TestMethod]
    public void SignedAssemblyRequiresAKeyAndIsResignedWithItsOriginalIdentity()
    {
        using var rsa = new RSACryptoServiceProvider(1024);
        var key = rsa.ExportCspBlob(true);
        var file = CreateFixture("WawiCallPro.Signed.dll", key);
        var before = File.ReadAllBytes(file);
        var token = AssemblyName.GetAssemblyName(file).GetPublicKeyToken()!;
        Assert.AreEqual(2, StripTool.Main(["--input", file]));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(file));
        var keyFile = Path.Combine(_root, "fixture.snk");
        File.WriteAllBytes(keyFile, key);
        Assert.AreEqual(0, StripTool.Main(["--input", file, "--key-file", keyFile]));
        CollectionAssert.AreEqual(token, AssemblyName.GetAssemblyName(file).GetPublicKeyToken()!);
        AssertLicenseCount(file, 0);
        using var rewritten = AssemblyDefinition.ReadAssembly(file);
        Assert.IsTrue((rewritten.MainModule.Attributes & ModuleAttributes.StrongNameSigned) != 0);
    }

    [TestMethod]
    public void InvalidArgumentsAndMissingFilesFailInsteadOfReportingASuccessfulScan()
    {
        Assert.AreEqual(1, StripTool.Main([]));
        Assert.AreEqual(0, StripTool.Main(["--help"]));
        Assert.AreEqual(1, StripTool.Main(["--unknown"]));
        Assert.AreEqual(1, StripTool.Main(["--input", Path.Combine(_root, "missing.dll")]));
    }

    [TestMethod]
    public void LockedInputFailsWithoutChangingTheFileOrLeavingTemporaryFiles()
    {
        var file = CreateFixture("WawiCallPro.Locked.dll");
        var before = File.ReadAllBytes(file);
        using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.AreEqual(2, StripTool.Main(["--input", file]));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(file));
        Assert.HasCount(0, Directory.GetFiles(_root, "*.tmp"));
    }

    private string CreateFixture(string relativeFile, byte[]? key = null)
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, relativeFile);
        using var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Fixture" + Guid.NewGuid().ToString("N"), new Version(1, 0)), "Fixture", ModuleKind.Dll);
        var module = assembly.MainModule;
        var attributeType = new TypeDefinition("DevExpress.Utils.About", "DXLicenseInformationAttribute", CecilTypeAttributes.Public | CecilTypeAttributes.Class, module.ImportReference(typeof(Attribute)));
        module.Types.Add(attributeType);
        var constructor = new MethodDefinition(".ctor", CecilMethodAttributes.Public | CecilMethodAttributes.SpecialName | CecilMethodAttributes.RTSpecialName, module.TypeSystem.Void);
        constructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        var baseConstructor = typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!;
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, module.ImportReference(baseConstructor)));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        attributeType.Methods.Add(constructor);

        var sample = new TypeDefinition("Fixture", "Sample", CecilTypeAttributes.Public | CecilTypeAttributes.Abstract, module.TypeSystem.Object);
        module.Types.Add(sample);
        var nested = new TypeDefinition("", "Nested", CecilTypeAttributes.NestedPublic | CecilTypeAttributes.Class, module.TypeSystem.Object);
        sample.NestedTypes.Add(nested);
        nested.GenericParameters.Add(new GenericParameter("T", nested));
        var answer = new MethodDefinition("Answer", CecilMethodAttributes.Public | CecilMethodAttributes.Static, module.TypeSystem.Int32);
        answer.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4, 42));
        answer.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        sample.Methods.Add(answer);
        var generic = new MethodDefinition("Generic", CecilMethodAttributes.Public | CecilMethodAttributes.Static, module.TypeSystem.Void);
        generic.GenericParameters.Add(new GenericParameter("T", generic));
        generic.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
        generic.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        sample.Methods.Add(generic);
        sample.Fields.Add(new FieldDefinition("Value", Mono.Cecil.FieldAttributes.Public, module.TypeSystem.Int32));
        var property = new PropertyDefinition("Number", Mono.Cecil.PropertyAttributes.None, module.TypeSystem.Int32) { GetMethod = answer };
        sample.Properties.Add(property);
        sample.Events.Add(new EventDefinition("Changed", Mono.Cecil.EventAttributes.None, module.ImportReference(typeof(Action))));
        var implementation = new InterfaceImplementation(module.ImportReference(typeof(IDisposable)));
        sample.Interfaces.Add(implementation);
        sample.Methods.Add(new MethodDefinition("Dispose", CecilMethodAttributes.Public | CecilMethodAttributes.Virtual | CecilMethodAttributes.Abstract | CecilMethodAttributes.NewSlot, module.TypeSystem.Void));

        Mono.Cecil.ICustomAttributeProvider[] providers = [assembly, module, sample, nested, nested.GenericParameters[0], answer,
            answer.MethodReturnType, generic.GenericParameters[0], generic.Parameters[0], sample.Fields[0], property, sample.Events[0], implementation];
        foreach (var provider in providers)
        {
            var attribute = new CustomAttribute(constructor);
            attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, "synthetic-license-marker"));
            provider.CustomAttributes.Add(attribute);
        }
        var metadata = new CustomAttribute(module.ImportReference(typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!));
        metadata.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, "LexwareProductLine"));
        metadata.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, "Pro"));
        assembly.CustomAttributes.Add(metadata);
        assembly.Write(file, new WriterParameters { StrongNameKeyBlob = key });
        AssertLicenseCount(file, 13);
        return file;
    }

    private static void AssertLicenseCount(string file, int expected)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(file);
        var providers = new List<Mono.Cecil.ICustomAttributeProvider> { assembly, assembly.MainModule };
        foreach (var type in assembly.MainModule.GetTypes())
        {
            providers.Add(type);
            providers.AddRange(type.GenericParameters);
            providers.AddRange(type.Interfaces);
            providers.AddRange(type.Fields);
            providers.AddRange(type.Properties);
            providers.AddRange(type.Events);
            foreach (var method in type.Methods)
            {
                providers.Add(method);
                providers.Add(method.MethodReturnType);
                providers.AddRange(method.GenericParameters);
                providers.AddRange(method.Parameters);
            }
        }
        Assert.AreEqual(expected, providers.Sum(provider => provider.CustomAttributes.Count(attribute => attribute.AttributeType.Name == "DXLicenseInformationAttribute")));
    }
}
