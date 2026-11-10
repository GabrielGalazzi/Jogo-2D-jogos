# Ricochet / Ricochete — Game Design Document

| | |
|---|---|
| **Projeto** | Projeto 2D — Jogos Digitais, Jogos e Interação 2026.1 (Insper) |
| **Título** | **Ricochet** (inglês) / **Ricochete** (português) |
| **Equipe** | Gabriel Galazzi, João Pedro Sardou |
| **Gênero** | Tiro 2D de rolagem lateral com mira de precisão por ricochete |
| **Engine** | Unity 6 (6000.6.6f1), URP 2D, Input System |
| **Plataforma** | WebGL (itch.io), janela 1680×840 em paisagem |
| **Idiomas** | Português (pt-BR) e inglês |
| **Repositório** | https://github.com/GabrielGalazzi/Jogo-2D-jogos |
| **Página no itch.io** | Publicação em 10/11/2026, à tarde: [link] |
| **Entrega** | 10/11/2026, 23:50 |
| **Rubricas com peso dobrado** | Mecânicas · Efeitos Visuais e Polimento Gráfico |
| **Rubricas desconsideradas** | Conceito e Narrativa · Rejogabilidade |
| **Última atualização deste documento** | 10/11/2026 |

> Este GDD é um documento vivo. A seção 19 registra, em ordem, cada mudança de rumo do projeto, com a data e o motivo.

---

## 1. Visão geral

### 1.1 Pitch

Uma partida de paintball dentro de um prédio moderno de universidade, com uma regra só: **um tiro só elimina depois de
ricochetear**. Acertar o oponente de frente não faz nada. É preciso ler a sala, usar o chão, o teto, as paredes e os painéis
inclinados, e acertar de tabela. Ao longo de 10 fases, o jogador atravessa salas cada vez mais bem defendidas até enfrentar
O Guardião, o segurança blindado do campus.

### 1.2 Pilares de design

1. **O ricochete é a arma.** Todo tiro que conta é um tiro de tabela, e toda superfície sólida é parte do arsenal.
2. **Pensar antes de atirar.** A mira laser mostra o primeiro ricochete, mas acertar ainda pede leitura de ângulo e de tempo.
3. **Justiça.** A mesma regra vale para os dois lados: tiro inimigo também só machuca depois de ricochetear. Ninguém atira em
   quem ainda não viu, e cada fase começa numa zona segura.
4. **Clareza.** O jogo comunica o estado das coisas pelo próprio mundo: cor e brilho dos tiros, a linha de mira dos inimigos,
   a armadura do chefe, a placa de saída e o splash pequeno de tiro inofensivo contra o grande de um tiro que ricocheteou antes.
5. **Progressão gradual.** Cada fase acrescenta uma ideia nova, melhora a precisão dos inimigos e as fases intermediárias existem para o jogador dominar o que
   já aprendeu.

### 1.3 Público

Jogadores que gostam de *bullet hell* e de jogos 2D de rolagem lateral com foco em mira de precisão.

---

## 2. Conceito, mundo e personagens

### 2.1 Ambientação

Um prédio universitário **moderno**: laminado branco e cinza-claro, alumínio escovado, grafite, vidro jateado, concreto
polido e fitas de LED. Nada de madeira velha ou tijolo. Os canos e painéis de luz pendurados no teto dão a sensação de um
espaço fechado, meio industrial, que era o clima que queríamos. Os obstáculos são peças do próprio prédio:

| Elemento do mundo | Função na jogabilidade |
|---|---|
| Bancadas de laboratório | Cobertura baixa (meia altura do jogador) |
| Dutos suspensos com LED | Blocos baixos no teto, onde inimigos podem se pendurar |
| Canos de alumínio pendurados no teto | Plataformas |
| Canos com faixas de perigo | Plataformas |
| Painéis de luz quebrados, presos por um só cabo | Placas inclinadas (30° a 60°) para tiros de tabela |
| Divisória de laminado com porta de vidro jateado | Parede e porta da zona segura |
| Porta de segurança de aço | Entrada da arena do chefe |
| Portas de vidro com placa EXIT | Saída da fase |

### 2.2 Personagens

| Personagem | Descrição | Papel |
|---|---|---|
| **Jogador** | Estudante de moletom azul, calça jeans escura, tênis brancos, óculos de paintball no rosto| Protagonista |
| **Variante vermelha** | Estudantes de moletom vermelho e máscara preta de visor escuro | Inimigo padrão |
| **Variante laranja (Twin)** | O mesmo uniforme, em laranja | Atira dois tiros em leque |
| **Variante roxa (Heavy)** | O mesmo uniforme, em roxo | Atira um tiro grande e lento |
| **O Guardião** (*The Warden*) | Presidente da universidade, enorme, com armadura de aço nos ombros, no capacete, no peito e nas costas, e faixas amarelas de alerta | Chefe final (fase 10) |

