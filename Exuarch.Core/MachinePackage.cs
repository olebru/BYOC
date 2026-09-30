using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace Exuarch.Core
{
    // A machine with its microcode and example programs, loaded and saved together. As a file it is one JSON
    // document; the built in packages are folders (package.json, machine.json and .asm files) embedded in the
    // assembly.
    public class MachinePackage
    {
        public string Name { get; set; }
        // One or two sentences, for lists and the package picker.
        public string Description { get; set; }
        // A longer introduction in Markdown, like a README: the ideas behind the machine, its parts, how its
        // instructions work and what to try. Links written exuarch:device/<id>, exuarch:instruction/<mnemonic>,
        // exuarch:program/<name> and exuarch:tab/<tab> jump to that part of the app.
        public string Readme { get; set; }
        public MachineDefinition Machine { get; set; }
        public List<PackageProgram> Programs { get; set; } = new List<PackageProgram>();

        public PackageProgram Program(string name)
        {
            return Programs.FirstOrDefault(p => p.Name == name) ?? throw new ArgumentException($"Package '{Name}' has no program '{name}'.");
        }

        public string ToJson()
        {
            return CompactJson.Format(JsonSerializer.Serialize(this, MachineDefinitionJsonContext.Default.MachinePackage));
        }
        public static MachinePackage FromJson(string json)
        {
            try
            {
                var package = JsonSerializer.Deserialize(json, MachineDefinitionJsonContext.Default.MachinePackage)
                              ?? throw new MachineDefinitionException("Package is empty.");
                if (package.Machine == null) throw new MachineDefinitionException("A package needs a \"machine\".");
                package.Machine.DropRemovedParameters();
                return package;
            }
            catch (JsonException e)
            {
                var location = e.Path == null ? "" : $" at {e.Path} (line {e.LineNumber + 1})";
                throw new MachineDefinitionException($"Package is not valid JSON{location}: {e.Message}");
            }
        }
        public MachinePackage Clone()
        {
            return FromJson(ToJson());
        }
    }

    public class PackageProgram
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
    }

    // The package.json of a built in package folder.
    internal class PackageManifest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        // The README file in the package folder, if any.
        public string Readme { get; set; }
        public string Machine { get; set; }
        public List<ManifestProgram> Programs { get; set; } = new List<ManifestProgram>();
    }
    internal class ManifestProgram
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string File { get; set; }
    }

    public static class BuiltInPackages
    {
        private const string Prefix = "Exuarch.Core.Packages/";
        private static readonly Lazy<IReadOnlyList<MachinePackage>> all = new Lazy<IReadOnlyList<MachinePackage>>(Load);

        // In folder name order; the first is the default.
        public static IReadOnlyList<MachinePackage> All { get { return all.Value; } }
        public static MachinePackage Default { get { return All[0]; } }

        // A fresh copy that can be changed without affecting the built in one.
        public static MachinePackage Get(string name)
        {
            return (All.FirstOrDefault(p => p.Name == name) ?? throw new ArgumentException($"No built in package '{name}'.")).Clone();
        }

        private static IReadOnlyList<MachinePackage> Load()
        {
            var assembly = typeof(BuiltInPackages).Assembly;
            var folders = assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(Prefix) && n.EndsWith("/package.json"))
                .Select(n => n.Substring(0, n.Length - "package.json".Length))
                .OrderBy(n => n, StringComparer.Ordinal);
            return folders.Select(folder =>
            {
                string Read(string file)
                {
                    using var stream = assembly.GetManifestResourceStream(folder + file)
                        ?? throw new InvalidOperationException($"Package file '{folder + file}' is missing.");
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
                var manifest = JsonSerializer.Deserialize(Read("package.json"), MachineDefinitionJsonContext.Default.PackageManifest);
                return new MachinePackage
                {
                    Name = manifest.Name,
                    Description = manifest.Description,
                    Readme = manifest.Readme == null ? null : Read(manifest.Readme).TrimEnd('\n', '\r'),
                    Machine = MachineDefinition.FromJson(Read(manifest.Machine)),
                    Programs = manifest.Programs.Select(p => new PackageProgram { Name = p.Name, Description = p.Description, Source = Read(p.File).TrimEnd('\n', '\r') }).ToList(),
                };
            }).ToList();
        }
    }
}
