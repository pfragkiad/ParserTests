using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParserLibrary.Tokenizers;

namespace ParserLibrary.ExpressionTree;

// TokenTree <-> JSON converter using only pure-binary Node<Token> shape (Left/Right).
// 'Other' children are ignored entirely.
public sealed class TokenTreeBinaryJsonConverter : JsonConverter<TokenTree>
{
    public override TokenTree? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for TokenTree.");

        Node<Token>? root = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName in TokenTree.");

            string prop = reader.GetString()!;
            reader.Read();

            switch (prop)
            {
                case "root":
                    root = JsonSerializer.Deserialize<Node<Token>>(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (root is null)
            return TokenTree.Empty;

        // Rebuild Token -> Node dictionary via DFS (Left/Right only)
        var dict = new Dictionary<Token, Node<Token>>();
        void Walk(Node<Token>? n)
        {
            if (n is null) return;
            if (n.Value is Token t) dict[t] = n;
            Walk(n.Left as Node<Token>);
            Walk(n.Right as Node<Token>);
        }
        Walk(root);

        return new TokenTree
        {
            Root = root,
            NodeDictionary = dict
        };
    }

    public override void Write(Utf8JsonWriter writer, TokenTree value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("root");
        if (value.Root is Node<Token> root)
        {
            // This will use NodeTokenBinaryJsonConverter registered in options
            JsonSerializer.Serialize(writer, root, options);
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WriteEndObject();
    }
}