namespace Tscs;

// Semantic types are independent of the source spelling in the syntax tree.
// Future function, object, union and type-parameter nodes extend this identity
// model; binder, module linking and checker consume the same type objects.
internal abstract record TypeSymbol
{
    public abstract string DisplayName { get; }
}

internal enum PrimitiveKind { Number, String, Boolean, Any, Void, Unknown, Never, Undefined, Null }

internal sealed record PrimitiveTypeSymbol(PrimitiveKind Kind) : TypeSymbol
{
    public override string DisplayName => Kind switch
    {
        PrimitiveKind.Number => "number",
        PrimitiveKind.String => "string",
        PrimitiveKind.Boolean => "boolean",
        PrimitiveKind.Any => "any",
        PrimitiveKind.Void => "void",
        PrimitiveKind.Unknown => "unknown",
        PrimitiveKind.Never => "never",
        PrimitiveKind.Undefined => "undefined",
        PrimitiveKind.Null => "null",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind))
    };
}

// Literal types refine primitive values only at explicit/contextual type sites.
internal sealed record LiteralTypeSymbol(PrimitiveKind Kind,string Value) : TypeSymbol
{
    public override string DisplayName=>Kind==PrimitiveKind.String
        ?System.Text.Json.JsonSerializer.Serialize(Value):Value;
}

internal sealed record ArrayTypeSymbol(TypeSymbol ElementType,bool Readonly=false) : TypeSymbol
{
    public override string DisplayName=>TypeDisplay.Format(this);
}

internal sealed record UnionTypeSymbol(IReadOnlyList<TypeSymbol> Alternatives) : TypeSymbol
{
    public override string DisplayName=>TypeDisplay.Format(this);
}

// Record both reachable logical branches so contextual excess-property checks
// can inspect fresh operand literals rather than only the joined type.
internal sealed record LogicalValueOutcome(TypeSymbol Result,TypeSymbol? LeftResult,TypeSymbol Right,bool RightReachable);

internal sealed record FunctionTypeSymbol(IReadOnlyList<TypeSymbol> Parameters,TypeSymbol ReturnType,int RequiredParameterCount=-1,TypeSymbol? RestElementType=null,TypeSymbol? NativeArrayMapElementType=null,TypeSymbol? NativeArrayReduceElementType=null) : TypeSymbol
{
    public int MinimumArguments=>RequiredParameterCount<0?Parameters.Count:RequiredParameterCount;
    public override string DisplayName=>TypeDisplay.Format(this);
}

// These relations cover primitives, any and required structural object data.
// Unsupported type forms remain errors; unresolved inference is null, while
// the caught runtime exception has a real unknown semantic type.
internal static class CompilerTypes
{
    public static readonly TypeSymbol Number=new PrimitiveTypeSymbol(PrimitiveKind.Number);
    public static readonly TypeSymbol String=new PrimitiveTypeSymbol(PrimitiveKind.String);
    public static readonly TypeSymbol Boolean=new PrimitiveTypeSymbol(PrimitiveKind.Boolean);
    public static readonly TypeSymbol Any=new PrimitiveTypeSymbol(PrimitiveKind.Any);
    public static readonly TypeSymbol Void=new PrimitiveTypeSymbol(PrimitiveKind.Void);
    public static readonly TypeSymbol Undefined=new PrimitiveTypeSymbol(PrimitiveKind.Undefined);
    public static readonly TypeSymbol Null=new PrimitiveTypeSymbol(PrimitiveKind.Null);
    // The immutable pinned Go reference treats catch bindings as unknown.
    public static readonly TypeSymbol Unknown=new PrimitiveTypeSymbol(PrimitiveKind.Unknown);
    // Internal inference for an empty literal. 'never' annotations and
    // evolving array inference remain outside this supported subset.
    public static readonly TypeSymbol Never=new PrimitiveTypeSymbol(PrimitiveKind.Never);

    public static LiteralTypeSymbol FromLiteralExact(Token token,bool negative=false)=>token.Kind switch
    {
        TokenKind.String when !negative=>new(PrimitiveKind.String,token.Value??token.Text),
        TokenKind.Number=>new(PrimitiveKind.Number,CanonicalNumericLiteral(token.Value??token.Text,negative)),
        TokenKind.True when !negative=>new(PrimitiveKind.Boolean,"true"),
        TokenKind.False when !negative=>new(PrimitiveKind.Boolean,"false"),
        _=>throw new ArgumentOutOfRangeException(nameof(token),"Only parsed primitive literals may form literal types.")
    };

