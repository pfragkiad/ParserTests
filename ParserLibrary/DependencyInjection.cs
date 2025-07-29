using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                    .AddStatefulParser<TParser>();
    }
    public static IServiceCollection ConfigureTokenizerOptions(
        this IServiceCollection services,
        HostBuilderContext context,
        string tokenizerSection = TokenizerOptions.TokenizerSection) =>
        services.Configure<TokenizerOptions>(context.Configuration.GetSection(tokenizerSection));

    public static IServiceCollection AddTokenizer(this IServiceCollection services) => services.AddSingleton<ITokenizer, Tokenizer>();

    public static ITokenizer? GetTokenizer(this IServiceProvider services) => services.GetService<ITokenizer>();

    public static IServiceCollection AddParser<TParser>(this IServiceCollection services) where TParser : Parser
             => services.AddSingleton<IParser, TParser>();


    public static IParser? GetParser(this IServiceProvider services) => services.GetService<IParser>();

    public static IParser GetRequiredParser(this IServiceProvider services) => services.GetRequiredService<IParser>();

    public static IServiceCollection AddStatefulParser<TParser>(this IServiceCollection services) where TParser : StatefulParser
            => services.AddTransient<IStatefulParser, TParser>();


    public static IStatefulParser? GetStatefulParser(this IServiceProvider services) => services.GetService<IStatefulParser>();

    public static IStatefulParser GetRequiredStatefulParser(this IServiceProvider services) => services.GetRequiredService<IStatefulParser>();

    #endregion

}