As variantes laranja e roxa são **recores** do inimigo vermelho, e não desenhos novos. A prioridade é o jogador identificar
na hora o que é inimigo e o que não é, e um visual novo para cada tipo atrapalharia isso. Só o chefe tem um desenho próprio.

### 2.3 Narrativa

O jogo não tem história. Conceito e Narrativa é uma das duas rubricas que combinamos com o professor de desconsiderar: o
foco do projeto está nas mecânicas e no polimento visual. A ambientação e os personagens acima existem para dar identidade
visual ao jogo e deixar claro o que é inimigo e o que é cenário.

---

## 3. Mecânica central: a regra do ricochete

- Todo tiro do jogador sai **"fresco"**: uma bola de tinta laranja e fosca, inofensiva.
- Ao ricochetear pela primeira vez em qualquer superfície sólida, ele fica **"vivo"**: vira ciano brilhante, ganha um halo
  pulsante e um rastro, com um flash e um som de "armado" no ponto do quique. A partir daí, ele elimina.
- O ricochete segue a lei da reflexão (ângulo de entrada = ângulo de saída), com até **4 ricochetes** por tiro. Depois do
  último, o tiro se desfaz em tinta na parede.
- **Acerto direto não faz nada:** o inimigo leva um splash pequeno e continua de pé.
- **A regra vale para os dois lados:** o tiro inimigo sai magenta e inofensivo e só passa a ferir o jogador depois de
  ricochetear, quando fica vermelho e brilhante.

---

## 4. Mecânicas do jogador

### 4.1 Movimento

| Ação | Tecla | Detalhes |
|---|---|---|
| Andar | A / D | 7 m/s |
| Correr | Shift (segurar) | ×1,5 a velocidade, para conseguir desviar de certos tiros. Agachar cancela a corrida. |
| Pular | W | Pulo com gravidade aumentada, para um arco curto e controlável |
| Agachar | S (segurar) | A altura e a hitbox diminuem 40%; anda com metade da velocidade |

### 4.2 Mira, laser e tiro

- A arma segue o mouse e fica nas mãos do personagem em todas as animações, inclusive agachado e no ar.
- O personagem vira para o lado em que o mouse está.
- **Mira laser:** uma linha vai da ponta do cano até o primeiro ricochete e continua um pouco depois dele, apagando aos
  poucos. Um ícone ciano marca o ponto do quique: é ali que o tiro passa a ser letal. O laser para em inimigos, como o tiro
  pararia. Nos testes, mirar ricochetes era difícil demais para a maioria dos jogadores, e o laser veio para resolver isso.
- **Tiro:** clique esquerdo, com intervalo mínimo de 0,15 s.

### 4.3 Pente e recarga

- Pente de **6 tiros**. Ao esvaziar, a recarga de **2 s** começa sozinha.
- **Por quê:** com tiros à vontade o jogo ficava fácil demais, e a recarga de 2 s evita que o jogador saia atirando sem
  parar.
- A recarga tem animação: a arma inclina e a mão da frente despeja um pote de tinta no reservatório.
- O HUD do pente fica no canto inferior esquerdo (seção 10.1).

### 4.4 Vida

- **3 corações.** Cada tiro inimigo vivo tira um coração, e o jogador fica invulnerável por 0,75 s depois de ser atingido.
- Três vidas é um clássico dos videogames: permite alguns erros, mas não muitos.
- Os corações ficam no canto inferior direito, na mesma altura do pente.

### 4.5 Arma dupla

- Fica flutuando na zona segura da fase 6, com um brilho dourado pulsante. Basta encostar nela para pegá-la, e ela continua
  disponível em todas as fases seguintes. Toda fase começa com a pistola na mão.
- **Por quê:** facilita lidar com hordas de inimigos, e a fase 6, com inimigos posicionados para se cobrirem, incentiva
  exatamente isso.
- **F** troca de arma. A troca leva 0,5 s, e nenhuma das duas atira durante ela. Depois, F fica travado por 1 s, para evitar
  abuso da troca.
- É a mesma arma dos inimigos laranja: **dois tiros abertos em 4°** (±2°), um tiro do pente cada, com intervalo mínimo de 0,4 s.
- O pente é compartilhado entre as duas armas. Com **uma bala restante, só o cano de cima dispara**.
- Cada cano tem sua própria linha de laser. Com uma bala, só a de cima aparece.
- No HUD, um painel ao lado do pente mostra o nome da arma em uso. Ele se enche durante a troca e fica esmaecido enquanto
  F está travado.

---

## 5. Inimigos

### 5.1 Comportamento comum

