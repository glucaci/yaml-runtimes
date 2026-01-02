using System.Text;
using Yamlify;

Console.InputEncoding = Console.OutputEncoding = Encoding.UTF8;

ReadOnlySpan<byte> yaml;
if (args.Length == 0)
{
    using var ms = new MemoryStream();
    Console.OpenStandardInput().CopyTo(ms);
    yaml = ms.ToArray();
}
else
{
    yaml = File.ReadAllBytes(args[0]);
}

var reader = new Utf8YamlReader(yaml);

// Track pending anchor and tag for the next node
string? pendingAnchor = null;
string? pendingTag = null;

// Track document state to avoid duplicate -DOC emissions
bool inDocument = false;

while (reader.Read())
{
    string? line = reader.TokenType switch
    {
        YamlTokenType.StreamStart => "+STR",
        YamlTokenType.StreamEnd => "-STR",
        YamlTokenType.DocumentStart => HandleDocStart(ref inDocument),
        YamlTokenType.DocumentEnd => HandleDocEnd(ref inDocument),
        YamlTokenType.MappingStart => FormatCollectionStart("+MAP", reader.CollectionStyle, ref pendingAnchor, ref pendingTag),
        YamlTokenType.MappingEnd => "-MAP",
        YamlTokenType.SequenceStart => FormatCollectionStart("+SEQ", reader.CollectionStyle, ref pendingAnchor, ref pendingTag),
        YamlTokenType.SequenceEnd => "-SEQ",
        YamlTokenType.Scalar => FormatScalar(reader.GetString(), reader.ScalarStyle, ref pendingAnchor, ref pendingTag),
        YamlTokenType.Alias => $"=ALI *{reader.GetString()}",
        YamlTokenType.Anchor => CaptureAnchor(reader.GetString(), ref pendingAnchor),
        YamlTokenType.Tag => CaptureTag(reader.GetString(), ref pendingTag),
        _ => null
    };

    if (line is not null)
    {
        Console.WriteLine(line);
    }
}

/// <summary>
/// Handles document start, ensuring we emit +DOC and track state.
/// </summary>
static string HandleDocStart(ref bool inDocument)
{
    inDocument = true;
    return "+DOC";
}

/// <summary>
/// Handles document end, ensuring we only emit -DOC once per document.
/// </summary>
static string? HandleDocEnd(ref bool inDocument)
{
    if (!inDocument)
    {
        return null; // Already ended, skip duplicate
    }

    inDocument = false;
    return "-DOC";
}

/// <summary>
/// Formats a collection start event with optional flow indicator, anchor, and tag.
/// </summary>
static string FormatCollectionStart(string prefix, CollectionStyle style, ref string? anchor, ref string? tag)
{
    var sb = new StringBuilder(prefix);

    // Add flow indicator for flow collections
    if (style == CollectionStyle.Flow)
    {
        sb.Append(prefix == "+MAP" ? " {}" : " []");
    }

    // Add anchor if present
    if (anchor is not null)
    {
        sb.Append(" &");
        sb.Append(anchor);
        anchor = null;
    }

    // Add tag if present
    if (tag is not null)
    {
        sb.Append(" <");
        sb.Append(tag);
        sb.Append('>');
        tag = null;
    }

    return sb.ToString();
}

/// <summary>
/// Formats a scalar event with style indicator, anchor, tag, and escaped value.
/// </summary>
static string FormatScalar(string? value, ScalarStyle style, ref string? anchor, ref string? tag)
{
    var sb = new StringBuilder("=VAL");

    // Add anchor if present
    if (anchor is not null)
    {
        sb.Append(" &");
        sb.Append(anchor);
        anchor = null;
    }

    // Add tag if present
    if (tag is not null)
    {
        sb.Append(" <");
        sb.Append(tag);
        sb.Append('>');
        tag = null;
    }

    // Add style indicator and value
    char styleIndicator = style switch
    {
        ScalarStyle.Plain => ':',
        ScalarStyle.SingleQuoted => '\'',
        ScalarStyle.DoubleQuoted => '"',
        ScalarStyle.Literal => '|',
        ScalarStyle.Folded => '>',
        _ => ':'
    };

    sb.Append(' ');
    sb.Append(styleIndicator);
    sb.Append(Escape(value));

    return sb.ToString();
}

/// <summary>
/// Captures an anchor token for attachment to the next node.
/// </summary>
static string? CaptureAnchor(string? name, ref string? anchor)
{
    anchor = name;
    return null; // Don't emit anything yet
}

/// <summary>
/// Captures a tag token for attachment to the next node.
/// </summary>
static string? CaptureTag(string? name, ref string? tag)
{
    tag = name;
    return null; // Don't emit anything yet
}

/// <summary>
/// Escapes special characters in scalar values for libyaml event format.
/// </summary>
static string Escape(string? s)
{
    if (s is null)
    {
        return string.Empty;
    }

    var sb = new StringBuilder(s.Length);
    foreach (char c in s)
    {
        sb.Append(c switch
        {
            '\\' => "\\\\",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            '\b' => "\\b",
            '\0' => "\\0",
            _ => c.ToString()
        });
    }

    return sb.ToString();
}