    // Literal identity follows the ECMAScript Number value, not source spelling:
    // 1, 1.0 and 1e0 share one semantic literal type; -0 is identical to 0.
    private static string CanonicalNumericLiteral(string text,bool negative)
    {
        if (!double.TryParse(text,System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,out var number) ||
            !double.IsFinite(number))
            throw new InvalidOperationException("Scanner must reject malformed and nonfinite numeric tokens.");
        if (negative) number=-number;
        return number==0?"0":number.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
    }

    public static TypeSymbol Union(IEnumerable<TypeSymbol> variants)
    {
        var options=new List<TypeSymbol>();
        foreach(var variant in variants)
        {
            foreach(var type in variant is UnionTypeSymbol nested ? nested.Alternatives : [variant])
            {
                if(type.Equals(Any))return Any;
                if(type.Equals(Never))continue;
                if(options.All(existing=>!IsEquivalent(existing,type)))options.Add(type);
                if(options.Count>64)throw new InvalidOperationException("Too many union variants for the bounded compiler.");
            }
        }
        // A primitive absorbs narrower literals; true | false is boolean.
        // This also makes stored boolean/number values assignable to the
        // corresponding fully covered primitive-literal union.
        foreach(var kind in new[]{PrimitiveKind.Number,PrimitiveKind.String,PrimitiveKind.Boolean})
            if(options.Any(x=>x is PrimitiveTypeSymbol primitive && primitive.Kind==kind))
                options.RemoveAll(x=>x is LiteralTypeSymbol lit && lit.Kind==kind);
        if(options.Any(x=>x is LiteralTypeSymbol { Kind:PrimitiveKind.Boolean,Value:"true" }) &&
           options.Any(x=>x is LiteralTypeSymbol { Kind:PrimitiveKind.Boolean,Value:"false" }))
        {
            options.RemoveAll(x=>x is LiteralTypeSymbol { Kind:PrimitiveKind.Boolean });
            options.Add(Boolean);
        }
        return options.Count switch {0=>Never,1=>options[0],_=>new UnionTypeSymbol(options)};
    }

    // Optional access and postfix ! recognise both null and undefined.
    // Only explicit nullish constituents are removed; void/unknown stay intact.
    public static bool ContainsUndefined(TypeSymbol value)=>
        value.Equals(Undefined) || value is UnionTypeSymbol union &&
        union.Alternatives.Any(type=>type.Equals(Undefined));
    public static bool ContainsNullish(TypeSymbol value)=>
        value.Equals(Null)||value.Equals(Undefined) || value is UnionTypeSymbol union &&
        union.Alternatives.Any(type=>type.Equals(Null)||type.Equals(Undefined));

    public static TypeSymbol NonNullAssertionResult(TypeSymbol value)
    {
        if(value.Equals(Undefined)||value.Equals(Null))return Never;
        if(value is not UnionTypeSymbol union)return value;
        return Union(union.Alternatives.Where(type=>!type.Equals(Undefined)&&!type.Equals(Null)));
    }


    // The value of a JavaScript logical expression is an operand, not a coerced
    // boolean. Preserve its possibly falsy left value (&&) or truthy left
    // value (||) alongside the lazily evaluated right operand.
    // Unknown/void are deliberately excluded until the full TS semantics are
    // represented; callers report an explicit unsupported diagnostic.
    public static LogicalValueOutcome? LogicalValueResult(TypeSymbol left,TypeSymbol right,bool conjunction)
    {
        if(left.Equals(Any))return new(Any,Any,right,true);
        var alternatives=left is UnionTypeSymbol union?union.Alternatives.ToArray():[left];
        var truthy=new List<TypeSymbol>();
        var falsy=new List<TypeSymbol>();
        foreach(var candidate in alternatives)
        {
            switch(candidate)
            {
                case PrimitiveTypeSymbol { Kind:PrimitiveKind.Null or PrimitiveKind.Undefined }:
                    falsy.Add(candidate);
                    break;
                case PrimitiveTypeSymbol { Kind:PrimitiveKind.Boolean }:
                    truthy.Add(new LiteralTypeSymbol(PrimitiveKind.Boolean,"true"));
                    falsy.Add(new LiteralTypeSymbol(PrimitiveKind.Boolean,"false"));
                    break;
                case PrimitiveTypeSymbol { Kind:PrimitiveKind.Number }:
                    truthy.Add(candidate);
                    falsy.Add(new LiteralTypeSymbol(PrimitiveKind.Number,"0"));
                    break;
                case PrimitiveTypeSymbol { Kind:PrimitiveKind.String }:
                    truthy.Add(candidate);
                    falsy.Add(new LiteralTypeSymbol(PrimitiveKind.String,""));
                    break;
                case LiteralTypeSymbol { Kind:PrimitiveKind.Boolean } boolean:
                    (boolean.Value=="true"?truthy:falsy).Add(candidate);
                    break;
                case LiteralTypeSymbol { Kind:PrimitiveKind.String } str:
                    (str.Value.Length>0?truthy:falsy).Add(candidate);
                    break;
                case LiteralTypeSymbol { Kind:PrimitiveKind.Number } number:
                    if(!double.TryParse(number.Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,out var n))
                        return null;
                    (n!=0 && !double.IsNaN(n)?truthy:falsy).Add(candidate);
                    break;
                case ObjectTypeSymbol objectType:
                    truthy.Add(candidate);
                    // Structural {} also accepts falsy primitive values.
                    if(objectType.Members.Count==0)falsy.Add(candidate);
                    break;
                case ArrayTypeSymbol or FunctionTypeSymbol:
                    truthy.Add(candidate);
                    break;
                case PrimitiveTypeSymbol { Kind:PrimitiveKind.Never }:
                    break;
                default:
                    return null;
            }
        }
        // A right-side 'any' is only part of the result if evaluation can
        // actually reach it; e.g. null && any must remain null, not any.
        var shortCircuit=conjunction?falsy:truthy;
        bool rightReachable=conjunction?truthy.Count>0:falsy.Count>0;
        TypeSymbol? leftResult=shortCircuit.Count==0?null:Union(shortCircuit);
        TypeSymbol result=rightReachable
            ?leftResult is null?right:Union([leftResult,right])
            :leftResult??Never;
        return new(result,leftResult,right,rightReachable);
    }

