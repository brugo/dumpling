# Versionamento do Mimo

O repositório fica nesta pasta `MimoGardenUnity`, na branch `main`. Ele preserva o projeto Unity: `Assets` com seus arquivos `.meta`, `Packages` (incluindo o lock), `ProjectSettings`, documentação e o comando de abertura do Editor.

Os `.meta` contêm os identificadores usados pelo Unity para ligar cenas, materiais, scripts e imagens. Ao mover ou remover um asset, inclua seu `.meta` na mesma alteração. Prefira mover assets pelo Editor.

`Library`, `Temp`, `Logs`, `obj`, configurações locais de usuário, caches de IDE, builds, APKs e credenciais são excluídos pelo `.gitignore`. As imagens criadas pelo ChatGPT e o ambiente de reflexão são assets necessários durante o jogo e fazem parte do histórico. Os executáveis, relatórios de teste e arquivos de trabalho nas pastas vizinhas não pertencem a este repositório.

## Rotina de trabalho

1. Confira o estado antes de começar: `git status --short` e `git log -5 --oneline`.
2. Faça uma alteração completa, confira o resultado no jogo e aguarde a conclusão das gravações e compilações. Evite commits enquanto outra tarefa altera os mesmos arquivos.
3. Revise com `git diff`. Inclua somente os arquivos da mudança com `git add CAMINHOS`, confira `git diff --cached --stat` e registre com `git commit -m "Descrição da mudança"`.

Para uma mudança de assets, por exemplo:

```powershell
git status --short
git diff
git add Assets Packages ProjectSettings
git diff --cached --stat
git commit -m "Melhora imagens e navegação do ateliê"
git status --short
```

Inclua também os arquivos de documentação e configuração que tiver alterado. Use `git check-ignore -v CAMINHO` para entender por que um arquivo está excluído. O histórico está disponível com `git log --oneline`; `git show HASH` mostra uma mudança específica.

## Assets e armazenamento

Na configuração inicial, os assets necessários somam cerca de 20 MB e o maior arquivo tem aproximadamente 8,4 MB. Git LFS não foi configurado. Reavalie LFS se surgirem modelos, texturas, áudio ou arquivos Blender grandes, sobretudo arquivos individuais acima de 50 MB que mudem com frequência. Preserve os arquivos editáveis de origem mantidos fora desta pasta em uma estratégia de backup própria.

O remoto `origin` aponta para [brugo/dumpling](https://github.com/brugo/dumpling). A branch principal é `main`, com o histórico local preservado. Depois de registrar e revisar as alterações, use `git push` para enviá-las ao GitHub.

## Coleções da 0.6.0

As novas imagens originais do ChatGPT e os modelos FBX fazem parte de Assets, com seus .meta. ArtSource agora guarda o script Blender e os três arquivos .blend editáveis; backups .blend1/.blend2 são excluídos. Os assets e fontes desta entrega somam aproximadamente 36,5 MiB; o maior arquivo individual tem 8 MiB. LFS não foi necessário nem configurado. Builds, capturas e relatórios continuam nas pastas vizinhas, fora do Git.
