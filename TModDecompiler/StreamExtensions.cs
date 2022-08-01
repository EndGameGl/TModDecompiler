namespace TModDecompiler;

public static class StreamExtensions
{
    public static void ReadBytes(this Stream stream, byte[] buf)
    {
        int r, pos = 0;
        while ((r = stream.Read(buf, pos, buf.Length - pos)) > 0)
            pos += r;

        if (pos != buf.Length)
            throw new IOException($"Stream did not contain enough bytes ({pos}) < ({buf.Length})");
    }

    public static byte[] ReadBytes(this Stream stream, long len)
    {
        var buf = new byte[len];
        stream.ReadBytes(buf);
        return buf;
    }
}