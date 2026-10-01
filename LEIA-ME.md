# Mimo - meu pequeno mundo

## Caudas traseiras - 0.6.1

As sete caudas nascem do centro da superfície traseira do corpo e crescem para trás, acompanhando sua rotação e escala. Pompom e enrolado preservam as peças importadas; as demais mantêm seus formatos e materiais. As prévias da loja atualizam o enquadramento ao girar para mostrar a cauda inteira. Escolhas de receita, economia e arquivos de progresso mantêm a compatibilidade.

A validação desta correção está em [relatório 0.6.1](../MimoGardenPreview/0.6.1/VALIDACAO.md), com capturas antes/depois, checks executados e limites de tablet.

## Coleções e cristais de teste — 0.6.0

O botão **Coleções** apresenta Jardim de Aurora, Guardião da Lua e Pérola do Mar, com materiais especiais, chapéus e acessórios coordenados. As texturas e o cristal foram criados pelo ChatGPT; os cartões mostram prévias renderizadas do personagem real, que podem ser giradas. Compras de cristais e gasto nas coleções passam pela área dos responsáveis, com confirmação e tratamento de saldo insuficiente e itens já adquiridos. Equipar e retirar preserva as escolhas gratuitas anteriores. Não há anúncios nem cobrança real.

Esta entrega contém exclusivamente um provedor de pagamento simulado, com preços provisórios, persistência atômica e prevenção de crédito duplicado. Leia [LOJA-TESTE.md](LOJA-TESTE.md) para usar os controles de teste, entender o armazenamento local e conferir as pendências precisas para Google Play Billing oficial.

Protótipo de bichinhos 3D que a criança cria e cuida. Projeto **0.6.1**, Unity **6000.6.0f1**, URP, pensado para tablets na horizontal. O nome Mimo e a identidade visual são provisórios.

## Banho de bolhas e parque temático — 0.5.5

A esponja espalha espuma por todo o corpo. São **3.072 bolhas** de vários tamanhos, com película transparente, reflexos e bordas iridescentes. A espuma continua presente ao soltar a esponja; ensaboar não remove a sujeira. A câmera do banho está mais próxima.

O chuveirinho tem um **cabo comprido**, com a saída de água afastada do ponto de toque. A ducha larga produz água transparente em movimento, gotas e respingos. Leve essa chuva até o corpo; ela alcança uma área ampla e escorre também para o outro lado. Poucos segundos de enxágue removem a espuma inteira, incluindo os pequenos resíduos.

O parque reúne **até cinco amigos**, cada um com sua aparência, cuidados e brincadeiras. Ao confirmar a sexta criação, você escolhe qual dos cinco amigos substituir e confirma a troca. Os amigos existentes são preservados pela atualização.

A nova imagem de parque temático foi criada no gerador de imagens integrado do ChatGPT e incorporada em `Assets/Resources/Environment/MimoThemePark.png`; o prompt fica no arquivo vizinho `ImageGen-park-prompt.txt`. Os brinquedos continuam sendo objetos 3D interativos. Sombras suaves de contato acompanham cada amigo, ficando mais difusas quando ele pula. Cada sombra usa apenas dois triângulos e uma textura compartilhada.

O perfil móvel permite até 60 FPS e ajusta a resolução do cenário para manter margem sobre a meta mínima de 30 FPS. A interface permanece na resolução nativa. Resultados medidos, capturas e limites da verificação ficam no [relatório da 0.5.5](../MimoGardenPreview/0.5.5/VALIDACAO.md).

## Mistura com movimentos livres — 0.5.4

Qualquer movimento do dedo sobre a vaporeira conta para misturar: reto, de ida e volta, irregular ou passando pelo centro. A colher gira automaticamente quando o dedo se move. Alguns movimentos simples já completam a mistura; não é preciso desenhar círculos. Pausar ou tirar o dedo preserva o progresso.

A barra de progresso fica logo abaixo da vaporeira, acompanhando seu enquadramento. Ela se enche em roxo e fica verde com **Tudo misturado!** ao concluir. A porcentagem e a orientação para mexer em círculos foram retiradas do menu.

