using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Parsers.Interfaces;
using System.Runtime.CompilerServices;

namespace ParserLibrary;

public static class DependencyInjection
{
    #region TokenizerOptions

    private static void CopyFrom(this TokenizerOptions target, TokenizerOptions source)
    {
        target.Version = source.Version;
        target.CaseSensitive = source.CaseSensitive;
        target.TokenPatterns = source.TokenPatterns; // shallow copy; deep clone if you mutate later
    }

    public static IServiceCollection ConfigureTokenizerOptions(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection) =>
        services.Configure<TokenizerOptions>(context.Configuration.GetSection(tokenizerSection));

    // Provide IOptions<TokenizerOptions> for default (unnamed) consumers
    public static IServiceCollection ConfigureTokenizerOptions(
        this IServiceCollection services,
        TokenizerOptions options) =>
        services.AddSingleton(Options.Create(options));

    #endregion

    #region Tokenizer

    public static IServiceCollection AddTokenizer(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection)
    {
        return services
            .ConfigureTokenizerOptions(context, tokenizerSection)
            .AddSingleton<ITokenizer, Tokenizer>();
    }

    public static IServiceCollection AddTokenizer(
    this IServiceCollection services,
    TokenizerOptions options)
    {
        return services
            .ConfigureTokenizerOptions(options)
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
        // Bind named options
        services.AddOptions<TokenizerOptions>(key)
                .Bind(configuration.GetSection(tokenizerSectionPath))
                .PostConfigure(o =>
                {
                    o.TokenPatterns ??= TokenizerOptions.Default.TokenPatterns;
                });

        // Register keyed tokenizer
        services.AddKeyedSingleton<ITokenizer, Tokenizer>(key, (provider, key) =>
        {
            var logger = provider.GetRequiredService<ILogger<Tokenizer>>();
            var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
            var opts = monitor.Get(key as string);
            return new Tokenizer(logger, Options.Create(opts));
        });

        return services;
    }

