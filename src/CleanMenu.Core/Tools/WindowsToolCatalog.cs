using CleanMenu.Core.Platform;

namespace CleanMenu.Core.Tools;

/// <summary>A built-in Windows tool, explained in plain words.</summary>
public sealed record WindowsTool(string Title, string Description, string Glyph, string Command, OsScope Os, string? Arguments = null);

/// <summary>Shortcuts to tools that come with Windows. Glyphs are code points valid on Windows 10 and 11.</summary>
public static class WindowsToolCatalog
{
    public static IReadOnlyList<WindowsTool> All { get; } =
    [
        new("Gerenciador de Tarefas", "Mostra os programas abertos e permite fechar os que travaram.", "", "taskmgr.exe", OsScope.All),
        new("Windows Update", "Procura e instala as atualizações de segurança do Windows.", "", "ms-settings:windowsupdate", OsScope.All),
        new("Limpeza de Disco", "Ferramenta do Windows para apagar arquivos desnecessários.", "", "cleanmgr.exe", OsScope.All),
        new("Armazenamento", "Mostra o que ocupa espaço e liga o Sensor de Armazenamento.", "", "ms-settings:storagesense", OsScope.All),
        new("Gerenciador de Dispositivos", "Lista as peças do computador e os drivers. Útil quando algo não funciona.", "", "devmgmt.msc", OsScope.All),
        new("Gerenciamento de Disco", "Mostra os discos e partições. Use com cuidado: mudanças aqui podem apagar dados.", "", "diskmgmt.msc", OsScope.All),
        new("Monitor de Recursos", "Mostra em detalhes o uso de processador, memória, disco e rede.", "", "resmon.exe", OsScope.All),
        new("Monitor de Desempenho", "Gráficos avançados de desempenho para técnicos.", "", "perfmon.exe", OsScope.All),
        new("Visualizador de Eventos", "Registro de erros e avisos do Windows. Útil para técnicos descobrirem problemas.", "", "eventvwr.msc", OsScope.All),
        new("Informações do Sistema", "Ferramenta do Windows com todos os detalhes do computador.", "", "msinfo32.exe", OsScope.All),
        new("Serviços", "Lista os serviços do Windows. Para técnicos: desativar o serviço errado pode causar problemas.", "", "services.msc", OsScope.All),
        new("Conexões de Rede", "Mostra as placas de rede (Wi-Fi e cabo) e permite ativar ou desativar cada uma.", "", "ncpa.cpl", OsScope.All),
        new("Segurança do Windows", "Mostra o estado do antivírus, do firewall e da proteção do computador.", "", "windowsdefender:", OsScope.All),
        new("Programas instalados", "Lista os programas instalados para desinstalar os que você não usa.", "", "ms-settings:appsfeatures", OsScope.All),
        new("Painel de Controle", "As configurações clássicas do Windows.", "", "control.exe", OsScope.All),
        new("Terminal", "Linha de comando moderna do Windows 11. Para usuários avançados.", "", "wt.exe", OsScope.Windows11),
        new("Prompt de Comando", "Linha de comando clássica. Para usuários avançados.", "", "cmd.exe", OsScope.Windows10),
        new("PowerShell", "Linha de comando avançada do Windows. Para usuários avançados.", "", "powershell.exe", OsScope.All),
    ];

    public static IReadOnlyList<WindowsTool> For(OsProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return All.Where(t => profile.Effective is null ? t.Os == OsScope.All : profile.Shows(t.Os)).ToList();
    }
}
