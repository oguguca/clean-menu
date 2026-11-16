# Arquitetura do Clean Menu

## Visão geral

```
┌──────────────────────┐      ┌───────────────────┐
│   CleanMenu.App      │─────▶│   CleanMenu.UI    │  temas, estilos, controles
│ (WPF, ViewModels, DI)│      └───────────────────┘
└─────────┬────────────┘
          │ depende de
          ▼
┌──────────────────────┐      ┌──────────────────────────┐
│ CleanMenu.Infrastr.  │─────▶│      CleanMenu.Core      │  domínio puro
│ (WMI, P/Invoke, ...) │      │ (modelos, interfaces,    │
└──────────────────────┘      │  regras de segurança)    │
                              └──────────────────────────┘
┌──────────────────────┐                 ▲
│ CleanMenu.Elevated   │─────────────────┘
│ (processo UAC)       │
└──────────────────────┘
```

Regra de dependência: **Core não depende de ninguém.** Infrastructure implementa as interfaces do
Core. App compõe tudo via injeção de dependência. UI não conhece o domínio, só apresentação.

## Projetos

### CleanMenu.Core
Biblioteca `net10.0`, sem referências a WPF ou a APIs do Windows. Contém:

- **Abstractions**: `IFileSystem`, `IClock`, `IProcessProvider` etc. Permitem testar toda a lógica
  sem tocar no disco ou no sistema real.
- **Cleanup**: categorias de limpeza, motor de análise, `PathSafetyValidator` (lista de raízes
  permitidas e proibidas), modo Dry Run.
- **Duplicates**: agrupamento por tamanho, depois hash SHA-256 apenas nos candidatos.
- **OperatingSystem**: perfil do sistema operacional (Windows 10 / Windows 11). Cada ferramenta,
  configuração ou atalho declara a quais sistemas se aplica.
- Monitoring, Startup, Processes, Disks, History: modelos e contratos de cada módulo.

### CleanMenu.Infrastructure
Biblioteca `net10.0-windows` com as implementações reais:

| Necessidade | Fonte |
|---|---|
| Memória | `GlobalMemoryStatusEx` (P/Invoke) |
| CPU / GPU | Contadores de desempenho (`Processor Information`, `GPU Engine`) |
| Hardware / SO | WMI: `Win32_Processor`, `Win32_OperatingSystem`, `Win32_ComputerSystem` |
| Discos | WMI Storage: `MSFT_PhysicalDisk`, `MSFT_StorageReliabilityCounter` (SMART) |
| Lixeira | `SHQueryRecycleBin`, `SHEmptyRecycleBin` |
| Arquivos em uso | Restart Manager (`RmGetList`) |
| Inicialização | Registro `Run`/`RunOnce`, `StartupApproved`, pastas Startup, Agendador de Tarefas |
| Histórico/config | SQLite (`Microsoft.Data.Sqlite`) |
| Logs | Serilog, arquivo rotativo |

Temperatura da CPU: o Windows não expõe um valor confiável sem driver de kernel. O Clean Menu não
instala drivers; quando o dado não estiver disponível, exibe "não suportado".

### CleanMenu.UI
Biblioteca WPF com o design system: dicionários de recursos para tema escuro e claro, tipografia,
estilos e controles reutilizáveis (Card, medidor, gráfico de linha, sidebar).

### CleanMenu.App
Aplicativo WPF. Inicializa o `Generic Host`, registra serviços, ViewModels e Views, e controla a
navegação. ViewModels usam `CommunityToolkit.Mvvm` (`ObservableObject`, `RelayCommand`,
`AsyncRelayCommand` com cancelamento).

### CleanMenu.Elevated
Executável separado com manifesto `requireAdministrator`. É iniciado pelo App somente quando uma
operação precisa de privilégio (SFC, DISM, CHKDSK, `Optimize-Volume`, temporários do Windows, ponto
de restauração). Recebe uma operação de uma lista fechada (nunca um comando livre), executa,
devolve o resultado e encerra. O App mostra ao usuário o comando exato antes de pedir a elevação.

## Windows 10 e Windows 11

Na primeira execução, o sistema é detectado pelo número de build (`>= 22000` = Windows 11) e
sugerido ao usuário, que confirma ou escolhe manualmente nas Configurações. A escolha controla:

- quais atalhos de Ferramentas Windows aparecem (ex.: URIs `ms-settings:` que só existem em um dos sistemas);
- quais configurações de privacidade e desempenho são consultadas (as chaves de registro diferem);
- quais categorias de limpeza existem.

Funcionalidades marcadas para um sistema diferente do escolhido ficam ocultas.

## Segurança na exclusão

Toda exclusão passa por, nesta ordem:

1. Normalização do caminho (`Path.GetFullPath`) e rejeição de caminhos relativos, UNC e de dispositivo.
2. Verificação de que o caminho está dentro de uma raiz permitida da categoria e fora das raízes proibidas
   (`C:\Windows\System32`, perfis de navegador com credenciais, pastas pessoais etc.).
3. Rejeição de reparse points (symlinks/junctions) para impedir redirecionamento.
4. Idade mínima do arquivo, quando a categoria exigir.
5. Tentativa de exclusão; arquivos em uso são ignorados e registrados, nunca forçados.

O modo Dry Run executa os passos 1 a 4 e gera a pré-visualização sem apagar nada.

## Testes

- `CleanMenu.Core.Tests`: unidade, sem disco real, sem privilégio. Executam em qualquer máquina.
- `CleanMenu.Infrastructure.Tests`: integração em pastas sandbox criadas em `%TEMP%` pelo próprio
  teste. Testes que exigem privilégio administrativo são marcados com `[Trait("Category", "RequiresAdmin")]`
  e ficam fora da execução padrão.