- **Só atiram depois de terem aparecido na tela pelo menos uma vez**, para nunca atingirem o jogador de fora da tela.
- **Não atiram enquanto o jogador está na zona segura.**
- **Mira por ricochete:** como só tiro ricocheteado fere, o inimigo calcula por traçado de raios um tiro de tabela no chão,
  no teto, nas paredes ou em objetos do cenário, prevendo onde o jogador vai estar.
- **Linha de mira:** ao disparar, o inimigo deixa por um instante uma linha fraca com o caminho que planejou. É o aviso de
  onde o tiro vai quicar.
- **Previsão suavizada:** a previsão usa a média da velocidade dos últimos 5 passos de física. Assim, uma inversão de direção
  que dura um instante não faz o inimigo atirar para o lado errado.
- **Ruído de mira:** cada ricochete inimigo recebe um desvio aleatório de distribuição normal, para que ele possa errar e não
  vire "aimbot". O desvio diminui fase a fase (seção 8).
- **Agachar, a partir da fase 4:** cada tiro letal do jogador prestes a acertar um inimigo tem 10% de chance de fazê-lo
  agachar. Só salva de tiros retos na altura da cabeça.
- Inimigos comuns nunca andam: ficam no lugar, alguns pulando.

### 5.2 Tipos

| Tipo | Aparece a partir de | Descrição |
|---|---|---|
| Padrão (vermelho) | Fase 1 | Fica no chão ou em plataformas e atira um tiro por vez |
| Pendurado | Fase 4 | O mesmo inimigo, de cabeça para baixo, preso ao teto ou à parte de baixo de um duto |
| Saltador | Fase 4 | Pula sem parar, mudando a altura de onde atira |
| Twin (laranja) | Fase 8 | Dois tiros em leque |
| Heavy (roxo) | Fase 8 | Um tiro grande e lento |

### 5.3 Posições aleatórias

- Toda vez que uma fase carrega, os inimigos recebem posições novas. Cada um mantém o tipo e o lado em que fica (chão ou
  teto).
- **Por quê:** para a fase não ficar chata depois de o jogador morrer várias vezes nela.
- Inimigos a até 3 m um do outro formam um grupo, que divide um conjunto de pontos possíveis: pelo menos 1,7 ponto por inimigo,
  sorteados a até ±3 m (±6 m em trechos apertados). A variação é baixa de propósito: o jogador se adapta, em vez de encontrar
  uma configuração totalmente nova a cada tentativa.
- Nenhum ponto pode deixar o inimigo flutuando ou sobreposto a outro: o corpo inteiro precisa apoiar numa superfície plana,
  com espaço para o pulo.

---

## 6. O chefe: O Guardião (fase 10)

| Aspecto | Regra |
|---|---|
| Vida | 6 pontos |
| Armadura | O corpo é dividido em três regiões: cabeça, frente e costas. Uma região precisa levar **dois** tiros vivos para perder 1 de vida: o primeiro só racha a armadura (uma rachadura amarela aparece) |
| Armas | Alterna entre as armas dos inimigos: padrão, Twin e Heavy |
| Esquiva | Tem 33% de chance de notar um tiro letal e tentar desviar, agachando, pulando ou dando um passo para o lado. A chance é sorteada uma vez por tiro. Desviar de todos os tiros seria injusto com o jogador. Ele tem um tempo de reação (0,12 s), então tiros que quicam perto dele ainda acertam. Depois de desviar, volta ao meio da sua área |
| Fase 2 (2/3 da vida) | Saca uma segunda arma e fica com ela até morrer. A armadura fica preta e ele fica invulnerável por 4 s, atirando com as duas armas duas vezes por segundo |
| Fase 3 (1/3 da vida) | Salta para o teto e vira um inimigo de teto, com a mesma rajada invulnerável. Lá em cima ele fica: só agacha ou anda para o lado |
| Leitura visual | Armadura em azul-aço claro, preta só enquanto ele está imune |

**Por que as fases:** a luta estava monótona e constante. As fases, com a rajada invulnerável, dão ritmo à luta e mantêm o
jogador engajado.

**Arena:** uma sala fechada no fim da fase 10, maior que o resto da fase, com uma porta de mão única igual à da zona segura.
Depois que o jogador entra, não sai mais, e a câmera fica dentro da arena. Ela existe para o jogador não se afastar do chefe e
"trapacear" na luta, atirando enquanto o chefe está fora da tela. A porta da arena só abre depois que todos os guardas de fora
caem, para ninguém correr direto para o chefe sem limpar a fase. A vitória vem quando o chefe cai.

---

## 7. Estrutura de uma fase

```
[ zona segura | porta ] ──────── sala de 50 m: obstáculos e inimigos ──────── [ EXIT ]
```

