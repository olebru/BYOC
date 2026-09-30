using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace Exuarch.Core
{
    // Links in a package README that point into the app, written exuarch:<kind>/<target>:
    //   exuarch:device/alu              the device, in the hardware design
    //   exuarch:instruction/ADD         the instruction, in the microcode editor
    //   exuarch:program/Hello, world    that example program (write the link as <exuarch:program/...> when it has spaces)
    //   exuarch:tab/Run                 one of the app's tabs
    public static class ReadmeLinks
    {
        public const string Scheme = "exuarch:";
        public static readonly string[] Kinds = { "device", "instruction", "program", "tab" };
        public static readonly string[] Tabs = { "About", "Hardware design", "Microcode", "Program", "JSON", "Run" };
        private static readonly Regex Markdown = new Regex(@"\]\(<?(exuarch:[^)>]+)>?\)", RegexOptions.Compiled);

        public static bool TryParse(string href, out string kind, out string target)
        {
            kind = target = null;
            if (href == null || !href.StartsWith(Scheme, StringComparison.Ordinal)) return false;
            var rest = Uri.UnescapeDataString(href.Substring(Scheme.Length));
            int slash = rest.IndexOf('/');
            if (slash <= 0) return false;
            kind = rest.Substring(0, slash);
            target = rest.Substring(slash + 1);
            return Kinds.Contains(kind) && target.Length > 0;
        }

        // Every exuarch: link in a README, as written.
        public static IEnumerable<string> In(string markdown)
        {
            return markdown == null ? Enumerable.Empty<string>() : Markdown.Matches(markdown).Select(m => m.Groups[1].Value);
        }

        // Why the link does not lead anywhere in this package, or null when it does.
        public static string Problem(MachinePackage package, string href)
        {
            if (!TryParse(href, out var kind, out var target)) return $"'{href}' is not an exuarch: link to a device, instruction, program or tab";
            switch (kind)
            {
                case "device":
                    return package.Machine.FindDevice(target) == null ? $"there is no device '{target}'" : null;
                case "instruction":
                    return package.Machine.Decoder?.Microcode?.AllInstructions.Any(i => i.Mnemonic == target) == true ? null : $"there is no instruction '{target}'";
                case "program":
                    return package.Programs.Any(p => p.Name == target) ? null : $"there is no program '{target}'";
                default:
                    return Tabs.Contains(target) ? null : $"there is no tab '{target}'";
            }
        }
    }
}
