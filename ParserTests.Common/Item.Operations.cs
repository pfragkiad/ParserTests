
namespace ParserTests.Common;

public partial class Item
{
    //we define a custom operator for the type to simplify the evaluateoperator example later
    //this is not 100% needed, but it keeps the code in the CustomTypeParser simpler
    public static Item operator +(int v1, Item v2) =>
        new() { Name = v2.Name, Value = v2.Value + v1 };
    public static Item operator +(Item v2, int v1) =>
        new() { Name = v2.Name, Value = v2.Value + v1 };
    public static Item operator *(Item v2, int v1) =>
        new() { Name = v2.Name, Value = v2.Value * v1 };

    public static Item operator +(Item v1, Item v2) =>
        new() { Name = $"{v1.Name} {v2.Name}", Value = v2.Value + v1.Value };

    // Addition operators for double (rounded to zero decimals and converted to int)
    public static Item operator +(double v1, Item v2) =>
        new() { Name = v2.Name, Value = v2.Value + (int)Math.Round(v1, 0) };

    public static Item operator +(Item v2, double v1) =>
        new() { Name = v2.Name, Value = v2.Value + (int)Math.Round(v1, 0) };

    // Multiplication operators for double (rounded to zero decimals and converted to int)
    public static Item operator *(double v1, Item v2) =>
        new() { Name = v2.Name, Value = v2.Value * (int)Math.Round(v1, 0) };

    public static Item operator *(Item v2, double v1) =>
        new() { Name = v2.Name, Value = v2.Value * (int)Math.Round(v1, 0) };


    public override string ToString() => $"{Name} {Value}";


}