    public static IServiceCollection AddTokenizer(
        this IServiceCollection services,
        string key,
        TokenizerOptions options)
    {
        // Bind named options
        services.AddOptions<TokenizerOptions>(key)
            .Configure(o => o.CopyFrom(options))
            .PostConfigure(o =>
            {
                o.TokenPatterns ??= TokenizerOptions.Default.TokenPatterns;
            });

        // Register keyed tokenizer
        services.AddKeyedSingleton<ITokenizer, Tokenizer>(key, (provider, key) =>
        {
            var logger = provider.GetRequiredService<ILogger<Tokenizer>>();
            var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
            var opts = monitor.Get(key as string);
            return new Tokenizer(logger, Options.Create(opts));
        });

        return services;
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


    #endregion


    #region Parser

    public static IServiceCollection AddParser<TParser>(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParser : ParserBase
    {
        return services
            .ConfigureTokenizerOptions(context, tokenizerSectionPath)
            .AddSingleton<IParser, TParser>();
    }

    public static IServiceCollection AddParser<TParser>(
        this IServiceCollection services,
        TokenizerOptions options) where TParser : ParserBase
    {
        return services
            .ConfigureTokenizerOptions(options)
            .AddSingleton<IParser, TParser>();
    }

    public static IParser GetParser(this IServiceProvider services) => services.GetRequiredService<IParser>();

    public static IServiceCollection AddParser<TParser>(
       this IServiceCollection services,
       IConfiguration configuration,
       string key,
       string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParser : ParserBase
    {
        // Bind named options
        services.AddOptions<TokenizerOptions>(key)
                .Bind(configuration.GetSection(tokenizerSectionPath))
                .PostConfigure(o =>
                {
                    o.TokenPatterns ??= TokenizerOptions.Default.TokenPatterns;
                });

        // Register keyed tokenizer
        services.AddKeyedSingleton<IParser, TParser>(key, (provider, key) =>
        {
            var logger = provider.GetRequiredService<ILogger<ParserBase>>();
            var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
            var opts = monitor.Get(key as string);
            return ActivatorUtilities.CreateInstance<TParser>(provider, Options.Create(opts));
        });

        return services;
    }
    public static IServiceCollection AddParser<TParser>(
       this IServiceCollection services,
       string key,
       TokenizerOptions options) where TParser : ParserBase
    {
        // Bind named options
        services.AddOptions<TokenizerOptions>(key)
            .Configure(o => o.CopyFrom(options))
            .PostConfigure(o =>
            {
                o.TokenPatterns ??= TokenizerOptions.Default.TokenPatterns;
            });

        // Register keyed tokenizer
        services.AddKeyedSingleton<IParser, TParser>(key, (provider, key) =>
        {
            var logger = provider.GetRequiredService<ILogger<ParserBase>>();
            var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
            var opts = monitor.Get(key as string);
            return ActivatorUtilities.CreateInstance<TParser>(provider, Options.Create(opts));
        });

        return services;
    }

    /// <summary>
    /// Resolve a keyed parser.
    /// </summary>
    public static IParser GetParser(this IServiceProvider services, string key) =>
        services.GetRequiredKeyedService<IParser>(key);

    #endregion

    #region StatefulParser

    public static IServiceCollection AddStatefulParser<TStatefulParser>(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection) where TStatefulParser : StatefulParserBase
    {
        return services
            .ConfigureTokenizerOptions(context, tokenizerSection)
            .AddTransient<IStatefulParser, TStatefulParser>();
    }

    public static IServiceCollection AddStatefulParser<TStatefulParser>(
        this IServiceCollection services,
        TokenizerOptions options) where TStatefulParser : StatefulParserBase
    {
        return services
            .ConfigureTokenizerOptions(options)
            .AddTransient<IStatefulParser, TStatefulParser>();
    }  
    
    public static IStatefulParser GetStatefulParser(this IServiceProvider services) => services.GetRequiredService<IStatefulParser>();

    public static IServiceCollection AddStatefulParser<TStatefulParser>(
        this IServiceCollection services,
        IConfiguration configuration,
        string key,
        string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TStatefulParser : StatefulParserBase
    {
        // Bind named options
        services.AddOptions<TokenizerOptions>(key)
                .Bind(configuration.GetSection(tokenizerSectionPath))
                .PostConfigure(o =>
                {
                    o.TokenPatterns ??= TokenizerOptions.Default.TokenPatterns;
                });

        // Register keyed tokenizer
        services.AddKeyedTransient<IStatefulParser, TStatefulParser>(key, (provider, key) =>
        {
            var logger = provider.GetRequiredService<ILogger<ParserBase>>();
            var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
            var opts = monitor.Get(key as string);
            return ActivatorUtilities.CreateInstance<TStatefulParser>(provider, Options.Create(opts));
        });

        return services;
    }

    public static IServiceCollection AddStatefulParser<TStatefulParser>(
        this IServiceCollection services,
        string key,
        TokenizerOptions options) where TStatefulParser : StatefulParserBase
    {
        // Bind named options
        services.AddOptions<TokenizerOptions>(key)
            .Configure(o => o.CopyFrom(options))
            .PostConfigure(o =>
            {
                o.TokenPatterns ??= TokenizerOptions.Default.TokenPatterns;
            });

        // Register keyed tokenizer
        services.AddKeyedTransient<IStatefulParser, TStatefulParser>(key, (provider, key) =>
        {
            var logger = provider.GetRequiredService<ILogger<ParserBase>>();
            var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
            var opts = monitor.Get(key as string);
            return ActivatorUtilities.CreateInstance<TStatefulParser>(provider, Options.Create(opts));
        });

        return services;
    }

    /// <summary>
    /// Resolve a keyed stateful parser.
    /// </summary>
    public static IStatefulParser GetStatefulParser(this IServiceProvider services, string key) =>
        services.GetRequiredKeyedService<IStatefulParser>(key);


    #endregion


    #region Common parsers

    public static IServiceCollection AddCommonParsers(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddParser<DefaultParser>(configuration, "Default")
            .AddParser<Vector3Parser>(configuration, "Vector3")
            .AddParser<ComplexParser>(configuration, "Complex");
    }


    #endregion


}
