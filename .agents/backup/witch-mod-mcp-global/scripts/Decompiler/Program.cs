using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using System;
using System.IO;
using System.Linq;

var dllPath = args[0];
var outDir = args[1];

var settings = new DecompilerSettings {
    ShowXmlDocumentation = false,
    ThrowOnAssemblyResolveErrors = false,
};

var decompiler = new CSharpDecompiler(dllPath, settings);
var module = decompiler.TypeSystem.MainModule;
var types = module.TypeDefinitions
    .Where(t => !t.FullName.Contains('<'))
    .OrderBy(t => t.FullName);

Directory.CreateDirectory(outDir);

foreach (var type in types)
{
    try
    {
        var name = type.FullName.Replace("/", ".");
        var code = decompiler.DecompileTypeAsString(type.FullTypeName);
        var dir = Path.GetDirectoryName(Path.Combine(outDir, name + ".cs"));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(outDir, name + ".cs"), code);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {type.FullName}: {ex.Message}");
    }
}
