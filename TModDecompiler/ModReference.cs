namespace TModDecompiler;

public struct ModReference
{
    public string Mod { get; }
    public Version Target { get; }

    public ModReference(string mod, Version target)
    {
        Mod = mod;
        Target = target;
    }

    public override string ToString() => Target == null ? Mod : Mod + '@' + Target;

    public static ModReference Parse(string spec)
    {
        var split = spec.Split('@');
        if (split.Length == 1)
            return new ModReference(split[0], null);

        if (split.Length > 2)
            throw new Exception("Invalid mod reference: " + spec);

        try
        {
            return new ModReference(split[0], new Version(split[1]));
        }
        catch
        {
            throw new Exception("Invalid mod reference: " + spec);
        }
    }
}