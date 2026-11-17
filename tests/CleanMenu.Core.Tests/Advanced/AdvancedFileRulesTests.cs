using CleanMenu.Core.Advanced;

namespace CleanMenu.Core.Tests.Advanced;

/// <summary>
/// Regression tests from a real incident: "Duplicados" over the user profile sent program and
/// project files to the Recycle Bin (VS Code, Epic, WhatsApp, Android SDK, node_modules, .venv).
/// </summary>
public class AdvancedFileRulesTests
{
    [Theory]
    [InlineData(@"C:\Users\u\AppData\Local\Programs\Microsoft VS Code\69047191fc\resources\app\extensions\copilot\dist\tree-sitter-cpp.wasm")]
    [InlineData(@"C:\Users\u\AppData\Local\Epic Games\EOSOverlay\x.png")]
    [InlineData(@"C:\Users\u\AppData\Local\Packages\5319275A.WhatsAppDesktop_cv1g1gvanyjgm\LocalCache\a.jpg")]
    [InlineData(@"C:\Users\u\Projetos\app\node_modules\next\dist\logo.png")]
    [InlineData(@"C:\Users\u\Projetos\corte-viral\.venv\Lib\site-packages\x\icon.png")]
    [InlineData(@"C:\Users\u\Projetos\hot-collection\mobile\android\app\src\main\res\icon.png")]
    [InlineData(@"C:\Users\u\.gradle\caches\9.3.1\transforms\a.jpg")]
    [InlineData(@"C:\Users\u\AppData\Roaming\npm\node_modules\pkg\readme.txt")]
    [InlineData(@"D:\SteamLibrary\steamapps\common\Game\movie.mp4")]
    public void ProgramAndProjectFiles_AreNeverOffered(string path)
    {
        Assert.True(AdvancedFileRules.IsInProgramArea(path));
        Assert.False(AdvancedFileRules.CanOfferDuplicate(path));
        Assert.False(AdvancedFileRules.CanOfferLargeFile(path));
        Assert.False(AdvancedFileRules.CanRecycle(path));
    }

    [Theory]
    [InlineData(@"C:\Users\u\Videos\viagem.mp4")]
    [InlineData(@"C:\Users\u\Pictures\Férias\IMG_001.jpg")]
    [InlineData(@"C:\Users\u\Documents\contrato (1).pdf")]
    [InlineData(@"C:\Users\u\Music\album\faixa.mp3")]
    public void UserContent_CanBeOffered(string path)
    {
        Assert.True(AdvancedFileRules.CanOfferDuplicate(path));
        Assert.True(AdvancedFileRules.CanOfferLargeFile(path));
    }

    [Theory]
    [InlineData(@"C:\Users\u\Documents\tool\app.exe")]
    [InlineData(@"C:\Users\u\Documents\lib\core.dll")]
    [InlineData(@"C:\Users\u\Documents\x\addon.node")]
    [InlineData(@"C:\Users\u\Documents\x\script.js")]
    [InlineData(@"C:\Users\u\Documents\x\data.json")]
    public void ProgramFiles_AreNeverOffered_EvenInUserFolders(string path)
    {
        Assert.False(AdvancedFileRules.CanOfferDuplicate(path));
        Assert.False(AdvancedFileRules.CanOfferLargeFile(path));
    }
}
