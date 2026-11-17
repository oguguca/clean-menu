using CleanMenu.Core.SystemInfo;

namespace CleanMenu.Core.Tests.SystemInfo;

public class DeviceStatusTests
{
    [Theory]
    [InlineData(397568L, true, true, "Ligado e atualizado")]       // 0x061100: Defender on, up to date
    [InlineData(397584L, true, false, "Ligado, mas DESATUALIZADO")] // 0x061110
    [InlineData(393472L, false, true, "Desligado")]                 // 0x060100
    public void Antivirus_DecodesProductState(long state, bool enabled, bool upToDate, string text)
    {
        var product = AntivirusProduct.FromState("Defender", state);

        Assert.Equal(enabled, product.Enabled);
        Assert.Equal(upToDate, product.UpToDate);
        Assert.Equal(text, product.StatusText);
        Assert.Equal(!(enabled && upToDate), product.NeedsAttention);
    }

    [Fact]
    public void Antivirus_MissingState_IsNotGuessed()
    {
        var product = AntivirusProduct.FromState("X", null);

        Assert.Null(product.Enabled);
        Assert.Equal("Estado não informado", product.StatusText);
        Assert.False(product.NeedsAttention);
    }

    [Theory]
    [InlineData(true, ServiceStartMode.Disabled, false, true)]   // security service disabled
    [InlineData(false, ServiceStartMode.Manual, true, false)]    // stopped manual service is normal
    [InlineData(false, ServiceStartMode.Automatic, false, true)] // automatic but stopped
    [InlineData(true, ServiceStartMode.Automatic, true, false)]
    public void Service_FlagsOnlyClearProblems(bool security, ServiceStartMode mode, bool running, bool attention)
    {
        var service = new ServiceStatusInfo("x", "X", "x", running, mode, security);

        Assert.Equal(attention, service.NeedsAttention);
    }

    [Theory]
    [InlineData("DiskDrive", "Armazenamento (pen drive, HD externo, celular)")]
    [InlineData("Mouse", "Mouse")]
    [InlineData(null, "Outro")]
    [InlineData("Estranho", "Estranho")]
    public void DeviceKind_IsPlain(string? pnpClass, string expected)
    {
        Assert.Equal(expected, ImportantServices.DeviceKind(pnpClass));
    }

    [Fact]
    public void StartMode_Parses()
    {
        Assert.Equal(ServiceStartMode.Automatic, ImportantServices.ParseStartMode("Auto"));
        Assert.Equal(ServiceStartMode.Disabled, ImportantServices.ParseStartMode("Disabled"));
        Assert.Equal(ServiceStartMode.Unknown, ImportantServices.ParseStartMode(null));
    }
}

public class VersionTextTests
{
    [Theory]
    [InlineData("v0.2.0", "0.1.0", true)]
    [InlineData("0.1.0", "0.1.0", false)]
    [InlineData("v0.1", "0.1.0", false)]
    [InlineData("v1.0.0-beta", "0.9.9", true)]
    [InlineData("nada", "0.1.0", false)]
    [InlineData(null, "0.1.0", false)]
    public void IsNewer(string? latest, string current, bool expected)
    {
        Assert.Equal(expected, CleanMenu.Core.Settings.VersionText.IsNewer(latest, current));
    }
}
