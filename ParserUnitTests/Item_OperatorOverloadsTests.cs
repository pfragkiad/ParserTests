using ParserTests.Common;
using Xunit;

namespace ParserUnitTests;

public class Item_OperatorOverloadsTests
{
    [Fact]
    public void Item_Add_Double_Rounds()
    {
        var item = new Item { Name = "X", Value = 10 };
        var r = item + 5.7;           // 5.7 -> 6
        Assert.Equal(16, r.Value);
        Assert.Equal("X", r.Name);

        var r2 = 3.2 + item;          // 3.2 -> 3
        Assert.Equal(13, r2.Value);
        Assert.Equal("X", r2.Name);
    }

    [Fact]
    public void Item_Multiply_Double_Rounds()
    {
        var item = new Item { Name = "Y", Value = 10 };
        var r = item * 2.8;           // 2.8 -> 3
        Assert.Equal(30, r.Value);
        Assert.Equal("Y", r.Name);

        var r2 = 1.9 * item;          // 1.9 -> 2
        Assert.Equal(20, r2.Value);
        Assert.Equal("Y", r2.Name);
    }
}