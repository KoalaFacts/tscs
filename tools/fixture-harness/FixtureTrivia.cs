using System.Text;
using System.Buffers;
namespace Tscs.Conformance;

// scanner.SkipTrivia with its default options, only for discarded preludes.
internal static class FixtureTrivia
{
    private static bool LineBreak(int c)=>c is 10 or 13 or 0x2028 or 0x2029;
    private static bool Space(int c)=>LineBreak(c)||c is 9 or 11 or 12 or 32 or 0x85 or 0xa0 or 0x1680 or >=0x2000 and <=0x200b or 0x202f or 0x205f or 0x3000 or 0xfeff;
    private static int Decode(byte[] b,int p,out Rune r)
    {
        if(Rune.DecodeFromUtf8(b.AsSpan(p),out r,out int n)==OperationStatus.Done)return n;
        r=Rune.ReplacementChar;return p<b.Length?1:0;
    }
    public static bool OnlyTrivia(byte[] bytes)
    {
        bool Marker(int p)
        {
            if(p+1>=bytes.Length||bytes[p+1]!=bytes[p])return false;
            bool start=p==0||LineBreak(bytes[p-1]);
            if(!start&&p>=2){Rune.DecodeLastFromUtf8(bytes.AsSpan(0,p-2),out var previous,out _);start=LineBreak(previous.Value);}
            if(!start||p+7>=bytes.Length)return false;
            for(int n=1;n<7;n++)if(bytes[p+n]!=bytes[p])return false;
            return bytes[p]=='='||bytes[p+7]==' ';
        }
        int pos=0;
        void SkipLine(){while(pos<bytes.Length){int n=Decode(bytes,pos,out var r);if(LineBreak(r.Value))break;pos+=n;}}
        while(pos<bytes.Length)
        {
            int size=Decode(bytes,pos,out var rune),ch=rune.Value;
            if(Space(ch)){pos+=size;continue;}
            if((pos==0&&bytes.Length>1&&bytes[0]=='#'&&bytes[1]=='!')||(ch=='/'&&pos+1<bytes.Length&&bytes[pos+1]=='/')){pos+=2;SkipLine();continue;}
            if(ch=='/'&&pos+1<bytes.Length&&bytes[pos+1]=='*')
            {
                pos+=2;
                while(pos<bytes.Length){if(bytes[pos]=='*'&&pos+1<bytes.Length&&bytes[pos+1]=='/'){pos+=2;break;}pos+=Decode(bytes,pos,out _);}
                continue;
            }
            if(ch is '<' or '>' or '|' or '=' && Marker(pos))
            {
                if(ch is '<' or '>')SkipLine();
                else{pos++;while(pos<bytes.Length&&!((bytes[pos] is (byte)'=' or (byte)'>')&&bytes[pos]!=ch&&Marker(pos)))pos++;}
                continue;
            }
            return false;
        }
        return true;
    }
}
