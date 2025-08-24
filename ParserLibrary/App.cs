using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers.Interfaces;
using System.Numerics;

namespace ParserLibrary;

public static class App
{
    #region TokenizerOptions

    private static void CopyFrom(this TokenizerOptions target, TokenizerOptions source)
    {
        target.Version = source.Version;
        target.CaseSensitive = source.CaseSensitive;
        target.TokenPatterns = source.TokenPatterns; // shallow copy; deep clone if you mutate later
    }

    private static IHostBuilder GetHostBuilder(string? settingsFile = null)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                if (!string.IsNullOrWhiteSpace(settingsFile))
                    config.AddJsonFile(settingsFile);  // Configure app configuration if needed
            });
    }

    public static IServiceCollection AddTokenizerOptions(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection) =>
        services.Configure<TokenizerOptions>(context.Configuration.GetSection(tokenizerSection));

    // Provide IOptions<TokenizerOptions> for default (unnamed) consumers
    public static IServiceCollection AddTokenizerOptions(
        this IServiceCollection services,
        TokenizerOptions options) =>
        services.AddSingleton(Options.Create(options));

    // Added keyed overload (bind from IConfiguration section)
    public static IServiceCollection AddTokenizerOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        string key,
        string tokenizerSectionPath = TokenizerOptions.TokenizerSection)
    {
        services.AddOptions<TokenizerOptions>(key)
            .Bind(configuration.GetSection(tokenizerSectionPath))
            .PostConfigure(o =>
            {
                o.TokenPatterns ??= TokenizerOptions.Default.TokenPatterns;
            });
        return services;
    }

    // Added keyed overload (use an in-memory / code-created options instance)
    public static IServiceCollection AddTokenizerOptions(
        this IServiceCollection services,
        string key,
        TokenizerOptions options)
    {
        services.AddOptions<TokenizerOptions>(key)
            .Configure(o =>
            {
                o.CopyFrom(options);
            })
            .PostConfigure(o =>
            {
                o.TokenPatterns ??= TokenizerOptions.Default.TokenPatterns;
            });
        return services;
    }

    public static TokenizerOptions GetTokenizerOptions(this IServiceProvider provider, string? key = null)
    {
        if (key is null)
            return provider.GetRequiredService<IOptions<TokenizerOptions>>().Value;

        return provider
        .GetRequiredService<IOptionsMonitor<TokenizerOptions>>()
        .Get(key);
    }

    #endregion

    #region Tokenizer

    public static IServiceCollection AddTokenizer(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection)
    {
        return services
            .AddTokenizerOptions(context, tokenizerSection)
            .AddSingleton<ITokenizer, Tokenizer>();
    }

    public static IServiceCollection AddTokenizer(
    this IServiceCollection services,
    TokenizerOptions options)
    {
        return services
            .AddTokenizerOptions(options)
            .AddSingleton<ITokenizer, Tokenizer>();
    }

    public static ITokenizer GetTokenizer(this IServiceProvider services) => services.GetRequiredService<ITokenizer>();

    /// <summary>
    /// Registers a TokenizerOptions (named) and a keyed Tokenizer bound to a configuration section.
    /// key: logical name to later resolve the tokenizer.
    /// sectionPath: configuration section path (e.g. "Tokenizers:Math" or "MathTokenizer").
    /// </summary>
    public static IServiceCollection AddTokenizer(
        this IServiceCollection services,
        IConfiguration configuration,
        string key,
        string tokenizerSectionPath = TokenizerOptions.TokenizerSection)
    {
        return services
            .AddTokenizerOptions(configuration, key, tokenizerSectionPath)
            .AddKeyedSingleton<ITokenizer, Tokenizer>(key, (provider, key) =>
            {
                var logger = provider.GetRequiredService<ILogger<Tokenizer>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key as string);
                return new Tokenizer(logger, Options.Create(opts));
            });
    }

    public static IServiceCollection AddTokenizer(
        this IServiceCollection services,
        string key,
        TokenizerOptions options)
    {
        return services
            .AddTokenizerOptions(key, options)
            .AddKeyedSingleton<ITokenizer, Tokenizer>(key, (provider, key) =>
            {
                var logger = provider.GetRequiredService<ILogger<Tokenizer>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key as string);
                return new Tokenizer(logger, Options.Create(opts));
            });
    }

    /// <summary>
    /// Bulk register all subsections under a parent section.
    /// Example config:
    /// "Tokenizers": {
    ///   "Complex": { ... },
    ///   "Vector": { ... }
    /// }
    /// Call: services.AddTokenizers(configuration, "Tokenizers");
    /// Keys will be "Complex", "Vector".
    /// </summary>
    public static IServiceCollection AddTokenizers(
        this IServiceCollection services,
        IConfiguration configuration,
        string parentSectionPath)
    {
        var parent = configuration.GetSection(parentSectionPath);
        foreach (var child in parent.GetChildren())
        {
            services.AddTokenizer(configuration, child.Key, $"{parentSectionPath}:{child.Key}");
        }
        return services;
    }

    /// <summary>
    /// Resolve a keyed tokenizer.
    /// </summary>
    public static ITokenizer GetTokenizer(this IServiceProvider services, string key) =>
        services.GetRequiredKeyedService<ITokenizer>(key);


    public static IHost GetTokenizerApp(string? settingsFile = null, string tokenizerSectionPath = TokenizerOptions.TokenizerSection)
    {
        return GetHostBuilder(settingsFile)
            .ConfigureServices((context, services) =>
            {
                services.AddTokenizer(context, tokenizerSectionPath);
            })
            .Build();
    }
    public static IHost GetTokenizerApp(TokenizerOptions options)
    {
        return GetHostBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddTokenizer(options);
            })
            .Build();
    }

    #endregion


    #region Parser

    public static IServiceCollection AddParser<TParser>(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParser : ParserBase
    {
        return services
            .AddTokenizerOptions(context, tokenizerSectionPath)
            .AddSingleton<IParser, TParser>();
    }

    public static IServiceCollection AddParser<TParser>(
        this IServiceCollection services,
        TokenizerOptions options) where TParser : ParserBase
    {
        return services
            .AddTokenizerOptions(options)
            .AddSingleton<IParser, TParser>();
    }

    public static IParser GetParser(this IServiceProvider services) => services.GetRequiredService<IParser>();

    public static IServiceCollection AddParser<TParser>(
       this IServiceCollection services,
       IConfiguration configuration,
       string key,
       string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParser : ParserBase
    {
        return services
            .AddTokenizerOptions(configuration, key, tokenizerSectionPath)
            .AddKeyedSingleton<IParser, TParser>(key, (provider, key) =>
            {
                var logger = provider.GetRequiredService<ILogger<ParserBase>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key as string);
                return ActivatorUtilities.CreateInstance<TParser>(provider, Options.Create(opts));
            });
    }

    public static IServiceCollection AddParser<TParser>(
        this IServiceCollection services,
        string key,
        TokenizerOptions options) where TParser : ParserBase
    {
        return services
            .AddTokenizerOptions(key, options)
            .AddKeyedSingleton<IParser, TParser>(key, (provider, key) =>
            {
                var logger = provider.GetRequiredService<ILogger<ParserBase>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key as string);
                return ActivatorUtilities.CreateInstance<TParser>(provider, Options.Create(opts));
            });
    }

    /// <summary>
    /// Resolve a keyed parser.
    /// </summary>
    public static IParser GetParser(this IServiceProvider services, string key) =>
        services.GetRequiredKeyedService<IParser>(key);

    public static IHost GetParserApp<TParser>(string? settingsFile = null, string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParser : ParserBase
    {
        return GetHostBuilder(settingsFile)
            .ConfigureServices((context, services) =>
            {
                services.AddParser<TParser>(context, tokenizerSectionPath);
            })
            .Build();
    }
    public static IHost GetParserApp<TParser>(TokenizerOptions options) where TParser : ParserBase
    {
        return GetHostBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddParser<TParser>(options);
            })
            .Build();
    }
    #endregion

    #region StatefulParser

    public static IServiceCollection AddStatefulParser<TStatefulParser>(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection) where TStatefulParser : StatefulParserBase
    {
        return services
            .AddTokenizerOptions(context, tokenizerSection)
            .AddTransient<IStatefulParser, TStatefulParser>();
    }

    public static IServiceCollection AddStatefulParser<TStatefulParser>(
        this IServiceCollection services,
        TokenizerOptions options) where TStatefulParser : StatefulParserBase
    {
        return services
            .AddTokenizerOptions(options)
            .AddTransient<IStatefulParser, TStatefulParser>();
    }

    public static IStatefulParser GetStatefulParser(this IServiceProvider services) => services.GetRequiredService<IStatefulParser>();

    public static IServiceCollection AddStatefulParser<TStatefulParser>(
        this IServiceCollection services,
        IConfiguration configuration,
        string key,
        string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TStatefulParser : StatefulParserBase
    {
        return services
            .AddTokenizerOptions(configuration, key, tokenizerSectionPath)
            .AddKeyedTransient<IStatefulParser, TStatefulParser>(key, (provider, key) =>
            {
                var logger = provider.GetRequiredService<ILogger<ParserBase>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key as string);
                return ActivatorUtilities.CreateInstance<TStatefulParser>(provider, Options.Create(opts));
            });
    }

    public static IServiceCollection AddStatefulParser<TStatefulParser>(
        this IServiceCollection services,
        string key,
        TokenizerOptions options) where TStatefulParser : StatefulParserBase
    {
        return services
            .AddTokenizerOptions(key, options)
            .AddKeyedTransient<IStatefulParser, TStatefulParser>(key, (provider, key) =>
            {
                var logger = provider.GetRequiredService<ILogger<ParserBase>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key as string);
                return ActivatorUtilities.CreateInstance<TStatefulParser>(provider, Options.Create(opts));
            });
    }

    /// <summary>
    /// Resolve a keyed stateful parser.
    /// </summary>
    public static IStatefulParser GetStatefulParser(this IServiceProvider services, string key) =>
        services.GetRequiredKeyedService<IStatefulParser>(key);

    public static IHost GetStatefulParserApp<TStatefulParser>(string? settingsFile = null, string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TStatefulParser : StatefulParserBase
    {
        return GetHostBuilder(settingsFile)
            .ConfigureServices((context, services) =>
            {
                services.AddStatefulParser<TStatefulParser>(context, tokenizerSectionPath);
            })
            .Build();
    }
    public static IHost GetStatefulParserApp<TStatefulParser>(TokenizerOptions options) where TStatefulParser : StatefulParserBase
    {
        return GetHostBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddStatefulParser<TStatefulParser>(options);
            })
            .Build();
    }

    #endregion


    #region Common parsers

    public static IServiceCollection AddCommonParsers(this IServiceCollection services)
    {
        return services
            .AddParser<DefaultParser>("Default", TokenizerOptions.Default)
            .AddParser<Vector3Parser>("Vector3", TokenizerOptions.Default)
            .AddParser<ComplexParser>("Complex", TokenizerOptions.Default);
    }



    private static IHost? _commonParsersHost;

    private static void CreateCommonsHostIfNeeded()
    {
        _commonParsersHost ??= Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services
                .AddTokenizer(TokenizerOptions.Default) // default tokenizer for default parsers
                .AddCommonParsers();
            })
            .Build();
    }

    public static IHost GetCommonsApp()
    {
        CreateCommonsHostIfNeeded();
        return _commonParsersHost!;
    }

    public static IParser GetDefaultParser()
    {
        CreateCommonsHostIfNeeded();
        return _commonParsersHost!.Services.GetParser("Default");
    }

    public static IParser GetVector3Parser()
    {
        CreateCommonsHostIfNeeded();
        return _commonParsersHost!.Services.GetParser("Vector3");
    }

    public static IParser GetComplexParser()
    {
        CreateCommonsHostIfNeeded();
        return _commonParsersHost!.Services.GetParser("Complex");
    }

    public static double? Evaluate(string expression, Dictionary<string, object?>? variables = null)
    {
        return (double?)GetDefaultParser().Evaluate(expression, variables);
    }

    public static Complex? EvaluateComplex(string expression, Dictionary<string, object?>? variables = null)
    {
        return (Complex?)GetComplexParser().Evaluate(expression, variables);
    }

    public static Vector3? EvaluateVector3(string expression, Dictionary<string, object?>? variables = null)
    {
        return (Vector3?)GetVector3Parser().Evaluate(expression, variables);
    }

    public static ITokenizer GetCommonTokenizer()
    {
        CreateCommonsHostIfNeeded();
        return _commonParsersHost!.Services.GetTokenizer();
    }


    #endregion


}