Você pode acrescentar ingredientes enquanto a mistura ainda está incompleta. Cada novo ingrediente entra na mistura, preservando parte do trabalho já feito; cancelar um arraste mantém o progresso. O botão **Criar meu Mimo!** fica disponível quando há uma cor e todos os ingredientes adicionados estão misturados.

Capturas e resultados ficam no [relatório da 0.5.4](../MimoGardenPreview/0.5.4/VALIDACAO.md). Esta atualização não altera os amigos já salvos.

## Interface ilustrada — 0.5.3

As imagens da interface foram criadas pelo gerador de imagens integrado do ChatGPT: objetos com aparência de brinquedos 3D, cores pastel, materiais macios e detalhes perolados. As oito categorias e os 53 ingredientes do ateliê agora usam essa arte, com nomes curtos abaixo das imagens. O contorno roxo indica a opção selecionada.

Brincadeiras, gestos e ferramentas do banho aparecem em cartões ilustrados. Os atalhos **Comidinha**, **Banho**, **Brincar** e **Ateliê** permanecem na parte inferior da lateral. A fonte é mais arredondada, a lateral de cuidados ganhou espaço e as imagens reagem suavemente ao toque. No ateliê, as categorias ficam fixas e somente os ingredientes rolam.

No projeto Unity, artes e prompts ficam em `Assets/Resources/UI/`: `MimoUIAtlas.png`, `MimoColorsAtlas.png`, `MimoBodyAtlas.png`, `MimoEyesAtlas.png`, `MimoDressAtlas.png` e `ImageGen-prompts.json`. As imagens são ilustrações das peças; o personagem do cenário continua usando suas geometrias 3D.

Veja as capturas e os testes no [relatório da 0.5.3](../MimoGardenPreview/0.5.3/VALIDACAO.md).

## Histórico: menu por imagens — 0.5.2

As oito categorias do ateliê usam botões grandes com desenhos: garrafinha colorida, orelhas, rabinho, olhinho, óculos, chapéu, asas e uma amostra brilhante de textura. A categoria selecionada tem contorno roxo e um indicador. Os desenhos são vetoriais e permanecem nítidos na resolução da interface.

As categorias ficam fixas acima da lista. Somente os ingredientes rolam; tocar em outra categoria mostra suas primeiras opções. Os ingredientes também ganharam imagens maiores, com seus nomes abaixo. A lateral fica mais larga no ateliê para acomodar o menu; as outras telas mantêm suas dimensões.

## Parquinho dos amigos — 0.5.1

Até **três criações** ficam juntas no parquinho. Você escolhe quais amigos manter. Quando as três vagas estão ocupadas, levar uma nova criação para casa abre uma escolha de substituição: selecione um amigo existente e confirme sua exclusão. A criação mais antiga não é retirada automaticamente. O salvamento anterior é migrado para a coleção, preservando o personagem existente.

- Toque no dumpling ou no retrato dele na lateral para selecioná-lo. Cor, aparência, fome, alegria, sujeira e espuma pertencem a cada amigo.
- Arraste pelo chão para ir à frente, ao fundo ou às laterais. A câmera usa perspectiva e enquadra uma área maior.
- Escolha **Pular sem parar** ou **Pega-pega**. Cada dumpling mantém sua ação enquanto você seleciona outro; no pega-pega ele percorre o jardim com pequenos saltos. **Descansar** interrompe a ação.
- Solte sobre o **balanço**, **sofá**, **banco** ou **pula-pula**. O balanço se move com o ocupante, o pula-pula provoca saltos e os assentos permitem descansar. Arraste para retirar o amigo.
- **Banho** e **Comidinha** cuidam do amigo selecionado. Os retratos continuam disponíveis para trocar de amigo; voltar a **Brincar** reúne os três novamente.

O parquinho ganhou caminhos, árvores, canteiros, bandeirinhas e móveis. Cozinha e banheiro receberam novos acabamentos. Posições e brincadeiras são mantidas durante a sessão; ao reabrir o jogo, os três amigos retornam às posições iniciais. Aparência e cuidados ficam salvos no aparelho.

## Excluir ou substituir um amigo

1. Selecione o amigo no cenário ou no retrato da lateral.
2. Toque em **Excluir amigo...**, abaixo dos retratos.
3. Confira o nome, a cor e a textura apresentados. Toque em **Excluir** para remover esse amigo, ou em **Cancelar** para mantê-lo.

