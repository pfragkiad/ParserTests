using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ParserLibrary;

public static class DependencyInjection
{
    //This is a prerequisite for Tokenizer (ITokenizer), Parser (IParser).

    //StatefulParser (IStatefulParser) is available via factory only, due tot he expression, variable parameters  (unless we remove expression/variables from constructor..)

    //StatefulParserFactory (IStatefulParserFactory) also needs the tokenizer options configured. The problem with the StatefulParser is that uses reflection to create the instance.
    //That's why an explicit factory is needed for custom StatefulParsers. This is not nice - and that's why we should restore the default StatefulParser without the expression, variables fields ..

    //DefaultParser, Vector3Parser, ComplexParser are all Parser implementations.
    //Adding as them as IParser means that only one of them can be registered at a time, which bottlenecks their usage without explicit reason.



    public static IServiceCollection ConfigureTokenizerOptions(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection) =>
        services.Configure<TokenizerOptions>(context.Configuration.GetSection(tokenizerSection));


    #region Tokenizer

    //Tokenizer needs at least the TokenizerOptions configured
    public static IServiceCollection AddTokenizer(this IServiceCollection services) => services.AddSingleton<ITokenizer, Tokenizer>();

    public static ITokenizer? GetTokenizer(this IServiceProvider services) => services.GetService<ITokenizer>();

    #endregion


    #region Tokenizer, Parser services extensions

    public static IServiceCollection AddParserLibrary<TParser>(this IServiceCollection services, HostBuilderContext context
        , string tokenizerSection = TokenizerOptions.TokenizerSection) where TParser : Parser
    {
        return services
                    .ConfigureTokenizerOptions(context, tokenizerSection)
                    .AddTokenizer()
                    .AddParser<TParser>()
                    .AddStatefulParserFactory();
    }

    public static IServiceCollection AddStatefulParserLibrary(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection) 
    {
        return services
                    .ConfigureTokenizerOptions(context, tokenizerSection)
                    .AddTokenizer()
                    //.AddStatefulParser<TStatefulParser>()

                    //parser available via factory only
                    .AddStatefulParserFactory();
    }


    public static IServiceCollection AddParser<TParser>(this IServiceCollection services) where TParser : Parser
             => services.AddSingleton<IParser, TParser>();


    public static IParser? GetParser(this IServiceProvider services) => services.GetService<IParser>();

    public static IParser GetRequiredParser(this IServiceProvider services) => services.GetRequiredService<IParser>();

    //public static IServiceCollection AddStatefulParser<TParser>(this IServiceCollection services) where TParser : StatefulParser
    //        => services.AddTransient<IStatefulParser, TParser>();

    public static IServiceCollection AddStatefulParserFactory(this IServiceCollection services) =>
        services.AddSingleton<IStatefulParserFactory, StatefulParserFactory>();

    //public static IStatefulParser? GetStatefulParser(this IServiceProvider services) => services.GetService<IStatefulParser>();

    //public static IStatefulParser GetRequiredStatefulParser(this IServiceProvider services) => services.GetRequiredService<IStatefulParser>();

    public static IStatefulParserFactory? GetStatefulParserFactory(this IServiceProvider services) => services.GetService<IStatefulParserFactory>();

    public static IStatefulParserFactory GetRequiredStatefulParserFactory(this IServiceProvider services) => services.GetRequiredService<IStatefulParserFactory>();

    #endregion

}
