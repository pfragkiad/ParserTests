﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserLibrary;

public static class App
{
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
                    .ConfigureTokenizerOptions(context)
                    .AddTokenizer()
                    .AddParser<TParser>()
            ;
            })
           .UseSerilog((context, configuration) =>
           {
               configuration.MinimumLevel.Debug();
               configuration.WriteTo.Console();
           })
           .Build();
        return app;
    }

    #region Tokenizer
    public static IServiceCollection ConfigureTokenizerOptions(this IServiceCollection services, HostBuilderContext context) =>
        services.Configure<TokenizerOptions>(context.Configuration.GetSection(TokenizerOptions.TokenizerSection));


    public static IServiceCollection AddTokenizer(this IServiceCollection services) => services.AddSingleton<ITokenizer, Tokenizer>();

    public static ITokenizer? GetTokenizer(this IServiceProvider services) => services.GetService<ITokenizer>();

    #endregion

    #region Parser
    public static IServiceCollection AddParser<TParser>(this IServiceCollection services) where TParser : Parser
             => services.AddSingleton<IParser, TParser>();
    

    public static IParser? GetParser(this IServiceProvider services) => services.GetService<IParser>();

    #endregion

    private static void Tests()
    {
        //#1a
        IConfigurationBuilder builder = new ConfigurationBuilder();
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
