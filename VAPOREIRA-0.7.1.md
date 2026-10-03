# Mimo 0.7.1 — vaporeira de bambu

Refinamento de 03/10/2026 a partir da fotografia fornecida pelo usuário.

## Comportamento

A tampa fica oculta durante a escolha, o despejo e a mistura dos ingredientes, inclusive ao retomar uma receita. Somente o comando de finalizar a criação inicia sua entrada por cima da vaporeira. A animação existente fecha, solta vapor e abre a tampa para revelar o personagem; ao terminar, a tampa sai com a vaporeira.

## Aparência

Cinco faixas largas percorrem a lateral, alternando por cima e por baixo de 32 pares de hastes finas. O fundo e a tampa têm tiras contínuas cruzadas, com relevo, veios acompanhando o sentido das fibras e variação discreta de tom. A tampa tem superfície abaulada e acabamento circular que cobre as pontas das tiras.

O material é procedural, feito para as malhas do jogo. Não utiliza uma imagem baixada nem exige textura externa em alta definição. Os componentes estáticos são agrupados por material. Implementação em `MimoCreationStation.Weave.cs` e `MimoWovenBamboo.shader`.

## Entrega e verificação

Windows: `../MimoGardenPlayable/Abrir-prototipo-visual-0.7.1.cmd`. Android: `../MimoGardenAndroid/MimoGarden-0.7.1.apk`, versão 0.7.1, código 14. A entrega anterior permanece em sua própria pasta.

O teste de criação e banho no Windows, com perfil móvel em 1280 × 800, passou as 89 verificações, incluindo seis verificações do ciclo de visibilidade da tampa. O save normal permaneceu idêntico. As capturas finais foram inspecionadas para conferir as laterais, o acabamento da tampa e a área livre durante a mistura. Relatórios em `../MimoGardenPreview/0.7.1/`.

As 44 verificações de desempenho e funcionamento também passaram na versão Windows entregue. O ateliê apresentou 11 renderizadores ativos e 43.426 triângulos, com mediana e p95 próximos de 16,67 ms no computador com RTX 4060. Esses tempos não são uma medição de Android.

Os builds Windows e Android terminaram com sucesso. O manifesto do APK foi conferido: pacote `com.mimogarden.prototype`, versão 0.7.1/código 14, ARM64 e ARMv7. O tablet não foi detectado pelo ADB nas verificações desta entrega; a instalação e a medição da 0.7.1 no dispositivo permanecem pendentes. Nenhum dado do tablet foi alterado nesta etapa.

As regras de ingredientes, os personagens existentes e o formato dos saves permanecem compatíveis.
