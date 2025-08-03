using Corney.Features.Processes;
using Xunit;

namespace Corney.Tests.CmdParseing;

public class SimpleTest
{
    [Fact]
    public void Tokenize2_SimpleTest1()
    {
        // Arrange
        var cmd = @"C:\work\temp\runner\Run1.exe -p";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal(@"C:\work\temp\runner\Run1.exe", result.Program);
        Assert.Equal("-p", result.Arguments);
    }

    [Fact]
    public void Tokenize2_SimpleTest2()
    {
        // Arrange
        var cmd = @"C:\work\temp\runner\Run1.exe";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal(@"C:\work\temp\runner\Run1.exe", result.Program);
        Assert.Equal("", result.Arguments);
    }
}