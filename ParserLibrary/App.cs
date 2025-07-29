using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;
using Serilog;

namespace ParserLibrary;

public static class App
{
    //TODO: AddTokenizer, AddParser, ConfigureTokenizerOptions capability show
    //TODO: Serilog control via appsettings.json

    public static IHost GetParserApp<TParser>(
        string configFile = "appsettings.json",
        string tokenizerSection = TokenizerOptions.TokenizerSection) where TParser : Parser
    {
        IHost app = Host.CreateDefaultBuilder()
           .ConfigureAppConfiguration(builder =>
           {
               if (configFile != "appsettings.json")
                   builder.AddJsonFile(configFile, true, false);
           })
           .ConfigureServices((context, services) =>
            {
                services
                .AddParserLibrary<TParser>(context,tokenizerSection);
            })
           .UseSerilog((context, configuration) =>
           {
               configuration.ReadFrom.Configuration(context.Configuration);
               //configuration.MinimumLevel.Debug();
               //configuration.WriteTo.Console();
           })
           .Build();
        return app;
    }

    public static IHost GetStatefulParserApp<TParser>(
        string configFile = "appsettings.json",
        string tokenizerSection = TokenizerOptions.TokenizerSection) where TParser : StatefulParser
    {
        IHost app = Host.CreateDefaultBuilder()
           .ConfigureAppConfiguration(builder =>
           {
               if (configFile != "appsettings.json")
                   builder.AddJsonFile(configFile, true, false);
           })
           .ConfigureServices((context, services) =>
           {
               services
               .AddStatefulParserLibrary<TParser>(context, tokenizerSection);
           })
           .UseSerilog((context, configuration) =>
           {
               configuration.ReadFrom.Configuration(context.Configuration);
               //configuration.MinimumLevel.Debug();
               //configuration.WriteTo.Console();
           })
           .Build();
        return app;
    }


    #region Utility functions


    public static IParser GetCustomParser<TParser>(
        string configFile = "appsettings.json",
        string tokenizerSection = TokenizerOptions.TokenizerSection) where TParser : Parser =>
        GetParserApp<TParser>(configFile,tokenizerSection).Services.GetRequiredParser();

    public static IStatefulParser GetCustomStatefulParser<TParser>(
        string configFile = "appsettings.json",
        string tokenizerSection = TokenizerOptions.TokenizerSection) where TParser : StatefulParser =>
        GetStatefulParserApp<TParser>(configFile, tokenizerSection).Services.GetRequiredStatefulParser();

    /// <summary>
    /// Creates a StatefulParser with the specified expression using the factory pattern
    /// </summary>
    /// <typeparam name="TParser">The type of StatefulParser to create</typeparam>
    /// <param name="expression">The expression to parse</param>
    /// <param name="configFile">Configuration file path</param>
    /// <param name="tokenizerSection">Tokenizer configuration section</param>
    /// <returns>A StatefulParser instance configured with the expression</returns>
    public static TParser CreateStatefulParser<TParser>(
        string expression,
        string configFile = "appsettings.json",
        string tokenizerSection = TokenizerOptions.TokenizerSection) where TParser : StatefulParser =>
        GetStatefulParserApp<TParser>(configFile, tokenizerSection).Services.GetRequiredStatefulParserFactory().Create<TParser>(expression);

    public static IParser? GetDefaultParser(
        string configFile = "appsettings.json",
        string tokenizerSection = TokenizerOptions.TokenizerSection) =>
        GetParserApp<DefaultParser>(configFile, tokenizerSection).Services.GetParser();

    public static IParser GetRequiredDefaultParser(string configFile = "appsettings.json", string tokenizerSection = TokenizerOptions.TokenizerSection) =>
        GetParserApp<DefaultParser>(configFile).Services.GetRequiredParser();

    public static double Evaluate(string s, Dictionary<string, object?>? variables = null)
    {
        return (double?)GetParserApp<DefaultParser>().Services.GetRequiredParser().Evaluate(s, variables) ?? 0.0;
    }

    #endregion


 
    private static void Tests()
    {
        //#1a
        var builder = new ConfigurationBuilder();
        builder.AddJsonFile("appsettings.json");
        IConfiguration config = builder.Build();

        //#1b
        //IHost app = Host.CreateDefaultBuilder().Build();
        //IConfiguration config = app.Services.GetService<IConfiguration>();

        var tokenizer = new TokenizerOptions();
        //#i
        //config.GetSection(TokenizerOptions.TokenizerSection).Bind(tokenizer);
        //#ii
        config.Bind(TokenizerOptions.TokenizerSection, tokenizer);
        //var s  = config["test"]; //works


        //#2
        //IHost app = Host.CreateDefaultBuilder()
        //   .ConfigureServices((context, services) =>
        //    services.Configure<TokenizerOptions>(context.Configuration.GetSection(TokenizerOptions.TokenizerSection))).Build();
        //var tokenizer = app.Services.GetService<IOptions<TokenizerOptions>>().Value; //IOptionsMonitor, IOptionsSnapshot

    }
}
