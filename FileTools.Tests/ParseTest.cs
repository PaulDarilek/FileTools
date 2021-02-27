using System.IO;
using Xunit;
using FileTools.Parsing;

namespace FileTools.Tests
{
    public class ParseTest
    {
        private readonly FileAttributes[] _options = new FileAttributes[] { FileAttributes.ReadOnly, FileAttributes.Archive, FileAttributes.Hidden, FileAttributes.System };

        [Fact]
        public void FirstByName_Test()
        {
            string name;
            FileAttributes? expected;
            FileAttributes? actual;

            expected = FileAttributes.ReadOnly;

            name = expected.ToString();
            actual = _options.FindFirstByName(name);
            Assert.Equal(expected, actual);

            name = expected.ToString().ToLower();
            actual = _options.FindFirstByName(name);
            Assert.Equal(expected, actual);

            name = "Read";
            actual = _options.FindFirstByName(name);
            Assert.Null(actual);

            // Null Tests.
            name = FileAttributes.ReparsePoint.ToString();
            actual = _options.FindFirstByName(name);
            Assert.Null(actual);

            name = " ";
            actual = _options.FindFirstByName(name);
            Assert.Null(actual);
        }

        [Fact]
        public void FirstByNameStart_Test()
        {
            string name;
            FileAttributes? expected;
            FileAttributes? actual;

            expected = FileAttributes.ReadOnly;

            name = "R";
            actual = _options.FindFirstByNameStartsWith(name);
            Assert.Equal(expected, actual);

            name = "Read";
            actual = _options.FindFirstByNameStartsWith(name);
            Assert.Equal(expected, actual);

            name = "r";
            actual = _options.FindFirstByNameStartsWith(name);
            Assert.Equal(expected, actual);

            name = "re";
            actual = _options.FindFirstByNameStartsWith(name);
            Assert.Equal(expected, actual);

            // Null Tests.
            name = "RO";
            actual = _options.FindFirstByNameStartsWith(name);
            Assert.Null(actual);

            name = " ";
            actual = _options.FindFirstByNameStartsWith(name);
            Assert.Null(actual);
        }

        [Fact]
        public void FirstByCapitalizedInitials_Test()
        {
            string name;
            FileAttributes? expected = FileAttributes.ReadOnly;
            FileAttributes? actual;

            name = CommandParser.GetCapitalLetters(expected.ToString());
            Assert.Equal("RO", name);
            actual = _options.FindFirstByInitials(name);
            Assert.Equal(expected, actual);

            name = name.ToLower();
            actual = _options.FindFirstByInitials(name);
            Assert.Equal(expected, actual);

            // Null Tests.
            name = CommandParser.GetCapitalLetters(FileAttributes.ReparsePoint.ToString());
            Assert.Equal("RP", name);
            actual = _options.FindFirstByInitials(name);
            Assert.Null(actual);

            name = " ";
            actual = _options.FindFirstByInitials(name);
            Assert.Null(actual);
        }

    }
}