1. **Zona segura:** a fase começa atrás da parede do fundo da sala. Ali os inimigos não atiram, e nas fases 1 e 6 ficam os
   quadros de tutorial. Ela existe para o jogador não começar direto na ação: ele tem todo o tempo que quiser para planejar
   e ler os tutoriais.
2. **Porta de mão única:** quando o centro do jogador passa do meio da porta, ele é deslizado suavemente até o outro lado, e
   só então os inimigos começam a atirar. Não dá para voltar com tiros chegando. Tiros só atravessam a porta entrando na
   zona segura, nunca saindo, evitando que o jogador faça um ricochete de angulo reto na parede a sua esquerda para eleminar todos os inimigos em solo facilmente.
3. **A sala:** 50 m de comprimento por 12 m de altura, com chão, teto e obstáculos. Todas as superfícies são de ricochete.
4. **Saída:** a placa EXIT fica vermelha enquanto a porta está trancada e verde quando todos os inimigos caem. Atravessá-la
   leva à fase seguinte. Como a saída só abre com a sala limpa, não dá para correr até o fim da fase ignorando os inimigos.
5. **Fase 10:** no lugar da saída, a arena do chefe (seção 6).

---

## 8. Level design e progressão

A dificuldade sobe de três formas: mais inimigos, ferramentas novas e inimigos mais precisos e rápidos no gatilho.

| # | Nome (EN / PT) | Ideia nova | Inimigos | Intervalo de tiro inimigo | Ruído de mira |
|---|---|---|---|---|---|
| 1 | Boot Camp / Campo de Treino | Tutorial: sala plana, inimigos de chão bem espaçados | 4 | 2,6–3,8 s | 9° |
| 2 | Low Cover / Cobertura Baixa | Bancadas no chão e dutos no teto, ambos com meia altura do jogador | 5 | 2,3–3,4 s | 8° |
| 3 | Reinforcements / Reforços | Quase o dobro de inimigos, alguns em plataformas e em duplas | 9 | 2,1–3,1 s | 7,5° |
| 4 | Upside Down / De Ponta-Cabeça | Inimigos pendurados no teto e saltadores; inimigos começam a agachar | 9 | 1,9–2,9 s | 7° |
| 5 | Deflection / Desvio | Placas inclinadas de 30° a 60° e bunkers com telhado | 9 | 1,8–2,8 s | 6,5° |
| 6 | Crossfire / Fogo Cruzado | Arma dupla na zona segura; inimigos posicionados para se cobrirem | 11 | 1,7–2,6 s | 6° |
| 7 | Kill Box / Zona de Abate | Emboscadas, bunkers espelhados e um funil de placas | 12 | 1,6–2,5 s | 5,5° |
| 8 | Heavy Arms / Armamento Pesado | Inimigos Twin (2) e Heavy (3) | 12 | 1,6–2,5 s | 5,5° |
| 9 | Arsenal / Arsenal | Armas melhoradas em toda parte: Twin (5) e Heavy (5) | 13 | 1,5–2,3 s | 5° |
| 10 | The Warden / O Guardião | Uma aproximação guardada (59 m no total) e a arena do chefe | 3 + chefe | 1,4–2,2 s | 4,5° |

Notas de design:

- **Fase 1** ensina o básico sem pressão: poucos inimigos e muito espaço. O quadro de tutorial na zona segura explica os
  controles, a pistola, o pente e a regra do ricochete.
- **Fases 6 e 7** usam o mesmo conjunto de ferramentas da 5, de propósito, para o jogador amadurecer as habilidades. O foco
  passa a ser o posicionamento estratégico: inimigos que se cobrem, um atirador de retaguarda numa plataforma alta que
  continua atirando depois que o jogador passa, emboscadas logo na saída da zona segura.
- **Fase 10** é mais longa que as outras (59 m) para caber a arena de cerca de 30 m.

---

## 9. Controles

| Tecla | Ação |
|---|---|
| A / D | Andar para a esquerda / direita |
| Shift (segurar) | Correr |
| W | Pular |
| S (segurar) | Agachar |
| Mouse | Mirar |
| Clique esquerdo | Atirar |
| F | Trocar de arma (a partir da fase 6) |
| Esc | Pausar / continuar |
| R | Reiniciar a fase |
| Enter | Confirma o botão principal das telas de morte e vitória |

Os atalhos de teste usados durante o desenvolvimento (N para a próxima fase, P para a anterior, T para ligar e desligar a
linha de mira dos inimigos) foram removidos do build final. A linha de mira dos inimigos fica sempre ligada.

---

## 10. Interface (UI/HUD) e menus

### 10.1 HUD

- **Pente (inferior esquerdo):** painel azul com seis bolas de tinta. A cada tiro, a bola da direita vira um encaixe vazio.
  Ao esvaziar, o painel fica vermelho e se enche de azul da esquerda para a direita, recarregando cada bola por onde passa.