    public static TypeSymbol CoalescingResult(TypeSymbol left,TypeSymbol right)
    {
        if(left.Equals(Any) || right.Equals(Any))return Any;
        if(left.Equals(Unknown))return Unknown;
        var alternatives=left is UnionTypeSymbol union
            ?union.Alternatives.ToArray():[left];
        var live=alternatives.Where(type=>!type.Equals(Undefined) && !type.Equals(Null) && !type.Equals(Void)).ToList();
        if(live.Count==alternatives.Length)return left;
        live.Add(right);
        return Union(live);
    }

    public static TypeSymbol FromAnnotation(string name) => name switch
    {
        "number" => Number,
        "string" => String,
        "boolean" => Boolean,
        "any" => Any,
        "void" => Void,
        "undefined" => Undefined,
        "null" => Null,
        _ => throw new ArgumentOutOfRangeException(nameof(name),
            "Only parser-validated primitive annotations may reach the semantic type model.")
    };

    public static TypeSymbol FromAnnotation(TypeSyntax syntax)=>syntax switch
    {
        PrimitiveTypeSyntax primitive=>FromAnnotation(primitive.Name.Text),
        ArrayTypeSyntax array=>new ArrayTypeSymbol(FromAnnotation(array.Element),array.Readonly),
        ObjectTypeSyntax shape=>new ObjectTypeSymbol(shape.Properties.Select(p=>
            new KeyValuePair<string,TypeSymbol>(Parser.PropertyName(p.Name),FromAnnotation(p.Type))),
            shape.Properties.Where(p=>p.Readonly).Select(p=>Parser.PropertyName(p.Name)),
            shape.Properties.Where(p=>p.Name.Kind==TokenKind.Number).Select(p=>Parser.PropertyName(p.Name)),
            shape.Properties.Where(p=>p.Optional).Select(p=>Parser.PropertyName(p.Name))),
        _=>throw new ArgumentOutOfRangeException(nameof(syntax))
    };

    // Prebinding may infer closed literal shapes without consulting another
    // symbol. Other cross-file initializers still need an explicit annotation.
    public static TypeSymbol? FromInitializer(ValueNode? value)
    {
        while(value is ValueParenthesized group)value=group.Expression;
        if(value is ValueTypeOperator { Keyword.Text:"satisfies" } verified)
            return FromInitializer(verified.Expression);
        if(value is ValueAtom atom)return FromLiteral(atom.Token);
        if(value is ValueArray array)
        {
            TypeSymbol? element=Never;
            foreach(var item in array.Elements)
            {
                TypeSymbol? inferred=item is ValueArraySpread spread
                    ? FromInitializer(spread.Operand) is ArrayTypeSymbol arraySource
                        ?arraySource.ElementType:null
                    :FromInitializer(item);
                if(inferred is not { } actual)return null;
                if(element==Never)element=actual;
                else if(!IsEquivalent(element,actual))
                {
                    try {element=Union([element,actual]);}
                    catch(InvalidOperationException) {return null;}
                }
            }
            return new ArrayTypeSymbol(element!);
        }
        if(value is not ValueObject literal)return null;
        var members=new Dictionary<string,TypeSymbol>(StringComparer.Ordinal);
        var numericMembers=new HashSet<string>(StringComparer.Ordinal);
        var optionalMembers=new HashSet<string>(StringComparer.Ordinal);
        foreach(var property in literal.Properties)
        {
            if(FromInitializer(property.Value) is not { } type)return null;
            if(property.Spread)
            {
                if(type is not ObjectTypeSymbol source)return null;
                foreach(var member in source.Members)
                {
                    if(source.OptionalMembers.Contains(member.Key) && members.ContainsKey(member.Key))
                        return null; // Conditional overlap of an optional spread is not modeled.
                    members[member.Key]=member.Value;
                    if(source.NumericMembers.Contains(member.Key))numericMembers.Add(member.Key);
                    else numericMembers.Remove(member.Key);
                    if(source.OptionalMembers.Contains(member.Key))optionalMembers.Add(member.Key);
                    else optionalMembers.Remove(member.Key);
                }
            }
            else
            {
                var name=Parser.PropertyName(property.Name);
                members[name]=type;
                optionalMembers.Remove(name);
                if(property.Name.Kind==TokenKind.Number)numericMembers.Add(name);
                else numericMembers.Remove(name);
            }
        }
        return new ObjectTypeSymbol(members,numericMembers:numericMembers,optionalMembers:optionalMembers);
    }

