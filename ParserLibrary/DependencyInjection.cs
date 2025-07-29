using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ParserLibrary;

public static class DependencyInjection
{
    #region Tokenizer, Parser services extensions
    public static IServiceCollection AddParserLibrary<TParser>(this IServiceCollection services, HostBuilderContext context
        , string tokenizerSection = TokenizerOptions.TokenizerSection) where TParser : Parser
    {
        return services
                    .ConfigureTokenizerOptions(context, tokenizerSection)
                    .AddTokenizer()
                    .AddParser<TParser>();
    }
    public static IServiceCollection AddStatefulParserLibrary<TParser>(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection) where TParser : StatefulParser
    {
        return services
                    .ConfigureTokenizerOptions(context, tokenizerSection)
                    .AddTokenizer()
                    .AddStatefulParser<TParser>()
                    .AddStatefulParserFactory();
    }
    public static IServiceCollection ConfigureTokenizerOptions(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection) =>
        services.Configure<TokenizerOptions>(context.Configuration.GetSection(tokenizerSection));

    public static IServiceCollection AddTokenizer(this IServiceCollection services) => services.AddSingleton<ITokenizer, Tokenizer>();

    public static ITokenizer? GetTokenizer(this IServiceProvider services) => services.GetService<ITokenizer>();

    public static IServiceCollection AddParser<TParser>(this IServiceCollection services) where TParser : Parser
             => services.AddScoped<IParser, TParser>();


    public static IParser? GetParser(this IServiceProvider services) => services.GetService<IParser>();

    public static IParser GetRequiredParser(this IServiceProvider services) => services.GetRequiredService<IParser>();

    public static IServiceCollection AddStatefulParser<TParser>(this IServiceCollection services) where TParser : StatefulParser
            => services.AddTransient<IStatefulParser, TParser>();

    public static IServiceCollection AddStatefulParserFactory(this IServiceCollection services) =>
        services.AddScoped<IStatefulParserFactory, StatefulParserFactory>();

    public static IStatefulParser? GetStatefulParser(this IServiceProvider services) => services.GetService<IStatefulParser>();

    public static IStatefulParser GetRequiredStatefulParser(this IServiceProvider services) => services.GetRequiredService<IStatefulParser>();

    public static IStatefulParserFactory? GetStatefulParserFactory(this IServiceProvider services) => services.GetService<IStatefulParserFactory>();

    public static IStatefulParserFactory GetRequiredStatefulParserFactory(this IServiceProvider services) => services.GetRequiredService<IStatefulParserFactory>();

    #endregion

}
