# Clean Menu

Central de limpeza, manutenção, diagnóstico e otimização para Windows 10 e Windows 11.

> **Status:** prévia (v0.1.0). As 12 seções funcionam: monitoramento em tempo real, análise do
> computador, Limpeza Inteligente (com simulação, confirmação e histórico), Limpeza Avançada
> (arquivos grandes, duplicados por SHA-256, pastas, sobras de programas; tudo vai para a Lixeira),
> Memória RAM (com memória virtual), Otimização (SFC, DISM, CHKDSK, reset do Windows Update, winget,
> registros de eventos, políticas de grupo), Inicialização, Processos, Discos (TRIM/desfragmentação),
> Rede e Internet (ping, teste de velocidade, ipconfig, reset de rede, download HTTPS), Ferramentas do
> Windows, Informações do Sistema (antivírus, serviços, USB), Histórico e Configurações (atualizações). Ainda não disponíveis: limpezas que
> exigem administrador (temporários do Windows e arquivos antigos de atualização) e o instalador.
> Regras de segurança da limpeza: [docs/cleanup-safety.md](docs/cleanup-safety.md).

---

## Objetivos

- Liberar espaço em disco removendo **apenas** arquivos comprovadamente descartáveis.
- Mostrar o estado real do computador (CPU, RAM, GPU, discos, processos) a partir de fontes do sistema.
- Facilitar a manutenção do Windows (SFC, DISM, CHKDSK, otimização de unidades) com transparência total.
- Respeitar as diferenças entre **Windows 10** e **Windows 11**: o usuário escolhe seu sistema e o
  aplicativo exibe as ferramentas e configurações próprias de cada um.
- Nunca prometer ganhos de desempenho não comprovados nem simular resultados.

## Funcionalidades planejadas

| Módulo | Descrição |
|---|---|
| Dashboard | Visão geral do hardware e do uso de recursos; análise completa do computador |
| Limpeza Inteligente | Temporários, cache de navegadores, Lixeira, relatórios de erro, cache de miniaturas e shaders |
| Limpeza Avançada | Arquivos grandes, duplicados (SHA-256), pastas volumosas, sobras de apps desinstalados |
| Memória RAM | Uso em tempo real, processos que mais consomem, indícios de vazamento |
| Otimização | Planos de energia, efeitos visuais, apps em segundo plano, privacidade (somente alterações reversíveis) |
| Inicialização | Registro, pastas de inicialização e tarefas agendadas; habilitar/desabilitar sem excluir |
| Processos | Lista ordenável por CPU/memória, finalização protegida |
| Discos | Modelo, tipo (HDD/SSD), SMART quando disponível, TRIM para SSD, desfragmentação apenas para HDD |
| Rede e Internet | Ping, teste de velocidade real (Cloudflare), IP/gateway/DNS de cada placa, reset de rede, download HTTPS com SHA-256 |
| Ferramentas Windows | Atalhos para ferramentas nativas, adaptados ao Windows 10 ou 11 |
| Informações do Sistema | Hardware e software detalhados |
| Histórico | Registro de todas as operações executadas |
| Configurações | Tema, sistema operacional, frequência de atualização, categorias padrão, logs |

## Tecnologias

- C# / **.NET 10** (LTS, suporte até novembro de 2028)
- **WPF** com padrão **MVVM** ([CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/))
- Injeção de dependência via `Microsoft.Extensions.Hosting`
- APIs nativas do Windows (P/Invoke, WMI, Registro, Agendador de Tarefas)
- SQLite para histórico e configurações
- xUnit para testes automatizados

> O .NET 8 foi descartado porque seu suporte oficial terminou em 10/11/2026.

## Requisitos

- Windows 10 (versão 1809 ou superior) ou Windows 11, x64
- Para compilar: [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) (versão fixada em `global.json`)

## Como executar

```powershell
git clone https://github.com/oguguca/clean-menu.git
cd clean-menu
dotnet run --project src/CleanMenu.App
```

## Como compilar e testar

```powershell
dotnet build CleanMenu.sln -c Release
dotnet test CleanMenu.sln
```

Os testes que tocam o sistema de arquivos real usam apenas pastas temporárias isoladas, criadas e
removidas pelo próprio teste. Nenhum teste apaga arquivos do usuário.

## Estrutura do projeto

```
CleanMenu/
├── src/
│   ├── CleanMenu.Core/            Domínio: modelos, interfaces e regras (sem dependência de WPF)
│   ├── CleanMenu.Infrastructure/  Implementações Windows: WMI, P/Invoke, Registro, SQLite, logs
│   ├── CleanMenu.UI/              Design system WPF: temas, estilos e controles reutilizáveis
│   ├── CleanMenu.App/             Aplicativo WPF: inicialização, DI, ViewModels e Views
│   └── CleanMenu.Elevated/        Auxiliar elevado (UAC) executado somente por operação
├── tests/
│   ├── CleanMenu.Core.Tests/
│   └── CleanMenu.Infrastructure.Tests/
├── docs/                          Documentação técnica
├── assets/                        Ícones e imagens
└── scripts/                       Scripts de build e publicação
```

Detalhes em [docs/architecture.md](docs/architecture.md).

## Segurança

O Clean Menu segue regras obrigatórias:

1. Nunca exclui arquivos essenciais do Windows, documentos pessoais, senhas, cookies ou sessões.
2. Toda exclusão passa por validação de caminho: somente dentro de pastas permitidas, sem seguir
   links simbólicos ou junctions, ignorando arquivos em uso.
3. **Modo de simulação (Dry Run)** e pré-visualização antes de qualquer remoção.
4. Arquivos escolhidos pelo usuário (duplicados, grandes) vão para a Lixeira, não são apagados direto.
5. Nunca desativa Windows Defender, Firewall, Windows Update ou serviços essenciais.
6. Comandos administrativos são exibidos ao usuário antes da execução e pedem elevação (UAC)
   apenas para aquela operação. O aplicativo em si não roda como administrador.
7. Todas as operações são registradas em log e no histórico.

## Roadmap

| Etapa | Conteúdo | Status |
|---|---|---|
| 1 | Arquitetura, estrutura, documentação, detecção Windows 10/11 | Concluída |
| 2 | Interface: identidade visual, sidebar, dashboard, navegação | Concluída |
| 3 | Monitoramento: sistema, CPU, RAM, discos, processos | Concluída |
| 4 | Limpeza: motor de análise, temporários, caches, Lixeira, navegadores | Concluída |
| 5 | Otimização: inicialização, processos, manutenção, HDD/SSD | Concluída |
| 6 | Avançado: duplicados, arquivos grandes, histórico, configurações | Concluída |
| 7 | Testes finais, documentação, instalador, release | Pendente |

## Como contribuir

1. Crie um branch a partir de `main` (`feat/nome-da-funcionalidade`).
2. Siga o padrão [Conventional Commits](https://www.conventionalcommits.org/pt-br/).
3. Garanta que `dotnet build` e `dotnet test` passam sem avisos.
4. Abra um Pull Request descrevendo o que mudou e como foi testado.

## Aviso

Ferramentas de limpeza e manutenção alteram o sistema. Use o modo de simulação antes de limpar e
mantenha backups dos seus dados importantes.
