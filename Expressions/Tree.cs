using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserTests.Expressions;

public class Tree<T>
{
    public Node<T>? Root { get; set; }
    public Dictionary<Token, Node<Token>> NodeDictionary { get; internal set; }
}

