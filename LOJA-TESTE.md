# Mimo 0.6.0 — coleções e cristais de teste

Esta versão não contém anúncios, não cobra dinheiro e não se conecta ao Google Play Billing. O botão **Coleções** abre três conjuntos completos. A imagem de cada cartão é renderizada com o mesmo personagem, modelo e materiais usados no jogo; toque nela ou em **Girar prévia** para ver outros lados.

| Coleção | Conteúdo | Custo provisório |
|---|---|---|
| Jardim de Aurora | Pele floral perolada, coroa de margaridas, asas de folhas | 150 cristais de teste |
| Guardião da Lua | Pele de constelações, chapéu lunar, capa de estrelas | 180 cristais de teste |
| Pérola do Mar | Pele de conchas, boina-concha, colar de pérolas, nadadeiras | 220 cristais de teste |

As texturas e o cristal foram gerados pelo ChatGPT. Os prompts, dimensões e hashes estão em `Assets/Resources/Cosmetics/ImageGen-provenance.json`. Os acessórios são modelos 3D com UV, superfícies curvas, bordas arredondadas e materiais coordenados. O código Blender e os arquivos editáveis ficam em `ArtSource`.

## Testar sem dinheiro real

1. Abra **Coleções** e examine os três conjuntos. As opções gratuitas anteriores permanecem livres.
2. Entre em **Cristais • responsáveis** e peça a um adulto para preencher a resposta. O acesso é temporário e termina ao sair da loja ou perder o foco. Este filtro simples é apenas parte da experiência de teste, não autenticação financeira.
3. Escolha 200, 500 ou 1.000 cristais. Os preços em reais são exemplos provisórios; a tela seguinte diz **SIMULAÇÃO — nenhuma cobrança**.
4. Escolha **Aprovar teste**, **Cancelar compra**, **Simular falha** ou **Deixar pendente**. Só a aprovação credita cristais. Um pedido pendente pode ser retomado na área dos responsáveis, inclusive depois de reabrir o jogo.
5. Toque em **Desbloquear**, confira o conteúdo, o custo e o saldo depois do gasto, e confirme. Depois use o conjunto no amigo selecionado ou na receita em andamento.
6. **Tirar coleção** devolve a aparência gratuita anterior. Não há sorteios, contadores de urgência, ofertas personalizadas ou assinatura.

## Persistência e limites

O progresso dos amigos continua em `mimo-pet.json`. A receita ganhou o campo opcional `cosmeticKitId`; arquivos anteriores continuam compatíveis. O conjunto equipado é salvo com o amigo e removê-lo preserva cor, textura e peças gratuitas anteriores.

O saldo simulado e os conjuntos adquiridos ficam em `mimo-pet.json.shop-test.json`. Crédito, token de pagamento e estado são gravados juntos. Débito e desbloqueio também são uma única gravação. A gravação usa arquivo temporário, flush e substituição atômica. Falha de escrita preserva o estado anterior; repetir uma aprovação registrada não repete o crédito. Respostas de produto diferente, tokens desconhecidos, cancelamentos e falhas não concedem saldo.

Um registro principal corrompido, ou ausente quando ainda existe backup/temporário, bloqueia as compras e preserva os arquivos. O saldo aparece indisponível em vez de ser confundido com zero de uma nova instalação. A recuperação exige conferir o ledger mais recente; o teste conserva a evidência e não credita novamente. A loja não volta automaticamente a um backup antigo, pois isso poderia repetir créditos já entregues. Este saldo é local, de teste e editável pelo dono do aparelho; não constitui uma implementação segura de dinheiro real. Desinstalar pode apagar saldo e coleções locais. Não há promessa de restauração de consumíveis após reinstalação.

## Integração oficial Google Play pendente

- Conta Play Console e app configurados pelo proprietário; contratos e dados comerciais aceitos pelo proprietário.
- Produtos consumíveis oficiais, IDs próprios e preços definidos na Console; consultar detalhes e preços localizados da loja em vez dos exemplos deste catálogo.
- Adaptador real de `IMimoPayments`, dependência de Billing compatível e inicialização/reconexão ao voltar ao app. O adaptador simulado não deve existir no canal de produção.
- Backend autenticado que valide cada purchase token, produto, estado PURCHASED e dono, com ledger durável e idempotente. Pending nunca libera cristais. Crédito e marcação do token devem ser uma transação no backend.
- Consumir a compra apenas após validar e entregar o conteúdo; retomar entrega/consumo em falhas de rede sem duplicar crédito. Cancelamentos, reembolsos e notificações de servidor exigem tratamento correspondente.
- Política de recuperação de saldo e coleções com conta/backend validado. Tokens de consumíveis já consumidos não restauram automaticamente um saldo local apagado.
- Assinatura de distribuição, canal de teste interno, license testers e contas/aparelhos Google compatíveis; testar instrumentos de aprovação, recusa e compra pendente oficiais.
- Revisão de acesso para responsáveis, privacidade, declarações de público infantil, classificação, política de dados e requisitos de publicação no momento da preparação comercial.
- Testes físicos de toque, desempenho, memória e sessões longas em Samsung e Amazon. Um APK de simulação pode rodar no Amazon; isso não valida Google Play Billing.

Fontes oficiais consultadas: [integração de Billing](https://developer.android.com/google/play/billing/integrate) e [testes oficiais](https://developer.android.com/google/play/billing/test).

## Verificações reproduzíveis

Execute o Windows player com `--mimo-shop-test --mimo-mobile-profile --mimo-test-output "PASTA"`. O teste usa arquivos isolados e cobre a UI real, cancelamentos, pending/retomada, falha, callback duplicado, interrupção da gravação, débito/desbloqueio, equipar/remover e cinco personagens premium. Inclui a loja antes do primeiro nascimento e após criar um amigo, isolamento da nova receita, falha real de gravação ao retirar coleção, perda do arquivo principal com backup e igualdade dos materiais sobre os seis acabamentos gratuitos. Depois encerre e inicie um **novo processo** com `--mimo-shop-restart-test --mimo-mobile-profile --mimo-test-output "MESMA_PASTA"` para verificar persistência e repetição de callbacks após reinício.

Os resultados e capturas da execução efetiva acompanham a entrega. A existência deste roteiro não significa que o teste tenha sido executado em cada aparelho.
