namespace WebUI.Components
{
    // An element's bounding box in the viewport, as byocEditor.rect returns it.
    public class ElementRect
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
