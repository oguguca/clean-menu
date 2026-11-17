namespace CleanMenu.Core.SystemInfo;

/// <summary>An antivirus registered in the Windows Security Center.</summary>
/// <param name="Enabled">Null when Windows did not report it.</param>
/// <param name="UpToDate">Null when Windows did not report it.</param>
public sealed record AntivirusProduct(string Name, bool? Enabled, bool? UpToDate)
{
    /// <summary>
    /// Decodes the Security Center "productState" value. Bit 12 = real-time protection on;
    /// low byte 0x00 = definitions up to date, 0x10 = out of date.
    /// </summary>
    public static AntivirusProduct FromState(string name, long? productState)
    {
        if (productState is not { } state || state < 0)
        {
            return new AntivirusProduct(name, null, null);
        }

        var enabled = (state & 0x1000) != 0;
        var low = state & 0xFF;
        bool? upToDate = low == 0x00 ? true : low == 0x10 ? false : null;
        return new AntivirusProduct(name, enabled, upToDate);
    }

    public string StatusText => (Enabled, UpToDate) switch
    {
        (true, true) => "Ligado e atualizado",
        (true, false) => "Ligado, mas DESATUALIZADO",
        (true, null) => "Ligado",
        (false, _) => "Desligado",
        _ => "Estado não informado",
    };

    public bool NeedsAttention => Enabled == false || UpToDate == false;
}

/// <summary>Start mode of a Windows service.</summary>
public enum ServiceStartMode
{
    Unknown,
    Automatic,
    Manual,
    Disabled,
}

/// <summary>Current state of an important Windows service. Read-only: Clean Menu never changes services.</summary>
public sealed record ServiceStatusInfo(string Name, string Title, string Explanation, bool IsRunning, ServiceStartMode StartMode, bool IsSecurity)
{
    public string StatusText => StartMode switch
    {
        ServiceStartMode.Disabled => "Desativado",
        _ when IsRunning => StartMode == ServiceStartMode.Automatic ? "Rodando · inicia sozinho" : "Rodando · inicia quando precisa",
        ServiceStartMode.Manual => "Parado · inicia quando precisa (normal)",
        ServiceStartMode.Automatic => "Parado · deveria estar rodando",
        _ => "Parado",
    };

    /// <summary>Only clear problems are flagged: a disabled security service or an automatic one that is stopped.</summary>
    public bool NeedsAttention => (IsSecurity && StartMode == ServiceStartMode.Disabled)
        || (StartMode == ServiceStartMode.Automatic && !IsRunning);
}

/// <summary>A device connected through USB.</summary>
public sealed record UsbDeviceInfo(string Name, string Kind, bool IsWorking);

/// <summary>A Windows page file (virtual memory on disk).</summary>
public sealed record PageFileInfo(string Path, long AllocatedMb, long CurrentUsageMb, long PeakUsageMb);

/// <summary>Reads security, service, USB and virtual-memory status. Read-only.</summary>
public interface IDeviceStatusProvider
{
    IReadOnlyList<AntivirusProduct> GetAntivirus();

    IReadOnlyList<ServiceStatusInfo> GetImportantServices();

    IReadOnlyList<UsbDeviceInfo> GetUsbDevices();

    IReadOnlyList<PageFileInfo> GetPageFiles();
}

/// <summary>The services worth checking, explained in plain words.</summary>
public static class ImportantServices
{
    public sealed record Definition(string Name, string Title, string Explanation, bool IsSecurity);

    public static IReadOnlyList<Definition> All { get; } =
    [
        new("WinDefend", "Antivírus Microsoft Defender", "Protege contra vírus. Fica parado quando outro antivírus está instalado — isso é normal.", true),
        new("mpssvc", "Firewall do Windows", "Bloqueia conexões perigosas vindas da internet.", true),
        new("wuauserv", "Windows Update", "Baixa as atualizações do Windows. Liga só quando precisa.", true),
        new("SecurityHealthService", "Central de Segurança", "Mostra o estado da proteção do computador.", true),
        new("BITS", "Transferência em segundo plano", "Usado pelo Windows Update para baixar arquivos.", false),
        new("Dhcp", "Cliente DHCP", "Recebe o endereço de internet do roteador automaticamente.", false),
        new("Dnscache", "Cliente DNS", "Traduz nomes de sites (google.com) em endereços.", false),
        new("WlanSvc", "Wi-Fi", "Conecta às redes sem fio.", false),
        new("Audiosrv", "Áudio do Windows", "Faz o som funcionar.", false),
        new("Spooler", "Impressão", "Envia os documentos para a impressora.", false),
        new("WSearch", "Pesquisa do Windows", "Encontra arquivos rapidamente no menu Iniciar.", false),
        new("W32Time", "Horário do Windows", "Mantém o relógio certo.", false),
        new("EventLog", "Registro de eventos", "Guarda os erros e avisos do Windows.", false),
    ];

    public static ServiceStartMode ParseStartMode(string? mode) => mode?.ToUpperInvariant() switch
    {
        "AUTO" or "AUTOMATIC" => ServiceStartMode.Automatic,
        "MANUAL" => ServiceStartMode.Manual,
        "DISABLED" => ServiceStartMode.Disabled,
        _ => ServiceStartMode.Unknown,
    };

    /// <summary>Plain name for a Windows device class (PNPClass).</summary>
    public static string DeviceKind(string? pnpClass) => pnpClass?.ToUpperInvariant() switch
    {
        "DISKDRIVE" or "WPD" or "VOLUME" => "Armazenamento (pen drive, HD externo, celular)",
        "KEYBOARD" => "Teclado",
        "MOUSE" => "Mouse",
        "HIDCLASS" => "Dispositivo de entrada (teclado, mouse, controle)",
        "IMAGE" or "CAMERA" => "Câmera ou scanner",
        "BLUETOOTH" => "Bluetooth",
        "MEDIA" or "AUDIOENDPOINT" => "Áudio (fone, microfone, caixa de som)",
        "NET" => "Rede",
        "PRINTER" or "PRINTQUEUE" => "Impressora",
        "PORTS" => "Porta serial",
        "BIOMETRIC" => "Leitor biométrico",
        "SMARTCARDREADER" => "Leitor de cartão",
        null or "" => "Outro",
        _ => pnpClass!,
    };
}
