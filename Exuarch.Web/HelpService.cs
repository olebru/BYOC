using System;

namespace Exuarch.Web
{
    // Lets any component ask for a handbook page (an exuarch: link kind and target, such as guide/microcode or
    // reference/alu) without passing callbacks down through every editor; the page opens it in the drawer.
    public class HelpService
    {
        public event Action<string, string> Requested;

        public void Open(string kind, string target)
        {
            Requested?.Invoke(kind, target);
        }
    }
}
