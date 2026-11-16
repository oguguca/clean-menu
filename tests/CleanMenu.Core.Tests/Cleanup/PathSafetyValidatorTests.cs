using CleanMenu.Core.Cleanup;

namespace CleanMenu.Core.Tests.Cleanup;

public class PathSafetyValidatorTests
{
    private const string Temp = @"C:\Users\ana\AppData\Local\Temp";
    private const string ChromeCache = @"C:\Users\ana\AppData\Local\Google\Chrome\User Data\Default\Cache";

    private static readonly PathSafetyValidator Validator = new(
    [
        @"C:\Windows",
        @"C:\Program Files",
        @"C:\Users\ana\Documents",
        @"C:\Users\ana\Desktop",
        @"C:\Users\ana\Pictures",
        @"C:\Users\ana\Downloads",
    ]);

    [Theory]
    [InlineData(Temp + @"\setup.tmp")]
    [InlineData(Temp + @"\sub\pasta\log.txt")]
    [InlineData(ChromeCache + @"\Cache_Data\f_000123")]
    public void AcceptsFilesInsideRoot(string path)
    {
        Assert.Equal(PathRejection.None, Validator.Validate(path, path.StartsWith(Temp, StringComparison.Ordinal) ? Temp : ChromeCache));
    }

    [Theory]
    [InlineData(@"C:\Users\ana\Documents\contrato.docx", PathRejection.OutsideAllowedRoot)]
    [InlineData(@"C:\Users\ana\AppData\Local\TempOutra\x.tmp", PathRejection.OutsideAllowedRoot)] // prefix trick
    [InlineData(Temp + @"\..\..\..\Documents\contrato.docx", PathRejection.OutsideAllowedRoot)]   // traversal
    [InlineData(Temp, PathRejection.IsTheRootItself)]
    [InlineData(Temp + @"\", PathRejection.IsTheRootItself)]
    [InlineData(@"temp\x.tmp", PathRejection.NotFullyQualified)]
    [InlineData(@"C:temp\x.tmp", PathRejection.NotFullyQualified)]
    [InlineData(@"\\servidor\share\x.tmp", PathRejection.DeviceOrNetworkPath)]
    [InlineData(@"\\?\C:\Users\ana\AppData\Local\Temp\x.tmp", PathRejection.DeviceOrNetworkPath)]
    [InlineData(@"\\.\PhysicalDrive0", PathRejection.DeviceOrNetworkPath)]
    [InlineData("", PathRejection.Empty)]
    [InlineData("   ", PathRejection.Empty)]
    public void RefusesUnsafePaths(string path, PathRejection expected)
    {
        Assert.Equal(expected, Validator.Validate(path, Temp));
    }

    [Theory]
    [InlineData(@"Network\Cookies")]
    [InlineData(@"Login Data")]
    [InlineData(@"Web Data")]
    [InlineData(@"Local Storage\leveldb\000003.log")]
    [InlineData(@"IndexedDB\https_site.indexeddb.leveldb\000005.ldb")]
    [InlineData(@"logins.json")]
    [InlineData(@"key4.db")]
    [InlineData(@"cookies.sqlite")]
    [InlineData(@"cofre.kdbx")]
    [InlineData(@"certificado.pfx")]
    public void RefusesCredentialAndSessionFiles_EvenInsideAllowedRoot(string relative)
    {
        Assert.Equal(PathRejection.ProtectedName, Validator.Validate(Path.Combine(ChromeCache, relative), ChromeCache));
    }

    [Fact]
    public void CaseDoesNotMatter_ForRootCheck()
    {
        Assert.Equal(PathRejection.None, Validator.Validate(@"c:\users\ANA\appdata\local\temp\X.TMP", Temp));
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Users")]
    [InlineData(@"D:\")]
    public void RefusesRootsThatAreTooBroad(string root)
    {
        Assert.Equal(PathRejection.UnsafeRoot, Validator.ValidateRoot(root));
    }

    [Fact]
    public void RefusesWholeUserProfileAsRoot()
    {
        // Refused because it contains protected personal folders.
        Assert.NotEqual(PathRejection.None, Validator.ValidateRoot(@"C:\Users\ana"));
    }

    [Theory]
    [InlineData(@"C:\Windows\Temp")]                 // inside protected (needs dedicated admin flow)
    [InlineData(@"C:\Users\ana\Documents\Temp")]
    [InlineData(@"C:\Program Files\App\cache")]
    public void RefusesRootsInsideProtectedFolders(string root)
    {
        Assert.Equal(PathRejection.ProtectedLocation, Validator.ValidateRoot(root));
    }

    [Fact]
    public void RefusesRootThatContainsProtectedFolder()
    {
        var validator = new PathSafetyValidator([@"C:\Users\ana\AppData\Local\Temp\importante"]);

        Assert.Equal(PathRejection.ProtectedLocation, validator.ValidateRoot(Temp));
    }

    [Fact]
    public void FileInsideAllowedRoot_ButInsideProtectedFolder_IsRefused()
    {
        var validator = new PathSafetyValidator([Temp + @"\guardar"]);

        Assert.Equal(PathRejection.UnsafeRoot, validator.Validate(Temp + @"\guardar\x.tmp", Temp));
    }

    [Fact]
    public void FileInUnsafeRoot_IsRefused()
    {
        Assert.Equal(PathRejection.UnsafeRoot, Validator.Validate(@"C:\Users\ana\foto.jpg", @"C:\Users\ana"));
    }

    [Fact]
    public void EveryRejection_HasPlainDescription()
    {
        foreach (var rejection in Enum.GetValues<PathRejection>())
        {
            Assert.False(string.IsNullOrWhiteSpace(PathSafetyValidator.Describe(rejection)));
        }
    }
}
