using OneOf;

namespace ParserTests.Item;

public partial class Item
{
    //we define a custom operator for the type to simplify the evaluateoperator example later
    //this is not 100% needed, but it keeps the code in the CustomTypeParser simpler

    //check return as Item
    public static Item operator +(int v1, Item v2) =>
        new() { Name = v2.Name, Value = v2.Value + v1 };

    public static Item operator +(Item v2, int v1) =>
        new() { Name = v2.Name, Value = v2.Value + v1 };


    //check return as double
    public static double operator +(Item v2, double v1) =>
        v2.Value + v1;

    public static double operator +(double v1, Item v2) =>
        v2.Value + v1;

    public static Item operator +(Item v1, Item v2) =>
        new() { Name = $"{v1.Name} {v2.Name}", Value = v2.Value + v1.Value };

    
    //public static OneOf<Item,double> Add(OneOf<Item,double> v1, OneOf<Item,double> v2) =>
    //    v1.Match(
    //        item1 => v2.Match(
    //            item2 => item1 + item2,
    //            dbl => item1 + dbl),
    //        dbl1 => v2.Match(
    //            item2 => dbl1 + item2,
    //            dbl2 => dbl1 + dbl2));


    public override string ToString() => $"{Name} {Value}";


}
