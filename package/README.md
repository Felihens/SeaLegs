# Sea Legs

**[English](#english) · [Português](#português)**

---

## English

Sailing is better together. While one player steers, the crew sings sea shanties on deck, the boat goes faster, and the helmsman gets a clearer view of the sea ahead.

### Features

- **Client-side.** The server doesn't need the mod. Everyone who wants to hear the songs needs it.
- **Crew song:** stand still on the deck for 3 seconds, or sit down, and your character starts singing and dancing. Any movement key stops it.
- **Held on deck:** while you sing standing, you are held in place the way a bench or the mast holds you, so a bump of the boat can't throw you overboard.
- **Faster sail:** each crew member singing adds **+15%** sail force, up to **+45%**. The helmsman's game moves the boat, so the helmsman needs the mod. Players without the mod count too when they use /vibe, /dance or /roar.
- **Works on every boat:** raft, karve, longship and drakkar. You can also sing alone.

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
- **Wider view:** the camera moves back and up when you take the helm. The **arrow keys** move it: left/right slide it sideways, up/down raise or lower it. The position is saved.
- **See-through sail:** the sail turns see-through while you steer, so it doesn't block the view ahead.

### Requirements

- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)

### Installation

**r2modman / Thunderstore Mod Manager:** click *Install*. To install a downloaded zip, go to Settings → *Import local mod*.

**Nexus Mods / Vortex:** also on Nexus Mods.

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
| AutoSing | on | Sing on your own on a boat when you stand still or sit. |
| OnlyWhileSomeoneSteers | off | Sing only while someone else is at the helm. |
| StandStillSeconds | 3 | How long you must stand still before you sing. |
| BonusPerSinger / MaxBonus | 0.15 / 0.45 | Extra sail force per singer and in total. |
| Volume | 0.6 | Volume of the crew song. |
| VoiceType | Auto | Your voice: Bass, Baritone, Tenor, Alto, MezzoSoprano (Auto = from your character). |
| ChoirSpreadMs | 100 | Most time between the earliest and the latest voice. |
| GameMusicWhileSinging | 0.15 | Game music volume while the crew sings. |
| DisabledSongs | empty | Songs you never want when you lead the playlist. |
| Style | Outline | Rocks: Outline, Fill or Both. |
| Range / Depth | 30 / 3 | How far around the boat and how deep to show rocks (metres). |
| Color / AboveWaterColor | orange / yellow | Rocks under and just above the water. |
| CameraDistance / CameraHeight | 12 / 1.5 | Camera at the helm (metres). |
| ArrowKeysMoveCamera | on | Arrow keys move the helm camera. |
| SeeThroughSail / SailOpacity | on / 0.25 | See-through sail at the helm. |

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
- Version 0.9: the songs and the choir are new. Please report anything odd.

---

## Português

Navegar é melhor junto. Enquanto um jogador pilota, a tripulação canta canções de marinheiro no deck, o barco anda mais rápido e quem está no leme enxerga melhor o mar à frente.

### Recursos

- **Só no cliente.** O servidor não precisa do mod. Quem quiser ouvir as músicas precisa tê-lo.
- **Canto da tripulação:** fique parado no deck por 3 segundos, ou sente, e o seu personagem começa a cantar e dançar. Qualquer tecla de movimento para.
- **Preso ao deck:** enquanto você canta em pé, fica preso no lugar do mesmo jeito que um banco ou o mastro prende, então um tranco do barco não te joga no mar.
- **Vela mais forte:** cada tripulante cantando dá **+15%** de força na vela, até **+45%**. Quem move o barco é o jogo de quem está no leme, então ele precisa do mod. Jogadores sem o mod também contam quando usam /vibe, /dance ou /roar.
- **Funciona em todo barco:** jangada, karve, longship e drakkar. Dá para cantar sozinho também.

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
- **Visão mais aberta:** a câmera se afasta e sobe quando você pega o leme. As **setas** do teclado movem a câmera: esquerda/direita para os lados, cima/baixo sobe e desce. A posição fica salva.
- **Vela transparente:** a vela fica transparente enquanto você pilota, para não tapar a visão.

### Requisitos

- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)

### Instalação

**r2modman / Thunderstore Mod Manager:** clique em *Install*. Para instalar um zip baixado, use Configurações → *Import local mod*.

**Nexus Mods / Vortex:** também está no Nexus Mods.

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
| AutoSing | ligado | Canta sozinho no barco quando você fica parado ou senta. |
| OnlyWhileSomeoneSteers | desligado | Só canta enquanto outra pessoa está no leme. |
| StandStillSeconds | 3 | Quanto tempo parado antes de cantar. |
| BonusPerSinger / MaxBonus | 0.15 / 0.45 | Força extra na vela por cantor e no total. |
| Volume | 0.6 | Volume da música. |
| VoiceType | Auto | Sua voz: Bass, Baritone, Tenor, Alto, MezzoSoprano (Auto = pelo personagem). |
| ChoirSpreadMs | 100 | Tempo máximo entre a voz mais adiantada e a mais atrasada. |
| GameMusicWhileSinging | 0.15 | Volume da música do jogo enquanto a tripulação canta. |
| DisabledSongs | vazio | Músicas que você não quer quando puxa a playlist. |
| Style | Outline | Pedras: Outline (contorno), Fill (preenchido) ou Both (os dois). |
| Range / Depth | 30 / 3 | Distância em volta do barco e profundidade das pedras (metros). |
| Color / AboveWaterColor | laranja / amarelo | Pedras abaixo e logo acima da água. |
| CameraDistance / CameraHeight | 12 / 1.5 | Câmera no leme (metros). |
| ArrowKeysMoveCamera | ligado | As setas movem a câmera do leme. |
| SeeThroughSail / SailOpacity | ligado / 0.25 | Vela transparente no leme. |

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
- Versão 0.9: as músicas e o coro são novos. Avise se notar algo estranho.
