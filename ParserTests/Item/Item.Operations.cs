namespace ParserTests.Item;

public partial class Item
{
    //we define a custom operator for the type to simplify the evaluateoperator example later
    //this is not 100% needed, but it keeps the code in the CustomTypeParser simpler
    public static Item operator +(int v1, Item v2) =>
        new() { Name = v2.Name, Value = v2.Value + v1 };

    public static Item operator +(Item v2, int v1) =>
        new() { Name = v2.Name, Value = v2.Value + v1 };

    public static double operator +(Item v2, double v1) =>
        v2.Value + v1;

    public static Item operator +(Item v1, Item v2) =>
        new() { Name = $"{v1.Name} {v2.Name}", Value = v2.Value + v1.Value };

    public override string ToString() => $"{Name} {Value}";


}
