namespace TModDecompiler;

public class EntryReadStream : Stream
{
    private readonly TModFile _file;
    private readonly TModFileEntry _entry;
    private Stream _stream;
    private readonly bool _leaveOpen;

    private int Start => _entry.Offset;

    public string Name => _entry.Name;

    public EntryReadStream(
        TModFile file,
        TModFileEntry entry,
        Stream stream,
        bool leaveOpen)
    {
        _file = file;
        _entry = entry;
        _stream = stream;
        _leaveOpen = leaveOpen;

        if (_stream.Position != Start)
            _stream.Position = Start;
    }

    public override bool CanRead => _stream.CanRead;

    public override bool CanSeek => _stream.CanSeek;

    public override bool CanWrite => false;

    public override long Length => _entry.CompressedLength;

    public override long Position
    {
        get => _stream.Position - Start;
        set
        {
            if (value < 0 || value > Length)
                throw new ArgumentOutOfRangeException($"Position {value} outside range (0-{Length})");

            _stream.Position = value + Start;
        }
    }

    public override void Flush() => throw new NotImplementedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        count = Math.Min(count, (int)(Length - Position));
        return _stream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (origin == SeekOrigin.Current)
        {
            long target = Position + offset;
            if (target < 0 || target > Length)
                throw new ArgumentOutOfRangeException($"Position {target} outside range (0-{Length})");

            return _stream.Seek(offset, origin) - Start;
        }

        Position = origin == SeekOrigin.Begin ? offset : Length - offset;
        return Position;
    }

    public override void SetLength(long value) => throw new NotImplementedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

    public override void Close()
    {
        if (_stream == null)
            return;

        if (!_leaveOpen)
            _stream.Close();

        _stream = null;

        _file.OnStreamClosed(this);
    }
}