using CleanMenu.UI;

namespace CleanMenu.App.Navigation;

/// <summary>
/// Single source of truth for sidebar sections: order, grouping, names and plain-language texts.
/// </summary>
public static class PageCatalog
{
    public const string GroupOverview = "VISÃO GERAL";
    public const string GroupCleanup = "LIMPEZA";
    public const string GroupPerformance = "DESEMPENHO";
    public const string GroupComputer = "COMPUTADOR";
    public const string GroupApp = "CLEAN MENU";

    public static IReadOnlyList<PageInfo> All { get; } =
    [
        new(AppPage.Dashboard, "Início",
            "Veja como está o seu computador e descubra, com um clique, o que pode ser melhorado.",
            Glyphs.Home, GroupOverview, false, [], 3),

        new(AppPage.SmartCleanup, "Limpeza Inteligente",
            "Apaga arquivos que não servem para nada e libera espaço. Seus documentos, fotos e senhas nunca são tocados.",
            Glyphs.Delete, GroupCleanup, false,
            [
                "Ver quanto espaço os arquivos inúteis estão ocupando",
                "Escolher o que limpar: arquivos temporários, cache dos navegadores, Lixeira e outros",
                "Ver a lista do que será apagado antes de confirmar",
                "Simular a limpeza sem apagar nada",
            ], 4),

        new(AppPage.AdvancedCleanup, "Limpeza Avançada",
            "Encontre arquivos muito grandes ou repetidos. Nada é apagado sozinho: você decide o que fazer.",
            Glyphs.Search, GroupCleanup, true,
            [
                "Encontrar os arquivos e pastas que mais ocupam espaço",
                "Encontrar arquivos repetidos (cópias exatas)",
                "Encontrar sobras de programas que já foram desinstalados",
                "Mandar para a Lixeira só o que você escolher",
            ], 6),

        new(AppPage.Memory, "Memória RAM",
            "Veja quanta memória os programas abertos estão usando. Quando ela acaba, o computador fica lento.",
            Glyphs.Memory, GroupPerformance, false,
            [
                "Ver a memória usada e livre em tempo real",
                "Descobrir quais programas estão usando mais memória",
                "Receber dicas de quais programas você pode fechar",
            ], 3),

        new(AppPage.Optimization, "Otimização",
            "Ajustes para o Windows funcionar melhor. Cada ajuste é explicado e pode ser desfeito.",
            Glyphs.Lightning, GroupPerformance, false,
            [
                "Ver e trocar o plano de energia",
                "Ver programas que rodam escondidos em segundo plano",
                "Verificar e reparar arquivos do Windows (SFC e DISM)",
            ], 5),

        new(AppPage.Startup, "Inicialização",
            "Escolha quais programas abrem sozinhos quando o computador liga. Menos programas, início mais rápido.",
            Glyphs.Power, GroupPerformance, false,
            [
                "Ver todos os programas que abrem junto com o Windows",
                "Desligar ou religar cada um com um clique",
                "Nada é apagado: você pode voltar atrás quando quiser",
            ], 5),

        new(AppPage.Processes, "Processos",
            "Lista de tudo o que está rodando agora no computador, com o consumo de cada item.",
            Glyphs.Diagnostic, GroupPerformance, true,
            [
                "Ordenar por uso de processador ou de memória",
                "Abrir a pasta de um programa",
                "Fechar programas travados (os do Windows ficam protegidos)",
            ], 5),

        new(AppPage.Disks, "Discos",
            "Veja o espaço e a saúde do seu HD ou SSD.",
            Glyphs.HardDrive, GroupComputer, false,
            [
                "Ver espaço usado e livre de cada disco",
                "Saber se o disco é HD ou SSD e como ele está",
                "Otimizar o disco do jeito certo para cada tipo",
            ], 5),

        new(AppPage.WindowsTools, "Ferramentas do Windows",
            "Atalhos para ferramentas que já vêm no Windows, explicadas em palavras simples.",
            Glyphs.Repair, GroupComputer, false,
            [
                "Abrir o Gerenciador de Tarefas, Limpeza de Disco, Windows Update e outras ferramentas",
                "Ver só as ferramentas que existem no seu Windows (10 ou 11)",
            ], 6),

        new(AppPage.SystemInfo, "Informações do Sistema",
            "Detalhes do seu computador: processador, memória, placa de vídeo e versão do Windows.",
            Glyphs.Laptop, GroupComputer, false,
            [
                "Ver as peças do computador e a versão do Windows",
                "Copiar as informações para pedir ajuda a alguém",
            ], 3),

        new(AppPage.History, "Histórico",
            "Tudo o que o Clean Menu já fez no seu computador, com data e resultado.",
            Glyphs.History, GroupApp, false,
            [
                "Ver cada limpeza feita, quanto espaço foi liberado e se houve erro",
            ], 6),

        new(AppPage.Settings, "Configurações",
            "Escolha o tema, a versão do seu Windows e outras preferências.",
            Glyphs.Settings, GroupApp, false,
            [
                "Escolher tema escuro ou claro",
                "Confirmar se o seu sistema é Windows 10 ou Windows 11",
                "Definir o que vem marcado na limpeza",
            ], 6),
    ];

    public static PageInfo Get(AppPage page) =>
        All.FirstOrDefault(p => p.Page == page)
        ?? throw new ArgumentOutOfRangeException(nameof(page), page, "Page not registered in the catalog.");
}
