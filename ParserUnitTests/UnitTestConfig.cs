using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;

namespace ParserUnitTests;

public class UnitTestConfig
{

    [Fact]
    public void TestConfigFile()
    {
        var app = App.GetParserApp<Parser>("appsettings2.json");
        var options = app.Services.GetRequiredService<IOptions<TokenizerOptions>>().Value;

        Assert.Equal("2.0", options.Version);
    }


}
