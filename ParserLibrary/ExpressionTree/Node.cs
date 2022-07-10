namespace ParserLibrary.ExpressionTree;

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
}


