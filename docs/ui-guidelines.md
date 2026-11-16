# Diretrizes de interface

O Clean Menu é feito para qualquer pessoa, inclusive quem tem pouca familiaridade com computadores.
Toda tela nova deve seguir estas regras.

## Linguagem

- Português simples. Nada de jargão sem explicação ("cache", "processo", "SMART" sempre vêm com
  uma frase do que significam).
- Toda página começa com título e **uma frase** dizendo para que ela serve (`PageHeader`).
- Todo número tem um **?** ao lado (`InfoTip`) explicando o que ele significa em palavras do dia a dia.
- Mensagens dizem o que aconteceu e o que fazer, sem códigos de erro na frente.

## Ações

- Uma ação principal por tela, em destaque (`Button.Primary`). As demais são secundárias.
- Antes de qualquer ação que altere o computador: explicar o que vai acontecer e pedir confirmação.
- Deixar claro quando algo **não apaga nada** (ex.: análise).
- Áreas para usuários experientes têm a etiqueta **Avançado** na barra lateral.

## Honestidade

- Nunca mostrar valor inventado. Sem dado real, o valor é **"—"** com uma legenda dizendo quando
  ele vai aparecer.
- Indicadores de status (`StatusLevel`) ficam neutros e sem cor quando não há dados para julgar.
- Seções ainda não implementadas mostram `ComingSoonPanel` com o que farão, nunca controles falsos.

## Visual

| Item | Regra |
|---|---|
| Tema | Escuro por padrão; cores só por `DynamicResource` (arquivos `Colors.*.xaml`) |
| Texto | Mínimo 13px para legendas e 15px para corpo |
| Fonte | Segoe UI Variable (Win 11) com fallback para Segoe UI (Win 10) |
| Ícones | Somente códigos de `Glyphs`, que existem no Segoe MDL2 (Win 10) e Segoe Fluent Icons (Win 11) |
| Cor de destaque | Verde (`Brush.Accent`), usada com moderação: ação principal e item selecionado |
| Estados | Verde = bom, âmbar = atenção, vermelho = problema, cinza = sem dados |
| Janela | Moldura nativa do Windows; barra de título acompanha o tema |

## Componentes (`CleanMenu.UI`)

| Componente | Uso |
|---|---|
| `PageHeader` | Título + descrição de uma frase |
| `StatCard` | Uma métrica: ícone, nome, `?`, valor e legenda |
| `InfoTip` | O `?` com explicação ao passar o mouse ou com foco do teclado |
| `RingGauge` | Anel de progresso; vazio quando não há valor |
| `Sparkline` | Mini gráfico de linha para histórico recente |
| `ComingSoonPanel` | Seção ainda não implementada |

## Acessibilidade

- Tudo alcançável pelo teclado (Tab, setas na barra lateral) com contorno de foco visível.
- Botões só com ícone ou com significado especial têm `AutomationProperties.Name`.
- Dicas aparecem também com foco do teclado, não só com o mouse.
