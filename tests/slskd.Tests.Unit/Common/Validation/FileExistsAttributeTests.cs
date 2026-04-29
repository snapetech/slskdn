// <copyright file="FileExistsAttributeTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Common.Validation;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using slskd.Validation;
using Xunit;

public class FileExistsAttributeTests
{
    private sealed class TestOptions
    {
        [FileExists]
        public string File { get; init; } = string.Empty;
    }

    private sealed class MustNotExistOptions
    {
        [FileDoesNotExist]
        public string File { get; init; } = string.Empty;
    }

    private sealed class DirectoryOptions
    {
        [DirectoryExists]
        public string Directory { get; init; } = string.Empty;
    }

    private sealed class RelativeDirectoryOptions
    {
        [DirectoryExists(relativeToApplicationDirectory: true)]
        public string Directory { get; init; } = string.Empty;
    }

    [Fact]
    public void Empty_string_is_treated_as_unset()
    {
        var options = new TestOptions { File = string.Empty };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void Whitespace_string_is_treated_as_unset()
    {
        var options = new TestOptions { File = "   " };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void Missing_file_does_not_echo_absolute_path()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "secret-file.txt");
        var options = new TestOptions { File = path };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        Assert.False(isValid);
        var error = Assert.Single(results).ErrorMessage;
        Assert.Equal("The File field specifies a non-existent file.", error);
        Assert.DoesNotContain(path, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Existing_file_does_not_echo_absolute_path_for_file_does_not_exist_attribute()
    {
        var path = Path.GetTempFileName();
        try
        {
            var options = new MustNotExistOptions { File = path };
            var results = new List<ValidationResult>();

            var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

            Assert.False(isValid);
            var error = Assert.Single(results).ErrorMessage;
            Assert.Equal("The File field specifies an existing file.", error);
            Assert.DoesNotContain(path, error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Missing_directory_does_not_echo_absolute_path()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var options = new DirectoryOptions { Directory = path };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        Assert.False(isValid);
        var error = Assert.Single(results).ErrorMessage;
        Assert.Equal("The Directory field specifies a non-existent directory.", error);
        Assert.DoesNotContain(path, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Relative_directory_validation_does_not_echo_raw_input_path()
    {
        var options = new RelativeDirectoryOptions { Directory = "/very/secret/path" };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        Assert.False(isValid);
        var error = Assert.Single(results).ErrorMessage;
        Assert.Equal("The Directory field specifies a non-relative directory path.", error);
        Assert.DoesNotContain("/very/secret/path", error, StringComparison.OrdinalIgnoreCase);
    }
}