    // A successful satisfies expression preserves the source's inferred shape,
    // with contextual exact literal members retained for later writes.
    // Stored variables are not narrowed just because they satisfy a constraint.
    public static TypeSymbol PreserveSatisfiedLiteralTypes(
        ValueNode expression,TypeSymbol actual,TypeSymbol constraint,int depth=0)
    {
        if(depth>=64)return actual;
        while(expression is ValueParenthesized group)expression=group.Expression;
        if(expression is ValueAtom atom && atom.Token.Kind is
            (TokenKind.Number or TokenKind.String or TokenKind.True or TokenKind.False))
        {
            var literal=FromLiteralExact(atom.Token);
            if(constraint is LiteralTypeSymbol expected && literal.Equals(expected))
                return literal;
            if(constraint is UnionTypeSymbol alternatives &&
               alternatives.Alternatives.Any(option=>option.Equals(literal)))
                return literal;
        }
        if(expression is ValueUnary { Operator.Kind:TokenKind.Minus,Operand:ValueAtom number } &&
           number.Token.Kind==TokenKind.Number)
        {
            var literal=FromLiteralExact(number.Token,negative:true);
            if(constraint is LiteralTypeSymbol expected && literal.Equals(expected))
                return literal;
            if(constraint is UnionTypeSymbol alternatives &&
               alternatives.Alternatives.Any(option=>option.Equals(literal)))
                return literal;
        }
        if(expression is ValueObject fresh && actual is ObjectTypeSymbol data &&
           constraint is ObjectTypeSymbol shape)
        {
            var members=new List<KeyValuePair<string,TypeSymbol>>(data.Members.Count);
            foreach(var (name,type) in data.Members)
            {
                TypeSymbol selected=type;
                if(shape.Members.TryGetValue(name,out var required))
                {
                    var property=fresh.Properties.LastOrDefault(p=>!p.Spread && Parser.PropertyName(p.Name)==name);
                    if(property!=null)
                        selected=PreserveSatisfiedLiteralTypes(property.Value,type,required,depth+1);
                }
                members.Add(new(name,selected));
            }
            return new ObjectTypeSymbol(members,data.ReadonlyMembers,data.NumericMembers,data.OptionalMembers);
        }
        return actual;
    }

    public static TypeSymbol? FromLiteral(Token token) => token.Kind switch
    {
        TokenKind.Number => Number,
        TokenKind.String => String,
        TokenKind.True or TokenKind.False => Boolean,
        TokenKind.Null => Null,
        _ => null
    };

    public static bool IsEquivalent(TypeSymbol a,TypeSymbol b)=>
        // Assignment may treat runtime property 0 and "0" alike, whereas
        // keyof distinguishes their *source* identity at every nested level.
        // Never collapse union alternatives with different nested key types.
        PreservesNumericKeyIdentity(a,b) && IsAssignable(a,b) && IsAssignable(b,a);