- **Arma (ao lado do pente, a partir da fase 6):** nome da arma em uso. O painel se enche durante a troca e fica esmaecido
  enquanto F está travado.
- **Vida (inferior direito):** três corações, que esvaziam da direita para a esquerda e piscam em vermelho a cada dano.
- **Chefe:** barra com o estado da armadura (regiões rachadas e invulnerabilidade).
- **Título da fase:** aparece ao entrar na fase.
- Os textos de depuração (inimigos restantes, descrição dos controles, descrição de cada fase) foram removidos.

### 10.2 Menus

| Tela | Conteúdo |
|---|---|
| Menu principal | Título (RICOCHET em inglês, RICOCHETE em português), botão JOGAR centralizado na parte de baixo e o seletor de idioma logo abaixo. Até o jogador escolher, vale o idioma do sistema, e a escolha fica salva |
| Pausa | Esc pausa o jogo, com um botão para voltar ao menu principal |
| Morte | "VOCÊ MORREU", com os botões **Tentar de novo** e, logo abaixo, **Menu principal** |
| Vitória | "VITÓRIA! O Guardião caiu.", com botão para o menu principal |

### 10.3 Tutorial

- Quadros nas fases 1 e 6, no canto superior direito, à direita da parede da zona segura. Aparecem na hora e somem quando o
  jogador sai da zona segura.
- Regra: dizer o que cada coisa faz e como usar, **nunca números ou tempos**, porque isso não importa para o jogador.
- As teclas aparecem como teclas desenhadas (keycaps).
- **Fase 1 (Como jogar):** Andar, Correr, Pular, Agachar, Mirar, Atirar, Pistola, Pente e um cartão largo e destacado:
  "Ricochete para matar".
- **Fase 6 (Nova arma: arma dupla):** Arma dupla, Trocar de arma, Tiro duplo e Última bala.
- **Animações:** cada cartão tem uma animação em loop mostrando a mecânica. Elas são montadas com os próprios sprites do
  jogo, e uma tecla ou mouse no canto acende enquanto aquele comando está sendo usado.

### 10.4 Idiomas

Português (pt-BR) e inglês em todo texto que o jogador vê: HUD, menus, tutoriais, nomes de fase.

---

## 11. Feedback e clareza de interação

O jogo mostra os estados importantes no próprio mundo, sem depender só do HUD:

| Situação | Feedback |
|---|---|
| Tiro do jogador ficou letal | Laranja → ciano brilhante, halo pulsante, rastro, flash ciano no quique e som de "armado" |
| Tiro inimigo ficou letal | Magenta → vermelho brilhante com halo, e flash vermelho no quique |
| Onde o tiro do jogador vai ficar letal | Mira laser até o primeiro ricochete, com ícone ciano no ponto |
| Para onde o inimigo atirou | Linha fraca com o caminho planejado, por um instante, no disparo |
| Acerto direto (sem efeito) | Splash verde **pequeno** e som de tinta abafado |
| Eliminação | Explosão de tinta verde **grande**, animação de queda e som de eliminação |
| Jogador atingido | Splash da tinta inimiga no jogador, coração piscando e som de dano |
| Chefe com a armadura rachada | Rachadura amarela na região atingida |
| Chefe imune | Armadura preta |
| Saída trancada / aberta | Placa EXIT vermelha / verde |
| Porta de mão única | LED de status na porta |
| Arma dupla para pegar | Brilho dourado pulsante e a arma flutuando |
| Recarga | Animação de recarga na arma e painel do pente vermelho |
| Ricochetes seguintes | Faísca e som de ricochete |
| Tiro que termina na parede | Mancha de tinta na cor do tiro, que fica na superfície |

---

## 12. Arte e direção visual

- **Pixel art em vista lateral**, luz vinda de cima à esquerda, contorno escuro de 1 pixel (`#14161c`) e sombreamento simples
  (2 a 3 tons por material).
- **Escala única:** 30 pixels por metro em todos os sprites. Na janela de 1680×840, cada pixel de arte ocupa exatamente 2×2
  pixels de tela, sem tremulação.
- **A arte se adapta à física, nunca o contrário:** cada sprite tem o tamanho exato do colisor do objeto.
- **Cores dos times saturadas** (azul do jogador, vermelho, laranja e roxo dos inimigos, magenta e roxo da tinta inimiga)
  contra um prédio neutro e claro.
- **Fundo:** a ideia original era ter uma imagem de fundo, mas no fim ela não foi necessária. O fundo ficou um off-white
  sólido, que escurecemos um pouco porque o primeiro tom era claro demais.
