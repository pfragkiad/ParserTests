using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParserLibrary.Tokenizers;

namespace ParserLibrary.ExpressionTree;

// Serializes only Value (Token), Left, Right. Ignores Other completely.
public sealed class NodeTokenBinaryJsonConverter : JsonConverter<Node<Token>>
{
    public override Node<Token>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Node<Token>.");

        Token? token = null;
        Node<Token>? left = null;
        Node<Token>? right = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName in Node<Token>.");

            string name = reader.GetString()!;
            reader.Read();

            switch (name)
            {
                case "value":
                case "token":
                case "v":
                    token = JsonSerializer.Deserialize<Token>(ref reader, options);
                    break;

                case "left":
                case "l":
                    left = JsonSerializer.Deserialize<Node<Token>>(ref reader, options);
                    break;

                case "right":
                case "r":
                    right = JsonSerializer.Deserialize<Node<Token>>(ref reader, options);
                    break;

                default:
                    // Ignore any unknown fields (including any 'other' if present)
                    reader.Skip();
                    break;
            }
        }

        var node = new Node<Token>(token);
        node.Left = left;
        node.Right = right;
        return node;
    }

    public override void Write(Utf8JsonWriter writer, Node<Token> value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        writer.WritePropertyName("value");
        JsonSerializer.Serialize(writer, value.Value, options);

        writer.WritePropertyName("left");
        if (value.Left is Node<Token> l)
            JsonSerializer.Serialize(writer, l, options);
        else
            writer.WriteNullValue();

        writer.WritePropertyName("right");
        if (value.Right is Node<Token> r)
            JsonSerializer.Serialize(writer, r, options);
        else
            writer.WriteNullValue();

        // Intentionally do NOT write 'Other'

        writer.WriteEndObject();
    }
}