    private static bool PreservesNumericKeyIdentity(TypeSymbol left,TypeSymbol right)
    {
        var pending=new Stack<(TypeSymbol Left,TypeSymbol Right)>();
        var visited=new HashSet<(TypeSymbol Source,TypeSymbol Target)>(TypePairComparer.Instance);
        pending.Push((left,right));
        int budget=16384;
        while(pending.Count>0)
        {
            if(--budget<0)return false; // preserve separate alternatives if uncertain
            var (actual,required)=pending.Pop();
            if(ReferenceEquals(actual,required) || !visited.Add((actual,required)))continue;
            switch(actual,required)
            {
                case (ObjectTypeSymbol a,ObjectTypeSymbol b):
                    if(!a.NumericMembers.SetEquals(b.NumericMembers) ||
                       !a.OptionalMembers.SetEquals(b.OptionalMembers))return false;
                    foreach(var (name,member) in a.Members)
                        if(b.Members.TryGetValue(name,out var other))
                            pending.Push((member,other));
                    break;
                case (ArrayTypeSymbol a,ArrayTypeSymbol b):
                    if(a.Readonly!=b.Readonly)return false;
                    pending.Push((a.ElementType,b.ElementType));
                    break;
                case (FunctionTypeSymbol a,FunctionTypeSymbol b):
                    if(a.Parameters.Count!=b.Parameters.Count)return false;
                    for(int i=0;i<a.Parameters.Count;i++)
                        pending.Push((a.Parameters[i],b.Parameters[i]));
                    pending.Push((a.ReturnType,b.ReturnType));
                    if(a.RestElementType is { } restA && b.RestElementType is { } restB)
                        pending.Push((restA,restB));
                    else if((a.RestElementType is null)!=(b.RestElementType is null))
                        return false;
                    break;
                case (UnionTypeSymbol a,UnionTypeSymbol b):
                    if(a.Alternatives.Count!=b.Alternatives.Count)return false;
                    // Conservatively keep alternatives rather than coalesce
                    // when different ordering prevents a simple safe pairing.
                    for(int i=0;i<a.Alternatives.Count;i++)
                        pending.Push((a.Alternatives[i],b.Alternatives[i]));
                    break;
            }
        }
        return true;
    }

