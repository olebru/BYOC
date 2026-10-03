using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace Exuarch.Web.Components
{
    // Direct calls into screen.js. A screen's words go across as a view of .NET's own memory, so a frame is neither
    // copied in .NET nor marshalled, which matters at tens of frames a second.
    internal static partial class ScreenInterop
    {
        [JSImport("globalThis.exuarchScreen.draw")]
        public static partial void Draw(string canvasId, int x, int y, int width, int height,
            [JSMarshalAs<JSType.MemoryView>] Span<byte> rows, bool depth);

        [JSImport("globalThis.exuarchScreen.nextTurn")]
        public static partial Task NextTurn();
    }
}
