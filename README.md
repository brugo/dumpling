# Dumpling — Mimo

Protótipo de bichinhos 3D que a criança cria e cuida, desenvolvido em Unity para tablets na horizontal. O nome Mimo e a identidade visual são provisórios.

A versão atual é **0.8.0**, com criação no Ateliê ou no modo Surpresa, parquinho para até cinco amigos, banho, alimentação e coleções com pagamentos simulados.

## Abrir o projeto

1. Instale o Unity **6000.6.0f1** pelo Unity Hub.
2. Clone o repositório:

   ```sh
   git clone https://github.com/brugo/dumpling.git
   ```

3. No Unity Hub, adicione a pasta `dumpling` como projeto existente e abra-a. Aguarde a importação dos assets e pacotes.
4. Abra `Assets/Scenes/MimoGarden.unity` e pressione **Play**.

Para gerar APKs, instale também o módulo Android Build Support dessa versão do Unity, incluindo SDK, NDK e OpenJDK.

## Conteúdo

- `Assets/`: scripts, cenas, modelos, materiais, imagens e arquivos `.meta`.
- `Packages/` e `ProjectSettings/`: dependências e configuração do projeto.
- `ArtSource/`: arquivos Blender e fontes de arte versionados.
- [LEIA-ME.md](LEIA-ME.md): funcionalidades e histórico do protótipo.
- [SURPRESA-0.8.0.md](SURPRESA-0.8.0.md): criação com ingredientes neutros.
- [LOJA-TESTE.md](LOJA-TESTE.md): funcionamento da loja simulada.
- [VERSIONAMENTO.md](VERSIONAMENTO.md): orientações para contribuir e preservar os assets.

Caches do Unity, builds e APKs ficam fora do Git. As referências da documentação histórica a pastas vizinhas de entregas e validações apontam para arquivos locais que não acompanham este repositório. O projeto pode ser aberto diretamente no Editor seguindo os passos acima.
