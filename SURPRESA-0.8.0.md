# Mimo 0.8.0 — experimento de criação surpresa

Protótipo solicitado em 05/10/2026 para comparar duas formas de criar dumplings. Parte da vaporeira refinada da versão 0.7.2.

## Como brincar

Há dois botões de criação no rodapé dos ambientes de cuidados: **Ateliê** e **Surpresa**. Um seletor no alto dos ingredientes também permite trocar entre eles, inclusive antes de ter o primeiro amigo. Trocar de modo começa uma receita vazia; os amigos já guardados permanecem no parquinho.

No **Ateliê**, a criança continua escolhendo diretamente cor, partes, acessórios e acabamento, como antes.

Na **Criação surpresa**, ela arrasta farinha, ovo e leite para a vaporeira, podendo repetir qualquer ingrediente. Cada arraste válido adiciona uma porção ao contador. Arrastes cancelados ou soltos fora da vaporeira não contam. Pode usar de 1 a 12 porções no total; não precisa colocar um de cada nem encher a vaporeira. Depois de misturar com o dedo, toca em **Revelar surpresa!**.

A tampa fica escondida durante o preparo. A revelação usa a animação de vapor existente, seguida da escolha do nome e do mesmo parquinho. O limite existente de cinco amigos e a confirmação para substituir um deles continuam valendo para os dois modos.

## O que é aleatório

Cor, orelhas, rabo, olhos, óculos, chapéu e textura são sorteados entre as opções gratuitas existentes. Ausência de orelhas, rabo, óculos ou chapéu também é um resultado possível. **Asas são sempre zero no modo surpresa**; os amigos antigos e as opções do Ateliê não foram removidos nesta experiência. Kits da loja não participam do sorteio.

Uma receita repetida, inclusive **1 farinha + 1 ovo + 1 leite**, recebe um novo sorteio. A receita não é usada como semente fixa. Coincidências continuam possíveis; a implementação não promete que dois resultados consecutivos sejam diferentes. O sorteio ocorre uma única vez ao finalizar, e cliques repetidos ou recuperação da mesma revelação não sorteiam outro amigo.

Quantidades desiguais influenciam chances, sem impor um resultado:

| Mais deste ingrediente | Tendência |
| --- | --- |
| Farinha | Acabamentos macio e pérola |
| Ovo | Rosa marshmallow, pêssego e baunilha; maior chance de usar chapéu |
| Leite | Pérola, gelatina e gel com glitter |

Com quantidades iguais, todas as opções de cada categoria permitida têm o mesmo peso. Com quantidades diferentes, cada opção mantém peso mínimo 1; a preferência acrescenta até 3. Isso preserva possibilidades fora da tendência. A quantidade não cria raridade, qualidade superior ou um personagem mais valioso. O sorteio é gratuito, sem anúncios, moedas ou compras.

## Estrutura

- `MimoSurpriseRecipe.cs`: opções e pesos; gerador independente da aparência anterior e da animação.
- `MimoExperience.Surprise.cs`: modo, contadores, botões e painel.
- `MimoCreationStation.Kitchen.cs` e `MimoKitchenIcon.cs`: ingredientes 3D e ilustrações de cozinha desenhadas em código.
- `MimoSurpriseCheck.cs`: verificação opt-in por `--mimo-surprise-test`, sempre com save isolado.

O resultado usa os campos existentes de `PetSave`; não há migração de saves nem armazenamento paralelo de amigos.

## Validação

Relatórios e capturas em `../MimoGardenPreview/0.8.0/`. O teste de surpresa passou 59 verificações: 28 mil sorteios, variedade com receita repetida, efeitos das quantidades, ausência de asas/kits, arraste real do cartão, cancelamento, limite de porções, mistura obrigatória, revelação única, persistência e convivência dos dois modos no parquinho.

Também passaram o ritual anterior (89 verificações), interface incluindo o novo destino (204 verificações em 1200 × 900) e gerenciamento de amigos (63 verificações, incluindo confirmação de substituição no limite de cinco). Todos usam saves isolados e conferem que o save normal permaneceu byte a byte igual. O modo surpresa foi testado em 1280 × 800 e repetido em 1200 × 900; suas capturas e a navegação em 4:3 foram inspecionadas visualmente.

Aparelho não detectado por USB nesta sessão. A entrega Android é um APK compilado para ARM64/ARMv7, versão 0.8.0/código 16; instalação e teste físico dessa versão dependem da reconexão do tablet.
