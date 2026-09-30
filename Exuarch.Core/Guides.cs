using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Exuarch.Core
{
    // How to use the app, as opposed to a package README about one machine.
    public class Guide
    {
        public string Title { get; set; }
        public string Markdown { get; set; }
    }

    // The guides embedded from the Guides folder, in file name order; the title is the first "# " heading.
    public static class Guides
    {
        private const string Prefix = "Exuarch.Core.Guides/";
        private static readonly Lazy<IReadOnlyList<Guide>> all = new Lazy<IReadOnlyList<Guide>>(Load);

        public static IReadOnlyList<Guide> All { get { return all.Value; } }

        private static IReadOnlyList<Guide> Load()
        {
            var assembly = typeof(Guides).Assembly;
            return assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(Prefix) && n.EndsWith(".md"))
                .OrderBy(n => n, StringComparer.Ordinal)
                .Select(n =>
                {
                    using var reader = new StreamReader(assembly.GetManifestResourceStream(n));
                    var markdown = reader.ReadToEnd().TrimEnd('\n', '\r');
                    var heading = markdown.Split('\n').FirstOrDefault(l => l.StartsWith("# "));
                    return new Guide { Title = heading?.Substring(2).Trim() ?? Path.GetFileNameWithoutExtension(n.Substring(Prefix.Length)), Markdown = markdown };
                })
                .ToList();
        }
    }
}
