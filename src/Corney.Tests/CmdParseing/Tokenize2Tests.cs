using System;
using Corney.Features.Processes.Services;
using Xunit;

namespace Corney.Tests.CmdParseing;

public class Tokenize2Tests
{
    [Fact]
    public void Tokenize2_ProgramOnly_ReturnsCorrectProgram()
    {
        // Arrange
        var cmd = "aaa";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal("aaa", result.Program);
        Assert.Equal("", result.Arguments);
    }

    [Fact]
    public void Tokenize2_ProgramWithSingleArgument_SplitsCorrectly()
    {
        // Arrange
        var cmd = "aaa bbb";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal("aaa", result.Program);
        Assert.Equal("bbb", result.Arguments);
    }

    [Fact]
    public void Tokenize2_ProgramWithMultipleArguments_PreservesAllArguments()
    {
        // Arrange
        var cmd = "aaa bbb ccc ddd";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal("aaa", result.Program);
        Assert.Equal("bbb ccc ddd", result.Arguments);
    }

    [Fact]
    public void Tokenize2_QuotedProgramOnly_RemovesQuotesFromProgram()
    {
        // Arrange
        var cmd = @"""C:\Program Files\app.exe""";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal(@"C:\Program Files\app.exe", result.Program);
        Assert.Equal("", result.Arguments);
    }

    [Fact]
    public void Tokenize2_QuotedProgramWithArguments_SplitsCorrectly()
    {
        // Arrange
        var cmd = @"""C:\Program Files\app.exe"" -flag";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal(@"C:\Program Files\app.exe", result.Program);
        Assert.Equal("-flag", result.Arguments);
    }

    [Fact]
    public void Tokenize2_QuotedProgramWithMultipleArguments_PreservesAllArguments()
    {
        // Arrange
        var cmd = @"""C:\Program Files\app.exe"" -flag value --verbose";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal(@"C:\Program Files\app.exe", result.Program);
        Assert.Equal("-flag value --verbose", result.Arguments);
    }

    [Fact]
    public void Tokenize2_WindowsPathWithExtension_WorksCorrectly()
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
    public void Tokenize2_RealWorldExample_Git_WorksCorrectly()
    {
        // Arrange
        var cmd = "git.exe status --porcelain";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal("git.exe", result.Program);
        Assert.Equal("status --porcelain", result.Arguments);
    }

    [Fact]
    public void Tokenize2_ExtraSpacesBetweenProgramAndArguments_HandlesCorrectly()
    {
        // Arrange
        var cmd = "program.exe     --flag value";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal("program.exe", result.Program);
        Assert.Equal("--flag value", result.Arguments);
    }

    [Fact]
    public void Tokenize2_QuotedProgramWithExtraSpaces_HandlesCorrectly()
    {
        // Arrange
        var cmd = @"""C:\Program Files\app.exe""     -flag";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal(@"C:\Program Files\app.exe", result.Program);
        Assert.Equal("-flag", result.Arguments);
    }

    [Fact]
    public void Tokenize2_ArgumentsWithQuotedValues_PreservesQuotes()
    {
        // Arrange
        var cmd = @"myapp.exe --input ""C:\path with spaces\file.txt""";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal("myapp.exe", result.Program);
        Assert.Equal(@"--input ""C:\path with spaces\file.txt""", result.Arguments);
    }

    [Fact]
    public void Tokenize2_UnixStylePath_WorksCorrectly()
    {
        // Arrange
        var cmd = "/usr/bin/python3 script.py --verbose";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal("/usr/bin/python3", result.Program);
        Assert.Equal("script.py --verbose", result.Arguments);
    }

    [Fact]
    public void Tokenize2_RelativePath_WorksCorrectly()
    {
        // Arrange
        var cmd = @".\bin\debug\myapp.exe --config production";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal(@".\bin\debug\myapp.exe", result.Program);
        Assert.Equal("--config production", result.Arguments);
    }

    [Fact]
    public void Tokenize2_JustExecutableName_WorksCorrectly()
    {
        // Arrange
        var cmd = "notepad";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal("notepad", result.Program);
        Assert.Equal("", result.Arguments);
    }

    [Fact]
    public void Tokenize2_ComplexArguments_PreservesStructure()
    {
        // Arrange
        var cmd = @"msbuild.exe MySolution.sln /p:Configuration=Release /p:Platform=""Any CPU"" /v:minimal";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal("msbuild.exe", result.Program);
        Assert.Equal(@"MySolution.sln /p:Configuration=Release /p:Platform=""Any CPU"" /v:minimal",
            result.Arguments);
    }


    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Tokenize2_EmptyOrWhitespaceInput_ThrowsArgumentException(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Pharse.Tokenize2(input));
    }

    [Fact]
    public void Tokenize2_NullInput_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Pharse.Tokenize2(null));
    }

    [Fact]
    public void Tokenize2_UnmatchedQuotes_ThrowsArgumentException()
    {
        // Arrange
        var cmd = @"""C:\Program Files\app.exe -flag";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Pharse.Tokenize2(cmd));
        Assert.Equal("Missing closing quote\r\nParameter name: cmd", exception.Message);
    }


    [Fact]
    public void Tokenize2_EmptyQuotes_ThrowsArgumentException()
    {
        // Arrange
        var cmd = @""""" -flag";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Pharse.Tokenize2(cmd));
    }

    [Fact]
    public void Tokenize2_LongPathWithSpaces_WorksCorrectly()
    {
        // Arrange
        var cmd =
            @"""C:\Program Files (x86)\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"" project.sln /p:Configuration=Debug";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal(
            @"C:\Program Files (x86)\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            result.Program);
        Assert.Equal("project.sln /p:Configuration=Debug", result.Arguments);
    }

    [Fact]
    public void Tokenize2_SingleCharacterArgument_WorksCorrectly()
    {
        // Arrange
        var cmd = "app.exe a";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal("app.exe", result.Program);
        Assert.Equal("a", result.Arguments);
    }

    [Fact]
    public void Tokenize2_ArgumentsWithEqualSigns_PreservesStructure()
    {
        // Arrange
        var cmd = "myapp.exe --key=value --another-key=another-value";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal("myapp.exe", result.Program);
        Assert.Equal("--key=value --another-key=another-value", result.Arguments);
    }

    [Fact]
    public void Tokenize2_ArgumentsWithSpecialCharacters_PreservesCharacters()
    {
        // Arrange
        var cmd = @"app.exe --regex=""[a-zA-Z]+\d{2,4}"" --path=C:\temp\file.txt";

        // Act
        var result = Pharse.Tokenize2(cmd);

        // Assert
        Assert.Equal("app.exe", result.Program);
        Assert.Equal(@"--regex=""[a-zA-Z]+\d{2,4}"" --path=C:\temp\file.txt", result.Arguments);
    }
}