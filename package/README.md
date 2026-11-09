# Sea Legs

**[English](#english) · [Português](#português)**

---

## English

Sailing is better together. While one player steers, the crew sings sea shanties on deck, the boat goes faster, and the helmsman gets a clearer view of the sea ahead.

### Features

- **Client-side.** The server doesn't need the mod. Everyone who wants to hear the songs needs it.
- **Crew song:** press **K** (or type /vibe, /dance or /roar) to start a song. Everyone on board who stands still on the deck or sits joins in, singing and dancing. Any movement key stops you, and K again stops you until the song is over.
- **Held on deck:** while you sing standing, you are held in place the way a bench or the mast holds you, so a bump of the boat can't throw you overboard.
- **Faster sail:** each crew member singing adds **+15%** sail force, up to **+45%**. The helmsman's game moves the boat, so the helmsman needs the mod. Players without the mod count too when they use /vibe, /dance or /roar.
- **The helmsman sings too:** while the crew sings, the helmsman sings along and counts as a singer.
- **See it working:** at the helm, golden wind ribbons blow through a golden sail, and the wind icon glows with clinking mugs and the number of singers.
- **Works on every boat:** raft, karve, longship and drakkar, at any sail speed. You can also sing alone.

### The song

- **One song for the whole boat**, in time for everyone on board. Someone who comes aboard joins the song already playing instead of starting a new one.
- **A real choir:** the instruments play once, and each singer sings the voice from their own character, at their own pitch (bass, baritone, tenor, alto or mezzo-soprano). The voices are a few milliseconds apart, like people singing together.
- **Playlist:** shuffled when the crew starts singing, always two sung songs and then one instrumental as a breather.
- **Fits the crew:** only women on board → women's songs, only men → men's songs, both → all of them.
- **New shuffle** when nobody has sung on the boat for a minute.
- The game music is turned down while the crew sings, and the song name shows when it starts.
- **Your own songs:** drop files in `BepInEx/config/SeaLegs/songs` (see the README.txt there). The crew hears a song only if everyone has the same files.

### At the helm

- **Hidden rocks:** rocks up to 3 m under the water are outlined around the boat, so you can steer around them. Rocks just above the water get their own color. You can also fill them instead of outlining.
- **The faster, the further:** at speed, rocks show further ahead of the bow (up to 100 m).
- **Wider view:** the camera moves back and up when you take the helm. The **arrow keys** move it: left/right slide it sideways, up/down raise or lower it. The position is saved.
- **See-through sail:** the sail turns see-through while you steer, so it doesn't block the view ahead.

### Side rowing

- Every boat has a spot by the rail on the **left** and on the **right**, level with the mast, marked with two pulsing rings on the deck (only crew members who aren't steering see them).
- Stand next to one and press **E**: your character holds on where they stand and works the ropes, and the boat moves **straight sideways** toward that side, without turning. Handy to come alongside a dock or slip past a rock.
- Each rower adds speed; rowers on opposite sides cancel out. Any movement key, or E again, lets go.
- The helmsman's game moves the boat, so the helmsman and the rowers need the mod.

### Requirements

- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)

### Installation

**r2modman / Thunderstore Mod Manager:** click *Install*. To install a downloaded zip, go to Settings → *Import local mod*.

**Nexus Mods / Vortex:** also on [Nexus Mods](https://www.nexusmods.com/valheim/mods/4906).

**Manual:**
1. Extract the zip.
2. Copy the `plugins/SeaLegs` folder into `Valheim/BepInEx/plugins/`.
   Result: `Valheim/BepInEx/plugins/SeaLegs/SeaLegs.dll` and the `songs` folder next to it.
3. Launch the game.

To update, delete the old `SeaLegs` folder before copying the new one.

### Configuration

Press **F1** in game (requires Configuration Manager) or edit `BepInEx/config/felih.SeaLegs.cfg`. The main options:

| Option | Default | What it does |
|---|---|---|
| SingKey | K | Starts a song on the boat; press again to stop singing. |
| AutoSing | on | Join in when someone on your boat starts a song, while you stand still or sit. |
| HelmsmanSingsAlong | on | At the helm, sing along while the crew sings. |
| HelmGlow | on | The golden ribbons, sail and wind icon at the helm. |
| OnlyWhileSomeoneSteers | off | Sing only while someone else is at the helm. |
| StandStillSeconds | 3 | How long you must stand still before you sing. |
| BonusPerSinger / MaxBonus | 0.15 / 0.45 | Extra sail force per singer and in total. |
| Volume | 0.6 | Volume of the crew song. |
| InstrumentVolume | 0.5 | Volume of the instruments under the choir. |
| VoiceType | Auto | Your voice: Bass, Baritone, Tenor, Alto, MezzoSoprano (Auto = from your character). |
| ChoirSpreadMs | 100 | Most time between the earliest and the latest voice. |
| GameMusicWhileSinging | 0.15 | Game music volume while the crew sings. |
| DisabledSongs | empty | Songs you never want when you lead the playlist. |
| Style | Outline | Rocks: Outline, Fill or Both. |
| Range / Depth | 30 / 3 | How far around the boat and how deep to show rocks (metres). |
| SecondsAhead / MaxRange | 6 / 100 | At speed, also show rocks this many seconds of sailing ahead, up to MaxRange metres. |
| Color / AboveWaterColor | orange / yellow | Rocks under and just above the water. |
| CameraDistance / CameraHeight | 12 / 1.5 | Camera at the helm (metres). |
| ArrowKeysMoveCamera | on | Arrow keys move the helm camera. |
| SeeThroughSail / SailOpacity | on / 0.25 | See-through sail at the helm. |
| SideRowing | on | The side rowing spots. |
| SpeedPerRower / MaxSpeed | 1 / 2.5 | Sideways speed per rower and in total (m/s). |
| SpotInset | 0 | Move the spots toward the middle (+) or the rail (-), in metres. |
| RowEmote | comehere | Emote played while rowing (empty = none). |

### Music credits

The songs come from [Pixabay](https://pixabay.com/music/) under the Pixabay Content License. Many are AI-generated. Each sung song was split into voice and instruments with [Demucs](https://github.com/facebookresearch/demucs) so every singer can sing at their own pitch.

- Viking Chant – Ancient Norse Style, by nickpanek
- Weigh the Anchor, The Ghost of Gallows Reef, The Cursed Compass, The Sailor's Bargain, by kaazoom
- Fjord Nordic, by renilsonb
- Woman Sing – Melodic Chant, by fidelfortune
- The Warriors Lament, by vishiv
- Perun – Perunie (vox by Izabela Szturemska), by ob-lix
- Wellerman – Epic Shanty (instrumental), by ebunny

### Notes

- This mod was made with the help of AI.
- Version 0.11: starting the song with K, the helmsman singing along and the golden glow are new. Please report anything odd.

---

## Português

Navegar é melhor junto. Enquanto um jogador pilota, a tripulação canta canções de marinheiro no deck, o barco anda mais rápido e quem está no leme enxerga melhor o mar à frente.

### Recursos

- **Só no cliente.** O servidor não precisa do mod. Quem quiser ouvir as músicas precisa tê-lo.
- **Canto da tripulação:** aperte **K** (ou digite /vibe, /dance ou /roar) para puxar uma música. Todo mundo a bordo parado no deck ou sentado entra junto, cantando e dançando. Qualquer tecla de movimento para você, e K de novo para você até a música acabar.
- **Preso ao deck:** enquanto você canta em pé, fica preso no lugar do mesmo jeito que um banco ou o mastro prende, então um tranco do barco não te joga no mar.
- **Vela mais forte:** cada tripulante cantando dá **+15%** de força na vela, até **+45%**. Quem move o barco é o jogo de quem está no leme, então ele precisa do mod. Jogadores sem o mod também contam quando usam /vibe, /dance ou /roar.
- **O timoneiro canta também:** enquanto a tripulação canta, o timoneiro canta junto e conta como cantor.
- **Veja funcionando:** no leme, faixas de vento douradas passam por uma vela dourada, e o ícone de vento brilha com canecas brindando e o número de cantores.
- **Funciona em todo barco:** jangada, karve, longship e drakkar, em qualquer velocidade da vela. Dá para cantar sozinho também.

### A música

- **Uma música para o barco todo**, no mesmo tempo para todos a bordo. Quem sobe no barco entra na música que já está tocando, sem começar outra.
- **Um coro de verdade:** os instrumentos tocam uma vez, e cada cantor canta a voz saindo do próprio personagem, no seu tom (baixo, barítono, tenor, contralto ou mezzo-soprano). As vozes ficam alguns milissegundos separadas, como gente cantando junto.
- **Playlist:** embaralhada quando a tripulação começa a cantar, sempre duas músicas cantadas e depois uma instrumental para respirar.
- **Combina com a tripulação:** só mulheres a bordo → músicas femininas, só homens → masculinas, os dois → todas.
- **Novo embaralhamento** quando ninguém canta no barco por um minuto.
- A música do jogo abaixa enquanto a tripulação canta, e o nome da música aparece quando ela começa.
- **Suas próprias músicas:** coloque arquivos em `BepInEx/config/SeaLegs/songs` (veja o README.txt de lá). A tripulação só ouve uma música se todos tiverem os mesmos arquivos.

### No leme

- **Pedras escondidas:** pedras até 3 m abaixo da água ganham um contorno em volta do barco, para você desviar. Pedras logo acima da água têm cor própria. Também dá para preencher em vez de contornar.
- **Quanto mais rápido, mais longe:** em velocidade, as pedras aparecem mais longe à frente da proa (até 100 m).
- **Visão mais aberta:** a câmera se afasta e sobe quando você pega o leme. As **setas** do teclado movem a câmera: esquerda/direita para os lados, cima/baixo sobe e desce. A posição fica salva.
- **Vela transparente:** a vela fica transparente enquanto você pilota, para não tapar a visão.

### Remo lateral

- Todo barco tem uma área junto da amurada, à **esquerda** e à **direita**, na altura do mastro, marcada com dois círculos pulsando no deck (só quem está a bordo e não está no leme vê).
- Fique do lado de uma e aperte **E**: o personagem se segura onde está e puxa as cordas, e o barco anda **reto de lado** para aquele lado, sem girar. Bom para encostar num cais ou passar raspando por uma pedra.
- Cada remador soma velocidade; remadores em lados opostos se anulam. Qualquer tecla de movimento, ou E de novo, solta.
- Quem move o barco é o jogo do timoneiro, então o timoneiro e os remadores precisam do mod.

### Requisitos

- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)

### Instalação

**r2modman / Thunderstore Mod Manager:** clique em *Install*. Para instalar um zip baixado, use Configurações → *Import local mod*.

**Nexus Mods / Vortex:** também está no [Nexus Mods](https://www.nexusmods.com/valheim/mods/4906).

**À mão:**
1. Extraia o zip.
2. Copie a pasta `plugins/SeaLegs` para dentro de `Valheim/BepInEx/plugins/`.
   Resultado: `Valheim/BepInEx/plugins/SeaLegs/SeaLegs.dll` e a pasta `songs` ao lado.
3. Abra o jogo.

Para atualizar, apague a pasta `SeaLegs` antiga antes de copiar a nova.

### Configuração

Aperte **F1** no jogo (precisa do Configuration Manager) ou edite `BepInEx/config/felih.SeaLegs.cfg`. As principais opções:

| Opção | Padrão | O que faz |
|---|---|---|
| SingKey | K | Puxa uma música no barco; aperte de novo para parar de cantar. |
| AutoSing | ligado | Entra junto quando alguém no barco puxa uma música, enquanto você está parado ou sentado. |
| HelmsmanSingsAlong | ligado | No leme, canta junto enquanto a tripulação canta. |
| HelmGlow | ligado | As faixas, a vela e o ícone de vento dourados no leme. |
| OnlyWhileSomeoneSteers | desligado | Só canta enquanto outra pessoa está no leme. |
| StandStillSeconds | 3 | Quanto tempo parado antes de cantar. |
| BonusPerSinger / MaxBonus | 0.15 / 0.45 | Força extra na vela por cantor e no total. |
| Volume | 0.6 | Volume da música. |
| InstrumentVolume | 0.5 | Volume dos instrumentos embaixo do coro. |
| VoiceType | Auto | Sua voz: Bass, Baritone, Tenor, Alto, MezzoSoprano (Auto = pelo personagem). |
| ChoirSpreadMs | 100 | Tempo máximo entre a voz mais adiantada e a mais atrasada. |
| GameMusicWhileSinging | 0.15 | Volume da música do jogo enquanto a tripulação canta. |
| DisabledSongs | vazio | Músicas que você não quer quando puxa a playlist. |
| Style | Outline | Pedras: Outline (contorno), Fill (preenchido) ou Both (os dois). |
| Range / Depth | 30 / 3 | Distância em volta do barco e profundidade das pedras (metros). |
| SecondsAhead / MaxRange | 6 / 100 | Em velocidade, mostra também as pedras a essa quantidade de segundos de navegação à frente, até MaxRange metros. |
| Color / AboveWaterColor | laranja / amarelo | Pedras abaixo e logo acima da água. |
| CameraDistance / CameraHeight | 12 / 1.5 | Câmera no leme (metros). |
| ArrowKeysMoveCamera | ligado | As setas movem a câmera do leme. |
| SeeThroughSail / SailOpacity | ligado / 0.25 | Vela transparente no leme. |
| SideRowing | ligado | As áreas de remo lateral. |
| SpeedPerRower / MaxSpeed | 1 / 2.5 | Velocidade de lado por remador e no total (m/s). |
| SpotInset | 0 | Move as áreas para o meio (+) ou para a amurada (-), em metros. |
| RowEmote | comehere | Emote usado enquanto rema (vazio = nenhum). |

### Créditos das músicas

As músicas vêm do [Pixabay](https://pixabay.com/music/), sob a Pixabay Content License. Muitas foram geradas por IA. Cada música cantada foi separada em voz e instrumentos com o [Demucs](https://github.com/facebookresearch/demucs), para cada cantor poder cantar no seu tom.

- Viking Chant – Ancient Norse Style, de nickpanek
- Weigh the Anchor, The Ghost of Gallows Reef, The Cursed Compass, The Sailor's Bargain, de kaazoom
- Fjord Nordic, de renilsonb
- Woman Sing – Melodic Chant, de fidelfortune
- The Warriors Lament, de vishiv
- Perun – Perunie (voz de Izabela Szturemska), de ob-lix
- Wellerman – Epic Shanty (instrumental), de ebunny

### Observações

- Este mod foi feito com ajuda de IA.
- Versão 0.11: puxar a música com K, o timoneiro cantando junto e o brilho dourado são novos. Avise se notar algo estranho.