A exclusão remove somente o amigo escolhido. Os outros continuam com sua aparência e seus cuidados. Se o último amigo for excluído, o jogo abre o ateliê para criar outro.

Para guardar uma sexta criação, termine a receita e o nome e toque em **Levar para casa**. Escolha qual dos cinco amigos dará lugar ao novo e confirme. Cancelar a escolha ou a confirmação mantém os amigos salvos e a receita com o nome digitado, permitindo decidir depois.

## Abrir

Execute `Abrir-no-Unity.cmd` nesta pasta. A cena é `Assets/Scenes/MimoGarden.unity`. Pressione Play no Unity para começar. O cenário e a interface são construídos ao entrar em Play.

A atualização **0.5.5** inclui o banho de bolhas, ducha maior, cinco amigos e parque temático com sombras suaves. A versão Windows e o APK ficam nas pastas de entrega abaixo. Resultados no [relatório da 0.5.5](../MimoGardenPreview/0.5.5/VALIDACAO.md); os relatórios anteriores são históricos.

O destino Windows é a pasta vizinha `MimoGardenPlayable`; abra `MimoGarden.exe` e mantenha o executável e sua pasta de dados juntos. O destino Android é [MimoGardenAndroid/MimoGarden.apk](../MimoGardenAndroid/MimoGarden.apk), com instruções no [guia Android](../MimoGardenAndroid/LEIA-ME.md).

## Materiais da 0.4.1

- **Macio:** relevo fino, reflexão ampla e luz suave sobre a superfície.
- **Pérola:** acabamento acetinado e variação de cor conforme o ângulo.
- **Gelatina:** cor com espessura, absorção de luz, refração do cenário e reflexos sobre uma camada molhada.
- **Gel com glitter:** 552 lâminas douradas, prateadas e iridescentes, com brilhos que respondem à câmera e à luz. A sujeira e a espuma ocultam as partículas.
- **Cristal de açúcar:** pequenos cristais sobre uma base fosca, com brilho nas facetas.
- **Brilho mágico:** superfície opalescente e aura discreta.

Os efeitos usam shaders próprios e um ambiente de reflexão HDR pré-calculado; não dependem de serviço externo durante o jogo. Apenas Gelatina e Gel com glitter solicitam uma cópia da cor do cenário para refração. Os demais acabamentos mantêm essa cópia desligada. A malha e a receita do personagem continuam compatíveis com os salvamentos existentes.

[Comparação dos seis materiais](../MimoGardenPreview/0.4.1/Materiais-0.4.1.png) · [Glitter em movimento](../MimoGardenPreview/0.4.1/Glitter-em-movimento.gif)

## Histórico: o que mudou na 0.4.0

1. **Criar com as mãos:** arrastar ingredientes para a vaporeira, mexer em círculos e descobrir o amigo na revelação final. O personagem fica escondido durante a preparação.
2. **Escolher a aparência:** oito cores pastel e seis texturas, além de orelhas, rabos, olhos, óculos, chapéus e asas. A receita começa sem as peças opcionais.
3. **Dar banho em duas etapas:** a esponja espalha espuma persistente; o chuveirinho remove a espuma e a sujeira aos poucos, onde a água passa.

## Criar o primeiro Mimo

1. Toque no desenho da **garrafinha colorida** e puxe uma garrafinha da lista para a esquerda, levando-a até a vaporeira. Solte dentro dela. Deslizar a lista para cima ou para baixo apenas percorre as opções.
2. Mova o dedo sobre a mistura em qualquer direção. A colher gira e a barra abaixo da vaporeira mostra o avanço.
3. Se quiser, adicione outras características pelas categorias da lateral. Você pode juntar vários ingredientes antes de completar a mistura.
4. Toque em **Criar meu Mimo!**, acompanhe a revelação, dê um nome e escolha **Levar para casa**.

Uma cor misturada já permite criar. Orelhas, rabo, óculos, chapéu e asas são opcionais e começam ausentes; os olhos iniciais são Clássicos e a textura inicial é Macio. O botão de criação só fica disponível depois de adicionar uma cor e terminar de mexer os ingredientes.

