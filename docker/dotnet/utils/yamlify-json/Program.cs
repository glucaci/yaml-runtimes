using System.Text;
using System.Text.Json;
using Yamlify;
using Yamlify.Nodes;

Console.InputEncoding = Console.OutputEncoding = Encoding.UTF8;

string yaml;
if (args.Length == 0)
{
    yaml = Console.In.ReadToEnd();
}
else
{
    yaml = File.ReadAllText(args[0]);
}

var stream = YamlStream.Load(yaml);

foreach (var doc in stream)
{
    var json = ToJson(doc.RootNode);
    Console.WriteLine(json);
}

/// <summary>
/// Converts a YAML node tree to a JSON string.
/// </summary>
static string ToJson(YamlNode? node)
{
    using var ms = new MemoryStream();
    using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false });
    WriteNode(writer, node);
    writer.Flush();
    return Encoding.UTF8.GetString(ms.ToArray());
}

/// <summary>
/// Recursively writes a YAML node to a JSON writer, applying YAML 1.2 Core Schema type inference.
/// </summary>
static void WriteNode(Utf8JsonWriter writer, YamlNode? node)
{
    switch (node)
    {
        case null:
            writer.WriteNullValue();
            break;

        case YamlScalarNode scalar:
            WriteScalar(writer, scalar);
            break;

        case YamlSequenceNode seq:
            writer.WriteStartArray();
            foreach (var item in seq)
            {
                WriteNode(writer, item);
            }
            writer.WriteEndArray();
            break;

        case YamlMappingNode map:
            writer.WriteStartObject();
            foreach (var kvp in map)
            {
                var key = kvp.Key switch
                {
                    YamlScalarNode keyScalar => keyScalar.Value ?? string.Empty,
                    _ => kvp.Key?.ToString() ?? string.Empty
                };
                writer.WritePropertyName(key);
                WriteNode(writer, kvp.Value);
            }
            writer.WriteEndObject();
            break;

        default:
            writer.WriteNullValue();
            break;
    }
}

/// <summary>
/// Writes a scalar value with YAML 1.2 Core Schema type inference.
/// </summary>
static void WriteScalar(Utf8JsonWriter writer, YamlScalarNode scalar)
{
    var value = scalar.Value;

    // Check for null values (empty unquoted or explicit null)
    if (scalar.IsNull)
    {
        writer.WriteNullValue();
        return;
    }

    // Quoted strings are always strings (no type inference)
    if (scalar.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted)
    {
        writer.WriteStringValue(value ?? string.Empty);
        return;
    }

    // Plain scalars get type inference per YAML 1.2 Core Schema
    if (value is null)
    {
        writer.WriteNullValue();
        return;
    }

    // Boolean detection (YAML 1.2 Core Schema: true/false only)
    if (value.Equals("true", StringComparison.Ordinal) || value.Equals("True", StringComparison.Ordinal) || value.Equals("TRUE", StringComparison.Ordinal))
    {
        writer.WriteBooleanValue(true);
        return;
    }

    if (value.Equals("false", StringComparison.Ordinal) || value.Equals("False", StringComparison.Ordinal) || value.Equals("FALSE", StringComparison.Ordinal))
    {
        writer.WriteBooleanValue(false);
        return;
    }

    // Null detection
    if (value.Equals("null", StringComparison.Ordinal) || value.Equals("Null", StringComparison.Ordinal) || value.Equals("NULL", StringComparison.Ordinal) || value == "~")
    {
        writer.WriteNullValue();
        return;
    }

    // Integer detection (decimal, octal 0o, hex 0x)
    if (TryParseInteger(value, out var intValue))
    {
        writer.WriteNumberValue(intValue);
        return;
    }

    // Float detection (including .inf, -.inf, .nan)
    if (TryParseFloat(value, out var floatValue))
    {
        // JSON doesn't support Infinity/NaN, write as null
        if (double.IsInfinity(floatValue) || double.IsNaN(floatValue))
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(floatValue);
        }
        return;
    }

    // Default: string
    writer.WriteStringValue(value);
}

/// <summary>
/// Attempts to parse a YAML integer value (decimal, octal 0o, hex 0x).
/// </summary>
static bool TryParseInteger(string value, out long result)
{
    result = 0;

    if (string.IsNullOrEmpty(value))
    {
        return false;
    }

    // Handle sign
    bool negative = false;
    var span = value.AsSpan();
    if (span[0] == '-')
    {
        negative = true;
        span = span[1..];
    }
    else if (span[0] == '+')
    {
        span = span[1..];
    }

    if (span.IsEmpty)
    {
        return false;
    }

    // Octal: 0o prefix
    if (span.Length > 2 && span[0] == '0' && (span[1] == 'o' || span[1] == 'O'))
    {
        if (TryParseOctal(span[2..], out result))
        {
            if (negative)
            {
                result = -result;
            }

            return true;
        }
        return false;
    }

    // Hex: 0x prefix
    if (span.Length > 2 && span[0] == '0' && (span[1] == 'x' || span[1] == 'X'))
    {
        if (long.TryParse(span[2..], System.Globalization.NumberStyles.HexNumber, null, out result))
        {
            if (negative)
            {
                result = -result;
            }

            return true;
        }
        return false;
    }

    // Decimal
    if (long.TryParse(span, out result))
    {
        if (negative)
        {
            result = -result;
        }

        return true;
    }

    return false;
}

/// <summary>
/// Attempts to parse an octal number.
/// </summary>
static bool TryParseOctal(ReadOnlySpan<char> span, out long result)
{
    result = 0;
    foreach (char c in span)
    {
        if (c < '0' || c > '7')
        {
            return false;
        }

        result = (result * 8) + (c - '0');
    }
    return span.Length > 0;
}

/// <summary>
/// Attempts to parse a YAML float value (including .inf, -.inf, .nan).
/// </summary>
static bool TryParseFloat(string value, out double result)
{
    result = 0;

    // Special float values
    if (value.Equals(".inf", StringComparison.OrdinalIgnoreCase) || value.Equals("+.inf", StringComparison.OrdinalIgnoreCase))
    {
        result = double.PositiveInfinity;
        return true;
    }

    if (value.Equals("-.inf", StringComparison.OrdinalIgnoreCase))
    {
        result = double.NegativeInfinity;
        return true;
    }

    if (value.Equals(".nan", StringComparison.OrdinalIgnoreCase))
    {
        result = double.NaN;
        return true;
    }

    // Standard float (must contain . or e/E to be considered float)
    if ((value.Contains('.') || value.Contains('e') || value.Contains('E')) &&
        double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result))
    {
        return true;
    }

    return false;
}
