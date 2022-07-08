using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserTests.Expressions;

public class Node<T> : NodeBase
{
    public Node(T? value) : base(value.ToString())
    {
        Value = value;
    }

    public Node():base("") { }

    protected T? _value;
    public T? Value
    {
        get => _value;
        set
        {
            _value = value;
            base.Text = _value?.ToString() ??"";
        }
    }

    //public Node<T>? Left { get; set; }
    //public Node<T>? Right { get; set; }
}

