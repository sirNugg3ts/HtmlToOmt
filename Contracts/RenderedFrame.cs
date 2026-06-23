namespace sirNugg3ts.HtmlToOmt.Contracts;

public sealed class RenderedFrame
{
    public RenderedFrame(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
    public int Stride => Width * 4;
}
