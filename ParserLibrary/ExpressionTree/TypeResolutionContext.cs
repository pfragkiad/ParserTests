namespace ParserLibrary.ExpressionTree;


    internal sealed class TypeResolutionContext
    {
        public readonly Dictionary<string, Type> VariableTypes;
        public readonly Dictionary<string, Type> FunctionReturnTypes;
        public readonly Dictionary<string, Func<Type?[], Type?>> AmbiguousFunctionReturnTypes;
        public readonly Dictionary<Node<Token>, Type?> TypeCache = new();
        public readonly string ArgumentSeparator;

        public TypeResolutionContext(
            Dictionary<string, Type> variableTypes,
            Dictionary<string, Type> functionReturnTypes,
            Dictionary<string, Func<Type?[], Type?>> ambiguous,
            string argumentSeparator)
        {
            VariableTypes = variableTypes;
            FunctionReturnTypes = functionReturnTypes;
            AmbiguousFunctionReturnTypes = ambiguous;
            ArgumentSeparator = argumentSeparator;
        }
    }