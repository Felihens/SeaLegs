# Sea Legs

A client-side BepInEx mod for Valheim: the crew sings sea shanties as a synced choir (each player in their own voice) for up to +45% sail force, held safely on deck; at the helm, hidden rocks are outlined, the camera opens up and the sail turns see-through.

Mod para Valheim (BepInEx, client-side): a tripulação canta canções de marinheiro em coro sincronizado (cada jogador com a sua voz) para até +45% de força na vela, preso com segurança ao deck; no leme, pedras escondidas ganham contorno, a câmera abre e a vela fica transparente.

For players, see [package/README.md](package/README.md). That file is also the Thunderstore page.
Para jogadores, veja [package/README.md](package/README.md).

Download: [Thunderstore](https://thunderstore.io/c/valheim/p/DJAventuras/SeaLegs/) · [GitHub Releases](https://github.com/Felihens/SeaLegs/releases)

## How it works

- **Singing:** your character plays the Vibe, Dance and Roar emotes (saved on the character, so everyone sees them). Standing singers are held with `Player.AttachStart` on a point created on the ship, the same way a bench or the mast holds you.
- **Sail force:** a postfix on `Ship.GetSailForce` multiplies the force by the number of crew members playing one of those emotes. Only the ship owner's game (the helmsman) moves the boat.
- **Synced playlist:** each player writes the playlist they follow on their own character (ZDO keys `SeaLegs_*`), with the current song's start time in the server clock. On a boat everyone follows the oldest playlist among the players on board; whoever is first in line moves it on, the others copy it.
- **Choir:** split songs (`name_inst` + `name_vocals`) play the instruments once on the ship and the voice on each singer, pitch-shifted by `PitchShift` (two sliding read heads over a short delay line, so the tempo stays the same) and spread a few milliseconds apart.
- **Rocks:** the collision shapes of rocks near the boat are copied to world space and drawn into stencil bits through the UI shader's clip rectangle, which keeps only the band around the water level; a full-screen pass paints the outline or the fill.

## Building

`sh build.sh` compiles `src/*.cs` against the game's assemblies and installs the dll into `BepInEx/plugins/SeaLegs/` (`sh build.sh nocopy` only compiles). Then `powershell -File pack.ps1` builds `dist/SeaLegs-<version>.zip` from `package/`.

## Credits

Music from [Pixabay](https://pixabay.com/music/) (Pixabay Content License); see [package/README.md](package/README.md#music-credits). Voice/instrument split with [Demucs](https://github.com/facebookresearch/demucs).

## License

Code: MIT. The songs keep their own Pixabay license.
