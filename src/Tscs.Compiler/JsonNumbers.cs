using System.Globalization;
namespace Tscs;
// Canonical numeric token value for the supported unsigned decimal-integer
// spelling subset. Sign belongs to the JSON prefix-unary AST projection.
internal static class JsonNumbers
{
    public static string CanonicalInteger(string text)
    {
        double number=double.Parse(text,NumberStyles.None,CultureInfo.InvariantCulture);
        if(double.IsPositiveInfinity(number))return "Infinity";
        if(number==0)return "0";
        // Round to binary64 first, then use the shortest round-tripping digits.
        // Apply ECMAScript's fixed/scientific threshold, not .NET's threshold.
        string roundtrip=number.ToString("R",CultureInfo.InvariantCulture);
        int at=roundtrip.IndexOf('E');
        string mantissa=at<0?roundtrip:roundtrip[..at];
        int exponent=at<0?0:int.Parse(roundtrip.AsSpan(at+1),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture);
        int point=mantissa.IndexOf('.');
        int decimalPosition=(point<0?mantissa.Length:point)+exponent;
        string digits=mantissa.Replace(".","",StringComparison.Ordinal).TrimEnd('0');
        if(decimalPosition<=21)
            return digits.Length<=decimalPosition?digits+new string('0',decimalPosition-digits.Length):digits.Insert(decimalPosition,".");
        return digits[0]+(digits.Length>1?"."+digits[1..]:"")+"e+"+(decimalPosition-1).ToString(CultureInfo.InvariantCulture);
    }
}