A receita em preparação não substitui os amigos salvos. **Voltar** retorna ao parquinho. A nova criação entra no grupo depois de confirmar o nome; quando já existem três amigos, é preciso escolher e confirmar qual será substituído. Cancelar essa escolha mantém a nova receita e os três amigos anteriores.

## Catálogo

São **53 opções em oito categorias**, incluindo as opções de ausência de peças.

| Categoria | Opções | Conteúdo |
|---|---:|---|
| Cores | 8 | Rosa marshmallow, Lavanda, Pistache, Azul nuvem, Rosa chiclete, Pêssego, Baunilha e Menta |
| Orelhas | 7 | Sem orelhas, coelho, gato, raposa, urso, cachorrinho e axolote |
| Rabos | 8 | Sem rabinho, pompom, enrolado, raposa, gato, dragão, sereia e raio |
| Olhos | 6 | Clássicos, redondinhos, estrelas, corações, felinos e robô |
| Óculos | 6 | Sem óculos, redondos, estrelas, corações, visor e explorador |
| Chapéus | 6 | Sem chapéu, boné, coroa, mago, flores e espacial |
| Asas | 6 | Sem asas, fada, borboleta, dragão, anjo e cósmicas |
| Texturas | 6 | Macio, Pérola, Gelatina, Gel com glitter, Cristal de açúcar e Brilho mágico |

As peças possuem geometrias próprias. Fada e Borboleta, por exemplo, têm silhuetas e detalhes diferentes. O corpo respira, pisca, amassa e reage ao carinho e aos cuidados. As variações são combinadas pelo Unity; não há geração pelo Tripo durante o jogo.

## Brincar e cuidar

A faixa de ações fica na lateral direita, reservando o restante da tela ao personagem e ao cenário. Comidinha, Banho e Brincar levam à cozinha, ao banheiro e ao jardim. No computador, o mouse representa o toque; no Android, use a tela sensível ao toque.

- **Mover:** toque para fazer carinho; arraste o corpo para levantar e mudar de lugar; solte para pousar.
- **Amassar:** escolha este modo e arraste sobre o corpo. Ele se comprime, muda a expressão e recupera a forma ao soltar.
- **Girar:** escolha este modo e arraste horizontalmente. O botão direito também gira no computador; o tablet tem suporte a dois dedos.
- **Comidinha:** arraste a frutinha até ele, ou use Dar frutinha.
- **Brincar:** interaja com o Mimo no jardim e experimente os gestos.

No **Banho**, toque em **Ensaboar**, pegue a esponjinha no cenário e esfregue no corpo. A espuma fica onde foi aplicada; ensaboar não apaga imediatamente a sujeira. Depois, toque em **Enxaguar**, pegue o chuveirinho e leve a água até o Mimo. Mantenha e mova o jato sobre as áreas ensaboadas para limpar aos poucos. Use **Virar o Mimo** para alcançar o outro lado. **Brincar na lama** permite sujá-lo novamente.

As ferramentas precisam ser pegadas no cenário. Tocar diretamente no Mimo continua usando Mover, Amassar ou Girar. Soltar a ferramenta interrompe o jato; trocar de atividade ou perder o foco cancela o gesto em andamento.

## Salvamento

Identidade, receita e necessidades ficam localmente em `Application.persistentDataPath`, no arquivo `mimo-pet.json`, com uma cópia anterior `.bak`. Não há conta ou conexão obrigatória para brincar. O tempo fora do jogo reduz levemente as necessidades, com limite de seis horas; o bichinho não morre por ausência.

Os salvamentos anteriores são migrados para o formato 3. Nome, necessidades e acessórios são preservados; as cores antigas são convertidas para a nova paleta pastel e o acabamento inicial é Macio. A distribuição exata das manchas é reconstruída a partir do nível de limpeza ao reabrir. A espuma permanece durante a sessão, mas não é recuperada depois de fechar completamente o aplicativo.

Na atualização instalada no SM-X200, o arquivo de salvamento de **ANGELINA foi preservado byte a byte pela instalação**. Ao abrir a 0.4.0, a migração para a paleta pastel manteve a identidade da criação. A preservação do arquivo durante a instalação e a migração posterior são etapas distintas.

## Desempenho e limites

