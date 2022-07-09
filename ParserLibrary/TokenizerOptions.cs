using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserLibrary;

public class TokenizerOptions
{

    public static string TokenizerSection = "tokenizer";

    public string? Version { get; set; }

    public bool CaseSensitive { get; set; } = false;

#nullable disable
    public TokenPatterns TokenPatterns { get; set; }
#nullable restore
}