using System.Text;

namespace Tscs.Compiler;

// Port of pinned vfs/internal.decodeBytes and decodeUtf16. Not yet wired to the
// bootstrap CLI: this byte boundary is a prerequisite for the full VFS/scanner.
public static class SourceDecoder
{
    public static byte[] Decode(ReadOnlySpan<byte> input)
    {
        if(input.Length>=2&&((input[0]==0xff&&input[1]==0xfe)||(input[0]==0xfe&&input[1]==0xff)))
        {
            var encoding=input[0]==0xff?Encoding.Unicode:Encoding.BigEndianUnicode;
            // Go reads len(s)/2 uint16 values, ignoring an odd final byte. Its
            // utf16.Decode replaces unpaired surrogates with U+FFFD.
            string text=encoding.GetString(input.Slice(2,(input.Length-2)/2*2));
            return Encoding.UTF8.GetBytes(text);
        }
        if(input.StartsWith(new byte[]{0xef,0xbb,0xbf}))input=input[3..];
        // Go's non-UTF16 path returns the original byte sequence, including
        // malformed UTF8. No Unicode decoder may substitute replacement bytes.
        return input.ToArray();
    }
}
