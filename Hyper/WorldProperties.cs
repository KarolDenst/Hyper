namespace Hyper;
internal class WorldProperties
{
    public static WorldProperties Instance
    {
        get => _properties ??= new WorldProperties(0.05f, 0f);
    }

    public float Scale { get; set; }
    public float Curve { get; set; }

    private static WorldProperties? _properties;

    private WorldProperties(float scale, float curve)
    {
        Scale = scale;
        Curve = curve;
    }
}
