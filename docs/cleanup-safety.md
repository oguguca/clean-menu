# Limpeza: regras de segurança

Este documento descreve como o Clean Menu decide o que pode ser apagado. O código está em
`src/CleanMenu.Core/Cleanup` (regras) e `src/CleanMenu.Infrastructure/Cleanup` (acesso ao Windows).

## Fluxo

1. **Análise** (`CleanupEngine.AnalyzeAsync`): só lê. Lista os arquivos de cada categoria e separa
   os que podem ser apagados dos que serão mantidos.
2. **Revisão**: o usuário vê, por categoria, quantos arquivos e quanto espaço, e pode abrir a lista
   dos maiores arquivos que serão apagados.
3. **Confirmação**: janela própria com o total, as categorias e os avisos. O botão padrão é
   "Cancelar", então Enter ou Esc nunca confirmam por acidente.
4. **Limpeza** (`CleanupEngine.CleanAsync`): cada arquivo é verificado de novo imediatamente antes
   de ser apagado.
5. **Histórico**: toda execução, inclusive simulação, é registrada em
   `%LOCALAPPDATA%\CleanMenu\history.jsonl`.

O **modo simulação** executa todas as verificações e mostra o resultado sem apagar nada.

## Regras aplicadas a cada arquivo

| # | Regra | Onde |
|---|---|---|
| 1 | Caminho local e completo; caminhos de rede (`\\servidor`) e de dispositivo (`\\?\`, `\\.\`) são recusados | `PathSafetyValidator` |
| 2 | Estritamente dentro da pasta da categoria (sem `..`, sem truques de prefixo como `Temp2`) | `PathSafetyValidator` |
| 3 | Fora das pastas protegidas: Windows, Program Files, Área de Trabalho, Documentos, Imagens, Músicas, Vídeos, Downloads, OneDrive, credenciais do Windows | `WindowsProtectedFolders` |
| 4 | Nunca arquivos de senha, cookies, histórico ou sessão (`Login Data`, `Cookies`, `logins.json`, `key4.db`…), nem pastas `Network`, `Local Storage`, `IndexedDB`… | `PathSafetyValidator` |
| 5 | Links simbólicos e junctions nunca são seguidos nem apagados | `WindowsCleanupFileSystem` + motor |
| 6 | Arquivos marcados como de sistema são mantidos | motor |
| 7 | Arquivos mais novos que a idade mínima da categoria são mantidos (ex.: temporários com menos de 1 dia) | motor |
| 8 | Arquivo alterado depois da análise é mantido | motor |
| 9 | Arquivo em uso por um programa é ignorado, nunca forçado | `WindowsCleanupFileSystem` |

Uma pasta de categoria só é aceita se tiver pelo menos três níveis (nunca `C:\` ou o perfil
inteiro) e não for, nem contiver, uma pasta protegida.

## Categorias

| Categoria | Pasta | Idade mínima | Marcada por padrão |
|---|---|---|---|
| Arquivos temporários do usuário | `%TEMP%` | 1 dia | Sim |
| Miniaturas de imagens | `…\Explorer\thumbcache_*.db` | — | Sim |
| Relatórios de erros | `…\WER\ReportArchive`, `ReportQueue` | 1 dia | Sim |
| Diagnóstico de travamentos | `%LOCALAPPDATA%\CrashDumps\*.dmp` | 1 dia | Sim |
| Cache de navegadores (Chrome, Edge, Brave, Opera, Firefox) | Somente `Cache`, `Code Cache`, `GPUCache`, `ShaderCache`, `cache2` | 1 hora | Sim |
| Lixeira | API do Windows (`SHEmptyRecycleBin`) | — | **Não** |
| Cache de gráficos (shaders) | DirectX, NVIDIA, AMD, Intel | 1 hora | Não |
| Temporários do Windows | `C:\Windows\Temp` | — | Precisa de administrador |
| Arquivos antigos de atualização | `SoftwareDistribution\Download` | — | Precisa de administrador |

As categorias que precisam de administrador aparecem desativadas até existir o fluxo de elevação
por operação (`CleanMenu.Elevated`).

## Testes

- `tests/CleanMenu.Core.Tests/Cleanup`: validador e motor contra um sistema de arquivos em memória.
- `tests/CleanMenu.Infrastructure.Tests/Cleanup`: arquivos reais, mas somente numa pasta sandbox
  criada em `%TEMP%` pelo próprio teste (inclui junction, arquivo bloqueado e somente leitura).
  Os testes nunca esvaziam a Lixeira real.
