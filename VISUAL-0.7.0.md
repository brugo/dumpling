# Mimo 0.7.0 — protótipo visual

Primeira etapa de refinamento aprovada em 03/10/2026. Base anterior: commit `5324f33` (0.6.1). O objetivo é avaliar a direção artística com o jogo funcionando.

## O que mudou

- Ateliê com fundo original de uma loja de dumplings aberta para uma rua com lanternas. Jardim com bambu, pavilhão, ponte e pátio livre para os brinquedos existentes.
- Vaporeira 3D com veios procedurais de bambu, trançado entrelaçado no fundo e na tampa, alça curva e vapor suave durante a animação existente.
- Materiais de squish ajustados para reflexos mais suaves, revestimento de silicone, profundidade do gel e glitter menos intenso. Os seis acabamentos existentes continuam disponíveis.
- Iluminação mais neutra, paleta jade/coral/bambu, cozinha e banheiro com cores mais suaves. Madeira dos móveis com veios e objetos circulares com contornos mais suaves.
- Interface com destaque jade, cartões claros e fundo de leitura para o título.

## Como avaliar

Abra `../MimoGardenPlayable/Abrir-prototipo-visual-0.7.0.cmd`. Visite Ateliê, Comidinha, Banho e Brincar. Experimente os acabamentos em Texturas, aperte e gire os personagens e observe a vaporeira durante a criação.

A versão anterior continua disponível nos executáveis existentes. O protótipo Windows fica em uma pasta própria. O APK está em `../MimoGardenAndroid/MimoGarden-0.7.0-visual.apk`.

## Escopo preservado

Não há mudança de regras de ingredientes, probabilidades, raridades, coleção, armazenamento, economia ou formato de save. As atividades, personalizações e coleções já existentes foram preservadas. Os saves de aceitação são isolados dos saves normais.

Os novos fundos da loja e do jardim são ilustrações estáticas; personagens, vaporeira, móveis e interações continuam em 3D. Esta etapa não cria ruas exploráveis nem NPCs. Cozinha e banheiro preservam a estrutura 3D existente com nova paleta. O formato do personagem e suas deformações foram preservados.

## Arte e fontes

As duas ilustrações foram geradas com a ferramenta integrada `image_gen`, sem referências de marcas. Os arquivos finais estão em `Assets/Resources/Environment/MimoLanternWorkshop.png` e `MimoBambooGarden.png`; os prompts completos estão em `ArtSource/visual-prototype-prompts.json`. A imagem anterior do parque foi preservada.

## Verificação

Relatórios e capturas ficam em `../MimoGardenPreview/0.7.0/`. Criação/banho: 83 checks; materiais: 188 checks; interface em perfil móvel e proporção 4:3: 192 checks, todos aprovados e com save normal preservado. As capturas de criação e interface usam renderização fora da tela, pois uma janela de teste oculta no Windows pode não apresentar um framebuffer. O helper de captura só é acionado pelos testes.

O perfil móvel executado no computador não substitui a validação de desempenho em tablet físico. Não houve instalação automática no dispositivo nesta etapa.

O teste antigo de desempenho foi atualizado para usar o amigo ativo após o nascimento (a prévia oculta da receita é um objeto diferente desde a introdução do grupo de amigos). A mesma checagem agora captura a imagem renderizada fora da tela, sem depender de uma janela visível. Nenhuma regra de sucesso foi relaxada.

Resultado da checagem atualizada: 44 checks aprovados, totalizando 507 nas quatro suítes. Perfil móvel no Windows, 1280 × 800, RTX 4060: mediana e percentil 95 de aproximadamente 16,7 ms por quadro em Ateliê, Comidinha, Banho e Brincar. Agrupamento dos ambientes estáticos: 401 renderizadores para 64. Relatório: `../MimoGardenPreview/0.7.0/release-performance-verified/performance-checks.json`.