    // A read through a union is safe only when every alternative has the
    // requested property. Its result type is the union of member types.
    public static TypeSymbol? TryGetProperty(TypeSymbol receiver,string name)
    {
        if(receiver.Equals(Any))return Any;
        // Native array reads are available through mutable and readonly views.
        // Optional arguments include explicit undefined, matching TypeScript's
        // standard array signatures. The ordinary call checker and ES2015
        // emitter validate inputs and preserve the native receiver.
        if(receiver is ArrayTypeSymbol array)
        {
            if(name=="length")return Number;
            if(name is "includes" or "indexOf" or "lastIndexOf")
                return new FunctionTypeSymbol([array.ElementType,Union([Number,Undefined])],
                    name=="includes"?Boolean:Number,1);
            if(name=="join")
                return new FunctionTypeSymbol([Union([String,Undefined])],String,0);
            if(name=="slice")
                return new FunctionTypeSymbol([Union([Number,Undefined]),Union([Number,Undefined])],
                    new ArrayTypeSymbol(array.ElementType),0);
            // Native ES2022 at() accepts signed numeric positions and may miss.
            // Emission keeps the ordinary JavaScript call; no polyfill is supplied.
            if(name=="at")
                return new FunctionTypeSymbol([Number],Union([array.ElementType,Undefined]));
            // ES2023 copying methods accept readonly views and return writable arrays.
            // Emission preserves native calls; no runtime polyfill is supplied.
            if(name=="toReversed")
                return new FunctionTypeSymbol([],new ArrayTypeSymbol(array.ElementType));
            if(name=="toSorted")
                return new FunctionTypeSymbol(
                    [Union([new FunctionTypeSymbol([array.ElementType,array.ElementType],Number),Undefined])],
                    new ArrayTypeSymbol(array.ElementType),0);
            // ES2023 copying methods accept readonly receivers but return
            // writable copies. The native call is emitted without a polyfill.
            if(name=="with")
                return new FunctionTypeSymbol([Number,array.ElementType],
                    new ArrayTypeSymbol(array.ElementType));
            if(name=="toSpliced")
                return new FunctionTypeSymbol(
                    [Number,Union([Number,Undefined]),array.ElementType],
                    new ArrayTypeSymbol(array.ElementType),1,array.ElementType);
            // Native concat returns a new writable array even for readonly views.
            // This finite signature accepts values and (readonly) arrays of T;
            // it does not claim Symbol.isConcatSpreadable or general overloads.
            if(name=="concat")
            {
                var item=Union([array.ElementType,new ArrayTypeSymbol(array.ElementType,Readonly:true)]);
                return new FunctionTypeSymbol([new ArrayTypeSymbol(item)],
                    new ArrayTypeSymbol(array.ElementType),0,item);
            }
            // Native ES2015 array predicate methods on mutable/readonly views.
            // This bounded contract intentionally requires one explicitly typed
            // value parameter; no contextual inference or predicate type guards.
            if(name is "some" or "every" or "find" or "findIndex" or "forEach")
            {
                var callback=new FunctionTypeSymbol([array.ElementType],
                    name=="forEach"?Void:Boolean);
                TypeSymbol result=name switch
                {
                    "some" or "every" => Boolean,
                    "find" => Union([array.ElementType,Undefined]),
                    "findIndex" => Number,
                    "forEach" => Void,
                    _ => throw new InvalidOperationException()
                };
                return new FunctionTypeSymbol([callback],result);
            }
            // A native map callback may return a type different from the element.
            // The checker derives that result from the checked callback signature;
            // this intrinsic marker cannot be supplied by user declarations.
            if(name=="map")
                return new FunctionTypeSymbol(
                    [new FunctionTypeSymbol([array.ElementType],Any)],
                    new ArrayTypeSymbol(Any),NativeArrayMapElementType:array.ElementType);
            // Checked native array transformations: filter always creates a
            // writable copy; same-element folds use an optional initial T.
            if(name=="filter")
                return new FunctionTypeSymbol(
                    [new FunctionTypeSymbol([array.ElementType],Boolean)],
                    new ArrayTypeSymbol(array.ElementType));
            if(name is "reduce" or "reduceRight")
                return new FunctionTypeSymbol(
                    [new FunctionTypeSymbol([array.ElementType,array.ElementType],array.ElementType),
                     array.ElementType],array.ElementType,1,NativeArrayReduceElementType:array.ElementType);
            if(array.Readonly)return null;
            return name switch
            {
                "push" or "unshift" => new FunctionTypeSymbol([new ArrayTypeSymbol(array.ElementType)],
                    Number,0,array.ElementType),
                "pop" or "shift" => new FunctionTypeSymbol([],Union([array.ElementType,Undefined])),
                // The native ES2015 emitter preserves the receiver identity;
                // these four methods mutate the original mutable array.
                "reverse" => new FunctionTypeSymbol([],new ArrayTypeSymbol(array.ElementType)),
                "sort" => new FunctionTypeSymbol(
                    [Union([new FunctionTypeSymbol([array.ElementType,array.ElementType],Number),Undefined])],
                    new ArrayTypeSymbol(array.ElementType),0),
                "fill" => new FunctionTypeSymbol(
                    [array.ElementType,Union([Number,Undefined]),Union([Number,Undefined])],
                    new ArrayTypeSymbol(array.ElementType),1),
                "copyWithin" => new FunctionTypeSymbol(
                    [Number,Number,Union([Number,Undefined])],
                    new ArrayTypeSymbol(array.ElementType),2),
                _ => null
            };
        }
        // These native string operations are non-mutating on primitive and
        // literal string receivers. The existing call checker validates all
        // arguments and ES2015 emission preserves JavaScript receiver semantics.
        if(IsStringFamily(receiver))
            return name switch
            {
                "length" => Number,
                "toUpperCase" or "toLowerCase" or "trim" =>
                    new FunctionTypeSymbol([],String),
                "slice" => new FunctionTypeSymbol(
                    [Union([Number,Undefined]),Union([Number,Undefined])],String,0),
                "includes" or "startsWith" or "endsWith" =>
                    new FunctionTypeSymbol([String,Union([Number,Undefined])],Boolean,1),
                // Split produces a fresh mutable array; all other methods
                // preserve native string semantics in the ES2015 emitter.
                "charAt" => new FunctionTypeSymbol([Number],String),
                "charCodeAt" => new FunctionTypeSymbol([Number],Number),
                "substring" => new FunctionTypeSymbol([Number,Union([Number,Undefined])],String,1),
                "indexOf" or "lastIndexOf" =>
                    new FunctionTypeSymbol([String,Union([Number,Undefined])],Number,1),
                "replace" => new FunctionTypeSymbol([String,String],String),
                "split" => new FunctionTypeSymbol([String,Union([Number,Undefined])],
                    new ArrayTypeSymbol(String),1),
                "repeat" => new FunctionTypeSymbol([Number],String),
                // Native string formatting / Unicode methods are checked on
                // known string receivers; no arbitrary standard library.
                "trimStart" or "trimEnd" => new FunctionTypeSymbol([],String),
                "padStart" or "padEnd" =>
                    new FunctionTypeSymbol([Number,Union([String,Undefined])],String,1),
                "normalize" =>
                    new FunctionTypeSymbol([Union([String,Undefined])],String,0),
                "codePointAt" =>
                    new FunctionTypeSymbol([Number],Union([Number,Undefined])),
                "localeCompare" => new FunctionTypeSymbol([String],Number),
                _ => null
            };
        if(receiver is ObjectTypeSymbol shape)
        {
            if(!shape.Members.TryGetValue(name,out var value))return null;
            return shape.OptionalMembers.Contains(name)?Union([value,Undefined]):value;
        }
        if(receiver is UnionTypeSymbol union)
        {
            var found=new List<TypeSymbol>(union.Alternatives.Count);
            foreach(var variant in union.Alternatives)
            {
                var member=TryGetProperty(variant,name);
                if(member==null)return null;
                found.Add(member);
            }
            return Union(found);
        }
        return null;
    }

