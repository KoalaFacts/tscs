namespace Tscs;

// Find one substitution's closing brace while respecting lexical strings,
// comments and finite nested braces. Nested templates remain unsupported.
internal static class TemplateInterpolation
{
    internal static int FindClose(string text,int begin)
    {
        int depth=1;
        for(int i=begin;i<text.Length;i++)
        {
            char c=text[i];
            if(c=='`')return -1;
            if(c is '\'' or '"')
            {
                char quote=c;
                bool closed=false;
                while(++i<text.Length)
                {
                    if(text[i]=='\\')
                    {
                        if(i+1>=text.Length)return -1;
                        i++;
                        continue;
                    }
                    if(text[i]==quote){closed=true;break;}
                    if(text[i] is '\r' or '\n')return -1;
                }
                if(!closed)return -1;
                continue;
            }
            if(c=='/' && i+1<text.Length)
            {
                if(text[i+1]=='/')
                {
                    i+=2;
                    while(i<text.Length && text[i] is not ('\r' or '\n'))i++;
                    continue;
                }
                if(text[i+1]=='*')
                {
                    var end=text.IndexOf("*/",i+2,StringComparison.Ordinal);
                    if(end<0)return -1;
                    i=end+1;continue;
                }
            }
            if(c=='{')
            {
                if(++depth>32)return -1;
            }
            else if(c=='}' && --depth==0)return i;
        }
        return -1;
    }
}
