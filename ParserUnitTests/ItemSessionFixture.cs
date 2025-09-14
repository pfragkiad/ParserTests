using Microsoft.Extensions.Hosting;
using ParserLibrary;
using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;
using ParserTests.Common.Parsers;
using System;

namespace ParserUnitTests;

public sealed class ItemSessionFixture : IDisposable
{
    public IHost Host { get; }

    public ItemSessionFixture()
    {
        Host = ParserApp.GetParserSessionApp<ItemParserSession>(TokenizerOptions.Default);
    }

    public ParserSessionBase CreateSession() => (ParserSessionBase)Host.GetParserSession();

    public void Dispose() => Host.Dispose();
}