- **Animações:** jogador (parado, andar, correr, subir, cair, aterrissar, agachar, andar agachado, morte), inimigos (parado,
  pulo, agachar, morte) e chefe (parado, andar, correr, pulo, agachar, morte). As armas são seguras com as duas mãos em
  todas as animações, menos na morte, quando a arma cai.
- Guia completo: `STYLE_GUIDE.md`. Inventário de cada asset com tamanho e status: `ASSET_MANIFEST.md`.

---

## 13. Efeitos visuais

Explosões de tinta, faíscas de ricochete, flash de armado, splashes de acerto, manchas de tinta nas superfícies, halo e rastro
dos tiros vivos, linha de mira dos inimigos, brilho pulsante da arma dupla, rachaduras e escurecimento da armadura do chefe e
iluminação 2D global do URP. Os efeitos animados foram montados no editor de VFX do PixelLab e recoloridos para cada time.

---

## 14. Som e música

- **25 efeitos sonoros**, sintetizados especialmente para o jogo: tiros (jogador, inimigo, pesado), ricochete "armado" e
  ricochetes seguintes, splash inofensivo, eliminação, dano, passos andando e correndo (ligados aos quadros da animação em
  que o pé toca o chão), pulo, aterrissagem, agachar, clique de UI, início, vitória e derrota de fase, troca de arma e recarga.
- **Música:** "Title Screen" (menu) e "Level 1" (jogo), de Juhani Junkala (CC0), com volume equalizado e a música cerca de
  10 dB abaixo dos efeitos.
- O som também é feedback: o ricochete "armado" soa diferente dos outros, e cada tipo de tiro tem o próprio som.

---

## 15. Balanceamento

Os principais valores ficam expostos no Inspector, no objeto GameBootstrap, para ajuste rápido sem mexer em código.

| Parâmetro | Valor | Motivo |
|---|---|---|
| Pente / recarga | 6 tiros / 2 s | Com tiros demais o jogo ficava fácil; a recarga evita o spam de tiros |
| Corrida | ×1,5 | Valor encontrado em testes manuais |
| Vida do jogador | 3 corações, 0,75 s invulnerável após dano | Clássico dos videogames: permite alguns erros, mas não muitos |
| Pontos por inimigo nas posições aleatórias | 1,7 | Variação baixa o bastante para o jogador se adaptar, em vez de encontrar uma configuração totalmente nova a cada tentativa |
| Chance de agachar dos inimigos | 10% por tiro | A primeira versão só agachava quando isso realmente desviaria o tiro, e isso acontecia em ~1% dos tiros: na prática, nunca. Passou a valer para todo tiro letal, e baixamos a chance de 50% para 10% para o agachamento continuar raro |
| Esquiva do chefe | 33% por tiro | Desviar de todo tiro era injusto com o jogador |
| Reação do chefe | 0,12 s | Tiros que quicam perto dele ainda acertam, e essa é a resposta que o jogo espera |
| Invulnerabilidade do chefe | 4 s por troca de fase | A luta estava monótona e constante; as fases dão ritmo e mantêm o jogador engajado |
| Troca de arma | 0,5 s, F travado por 1 s | Evitar abuso da troca de arma |
| Arma dupla | Leque de 4°, 2 balas por disparo, 0,4 s entre disparos | É a mesma arma dos inimigos laranja |
| Ruído de mira inimigo | 9° → 4,5° | Os inimigos ficam mais precisos fase a fase |
| Intervalo de tiro inimigo | 2,6–3,8 s → 1,4–2,2 s | A pressão aumenta fase a fase |

---

## 16. Rejogabilidade

Rejogabilidade é uma das duas rubricas desconsideradas, e desde o começo não fazia parte do escopo ter tempos, ranking ou
desafios extras. Mesmo assim, o jogo varia de uma tentativa para outra:

- **Posições aleatórias:** cada vez que a fase carrega, inclusive ao tentar de novo, os inimigos aparecem em posições novas.
- **Ruído na mira inimiga** e **esquiva sorteada do chefe** deixam cada tentativa um pouco diferente.
- Toda superfície pode ser usada, então cada inimigo tem várias soluções de ricochete.

---

## 17. Estados, falhas e recuperação

