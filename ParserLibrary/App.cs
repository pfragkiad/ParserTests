﻿using Microsoft.Extensions.Configuration;
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

    public static IHost GetParserApp<TParser>(string configFile = "appsettings.json") where TParser : Parser
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
                .AddParserLibrary<TParser>(context);
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

    public static IHost GetTransientParserApp<TParser>(string configFile = "appsettings.json") where TParser : TransientParser
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
               .AddTransientParserLibrary<TParser>(context);
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


    public static IParser GetCustomParser<TParser>(string configFile = "appsettings.json") where TParser : Parser =>
        GetParserApp<TParser>(configFile).Services.GetRequiredParser();

    public static ITransientParser GetCustomTransientParser<TParser>(string configFile = "appsettings.json") where TParser : TransientParser =>
        GetTransientParserApp<TParser>(configFile).Services.GetRequiredTransientParser();

    public static IParser? GetDefaultParser(string configFile = "appsettings.json") =>
        GetParserApp<DefaultParser>(configFile).Services.GetParser();

    public static IParser GetRequiredDefaultParser(string configFile = "appsettings.json") =>
        GetParserApp<DefaultParser>(configFile).Services.GetRequiredParser();

    public static double Evaluate(string s, Dictionary<string, object>? variables = null)
    {
        return (double)GetParserApp<DefaultParser>().Services.GetRequiredParser().Evaluate(s, variables);
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
