using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParserLibrary.Meta;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Parsers.Validation;
using ParserLibrary.Tokenizers.Interfaces;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParserLibrary;

public static class ParserApp
{
    #region TokenizerOptions

    private static void CopyFrom(this TokenizerOptions target, TokenizerOptions source)
    {
        target.Version = source.Version;
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

    #region Parser validators

    private static IServiceCollection AddParserValidators(this IServiceCollection services)
    {
        return services
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
               //var tokVal = sp.GetRequiredService<ITokenizerValidator>();
               var opts = sp.GetRequiredService<IOptions<TokenizerOptions>>().Value;
               var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
               return new ParserValidator(logger, patterns);
           })
           // ADDED: ParserServices bundle (singleton)
           .AddSingleton<ParserServices>(sp => new ParserServices
           {
               Options = sp.GetRequiredService<IOptions<TokenizerOptions>>(),
               TokenizerValidator = sp.GetRequiredService<ITokenizerValidator>(),
               ParserValidator = sp.GetRequiredService<IParserValidator>()
           });
    }

    private static IServiceCollection AddParserValidators(this IServiceCollection services, string key)
    {
        return services
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
             //var tokVal = provider.GetRequiredKeyedService<ITokenizerValidator>(key);
             var monitor = provider.GetRequiredService<IOptionsMonitor<TokenizerOptions>>();
             var opts = monitor.Get(key);
             var patterns = opts.TokenPatterns ?? TokenizerOptions.Default.TokenPatterns;
             return new ParserValidator(logger, patterns);
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
         });
    }



    #endregion

    #region Parser dependencies (common for Parser and ParserSession)

    public static IServiceCollection AddParserDependencies(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSectionPath = TokenizerOptions.TokenizerSection)
    {
        return services
            .AddTokenizerOptions(context, tokenizerSectionPath)
            .AddParserValidators();
    }

    public static IServiceCollection AddParserDependencies(
        this IServiceCollection services,
        TokenizerOptions options)
    {
        return services
            .AddTokenizerOptions(options)
            .AddParserValidators();
    }

    // Keyed: AddParser — register keyed IParserValidator and pass via factory
    public static IServiceCollection AddParserDependencies(
       this IServiceCollection services,
       IConfiguration configuration,
       string key,
       string tokenizerSectionPath = TokenizerOptions.TokenizerSection)
    {
        return services
            .AddTokenizerOptions(configuration, key, tokenizerSectionPath)
            .AddParserValidators(key);
    }

    // Keyed: AddParser with in-memory options
    public static IServiceCollection AddParserDependencies(
        this IServiceCollection services,
        string key,
        TokenizerOptions options)
    {
        return services
            .AddTokenizerOptions(key, options)
            .AddParserValidators(key);
    }

    #endregion

    #region Add Parser/Parser session fo ServiceCollection

    // Non-keyed: AddParser — add IParserValidator so DI can satisfy CoreParser constructor
    public static IServiceCollection AddParser<TParser>(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParser : ParserBase
    {
        return services
            .AddParserDependencies(context, tokenizerSectionPath)
            .AddSingleton<IParser, TParser>(sp =>
            {
                var servicesBundle = sp.GetRequiredService<ParserServices>();
                return ActivatorUtilities.CreateInstance<TParser>(sp, servicesBundle);
            })
        //.AddSingleton<IParser, TParser>();
        ;
    }

    // Non-keyed: AddParserSession — add IParserValidator so DI can resolve constructor
    public static IServiceCollection AddParserSession<TParserSession>(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection) where TParserSession : ParserSessionBase
    {
        return services
            .AddParserDependencies(context, tokenizerSection)
            .AddTransient<IParserSession, TParserSession>(sp =>
            {
                //var logger = sp.GetRequiredService<ILogger<TParserSession>>();
                var servicesBundle = sp.GetRequiredService<ParserServices>();
                return ActivatorUtilities.CreateInstance<TParserSession>(sp, servicesBundle);
            })
            //.AddTransient<IStatefulParser, TParserSession>()
            ;
    }


    // Non-keyed: AddParser overload with options
    public static IServiceCollection AddParser<TParser>(
        this IServiceCollection services,
        TokenizerOptions options) where TParser : ParserBase
    {
        return services
            .AddParserDependencies(options)
            .AddSingleton<IParser, TParser>(sp =>
            {
                var servicesBundle = sp.GetRequiredService<ParserServices>();
                return ActivatorUtilities.CreateInstance<TParser>(sp, servicesBundle);
            })
            //.AddSingleton<IParser, TParser>()
            ;
    }

    // Non-keyed: AddParserSession with options
    public static IServiceCollection AddParserSession<TParserSession>(
        this IServiceCollection services,
        TokenizerOptions options) where TParserSession : ParserSessionBase
    {
        return services
            .AddParserDependencies(options)
            .AddTransient<IParserSession, TParserSession>(sp =>
            {
                var servicesBundle = sp.GetRequiredService<ParserServices>();
                return ActivatorUtilities.CreateInstance<TParserSession>(sp, servicesBundle);
            })
            ;
    }

    // Keyed: AddParser — register keyed IParserValidator and pass via factory
    public static IServiceCollection AddParser<TParser>(
       this IServiceCollection services,
       IConfiguration configuration,
       string key,
       string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParser : ParserBase
    {
        return services
            .AddParserDependencies(configuration, key, tokenizerSectionPath)
            .AddKeyedSingleton<IParser, TParser>(key, (provider, _) =>
            {
                var servicesBundle = provider.GetRequiredKeyedService<ParserServices>(key);
                return ActivatorUtilities.CreateInstance<TParser>(provider, servicesBundle);
            });
    }

    // Keyed: AddParserSession — register keyed parser validator and pass explicitly
    public static IServiceCollection AddParserSession<TParserSession>(
        this IServiceCollection services,
        IConfiguration configuration,
        string key,
        string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParserSession : ParserSessionBase
    {
        return services
            .AddParserDependencies(configuration, key, tokenizerSectionPath)
            .AddKeyedTransient<IParserSession, TParserSession>(key, (provider, _) =>
            {
                var servicesBundle = provider.GetRequiredKeyedService<ParserServices>(key);
                return ActivatorUtilities.CreateInstance<TParserSession>(provider, servicesBundle);
            })
            ;
    }


    // Keyed: AddParser with in-memory options
    public static IServiceCollection AddParser<TParser>(
        this IServiceCollection services,
        string key,
        TokenizerOptions options) where TParser : ParserBase
    {
        return services
            .AddParserDependencies(key, options)
            .AddKeyedSingleton<IParser, TParser>(key, (provider, _) =>
            {
                var servicesBundle = provider.GetRequiredKeyedService<ParserServices>(key);
                return ActivatorUtilities.CreateInstance<TParser>(provider, servicesBundle);
            })
            ;
    }

    // Keyed: AddParserSession with options
    public static IServiceCollection AddParserSession<TParserSession>(
        this IServiceCollection services,
        string key,
        TokenizerOptions options) where TParserSession : ParserSessionBase
    {
        return services
            .AddParserDependencies(key, options)
            .AddKeyedTransient<IParserSession, TParserSession>(key, (provider, _) =>
            {
                var servicesBundle = provider.GetRequiredKeyedService<ParserServices>(key);
                return ActivatorUtilities.CreateInstance<TParserSession>(provider, servicesBundle);
            })
            ;
    }


    #endregion


    #region Common parsers

    public static IServiceCollection AddCommonParsers(this IServiceCollection services)
    {
        return services
            //.AddParser<Parser>("Core", TokenizerOptions.Default) // default parser for single Evaluate functions

            .AddParser<DoubleParser>("Default", TokenizerOptions.Default)
            .AddParser<DoubleParser>("Double", TokenizerOptions.Default) //practically an alias
            .AddParser<Vector3Parser>("Vector3", TokenizerOptions.Default)
            .AddParser<ComplexParser>("Complex", TokenizerOptions.Default)

            .AddParserSession<DoubleParserSession>("Default", TokenizerOptions.Default)
            .AddParserSession<DoubleParserSession>("Double", TokenizerOptions.Default) //practically an alias
            .AddParserSession<Vector3ParserSession>("Vector3", TokenizerOptions.Default)
            .AddParserSession<ComplexParserSession>("Complex", TokenizerOptions.Default)
            ;
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

    public static IParser GetDefaultParser() =>
        GetDoubleParser();

    public static IParserSession GetDefaultParserSession() =>
        GetDoubleParserSession();

    public static IParser GetDoubleParser()
    {
        CreateCommonsHostIfNeeded();
        return _commonParsersHost!.Services.GetParser("Double");
    }

    public static IParserSession GetDoubleParserSession()
    {
        CreateCommonsHostIfNeeded();
        return _commonParsersHost!.Services.GetParserSession("Double");
    }

    public static IParser GetVector3Parser()
    {
        CreateCommonsHostIfNeeded();
        return _commonParsersHost!.Services.GetParser("Vector3");
    }

    public static IParserSession GetVector3ParserSession()
    {
        CreateCommonsHostIfNeeded();
        return _commonParsersHost!.Services.GetParserSession("Vector3");
    }

    public static IParser GetComplexParser()
    {
        CreateCommonsHostIfNeeded();
        return _commonParsersHost!.Services.GetParser("Complex");
    }

    public static IParserSession GetComplexParserSession()
    {
        CreateCommonsHostIfNeeded();
        return _commonParsersHost!.Services.GetParserSession("Complex");
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


    #region GetParser/GetParserSession from service/hosts

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
    public static IParserSession GetParserSession(this IServiceProvider services) =>
        services.GetRequiredService<IParserSession>();

    public static IParserSession GetParserSession(this IHost host) =>
        host.Services.GetRequiredService<IParserSession>();

    // Resolve keyed stateful parser
    public static IParserSession GetParserSession(this IHost host, string key) =>
        host.Services.GetRequiredKeyedService<IParserSession>(key);

    public static IParserSession GetParserSession(this IServiceProvider services, string key) =>
        services.GetRequiredKeyedService<IParserSession>(key);

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

    public static IHost GetParserSessionApp<TParserSession>(string? settingsFile = null, string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParserSession : ParserSessionBase
    {
        return GetHostBuilder(settingsFile)
            .ConfigureServices((context, services) =>
            {
                services.AddParserSession<TParserSession>(context, tokenizerSectionPath);
            })
            .Build();
    }

    public static IHost GetParserSessionApp<TParserSession>(TokenizerOptions options) where TParserSession : ParserSessionBase
    {
        return GetHostBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddParserSession<TParserSession>(options);
            })
            .Build();
    }

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

    public static IParserSession GetParserSession<TParserSession>(string? settingsFile = null, string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParserSession : ParserSessionBase =>
        GetParserSessionApp<TParserSession>(settingsFile, tokenizerSectionPath).GetParserSession();

    public static IParserSession GetParserSession<TParserSession>(TokenizerOptions options) where TParserSession : ParserSessionBase =>
        GetParserSessionApp<TParserSession>(options).GetParserSession();

    public static IParserSession GetParserSession<TParserSession>() where TParserSession : ParserSessionBase =>
        GetParserSessionApp<TParserSession>(TokenizerOptions.Default).GetParserSession();

    public static IParser GetParser<TParser>(string? settingsFile = null, string tokenizerSectionPath = TokenizerOptions.TokenizerSection) where TParser : ParserBase =>
        GetParserApp<TParser>(settingsFile, tokenizerSectionPath).GetParser();

    public static IParser GetParser<TParser>(TokenizerOptions options) where TParser : ParserBase =>
        GetParserApp<TParser>(options).GetParser();

    public static IParser GetParser<TParser>() where TParser : ParserBase =>
        GetParserApp<TParser>(TokenizerOptions.Default).GetParser();

    #endregion


    #region JSON converters
    public static void AddParserConverters(this JsonSerializerOptions options)
    {
        // Existing: Node<Token> binary-only converter
        AddConverterOnce(options, new NodeTokenBinaryJsonConverter());

        // NEW: TokenTree binary-only converter (uses Node<Token> converter above)
        AddConverterOnce(options, new TokenTreeBinaryJsonConverter());
    }

    private static void AddConverterOnce(JsonSerializerOptions options, JsonConverter converter)
    {
        if (!options.Converters.Any(c => c.GetType() == converter.GetType()))
            options.Converters.Add(converter);
    }

    #endregion
}








