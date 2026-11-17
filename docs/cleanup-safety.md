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

## Limpeza Avançada: o que nunca é oferecido

Incidente (16/11/2026): o modo "Duplicados", rodando na pasta do usuário, tratou arquivos idênticos
dentro de programas e projetos como cópias e mandou 663 arquivos para a Lixeira. Entre eles estavam
arquivos do VS Code, do Epic, do WhatsApp, do Spotify, do Android SDK, de `node_modules` e de `.venv`.
Os arquivos foram restaurados da Lixeira. Desde então valem estas regras (`AdvancedFileRules`,
`AdvancedScanner`, `AdvancedProtectedFolders`):

1. Pastas de programas nunca são analisadas: `AppData` inteira, Program Files, ProgramData e toda
   pasta registrada como `InstallLocation` de um programa instalado.
2. Pastas de ferramentas e de projetos nunca são analisadas, em qualquer profundidade:
   - pastas ocultas (`.git`, `.venv`, `.gradle`, `.vscode`...);
   - `node_modules`, `site-packages`, `bin`, `obj`, `build`, `dist`, `android`, `steamapps` e similares.
3. Projetos de software são detectados por marcadores, e tudo dentro deles é ignorado:
   - pastas: `.git`, `.svn`, `.hg`;
   - arquivos: `package.json`, `*.sln`, `*.csproj`, `pyproject.toml`, `requirements.txt`,
     `build.gradle`, `Cargo.toml`, `go.mod` e outros.
4. "Duplicados" só considera conteúdo pessoal: fotos, vídeos, músicas, documentos e arquivos
   compactados.
5. Arquivos de programa (`.exe`, `.dll`, `.node`, `.js`, `.json`, `.wasm`, `.pak`...) nunca aparecem,
   nem em "Arquivos grandes".
6. As regras são conferidas de novo imediatamente antes de mandar cada arquivo para a Lixeira.

## Arquivos usados com frequência nunca saem

Pedido do usuário: "de forma alguma é para apagar arquivos que são utilizados frequentemente".

1. Limpeza Avançada ("Arquivos grandes" e "Duplicados") só mostra arquivos sem nenhum sinal de uso
   há mais de 3 meses (`AdvancedFileRules.RecentUseWindow`). Contam como uso:
   - abrir o arquivo (data de último acesso, quando o Windows registra);
   - alterar o arquivo (data de modificação);
   - colocar o arquivo no disco: baixar, copiar ou criar (data de criação).
2. Arquivos da lista de "Itens recentes" do Windows (`%APPDATA%\Microsoft\Windows\Recent`) nunca
   aparecem, mesmo com datas antigas. A lista só é lida; os atalhos não são alterados.
3. Arquivos usados recentemente são descartados antes da comparação SHA-256. Assim, o próprio
   Clean Menu não lê esses arquivos.
4. Logo antes de mandar cada arquivo para a Lixeira, ele é lido de novo. O envio é recusado se:
   - o arquivo sumiu;
   - mudou de tamanho ou de data;
   - foi criado ou copiado de novo;
   - entrou nos itens recentes.

   A data de acesso usada nessa conferência é a da análise, porque a comparação de duplicados
   lê os arquivos e o Windows registra essa leitura como acesso.
5. Limpeza Inteligente (temporários): um arquivo temporário antigo que um programa abriu
   recentemente também é mantido, porque a idade mínima conta o último sinal de uso.
6. Se a lista de itens recentes não puder ser lida, a regra dos 3 meses continua valendo.
