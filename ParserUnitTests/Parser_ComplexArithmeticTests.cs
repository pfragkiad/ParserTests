using System;
using System.Numerics;
using ParserLibrary;
using Xunit;

namespace ParserUnitTests;

public class Parser_ComplexArithmeticTests
{
    [Fact]
    public void Complex_Division_Equivalent_WithOrWithoutVariable()
    {
        var parser = ParserApp.GetComplexParser();

        var direct = (Complex)parser.Evaluate("(1+3*i)/(2-3*i)")!;
        var withVar = (Complex)parser.Evaluate("(1+3*i)/b", new()
        {
            { "b", new Complex(2, -3) }
        })!;

        Assert.Equal(direct.Real, withVar.Real, 12);
        Assert.Equal(direct.Imaginary, withVar.Imaginary, 12);

        // Known expected value from example (-0.538461538..., 0.692307692...)
        Assert.Equal(-0.5384615384615385, direct.Real, 12);
        Assert.Equal( 0.6923076923076924, direct.Imaginary, 12);
    }

    [Fact]
    public void Complex_Expression_With_Functions_EulerIdentity_Rounded()
    {
        var parser = ParserApp.GetComplexParser();
        var euler = (Complex)parser.Evaluate("round(exp(i*pi),8)")!;
        // (-1, 0) within rounding precision
        Assert.Equal(-1d, euler.Real, 12);
        Assert.Equal(0d, Math.Round(euler.Imaginary, 12));
    }
}