O perfil móvel mantém a meta de **30 FPS**, com interface na resolução nativa e resolução 3D adaptativa. Usa uma luz, sombra de contato simples, geometrias estáticas agrupadas e malhas compartilhadas. HDR, pós-processamento, MSAA e sombras em tempo real ficam desligados. O perfil também pode ser ativado no Windows com `--mimo-mobile-profile`.

No Samsung SM-X200, uma medição de **20,766 segundos no banho**, com gel/glitter, espuma e água ligada, registrou **29,86 FPS — aproximadamente 29,9 FPS**. A amostra medida pelo SurfaceFlinger teve intervalo de quadro no percentil 95 de 33,6 ms, com três quadros acima de 50 ms e máximo de 66,904 ms. É uma medição curta da 0.4.0 nessa situação; não garante o desempenho em sessões longas ou em todos os ambientes. Permanecem pendentes sessões prolongadas de desempenho, bateria e temperatura.

Esta é uma experiência inicial para avaliar criação, encanto e cuidados. Os materiais usam aproximações ópticas em tempo real; não há simulação completa de fluidos ou traçado de raios. A refração usa a cor visível do cenário, sem informação de objetos fora da tela. O banho atua no corpo, sem lavagem separada de cada acessório. Passeios fora do parquinho, várias casas, áudio final e conteúdo ampliado ainda não fazem parte do protótipo.

A orientação aceita apenas paisagem, nos dois lados. Multitoque com dedos humanos, teclado virtual para nomes personalizados, rotação física do aparelho e ergonomia precisam de verificação específica. Para iPad, será necessário exportar para iOS e compilar/assinar em um Mac com Xcode.

## Histórico de validação da 0.4.0

| Verificação | Situação registrada |
|---|---|
| Ritual de criação, revelação, salvamento e materiais | Windows: 78 verificações aprovadas em cada resolução, 1280×800 e 1200×900. Android/SM-X200: 78 aprovadas |
| Ferramentas de banho, espuma, água e interação direta com o Mimo | 30 verificações aprovadas em Windows e 30 no SM-X200; capturas do banho revisadas |
| Catálogo completo e combinações de aparência | 455 verificações aprovadas em Windows |
| Gestos e mudança de ambientes | 52 verificações aprovadas em Windows |
| Arraste dos ingredientes pela interface e rolagem | 22 verificações aprovadas em Windows e 22 no Android, incluindo o caso de deslocamento de 6 pixels |
| Perfil de desempenho móvel | 44 verificações aprovadas no Android |
| Nascimento no Editor | Regressão adaptada e compilada sem erros na geração Android; execução em Editor Play pendente |
| Entregas Windows e Android | Versões 0.4.0 geradas; APK instalado no SM-X200, preservando o salvamento existente |
| Desempenho da 0.4.0 no SM-X200 | Banho com gel/glitter, espuma e água ligada: 29,86 FPS em 20,766 segundos; sessões longas ainda não avaliadas |

As verificações e medições das versões 0.3.0 e 0.3.1 são históricas. Seus registros permanecem no [arquivo de validação](../MimoGardenPreview/VALIDACAO.md) e nas pastas Android anteriores; não constituem aprovação da 0.4.0.

## Reproduzir verificações

Execute uma compilação Windows da versão atual, **uma modalidade de teste por execução**, com a janela visível. Cada teste abaixo usa um salvamento separado e escreve seu relatório em `--mimo-test-output "PASTA_DE_RESULTADOS"`.

| Parâmetro | O que verifica | Relatório |
|---|---|---|
| `--mimo-ritual-test` | Ingredientes, mistura circular obrigatória, revelação, retorno ao amigo anterior, materiais, migração e cuidados | `ritual-checks.json` |
| `--mimo-bath-test` | Pegar a esponja e o chuveiro, espuma persistente, água gradual, soltar ferramentas e tocar diretamente no corpo | `bath-tool-checks.json` |
| `--mimo-ingredient-pointer-test` | Manipuladores reais de arraste dos cartões, distinção entre rolagem e ingrediente, cancelamento e perda de foco | `ingredient-pointer-checks.json` |
| `--mimo-creation-test` | Todas as 53 opções, geometrias distintas, seis acabamentos, reutilização de malhas, combinações e salvamento | `creation-checks.json` |
| `--mimo-interaction-test` | Movimentação, deformação, recuperação e mudança de ambientes | `interaction-checks.json` |

