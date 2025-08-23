using Microsoft.Extensions.DependencyInjection;
using ParserLibrary;
using ParserLibrary.ExpressionTree;
using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;

using ParserUnitTests.Parsers;
using Xunit;

namespace ParserUnitTests;

public class TreeOptimizerTests
{
    [Fact]
    public void TestTreeOptimizationWithMixedTypes()
    {
        // Arrange
        var app = App.GetParserApp<ItemParser>();
        IParser parser = app.Services.GetRequiredParser();
        
        string expression = "a + 5 + b + 10.5 + c"; // Mixed types: Item + int + Item + double + Item
        
        // Define variable types for optimization
        var variableTypes = new Dictionary<string, Type>
        {
            { "a", typeof(Item) },
            { "b", typeof(Item) },
            { "c", typeof(Item) }
        };
        
        // Act - Get the optimized expression tree
        var optimizedTree = parser.GetOptimizedExpressionTree(expression, variableTypes);
        
        // Assert - The tree should be restructured to group similar types
        Assert.NotNull(optimizedTree);
        Assert.NotNull(optimizedTree.Root);
        
        //// Print the tree structure for debugging
        //Console.WriteLine("Optimized Tree Structure:");
        //optimizedTree.Print();
        
        // Verify the tree can still be evaluated correctly
        var variables = new Dictionary<string, object?>
        {
            { "a", new Item { Name = "ItemA", Value = 1 } },
            { "b", new Item { Name = "ItemB", Value = 2 } },
            { "c", new Item { Name = "ItemC", Value = 3 } }
        };
        
        var result = parser.Evaluate(expression, variables);
        Assert.NotNull(result);
    }

    [Fact]
    public void TestTreeOptimizationWithCommutativeOperations()
    {
        string expression = "x * 2.0 * y * 3"; // Multiplication with mixed types
        IParser parser = App.GetDefaultParser()!;

        var variableTypes = new Dictionary<string, Type>
        {
            { "x", typeof(double) },
            { "y", typeof(int) }
        };
        
        // Act
        var originalTree = parser.GetExpressionTree(expression);
        var optimizedTree = parser.GetOptimizedExpressionTree(expression, variableTypes);
        
        // Assert
        Assert.NotNull(originalTree);
        Assert.NotNull(optimizedTree);
        
        // Both trees should evaluate to the same result
        var variables = new Dictionary<string, object?>
        {
            { "x", 4.0 },
            { "y", 5 }
        };
        
        var originalResult = parser.Evaluate(expression, variables);
        
        // Verify optimization doesn't change the result
        Assert.Equal(120.0, originalResult); // 4.0 * 2.0 * 5 * 3 = 120.0
    }

    [Fact]
    public void TestTreeOptimizerWithItemTypes()
    {
        // Arrange
        var app = App.GetParserApp<ItemParser>();
        IParser parser = app.Services.GetRequiredParser();
        
        string expression = "item1 + 10 + item2 + 5 + item3"; // Items mixed with integers
        
        var variableTypes = new Dictionary<string, Type>
        {
            { "item1", typeof(Item) },
            { "item2", typeof(Item) },
            { "item3", typeof(Item) }
        };
        
        // Act
        var optimizedTree = parser.GetOptimizedExpressionTree(expression, variableTypes);
        
        // Assert
        Assert.NotNull(optimizedTree);
        
        // Test evaluation with actual Item objects
        var variables = new Dictionary<string, object?>
        {
            { "item1", new Item { Name = "First", Value = 100 } },
            { "item2", new Item { Name = "Second", Value = 200 } },
            { "item3", new Item { Name = "Third", Value = 300 } }
        };
        
        var result = parser.Evaluate(expression, variables);
        Assert.NotNull(result);
        
        // The result should be an Item with combined values
        if (result is Item resultItem)
        {
            Assert.Contains("First", resultItem.Name);
            Assert.Equal(615, resultItem.Value); // 100 + 10 + 200 + 5 + 300
        }
    }

    [Fact]
    public void TestTreeOptimizerDirectUsage()
    {
        // Arrange
        IParser parser = App.GetDefaultParser()!;

        string expression = "a + b + 1.0 + c + 2";
        var originalTree = parser.GetExpressionTree(expression);
        
        var variableTypes = new Dictionary<string, Type>
        {
            { "a", typeof(double) },
            { "b", typeof(double) },
            { "c", typeof(int) }
        };
        
        // Act - Use TreeOptimizer directly
        var optimizer = new TreeOptimizer<Token>();
        var optimizedTree = optimizer.OptimizeForDataTypes(originalTree, variableTypes);
        
        // Assert
        Assert.NotNull(optimizedTree);
        Assert.NotEqual(originalTree, optimizedTree); // Should be a different instance
        
        // Verify both trees have the same structure in terms of node count
        Assert.Equal(originalTree.Count, optimizedTree.Count);
        
        //Console.WriteLine("Original Tree:");
        //originalTree.Print2();
        
        //Console.WriteLine("\nOptimized Tree:");
        //optimizedTree.Print2();
    }

    [Fact]
    public void TestTreeOptimizerWithNonCommutativeOperations()
    {
        // Arrange
        IParser parser = App.GetDefaultParser()!;

        string expression = "a - b + c"; // Subtraction is not commutative
        
        var variableTypes = new Dictionary<string, Type>
        {
            { "a", typeof(int) },
            { "b", typeof(int) },
            { "c", typeof(double) }
        };
        
        // Act
        var originalTree = parser.GetExpressionTree(expression);
        var optimizedTree = parser.GetOptimizedExpressionTree(expression, variableTypes);
        
        // Assert
        Assert.NotNull(optimizedTree);
        
        // Verify evaluation works correctly
        var variables = new Dictionary<string, object?>
        {
            { "a", 10 },
            { "b", 3 },
            { "c", 2.5 }
        };
        
        var result = parser.Evaluate(expression, variables);
        Assert.Equal(9.5, result); // 10 - 3 + 2.5 = 9.5
    }
}