| Risco | Como o jogo trata |
|---|---|
| Morrer | Tela de morte com **Tentar de novo** (recomeça a fase) e **Menu principal**. **R** também reinicia |
| Ser atacado antes de estar pronto | Zona segura no início de toda fase, e inimigos só atiram em quem já apareceu na tela |
| Voltar para a zona segura sob fogo | O deslize pela porta e a porta de mão única impedem |
| Tiros saindo da zona segura | A porta só deixa tiros entrarem |
| Correr até o fim da fase sem limpá-la | A saída e a porta da arena só abrem com todos os inimigos de fora mortos |
| Ficar preso fora da arena | A porta abre assim que o último guarda cai, e o LED mostra o estado |
| "Trapacear" o chefe de fora da tela | A arena fechada mantém o jogador e o chefe na mesma tela |
| Inimigo nascendo flutuando ou dentro de outro | O sorteio de posições só aceita pontos com o corpo inteiro apoiado e sem sobreposição |
| Chefe prensado num canto | Ele volta ao meio da sua área depois de desviar |
| Fim do jogo | Tela de vitória com botão para o menu principal |
| Asset faltando | Cada objeto tem uma forma geométrica de reserva, então o jogo sempre roda |
| Idioma | A escolha fica salva entre sessões |

---

## 18. Tecnologia e arquitetura

- **Sem prefabs:** o `GameBootstrap` monta cada fase em tempo de execução a partir de `Levels.cs`: geometria, portas,
  jogador, inimigos, câmera e HUD.
- **Física dos tiros própria:** circle casts com reflexão exata, em vez da física de corpos do Unity, para que o laser e o
  tiro sigam exatamente o mesmo caminho.
- **Biblioteca de arte:** um ScriptableObject (`ArtLibrary`) guarda todos os sprites, animações e sons. O menu
  *Ricochet ▸ Build Art Library* reimporta tudo, monta os Animator Controllers e preenche a biblioteca.
- **Camadas de desenho:** Background (reservada, sem uso) · Default · Objects · Characters · ProjectilesFX · UI.
- **Ferramentas próprias** em `Tools/ArtGen` (Python), que geram os objetos em pixel, encaixam as animações nos colisores,
  sintetizam os sons e montam as animações do tutorial.

---

## 19. Histórico de desenvolvimento

Datas e horários no fuso de Brasília. Cada entrada traz o que mudou e por quê.

| Data | O que mudou | Por quê |
|---|---|---|
| 22/10 | Protótipo mínimo: arma mirada pelo mouse, tiro com até 4 ricochetes pela lei da reflexão, inimigos que só morrem com tiro ricocheteado e miram por traçado de raios com ruído normal, WASD com agachar (−40% de hitbox) e tiro no clique. Primeiro commit ("Check-in inicial") | Validar o conceito central antes de qualquer conteúdo. O ruído existe para o inimigo poder errar e não ser um "aimbot" |
| 23/10 | Protótipo aprovado e commitado ("Bare bones prototype"). Inimigos só atiram depois de serem vistos. Janela fixa em 1680×840. Plano das 10 fases de 50 m e do chefe com armadura | Justiça (ninguém leva tiro de fora da tela) e definição do escopo completo |
| 29/10 (manhã) | Pente de 6 com recarga automática de 2 s, HUD do pente e mira laser até o primeiro ricochete | Com tiros demais o jogo ficava fácil, e a recarga evita o spam. Mirar era difícil demais para a maioria dos jogadores |
| 29/10 | **Sprint 1** em aula (seção 19.1) | |
| 29/10 (tarde) | Zona segura com porta de mão única, feita a partir do nosso esboço (`safezone.png`), e o deslize pela porta. Mira inimiga pela média dos últimos 5 passos. Inimigos que agacham a partir da fase 4, ajustado para 10% depois do teste | O jogador não começa direto na ação e tem tempo para planejar e ler os tutoriais. A média evita que o inimigo atire para o lado errado por causa de uma virada de um instante. Na primeira versão, o agachamento quase nunca acontecia |
| 04/11 | Corrida (Shift) e posições de nascimento aleatórias por grupo | A corrida ajuda a desviar de certos tiros. As posições aleatórias evitam que a fase fique chata depois de morrer várias vezes nela |
| 05/11 | **Sprint 2** em aula (seção 19.1) | |
| 08/11 (tarde) | Arma dupla na zona segura da fase 6, com troca por F e pente compartilhado | Facilitar lidar com hordas de inimigos, o que a fase 6 incentiva |
| 08/11 (fim da tarde) | Tutoriais em PT/EN nas fases 1 e 6, cartão "Ricochete para matar", menu principal com idioma e menu de morte | O jogo precisa se ensinar sozinho e funcionar nos dois idiomas. A regra do ricochete é a mais importante e precisava de destaque |
| 08/11 (noite) | Chefe refeito: arena fechada, esquiva, fases em 2/3 e 1/3 da vida. Esquiva ajustada para 33%, chefe fica no teto, arena trancada até os guardas caírem | A luta estava monótona e constante. Desviar sempre era injusto. A arena impede atirar no chefe de fora da tela, e a trava impede correr direto para o chefe |
| 09/11 (manhã) | Passe de arte e áudio: personagens e animações, objetos modernos, efeitos, UI, 25 sons e música. Inimigos laranja e roxo como recores do vermelho; só o chefe corre | Direção visual "moderna" para o prédio. Os recores mantêm fácil identificar o que é inimigo |
| 09/11 (tarde) | Splash verde nos acertos, brilho vermelho nos tiros inimigos letais, armadura do chefe mais clara (preta só quando imune), correção dos passos do chefe, corações no HUD, armas nas mãos em todas as animações, animação de recarga, plataformas viram canos e placas viram painéis de luz quebrados | As armas flutuavam na altura do ombro. O preto precisava significar só "imune". Os canos e painéis dão a sensação de espaço fechado e industrial |
| 09/11 (tarde) | Correção de bug: um tiro inimigo podia ferir o jogador sem ter ricocheteado | Não deveria acontecer: a regra do ricochete vale para os dois lados |
| 10/11 (manhã) | Fundo off-white sólido (depois escurecido), remoção dos textos de depuração e da descrição das fases, correção do sprite duplicado ao mirar perto do personagem, tela de pausa (Esc) e de vitória, quadros de tutorial voltando a aparecer na hora | A imagem de fundo planejada não foi necessária. Polimento para a entrega |
| 10/11 (manhã) | Animações em loop para todos os 13 cartões de tutorial | Mostrar cada mecânica em movimento, em vez de só descrever |
| 10/11 | Remoção dos atalhos de teste (N, P, T) do build final. Este GDD | Os atalhos eram só para testes. Documentação para a entrega |
| 10/11 (tarde) | Publicação do build WebGL no itch.io | Entrega |