    public static bool IsNumber(TypeSymbol? type) => type is PrimitiveTypeSymbol { Kind: PrimitiveKind.Number } or LiteralTypeSymbol { Kind: PrimitiveKind.Number };
    public static bool IsBoolean(TypeSymbol? type) => type is PrimitiveTypeSymbol { Kind: PrimitiveKind.Boolean } or LiteralTypeSymbol { Kind: PrimitiveKind.Boolean };
    public static bool IsString(TypeSymbol? type) => type is PrimitiveTypeSymbol { Kind: PrimitiveKind.String } or LiteralTypeSymbol { Kind: PrimitiveKind.String };

    // A union of string literals is still guaranteed to have string semantics.
    // Do not infer string behavior for mixed unions or unconstrained any.
    public static bool IsStringFamily(TypeSymbol? type) =>
        IsString(type) || type is UnionTypeSymbol union &&
        union.Alternatives.Count>0 && union.Alternatives.All(IsStringFamily);

    // Property keys and receivers must be checked, not coerced from arbitrary
    // primitive types. All alternatives in a union must satisfy the constraint.
    public static bool CanUseInLeft(TypeSymbol type) =>
        IsStringFamily(type) || IsNumber(type) || type.Equals(Any) ||
        type is UnionTypeSymbol union && union.Alternatives.All(CanUseInLeft);

    public static bool CanUseInRight(TypeSymbol type) =>
        type is ObjectTypeSymbol or ArrayTypeSymbol or FunctionTypeSymbol || type.Equals(Any) ||
        type is UnionTypeSymbol union && union.Alternatives.All(CanUseInRight);

    public static bool CanCompareRelational(TypeSymbol left,TypeSymbol right) =>
        (IsNumber(left)&&IsNumber(right)) ||
        (IsStringFamily(left)&&IsStringFamily(right));

    // Known strings concatenate with any supported value; an unconstrained
    // any + nonstring remains any, never guessed to be number or string.
    public static TypeSymbol? AdditionResult(TypeSymbol left,TypeSymbol right)
    {
        if(IsString(left)||IsString(right))return String;
        if(left.Equals(Any)||right.Equals(Any))return Any;
        if(IsNumber(left)&&IsNumber(right))return Number;
        return null;
    }

    // This is only the bounded, same-primitive/any equality subset.
    // Do not infer an overlap between unrelated or structural type forms.
    // Comparisons between primitive/literal unions are admitted only when a
    // primitive family overlaps; unrelated structural comparisons fail closed.
    public static bool CanCompareEquality(TypeSymbol left,TypeSymbol right)
    {
        var a=new HashSet<PrimitiveKind>();
        var b=new HashSet<PrimitiveKind>();
        static bool Collect(TypeSymbol type,HashSet<PrimitiveKind> kinds)
        {
            switch(type)
            {
                case PrimitiveTypeSymbol { Kind:PrimitiveKind.Any }: return true;
                case PrimitiveTypeSymbol { Kind:PrimitiveKind.Never }: return true;
                case PrimitiveTypeSymbol primitive: kinds.Add(primitive.Kind); return true;
                case LiteralTypeSymbol literal: kinds.Add(literal.Kind); return true;
                case UnionTypeSymbol union: return union.Alternatives.All(t=>Collect(t,kinds));
                default: return false;
            }
        }
        if(left.Equals(Any)||right.Equals(Any))return true;
        // A nullish sentinel may be compared with a finite nullish union
        // containing structural objects, arrays or functions. The primitive
        // collector intentionally rejects unrelated mixed unions otherwise.
        if(((left.Equals(Null)||left.Equals(Undefined)) && ContainsNullish(right)) ||
           ((right.Equals(Null)||right.Equals(Undefined)) && ContainsNullish(left)))return true;
        return Collect(left,a)&&Collect(right,b)&&a.Overlaps(b);
    }

