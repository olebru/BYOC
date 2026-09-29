using System;
namespace Exuarch.Core
{
    internal static class SourceText
    {
        // Splits on \r\n, \n or \r regardless of the platform the text was authored on.
        public static string[] SplitLines(string text)
        {
            return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        }
    }
}
