using System.Collections;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace TModDecompiler;

public class TModFile : IEnumerable<TModFileEntry>
{
    public const uint MIN_COMPRESS_SIZE = 1 << 10; //1KB
    public const uint MAX_CACHE_SIZE = 1 << 17; //128KB
    public const float COMPRESSION_TRADEOFF = 0.9f;

    private static string Sanitize(string path) => path.Replace('\\', '/');

    public readonly string path;

    private FileStream fileStream;
    private IDictionary<string, TModFileEntry> files = new Dictionary<string, TModFileEntry>();
    private TModFileEntry[] fileTable;

    private int openCounter;
    private EntryReadStream sharedEntryReadStream;
    private List<EntryReadStream> independentEntryReadStreams = new List<EntryReadStream>();

    public Version TModLoaderVersion { get; private set; }

    public string Name { get; private set; }

    public Version Version { get; private set; }

    public byte[] Hash { get; private set; }

    internal byte[] Signature { get; private set; } = new byte[256];

    internal TModFile(string path, string name = null, Version version = null)
    {
        this.path = path;
        this.Name = name;
        this.Version = version;
    }

    public bool HasFile(string fileName) => files.ContainsKey(Sanitize(fileName));

    public byte[] GetBytes(TModFileEntry entry)
    {
        if (entry.cachedBytes != null && !entry.IsCompressed)
            return entry.cachedBytes;

        using (var stream = GetStream(entry))
            return stream.ReadBytes(entry.Length);
    }

    public Stream GetStream(TModFileEntry entry, bool newFileStream = false)
    {
        Stream stream;
        if (entry.cachedBytes != null)
        {
            stream = new MemoryStream(entry.cachedBytes);
        }
        else if (fileStream == null)
        {
            throw new IOException($"File not open: {path}");
        }
        else if (newFileStream)
        {
            var ers = new EntryReadStream(this, entry, File.OpenRead(path), false);
            lock (independentEntryReadStreams)
            {
                // todo, make this a set? maybe?
                independentEntryReadStreams.Add(ers);
            }

            stream = ers;
        }
        else if (sharedEntryReadStream != null)
        {
            throw new IOException($"Previous entry read stream not closed: {sharedEntryReadStream.Name}");
        }
        else
        {
            stream = sharedEntryReadStream = new EntryReadStream(this, entry, fileStream, true);
        }

        if (entry.IsCompressed)
            stream = new DeflateStream(stream, CompressionMode.Decompress);

        return stream;
    }

    internal void OnStreamClosed(EntryReadStream stream)
    {
        if (stream == sharedEntryReadStream)
        {
            sharedEntryReadStream = null;
        }
        else
        {
            lock (independentEntryReadStreams)
            {
                if (!independentEntryReadStreams.Remove(stream))
                    throw new IOException(
                        $"Closed EntryReadStream not associated with this file. {stream.Name} @ {path}");
            }
        }
    }

    public Stream GetStream(string fileName, bool newFileStream = false)
    {
        if (!files.TryGetValue(Sanitize(fileName), out var entry))
            throw new KeyNotFoundException(fileName);

        return GetStream(entry, newFileStream);
    }
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IEnumerator<TModFileEntry> GetEnumerator()
    {
        foreach (var entry in fileTable)
            yield return entry;
    }

    private class DisposeWrapper : IDisposable
    {
        private readonly Action dispose;

        public DisposeWrapper(Action dispose)
        {
            this.dispose = dispose;
        }

        public void Dispose() => dispose?.Invoke();
    }

    public IDisposable? Open()
    {
        if (openCounter++ == 0)
        {
            if (fileStream != null)
                throw new Exception($"File already opened? {path}");

            try
            {
                if (Name == null)
                    Read();
                else
                    Reopen();
            }
            catch
            {
                try
                {
                    Close();
                }
                catch
                {
                }

                throw;
            }
        }

        return new DisposeWrapper(Close);
    }

    private void Close()
    {
        if (openCounter == 0)
            return;

        if (--openCounter == 0)
        {
            if (sharedEntryReadStream != null)
                throw new IOException($"Previous entry read stream not closed: {sharedEntryReadStream.Name}");
            if (independentEntryReadStreams.Count != 0)
                throw new IOException(
                    $"Shared entry read streams not closed: {string.Join(", ", independentEntryReadStreams.Select(e => e.Name))}");

            fileStream?.Close();
            fileStream = null;
        }
    }

    private void Read()
    {
        fileStream = File.OpenRead(path);
        var reader =
            new BinaryReader(
                fileStream); //intentionally not disposed to leave the stream open. In .NET 4.5+ the 3-arg constructor could be used

        // read header info
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "TMOD")
            throw new Exception("Magic Header != \"TMOD\"");

        TModLoaderVersion = new Version(reader.ReadString());
        Hash = reader.ReadBytes(20);
        Signature = reader.ReadBytes(256);
        //currently unused, included to read the entire data-blob as a byte-array without decompressing or waiting to hit end of stream
        int datalen = reader.ReadInt32();

        // verify integrity
        long pos = fileStream.Position;
        var verifyHash = SHA1.Create().ComputeHash(fileStream);
        if (!verifyHash.SequenceEqual(Hash))
            throw new Exception("tModLoader.LoadErrorHashMismatchCorrupted");

        fileStream.Position = pos;

        if (TModLoaderVersion < new Version(0, 11))
        {
            //Upgrade();
            return;
        }

        // read hashed/signed mod info
        Name = reader.ReadString();
        Version = new Version(reader.ReadString());

        // read file table
        int offset = 0;
        fileTable = new TModFileEntry[reader.ReadInt32()];
        for (int i = 0; i < fileTable.Length; i++)
        {
            var f = new TModFileEntry(
                reader.ReadString(),
                offset,
                reader.ReadInt32(),
                reader.ReadInt32());
            fileTable[i] = f;
            files[f.Name] = f;

            offset += f.CompressedLength;
        }

        int fileStartPos = (int)fileStream.Position;
        foreach (var f in fileTable)
            f.Offset += fileStartPos;
    }

    private void Reopen()
    {
        fileStream = File.OpenRead(path);
        var reader =
            new BinaryReader(
                fileStream); //intentionally not disposed to leave the stream open. In .NET 4.5+ the 3-arg constructor could be used

        // read header info
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "TMOD")
            throw new Exception("Magic Header != \"TMOD\"");

        reader.ReadString(); //tModLoader version
        if (!reader.ReadBytes(20).SequenceEqual(Hash))
            throw new Exception($"File has been modifed, hash. {path}");

        // could also check name and version but hash should suffice
    }
}