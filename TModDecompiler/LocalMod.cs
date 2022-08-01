namespace TModDecompiler;

public class LocalMod
{
    public TModFile ModFile { get; }
    public BuildProperties Properties { get; }
    public DateTime LastModified { get; set; }
    public string Name => ModFile.Name;

    public override string ToString() => Name;

    public LocalMod(TModFile modFile, BuildProperties properties)
    {
        ModFile = modFile;
        Properties = properties;
    }

    public LocalMod(TModFile modFile) : this(modFile, BuildProperties.ReadModFile(modFile))
    {
    }
}