    public static bool IsAssignable(TypeSymbol source,TypeSymbol target)
    {
        // Union sources are universally quantified; union targets require an
        // assignable alternative. Member/array relations use the same engine.
        // Memoize by reference identity and bound work on malformed broad graphs.
        int budget=16384;
        var active=new HashSet<(TypeSymbol Source,TypeSymbol Target)>(TypePairComparer.Instance);
        bool Check(TypeSymbol actual,TypeSymbol required)
        {
            if(--budget<0)return false; // fail closed on combinatorial input
            if(ReferenceEquals(actual,required) || actual.Equals(Never) ||
               actual.Equals(Any) || required.Equals(Any))return true;
            // Every value can enter unknown; unknown cannot flow into a
            // number/object/string until a real narrowing proves it safe.
            if(required.Equals(Unknown))return true;
            if(actual.Equals(Unknown))return false;
            // Unary void returns semantic undefined, assignable to void but
            // still distinct from a void function's return annotation.
            if(actual.Equals(Undefined) && required.Equals(Void))return true;
            var pair=(actual,required);
            if(!active.Add(pair))return false; // circular structural cases are outside this subset
            try
            {
                if(actual is UnionTypeSymbol sourceUnion)
                    return sourceUnion.Alternatives.All(variant=>Check(variant,required));
                if(required is UnionTypeSymbol targetUnion)
                    return targetUnion.Alternatives.Any(variant=>Check(actual,variant));
                if(required is LiteralTypeSymbol exact)
                    return actual is LiteralTypeSymbol value && value.Equals(exact);
                if(actual is LiteralTypeSymbol literal &&
                   required is PrimitiveTypeSymbol primitive)
                    return primitive.Kind==literal.Kind;
                if(required is ArrayTypeSymbol requiredArray)
                    return actual is ArrayTypeSymbol actualArray &&
                        (requiredArray.Readonly || !actualArray.Readonly) &&
                        Check(actualArray.ElementType,requiredArray.ElementType);
                if(required is FunctionTypeSymbol requiredFunction)
                {
                    if(actual is not FunctionTypeSymbol actualFunction)return false;
                    // This bounded native map marker is not a monomorphic
                    // (callback => any[]) function. A concrete annotation
                    // would erase its output-type dependency and could lie
                    // about the resulting element type. Reject unsupported
                    // explicit conversion instead of trusting the any[] stub.
                    if((actualFunction.NativeArrayMapElementType is not null &&
                        requiredFunction.NativeArrayMapElementType is null) ||
                       (actualFunction.NativeArrayMapElementType is null &&
                        requiredFunction.NativeArrayMapElementType is not null))return false;
                    // Reducing calls derive U from their explicitly typed callback.
                    // Do not erase that dependency into an ordinary callable.
                    if((actualFunction.NativeArrayReduceElementType is null) !=
                       (requiredFunction.NativeArrayReduceElementType is null))return false;
                    if(actualFunction.NativeArrayReduceElementType is { } foldActual &&
                       requiredFunction.NativeArrayReduceElementType is { } foldRequired &&
                       !Check(foldActual,foldRequired))return false;
                    // This bounded function relation admits only identical arity
                    // families. Do not guess at optional/rest compatibility.
                    // Parameter inputs are contravariant; return values covariant.
                    if(actualFunction.Parameters.Count!=requiredFunction.Parameters.Count ||
                       actualFunction.MinimumArguments!=requiredFunction.MinimumArguments ||
                       (actualFunction.RestElementType is null)!=(requiredFunction.RestElementType is null))
                        return false;
                    for(int i=0;i<requiredFunction.Parameters.Count;i++)
                        if(!Check(requiredFunction.Parameters[i],actualFunction.Parameters[i]))
                            return false;
                    if(requiredFunction.RestElementType is { } requiredRest &&
                       actualFunction.RestElementType is { } actualRest &&
                       !Check(requiredRest,actualRest))
                        return false;
                    return Check(actualFunction.ReturnType,requiredFunction.ReturnType);
                }
                if(required is ObjectTypeSymbol shape)
                {
                    if(shape.Members.Count==0)return true;
                    if(actual is not ObjectTypeSymbol data)return false;
                    return shape.Members.All(property=>
                    {
                        bool optional=shape.OptionalMembers.Contains(property.Key);
                        if(!data.Members.TryGetValue(property.Key,out var member))return optional;
                        if(!optional && data.OptionalMembers.Contains(property.Key))return false;
                        return Check(member,optional?Union([property.Value,Undefined]):property.Value);
                    });
                }
                return actual.Equals(required);
            }
            finally {active.Remove(pair);}
        }
        return Check(source,target);
    }
    private sealed class TypePairComparer : IEqualityComparer<(TypeSymbol Source,TypeSymbol Target)>
    {
        public static readonly TypePairComparer Instance=new();
        public bool Equals((TypeSymbol Source,TypeSymbol Target) x,(TypeSymbol Source,TypeSymbol Target) y)=>
            ReferenceEquals(x.Source,y.Source) && ReferenceEquals(x.Target,y.Target);
        public int GetHashCode((TypeSymbol Source,TypeSymbol Target) pair)=>HashCode.Combine(
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Source),
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Target));
    }
}
