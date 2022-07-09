using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserTests;

public record TokensFunctions(
    List<Token> OrderTokens,
    Dictionary<Token, int> FunctionsArgumentsCount);

