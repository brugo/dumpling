# Vaporeira 0.7.2 — revisão visual e técnica

Revisão de 03/10/2026 sobre a entrega 0.7.1, commit `0fdc810`. O trabalho original encerrou às 21:51 UTC; esta revisão começou depois, em `C:\Users\brugo\Documents\Codex\2026-10-03\task\work\MimoSteamerReview`.

## Referência e escopo autorizado

A conversa local **Analisar prompt de refinamento**, ID `01a10364-4441-76b0-b5db-0e5e5d754473`, contém um prompt completo colado e uma imagem anexada: `codex-clipboard-59445a41-7608-4cca-9514-13d6ea619330.png`. A imagem foi recuperada do conteúdo original e inspecionada visualmente; cópia e procedência estão em `ArtSource/SteamerReference.png` e `SteamerReference-source.txt`. Ela mostra a lateral, não a tampa. Não foi presumida uma segunda imagem de tampa.

O usuário primeiro pediu análise do prompt, depois autorizou começar pelo protótipo visual. Em seguida especificou a tampa oculta durante a escolha/mistura e o trançado em toda a lateral e sobre a tampa. Esta entrega permanece nesse escopo.

| Critério textual relativo à vaporeira | Resultado verificado |
| --- | --- |
| Formato circular tradicional | Preservado; corpo oco com cinco faixas largas e pares de hastes finas. |
| Bambu natural, bege quente/amarelo claro/marrom suave, variação discreta | Fibra gerada no ChatGPT aplicada às tiras e bordas. Tonalidade normalizada para preservar a paleta; brilho suave. Avaliação visual, não alegação de reprodução fotográfica. |
| Lateral com textura, fundo interno entrelaçado | Geometria contínua preservada; UVs acompanham as fibras. Pontas verticais terminam sob a amarração. |
| Tampa trançada e pequena alça central | Preservadas; verso das tiras renderizado e suporte interno aproximado à trama para fechar os vãos vistos em ângulo rasante. Contorno externo e posição da alça mantidos. |
| Esconder tampa enquanto a criança escolhe, despeja e mistura | Ciclo da 0.7.1 preservado e novamente aprovado nos testes, incluindo recuperação da receita. |
| Tampa entra ao finalizar, fecha, vapor/movimento, abre e revela o squish | Animação existente preservada; capturas durante o jogo e em poses de inspeção. Ingredientes e aparência final permanecem correspondentes à receita. |
| Suspense sem mostrar o resultado antes de abrir | Personagem permanece oculto durante preparo e antecipação; teste de ritual aprovado. |
| Leve para tablets | Sem aumento na contagem de triângulos/renderizadores do modelo. Uma textura compartilhada, importada a 512 px, com mipmaps; sem novos efeitos de pós-processamento. Medição no tablet pendente. |

O prompt geral também propõe squishies gelatinosos/glitter interno, variação modular de rostos e materiais, surpresa ponderada por ingredientes, raridades e proteção contra frustração, livro de coleção, utilidade para duplicatas, armazenamento expansível, cenário chinês estilizado vivo, interface e som refinados e revelações por raridade. Pede mudanças incrementais, dados configuráveis, testes e preservação dos saves, sem sorteios pagos. **Esses sistemas gerais não foram implementados ou redesenhados nesta revisão da vaporeira.** A criação determinística existente, loja simulada, kits, caudas, amigos e formato de persistência foram preservados; não há nova revelação condicionada à raridade.

## Mudanças técnicas

- `Assets/Resources/Textures/BambooGrain.png`: arte original do gerador integrado do ChatGPT; prompt em `ArtSource/BambooGrain-prompt.txt`. Nenhuma compra de asset ou serviço externo pago.
- `MimoWovenBamboo.shader`: fibra pela textura, verso das tiras visível com normal apropriada; material opaco em um passe.
- `MimoBoundBamboo.shader`: material próprio das bordas/alças da vaporeira, evitando mudar o shader compartilhado com móveis do cenário.
- `MimoCreationStation.cs` e `.Weave.cs`: UVs de anéis, casca e pequenas peças preservados pelo agrupamento de malhas; hastes aparadas e suporte da tampa corrigido. Regras de interação, receitas, animação e saves não foram alteradas.
- `MimoBambooTextureImporter.cs`: importação limitada ao asset novo, 512 px, mipmaps, filtro trilinear, anisotropia 2 e compressão.
- `MimoSteamerReview.cs`: inspeção opt-in por `--mimo-steamer-review --mimo-test-output <pasta>`, com save isolado, checagem de UVs/textura e oito capturas reais. Não é ativada numa sessão normal.

## Verificação final

Windows Unity 6000.6.0f1, RTX 4060, perfil móvel, 1280 × 800; inspeções em 1280 × 960. Todos os relatórios finais são os da pasta `accepted-*`, e não os checkpoints intermediários.

| Suíte | Checks aprovados |
| --- | ---: |
| Ritual, ingredientes, tampa, revelação e banho | 89 |
| Desempenho e ambientes | 44 |
| Loja, kits e persistência | 180 |
| Reabertura do save da loja | 9 |
| Caudas | 299 |
| Gerenciamento dos amigos | 63 |
| Total das suítes | **684** |

A inspeção adicional da vaporeira também passou, sem erros de UV, malha, textura ou alteração do save habitual. A textura no player Windows mede 512 × 512, 10 níveis de mipmap, formato BC7. As capturas cobrem cena real aberta/fechada, frente, oblíqua, topo, abertura, interior e lado traseiro. A comparação usa a referência recuperada e a mesma câmera/iluminação para 0.7.1 e 0.7.2, sem retoque das capturas.

Contagem da estação: 42.656 triângulos e 7 renderizadores abertos; 62.470 triângulos e 16 renderizadores durante a pose fechada com efeitos. Contagens iguais à base 0.7.1. O laboratório completo usa 43.426 triângulos e 11 renderizadores. Mediana/p95 próximos de 16,67 ms nos quatro ambientes do PC, com escala 0,85. São observações de regressão com frame pacing em uma RTX 4060, **não uma medição de FPS Android**.

Builds Windows e Android concluídos com sucesso. APK: `com.mimogarden.prototype`, versão 0.7.2, código 15, ARM64 + ARMv7. O ADB não listou dispositivos; não houve instalação, medição física, teste de toque real ou alteração dos dados do tablet. Saves de QA ficaram em pastas separadas e as suítes confirmaram preservação byte a byte do save habitual.

## Entrega

Build Windows versionado: `../MimoGardenPlayable/0.7.2-visual/MimoGarden.exe`; launcher `../MimoGardenPlayable/Abrir-prototipo-visual-0.7.2.cmd`. APK: `../MimoGardenAndroid/MimoGarden-0.7.2.apk`. Evidências: `../MimoGardenPreview/0.7.2/`, com comparação, capturas, logs e JSONs. As versões anteriores permanecem disponíveis.

A integração local deve avançar o checkout original somente após confirmar que ele continua limpo e descendente da base revisada. Nenhuma publicação ou envio a remoto foi solicitado ou executado.
