namespace CustomHUD.Config;

// Indirection so live config reloads reach services that were constructed once
// at load. Holding CustomHudConfig directly would pin every service to the
// values present at startup.
public sealed class CustomHudConfigProvider
{
    private volatile CustomHudConfig _current = new();

    public CustomHudConfig Current => _current;

    public void Replace(CustomHudConfig config) => _current = config;
}
