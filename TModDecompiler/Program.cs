using TModDecompiler;

if (args.Length != 2)
{
    Console.WriteLine("Wrong count of parameters");
    Environment.Exit(0);
}

var modFilePath = args[0];

if (string.IsNullOrEmpty(modFilePath))
{
    Console.WriteLine("Mod file path is empty");
    Environment.Exit(0);
}

var extractToPath = args[1];

if (string.IsNullOrEmpty(extractToPath))
{
    Console.WriteLine("Extraction path is empty");
    Environment.Exit(0);
}

var lastModified = File.GetLastWriteTime(modFilePath);

var modFile = new TModFile(modFilePath);

using (modFile.Open())
{
    var mod = new LocalMod(modFile)
    {
        LastModified = lastModified
    };

    Extract(mod, extractToPath);
}


void Extract(
    LocalMod mod, 
    string extractTo)
{
    IDisposable? modHandle = null;

    try
    {
        modHandle = mod.ModFile.Open();
        foreach (var entry in mod.ModFile)
        {
            var name = entry.Name;

            var path = Path.Combine(extractTo, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using var destination = File.OpenWrite(path);
            using var source = mod.ModFile.GetStream(entry);
            source.CopyTo(destination);
        }
    }
    finally
    {
        modHandle?.Dispose();
    }
}