using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers.Interfaces;
using System.Numerics;
using ParserLibrary.Parsers.Validation;
using ParserLibrary.Parsers; // ADDED: for ParserServices

namespace ParserLibrary;

public static class ParserApp
{
    #region TokenizerOptions

    private static void CopyFrom(this TokenizerOptions target, TokenizerOptions source)
    {
        target.Version = source.Version;
        target.CaseSensitive = source.CaseSensitive;
        target.TokenPatterns = source.TokenPatterns; // shallow copy; deep clone if you mutate later
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

    // Non-keyed (HostBuilderContext)
    public static IServiceCollection AddTokenizer(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection)
    {
        return services
            .AddTokenizerOptions(context, tokenizerSection)
            .AddSingleton<ITokenizerValidator>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<TokenizerValidator>>();
                var opts = sp.GetRequiredService<IOptions<TokenizerOptions>>().Value;
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new TokenizerValidator(logger, patterns);
            })
            // EXPLICIT: inject validator into Tokenizer
            .AddSingleton<ITokenizer>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<Tokenizer>>();
                var options = sp.GetRequiredService<IOptions<TokenizerOptions>>();
                var validator = sp.GetRequiredService<ITokenizerValidator>();
                return new Tokenizer(logger, options, validator);
            });
    }

    // Non-keyed (TokenizerOptions instance)
    public static IServiceCollection AddTokenizer(
        this IServiceCollection services,
        TokenizerOptions options)
    {
        return services
            .AddTokenizerOptions(options)
            .AddSingleton<ITokenizerValidator>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<TokenizerValidator>>();
                var patterns = options.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new TokenizerValidator(logger, patterns);
            })
            // EXPLICIT: inject validator into Tokenizer
            .AddSingleton<ITokenizer>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<Tokenizer>>();
                var iopts = sp.GetRequiredService<IOptions<TokenizerOptions>>();
                var validator = sp.GetRequiredService<ITokenizerValidator>();
                return new Tokenizer(logger, iopts, validator);
            });
    }


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
            // Keyed validator (same key as tokenizer)
            .AddKeyedSingleton<ITokenizerValidator>(key, (provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<TokenizerValidator>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new TokenizerValidator(logger, patterns);
            })
            // EXPLICIT: inject keyed validator into Tokenizer
            .AddKeyedSingleton<ITokenizer, Tokenizer>(key, (provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<Tokenizer>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var validator = provider.GetRequiredKeyedService<ITokenizerValidator>(key);
                return new Tokenizer(logger, Options.Create(opts), validator);
            });
    }

    // Keyed (options instance)
    public static IServiceCollection AddTokenizer(
        this IServiceCollection services,
        string key,
        TokenizerOptions options)
    {
        return services
            .AddTokenizerOptions(key, options)
            .AddKeyedSingleton<ITokenizerValidator>(key, (provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<TokenizerValidator>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new TokenizerValidator(logger, patterns);
            })
            // EXPLICIT: inject keyed validator into Tokenizer
            .AddKeyedSingleton<ITokenizer, Tokenizer>(key, (provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<Tokenizer>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var validator = provider.GetRequiredKeyedService<ITokenizerValidator>(key);
                return new Tokenizer(logger, Options.Create(opts), validator);
            });
    }

    /// <summary>
    /// Resolve a keyed tokenizer.
    /// </summary>
    public static ITokenizer GetTokenizer(this IServiceProvider services, string key) =>
        services.GetRequiredKeyedService<ITokenizer>(key);

    /// <summary>
    /// Resolve a keyed tokenizer validator.
    /// </summary>
    public static ITokenizerValidator GetTokenizerValidator(this IServiceProvider services, string key) =>
        services.GetRequiredKeyedService<ITokenizerValidator>(key);



    #endregion


    #region Add Parser/Stateful Parser fo ServiceCollection

    // Non-keyed: AddParser — add IParserValidator so DI can satisfy CoreParser constructor
    public static IServiceCollection AddParser<TParser>(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParser : CoreParser
    {
        return services
            .AddTokenizerOptions(context, tokenizerSectionPath)
            .AddSingleton<ITokenizerValidator>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<TokenizerValidator>>();
                var opts = sp.GetRequiredService<IOptions<TokenizerOptions>>().Value;
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new ParserLibrary.Parsers.Validation.TokenizerValidator(logger, patterns);
            })
            .AddSingleton<IParserValidator>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ParserValidator>>();
                var tokVal = sp.GetRequiredService<ITokenizerValidator>();
                var opts = sp.GetRequiredService<IOptions<TokenizerOptions>>().Value;
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new ParserLibrary.Parsers.Validation.ParserValidator(logger, tokVal, patterns);
            })
            // ADDED: ParserServices bundle (singleton)
            .AddSingleton<ParserServices>(sp => new ParserServices
            {
                Options = sp.GetRequiredService<IOptions<TokenizerOptions>>(),
                TokenizerValidator = sp.GetRequiredService<ITokenizerValidator>(),
                ParserValidator = sp.GetRequiredService<IParserValidator>()
            })
            .AddSingleton<IParser, TParser>();
    }

    // Non-keyed: AddParser overload with options
    public static IServiceCollection AddParser<TParser>(
        this IServiceCollection services,
        TokenizerOptions options) where TParser : CoreParser
    {
        return services
            .AddTokenizerOptions(options)
            .AddSingleton<ITokenizerValidator>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<TokenizerValidator>>();
                var patterns = options.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new ParserLibrary.Parsers.Validation.TokenizerValidator(logger, patterns);
            })
            .AddSingleton<IParserValidator>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ParserValidator>>();
                var tokVal = sp.GetRequiredService<ITokenizerValidator>();
                var patterns = options.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new ParserLibrary.Parsers.Validation.ParserValidator(logger, tokVal, patterns);
            })
            // ADDED: ParserServices bundle (singleton)
            .AddSingleton<ParserServices>(sp => new ParserServices
            {
                Options = sp.GetRequiredService<IOptions<TokenizerOptions>>(),
                TokenizerValidator = sp.GetRequiredService<ITokenizerValidator>(),
                ParserValidator = sp.GetRequiredService<IParserValidator>()
            })
            .AddSingleton<IParser, TParser>();
    }

    // Keyed: AddParser — register keyed IParserValidator and pass via factory
    public static IServiceCollection AddParser<TParser>(
       this IServiceCollection services,
       IConfiguration configuration,
       string key,
       string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParser : CoreParser
    {
        return services
            .AddTokenizerOptions(configuration, key, tokenizerSectionPath)
            .AddKeyedSingleton<ITokenizerValidator>(key, (provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<TokenizerValidator>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new ParserLibrary.Parsers.Validation.TokenizerValidator(logger, patterns);
            })
            .AddKeyedSingleton<IParserValidator>(key, (provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<ParserValidator>>();
                var tokVal = provider.GetRequiredKeyedService<ITokenizerValidator>(key);
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new ParserLibrary.Parsers.Validation.ParserValidator(logger, tokVal, patterns);
            })
            // ADDED: ParserServices bundle (keyed singleton)
            .AddKeyedSingleton<ParserServices>(key, (provider, _) =>
            {
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                return new ParserServices
                {
                    Options = Options.Create(opts),
                    TokenizerValidator = provider.GetRequiredKeyedService<ITokenizerValidator>(key),
                    ParserValidator = provider.GetRequiredKeyedService<IParserValidator>(key)
                };
            })
            .AddKeyedSingleton<IParser, TParser>(key, (provider, _) =>
            {
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var tokVal = provider.GetRequiredKeyedService<ITokenizerValidator>(key);
                var parVal = provider.GetRequiredKeyedService<IParserValidator>(key);
                // pass options + both validators explicitly
                return ActivatorUtilities.CreateInstance<TParser>(provider, Options.Create(opts), tokVal, parVal);
            });
    }

    // Keyed: AddParser with in-memory options
    public static IServiceCollection AddParser<TParser>(
        this IServiceCollection services,
        string key,
        TokenizerOptions options) where TParser : CoreParser
    {
        return services
            .AddTokenizerOptions(key, options)
            .AddKeyedSingleton<ITokenizerValidator>(key, (provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<TokenizerValidator>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new ParserLibrary.Parsers.Validation.TokenizerValidator(logger, patterns);
            })
            .AddKeyedSingleton<IParserValidator>(key, (provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<ParserValidator>>();
                var tokVal = provider.GetRequiredKeyedService<ITokenizerValidator>(key);
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new ParserLibrary.Parsers.Validation.ParserValidator(logger, tokVal, patterns);
            })
            // ADDED: ParserServices bundle (keyed singleton)
            .AddKeyedSingleton<ParserServices>(key, (provider, _) =>
            {
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                return new ParserServices
                {
                    Options = Options.Create(opts),
                    TokenizerValidator = provider.GetRequiredKeyedService<ITokenizerValidator>(key),
                    ParserValidator = provider.GetRequiredKeyedService<IParserValidator>(key)
                };
            })
            .AddKeyedSingleton<IParser, TParser>(key, (provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<TParser>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var tokVal = provider.GetRequiredKeyedService<ITokenizerValidator>(key);
                var parVal = provider.GetRequiredKeyedService<IParserValidator>(key);
                return ActivatorUtilities.CreateInstance<TParser>(provider, Options.Create(opts), tokVal, parVal);
                //return ActivatorUtilities.CreateInstance<TParser>(provider,logger,provider);
            });
    }

    // Non-keyed: AddStatefulParser — add IParserValidator so DI can resolve constructor
    public static IServiceCollection AddStatefulParser<TStatefulParser>(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection) where TStatefulParser : CoreStatefulParser
    {
        return services
            .AddTokenizerOptions(context, tokenizerSection)
            .AddSingleton<ITokenizerValidator>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<TokenizerValidator>>();
                var opts = sp.GetRequiredService<IOptions<TokenizerOptions>>().Value;
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new TokenizerValidator(logger, patterns);
            })
            .AddSingleton<IParserValidator>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ParserValidator>>();
                var tokVal = sp.GetRequiredService<ITokenizerValidator>();
                var opts = sp.GetRequiredService<IOptions<TokenizerOptions>>().Value;
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new ParserLibrary.Parsers.Validation.ParserValidator(logger, tokVal, patterns);
            })
            // ADDED: ParserServices bundle (singleton)
            .AddSingleton<ParserServices>(sp => new ParserServices
            {
                Options = sp.GetRequiredService<IOptions<TokenizerOptions>>(),
                TokenizerValidator = sp.GetRequiredService<ITokenizerValidator>(),
                ParserValidator = sp.GetRequiredService<IParserValidator>()
            })
            .AddTransient<IStatefulParser, TStatefulParser>();
    }

    // Non-keyed: AddStatefulParser with options
    public static IServiceCollection AddStatefulParser<TStatefulParser>(
        this IServiceCollection services,
        TokenizerOptions options) where TStatefulParser : CoreStatefulParser
    {
        return services
            .AddTokenizerOptions(options)
            .AddSingleton<ITokenizerValidator>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<TokenizerValidator>>();
                var patterns = options.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new TokenizerValidator(logger, patterns);
            })
            .AddSingleton<IParserValidator>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ParserValidator>>();
                var tokVal = sp.GetRequiredService<ITokenizerValidator>();
                var patterns = options.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new ParserLibrary.Parsers.Validation.ParserValidator(logger, tokVal, patterns);
            })
            // ADDED: ParserServices bundle (singleton)
            .AddSingleton<ParserServices>(sp => new ParserServices
            {
                Options = sp.GetRequiredService<IOptions<TokenizerOptions>>(),
                TokenizerValidator = sp.GetRequiredService<ITokenizerValidator>(),
                ParserValidator = sp.GetRequiredService<IParserValidator>()
            })
            .AddTransient<IStatefulParser, TStatefulParser>();
    }

    // Keyed: AddStatefulParser — register keyed parser validator and pass explicitly
    public static IServiceCollection AddStatefulParser<TStatefulParser>(
        this IServiceCollection services,
        IConfiguration configuration,
        string key,
        string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TStatefulParser : CoreStatefulParser
    {
        return services
            .AddTokenizerOptions(configuration, key, tokenizerSectionPath)
            .AddKeyedSingleton<ITokenizerValidator>(key, (provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<TokenizerValidator>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new TokenizerValidator(logger, patterns);
            })
            .AddKeyedSingleton<IParserValidator>(key, (provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<ParserValidator>>();
                var tokVal = provider.GetRequiredKeyedService<ITokenizerValidator>(key);
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new ParserLibrary.Parsers.Validation.ParserValidator(logger, tokVal, patterns);
            })
            // ADDED: ParserServices bundle (keyed singleton)
            .AddKeyedSingleton<ParserServices>(key, (provider, _) =>
            {
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                return new ParserServices
                {
                    Options = Options.Create(opts),
                    TokenizerValidator = provider.GetRequiredKeyedService<ITokenizerValidator>(key),
                    ParserValidator = provider.GetRequiredKeyedService<IParserValidator>(key)
                };
            })
            .AddKeyedTransient<IStatefulParser, TStatefulParser>(key, (provider, k) =>
            {
                var name = k as string;
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(name);
                var tokVal = provider.GetRequiredKeyedService<ITokenizerValidator>(name);
                var parVal = provider.GetRequiredKeyedService<IParserValidator>(name);
                return ActivatorUtilities.CreateInstance<TStatefulParser>(provider, Options.Create(opts), tokVal, parVal);
            });
    }

    // Keyed: AddStatefulParser with options
    public static IServiceCollection AddStatefulParser<TStatefulParser>(
        this IServiceCollection services,
        string key,
        TokenizerOptions options) where TStatefulParser : CoreStatefulParser
    {
        return services
            .AddTokenizerOptions(key, options)
            .AddKeyedSingleton<ITokenizerValidator>(key, (provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<TokenizerValidator>>();
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new TokenizerValidator(logger, patterns);
            })
            .AddKeyedSingleton<IParserValidator>(key, (provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<ParserValidator>>();
                var tokVal = provider.GetRequiredKeyedService<ITokenizerValidator>(key);
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
                return new ParserLibrary.Parsers.Validation.ParserValidator(logger, tokVal, patterns);
            })
            // ADDED: ParserServices bundle (keyed singleton)
            .AddKeyedSingleton<ParserServices>(key, (provider, _) =>
            {
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(key);
                return new ParserServices
                {
                    Options = Options.Create(opts),
                    TokenizerValidator = provider.GetRequiredKeyedService<ITokenizerValidator>(key),
                    ParserValidator = provider.GetRequiredKeyedService<IParserValidator>(key)
                };
            })
            .AddKeyedTransient<IStatefulParser, TStatefulParser>(key, (provider, k) =>
            {
                var name = k as string;
                var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
                var opts = monitor.Get(name);
                var tokVal = provider.GetRequiredKeyedService<ITokenizerValidator>(name);
                var parVal = provider.GetRequiredKeyedService<IParserValidator>(name);
                return ActivatorUtilities.CreateInstance<TStatefulParser>(provider, Options.Create(opts), tokVal, parVal);
            });
    }


    #endregion


    #region Common parsers

    public static IServiceCollection AddCommonParsers(this IServiceCollection services)
    {
        return services
            .AddParser<CoreParser>("Core", TokenizerOptions.Default) // base parser with no customizations  

            .AddParser<DoubleParser>("Default", TokenizerOptions.Default)
            .AddParser<DoubleParser>("Double", TokenizerOptions.Default) //practically an alias

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

    public static IParser GetCoreParser()
    {
        CreateCommonsHostIfNeeded();
        return _commonParsersHost!.Services.GetParser("Core");
    }

    public static IParser GetDefaultParser() =>
        GetDoubleParser();

    public static IParser GetDoubleParser()
    {
        CreateCommonsHostIfNeeded();
        return _commonParsersHost!.Services.GetParser("Double");
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


    #region GetParser/GetStatefulParser from service/hosts

    public static ITokenizer GetTokenizer(this IServiceProvider services) => services.GetRequiredService<ITokenizer>();

    public static ITokenizer GetTokenizer(this IHost host) => host.Services.GetRequiredService<ITokenizer>();

    public static ITokenizerValidator GetTokenizerValidator(this IServiceProvider services) =>
        services.GetRequiredService<ITokenizerValidator>();

    public static ITokenizerValidator GetTokenizerValidator(this IHost host) =>
        host.Services.GetRequiredService<ITokenizerValidator>();


    // Resolve non-keyed parser
    public static IParser GetParser(this IServiceProvider services) =>
        services.GetRequiredService<IParser>();

    public static IParser GetParser(this IHost host) =>
        host.Services.GetRequiredService<IParser>();

    public static IParser GetParser(this IHost host, string key) =>
        host.Services.GetRequiredKeyedService<IParser>(key);

    // Resolve keyed parser
    public static IParser GetParser(this IServiceProvider services, string key) =>
        services.GetRequiredKeyedService<IParser>(key);

    // Resolve non-keyed stateful parser
    public static IStatefulParser GetStatefulParser(this IServiceProvider services) =>
        services.GetRequiredService<IStatefulParser>();

    public static IStatefulParser GetStatefulParser(this IHost host) =>
        host.Services.GetRequiredService<IStatefulParser>();

    // Resolve keyed stateful parser
    public static IStatefulParser GetStatefulParser(this IHost host, string key) =>
        host.Services.GetRequiredKeyedService<IStatefulParser>(key);

    public static IStatefulParser GetStatefulParser(this IServiceProvider services, string key) =>
        services.GetRequiredKeyedService<IStatefulParser>(key);

    #endregion


    #region Get apps and parsers (for single cases only)

    private static IHostBuilder GetHostBuilder(string? settingsFile = null)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                if (!string.IsNullOrWhiteSpace(settingsFile) && settingsFile != "appsettings.json")
                    config.AddJsonFile(settingsFile);  // Configure app configuration if needed
            });
    }

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

    public static IHost GetStatefulParserApp<TStatefulParser>(string? settingsFile = null, string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TStatefulParser : CoreStatefulParser
    {
        return GetHostBuilder(settingsFile)
            .ConfigureServices((context, services) =>
            {
                services.AddStatefulParser<TStatefulParser>(context, tokenizerSectionPath);
            })
            .Build();
    }

    public static IHost GetStatefulParserApp<TStatefulParser>(TokenizerOptions options) where TStatefulParser : CoreStatefulParser
    {
        return GetHostBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddStatefulParser<TStatefulParser>(options);
            })
            .Build();
    }

    public static IHost GetParserApp<TParser>(string? settingsFile = null, string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParser : CoreParser
    {
        return GetHostBuilder(settingsFile)
            .ConfigureServices((context, services) =>
            {
                services.AddParser<TParser>(context, tokenizerSectionPath);
            })
            .Build();
    }

    public static IHost GetParserApp<TParser>(TokenizerOptions options) where TParser : CoreParser
    {
        return GetHostBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddParser<TParser>(options);
            })
            .Build();
    }

    public static IStatefulParser GetStatefulParser<TStatefulParser>(string? settingsFile = null, string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TStatefulParser : CoreStatefulParser =>
        GetStatefulParserApp<TStatefulParser>(settingsFile, tokenizerSectionPath).GetStatefulParser();

    public static IStatefulParser GetStatefulParser<TStatefulParser>(TokenizerOptions options) where TStatefulParser : CoreStatefulParser =>
        GetStatefulParserApp<TStatefulParser>(options).GetStatefulParser();

    public static IStatefulParser GetStatefulParser<TStatefulParser>() where TStatefulParser : CoreStatefulParser =>
        GetStatefulParserApp<TStatefulParser>(TokenizerOptions.Default).GetStatefulParser();

    public static IParser GetParser<TParser>(string? settingsFile = null, string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParser : CoreParser =>
        GetParserApp<TParser>(settingsFile, tokenizerSectionPath).GetParser();

    public static IParser GetParser<TParser>(TokenizerOptions options) where TParser : CoreParser =>
        GetParserApp<TParser>(options).GetParser();

    public static IParser GetParser<TParser>() where TParser : CoreParser =>
        GetParserApp<TParser>(TokenizerOptions.Default).GetParser();

    #endregion


}



