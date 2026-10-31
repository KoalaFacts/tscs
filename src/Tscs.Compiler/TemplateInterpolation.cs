namespace Tscs;

// Find one substitution's closing brace while respecting lexical strings,
// comments and finite nested braces. Nested untagged templates are lexically bounded.
internal static class TemplateInterpolation
{
    internal static int FindClose(string text,int begin)=>FindClose(text,begin,0);

    private static int FindClose(string text,int begin,int templateDepth)
    {
        int depth=1;
        for(int i=begin;i<text.Length;i++)
        {
            char c=text[i];
            if(c=='`')
            {
                int end=FindTemplateEnd(text,i+1,templateDepth+1);
                if(end<0)return -1;
                i=end;
                continue;
            }
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
                    while(i<text.Length && !Scanner.IsLineBreak(text[i]))i++;
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

    // Skip a complete nested untagged backtick literal, including its own
    // substitutions. Do not treat braces/backticks inside it as enclosing
    // expression delimiters. The finite depth bound fails closed.
    private static int FindTemplateEnd(string text,int begin,int depth)
    {
        if(depth>8)return -1;
        for(int i=begin;i<text.Length;i++)
        {
            char c=text[i];
            if(c=='\\'){i++;continue;}
            if(c=='`')return i;
            if(c=='$' && i+1<text.Length && text[i+1]=='{')
            {
                int end=FindClose(text,i+2,depth);
                if(end<0)return -1;
                i=end;
            }
        }
        return -1;
    }
}
