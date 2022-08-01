namespace TModDecompiler;

public class BuildProperties
{
    public string[] DllReferences { get; private set; } = Array.Empty<string>();
    public ModReference[] ModReferences { get; private set; } = Array.Empty<ModReference>();
    public ModReference[] WeakReferences { get; private set; } = Array.Empty<ModReference>();
    public string[] SortAfter { get; private set; } = Array.Empty<string>();
    public string[] SortBefore { get; private set; } = Array.Empty<string>();

    public string[] BuildIgnores { get; private set; } = Array.Empty<string>();
    public string Author { get; private set; } = string.Empty;
    public Version Version { get; private set; } = new(1, 0);
    public string DisplayName { get; private set; } = string.Empty;
    public bool NoCompile { get; private set; }
    public bool HideCode { get; private set; }
    public bool HideResources { get; private set; }
    public bool IncludeSource { get; private set; }
    public string EacPath { get; private set; } = string.Empty;

    public bool Beta { get; private set; }
    public string Homepage { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ModSide Side { get; private set; }
    public bool PlayableOnPreview { get; private set; } = true;

    private static IEnumerable<string> ReadList(BinaryReader reader)
    {
        var list = new List<string>();
        for (var item = reader.ReadString(); item.Length > 0; item = reader.ReadString())
            list.Add(item);

        return list;
    }
    public static BuildProperties ReadModFile(TModFile modFile)
    {
        return ReadFromStream(modFile.GetStream("Info"));
    }
    public static BuildProperties ReadFromStream(Stream stream)
    {
        var properties = new BuildProperties
        {
            HideCode = true,
            HideResources = true
        };

        using var reader = new BinaryReader(stream);
        for (var tag = reader.ReadString(); tag.Length > 0; tag = reader.ReadString())
        {
            switch (tag)
            {
                case "dllReferences":
                    properties.DllReferences = ReadList(reader).ToArray();
                    break;
                case "modReferences":
                    properties.ModReferences = ReadList(reader).Select(ModReference.Parse).ToArray();
                    break;
                case "weakReferences":
                    properties.WeakReferences = ReadList(reader).Select(ModReference.Parse).ToArray();
                    break;
                case "sortAfter":
                    properties.SortAfter = ReadList(reader).ToArray();
                    break;
                case "sortBefore":
                    properties.SortBefore = ReadList(reader).ToArray();
                    break;
                case "author":
                    properties.Author = reader.ReadString();
                    break;
                case "version":
                    properties.Version = new Version(reader.ReadString());
                    break;
                case "displayName":
                    properties.DisplayName = reader.ReadString();
                    break;
                case "homepage":
                    properties.Homepage = reader.ReadString();
                    break;
                case "description":
                    properties.Description = reader.ReadString();
                    break;
                case "noCompile":
                    properties.NoCompile = true;
                    break;
                case "!playableOnPreview":
                    properties.PlayableOnPreview = false;
                    break;
                case "!hideCode":
                    properties.HideCode = false;
                    break;
                case "!hideResources":
                    properties.HideResources = false;
                    break;
                case "includeSource":
                    properties.IncludeSource = true;
                    break;
                case "eacPath":
                    properties.EacPath = reader.ReadString();
                    break;
                case "side":
                    properties.Side = (ModSide)reader.ReadByte();
                    break;
                case "buildVersion":
                    break;
            }
        }

        return properties;
    }
}