Exemplo: `MimoGarden.exe --mimo-bath-test --mimo-test-output "C:\MimoTestes\Banho"`. Os testes de ritual, banho, catálogo e interação também registram capturas. Uma janela oculta pode produzir imagens sem o conteúdo esperado. Para verificar outra proporção, acrescente, por exemplo, `-screen-width 1280 -screen-height 800 -screen-fullscreen 0` ou use 1200×900 para 4:3.

Com o Play parado, **Mimo > Validar nascimento no Editor** prepara uma receita rosa usando arraste e mistura, sem orelhas nem rabinho, e segue até a casa. Confere os componentes reais do corpo e a cor do catálogo, cobrindo diferenças entre o Editor e o executável. Usa salvamento isolado em `work`, escreve `MimoGardenPreview/Regressao-nascimento-editor.json` e sai do Play ao terminar.

## Gerar versões

**Windows:** com o Play parado e as cenas salvas, use **Mimo > Gerar versão Windows**. O comando recria a cena e exporta para a pasta vizinha `MimoGardenPlayable`.

**Android:** no Unity Hub, a instalação **6000.6.0f1** precisa dos módulos **Android Build Support**, **Android SDK & NDK Tools** e **OpenJDK**. Use **Mimo > Gerar APK Android de teste**. Se necessário, o comando ativa Android, aguarda a recompilação e inicia a geração.

O resultado é `MimoGardenAndroid/MimoGarden.apk`: APK único ARM64 e ARMv7, IL2CPP, Android 8.0/API 26 ou posterior, OpenGL ES 3 e orientação paisagem. Usa a assinatura de teste padrão do Unity. Gerar e instalar localmente não exige conta Google Play e não publica o jogo em loja.

O comando aplica o Input Manager usado pelos gestos durante a exportação Android e depois restaura a configuração de entrada do Editor. Também preserva a preferência de assinatura personalizada e as cenas abertas. As opções específicas de Windows continuam no comando de Windows.

Para automatizar em uma cópia do projeto, inicie o Unity com `-batchmode -quit -buildTarget Android -projectPath "PASTA_DO_PROJETO" -executeMethod MimoGarden.EditorTools.MimoBuild.BuildAndroid -logFile "ARQUIVO_DE_LOG"`. Inclua `-buildTarget Android` desde a abertura para compilar os scripts para a plataforma correta. O log registra `MIMO_ANDROID_BUILD_RESULT Succeeded` quando a geração termina. O SDK de destino usa a versão instalada mais recente; o aparelho também precisa cumprir os requisitos gráficos e de memória do Unity. Versão, tamanho e SHA-256 da entrega devem ser conferidos em [build-info.json](../MimoGardenAndroid/build-info.json).

## Arquivos principais

- `Assets/Resources/MimoCharacter.fbx`: corpo aprovado, rosto e peças opcionais.
- `Assets/Scripts/PetActor.cs`, `.Variants.cs` e `.Accessories.cs`: aparência, movimento, expressões e peças.
- `Assets/Scripts/PetActor.Finishes.cs` e `.Bath.cs`: acabamentos, espuma e lavagem.
- `Assets/Resources/Shaders/MimoPastelFinish.shader`: superfície pastel e camada de espuma.
- `Assets/Scripts/PetCatalog.cs` e `PetState.cs`: catálogo, receita, necessidades e salvamento.
- `Assets/Scripts/MimoExperience.cs`, `.Creation.cs` e `MimoCreationStation.cs`: fluxo do jogo e ritual de criação.
- `Assets/Scripts/MimoExperience.Sidebar.cs`, `.Gestures.cs`, `.Bath.cs` e `.Environments.cs`: interface, gestos, ferramentas e ambientes.
- `Assets/Scripts/MimoPerformance.cs` e `MimoExperience.MobileWorld.cs`: perfil de desempenho móvel.
- `Assets/Editor/MimoBuild.cs` e `MimoBirthRegression.cs`: geração de versões e regressão do nascimento no Editor.

O modelo editável está na pasta vizinha `mimo-source/MimoCharacterSource.blend`. O protótipo web anterior continua em `dumpling-lab`.