### 19.1 Sprints

| Sprint | Data | Conteúdo |
|---|---|---|
| 1 | Quinta, 29/10 | Apresentamos a prova de conceito, que recebeu o sinal verde para seguir. Definimos o movimento, as mecânicas iniciais dos inimigos e como seria a escalada de dificuldade das fases |
| 2 | Quinta, 05/11 | Zona segura, segunda arma e mais variação na luta contra o chefe |

### 19.2 Playtests

Testamos o jogo com outros jogadores ao longo do desenvolvimento, e várias mecânicas nasceram diretamente desses testes:

- **Mira laser:** a maioria dos jogadores achava difícil demais mirar ricochetes.
- **Agachar dos inimigos** e **esquiva do chefe:** para os inimigos reagirem aos tiros do jogador.
- **Corrida:** para conseguir desviar de certos tiros.
- **Arma dupla:** para lidar com grupos grandes de inimigos.
- **Zona segura:** para o jogador não ser jogado direto na ação.

---

## 20. Uso de inteligência artificial

Usamos ferramentas de IA nestas frentes:

- **Imagens e animações:** os personagens (jogador, inimigos e chefe) e suas animações foram gerados com o **PixelLab**, a
  partir de descrições nossas. Os efeitos visuais foram montados no editor de VFX do PixelLab. Objetos, armas e UI foram
  desenhados em grades de pixels por scripts escritos com IA.
- **Efeitos sonoros:** os 25 sons foram sintetizados por um script (`synth_sfx.py`) escrito com IA, sem samples de
  terceiros.
- **Implementação:** o código do jogo em C# (mecânicas, fases, inimigos, chefe, HUD, menus, tutorial), a montagem das
  animações no Unity e as ferramentas em Python foram escritos com um assistente de programação por IA (Claude Code), a
  partir das nossas especificações. Cada pedido e cada decisão de design estão no histórico da seção 19: as regras, as 10
  fases, os ajustes de balanceamento e a direção de arte. Revisamos e testamos tudo o que foi implementado.
- **Documentação:** este GDD foi avaliado com a ajuda do mesmo assistente, a partir do histórico do projeto. O texto também foi reestruturado para garantir uma leitura fluída.

A música não usa IA: são faixas CC0 de Juhani Junkala (seção 21).

---

## 21. Créditos

| Item | Fonte | Licença |
|---|---|---|
| Música "Title Screen" e "Level 1" | Juhani Junkala, *5 Action Chiptunes*, OpenGameArt | CC0 1.0 |
| Efeitos sonoros | Sintetizados para o projeto (`Tools/ArtGen/synth_sfx.py`) | Do projeto (CC0) |
| Personagens, animações e efeitos | Gerados no PixelLab, na conta do projeto | Conforme os Termos de Serviço do PixelLab |
| Objetos, armas, UI e animações do tutorial | Feitos para o projeto (`Tools/ArtGen`) | Do projeto |

Detalhes: `CREDITS.md` e `ASSET_MANIFEST.md`.

---

## 22. Possíveis ajustes futuros

Pontos conhecidos que ficaram fora do escopo da entrega:

- **Chefe:** durante a rajada, se o jogador se esconder num ponto que nenhum ricochete alcança, ele fica sem atirar.
