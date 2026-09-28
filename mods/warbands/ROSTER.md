# Warbands hero roster

Warbands uses a fixed lore-facing roster of four Heroes per World of Warcraft class currently represented by the mod. Hero identity is mod content only; Battlegrounds.Core remains theme-neutral.

The current roster is 13 classes x 4 Heroes = 52 Heroes.

| Class | Heroes |
| --- | --- |
| Warrior | Garrosh Hellscream; Varian Wrynn; Geya'rah; Genn Greymane |
| Mage | Jaina Proudmoore; Khadgar; First Arcanist Thalyssra; Grand Magister Rommath |
| Hunter | Rexxar; Alleria Windrunner; Shandris Feathermoon; Hemet Nesingwary |
| Paladin | Uther Lightbringer; Lady Liadrin; Yrel; Sunwalker Dezco |
| Shaman | Thrall; Drek'Thar; Magatha Grimtotem; Farseer Nobundo |
| Rogue | Valeera Sanguinar; Mathias Shaw; Garona Halforcen; Lilian Voss |
| Druid | Malfurion Stormrage; Zen'tabra; Celestine of the Harvest; Hamuul Runetotem |
| Warlock | Gul'dan; Nekros Skullcrusher; Wilfred Fizzlebang; Cho'gall |
| Priest | Anduin Wrynn; Prophet Velen; Tyrande Whisperwind; Calia Menethil |
| Demon Hunter | Illidan Stormrage; Kayn Sunfury; Altruis the Sufferer; Leona Darkstrider |
| Death Knight | Arthas Menethil; Darion Mograine; Thassarian; Koltira Deathweaver |
| Monk | Taran Zhu; Chen Stormstout; Aysa Cloudsinger; Ji Firepaw |
| Evoker | Scalecommander Emberthal; Scalecommander Azurathel; Scalecommander Cindrethresh; Scalecommander Sarkareth |

## Current mechanical policy

Roster identity is deliberately separated from balance iteration. Most Heroes still inherit the class baseline Power, while a growing set now has distinct mechanics that exercise the generic economy, generation, temporary-modifier and event-history systems. Every Hero still keeps the same baseline Health modifier and Armor for now.

Current distinct exceptions are Varian Wrynn (`strategic-command`), Geya'rah (`warborn-fury`), Genn Greymane (`royal-contract`), Jaina Proudmoore (`arcane-momentum`), Khadgar (`arcane-market`), First Arcanist Thalyssra (`arcane-breakthrough`), Shandris Feathermoon (`sentinel-hunt`), Hemet Nesingwary (`hunting-trophies`), Lady Liadrin (`radiant-armament`), Magatha Grimtotem (`ruthless-bargain`), Valeera Sanguinar (`shadow-cache`), Mathias Shaw (`covert-supplies`) and Arthas Menethil (`frozen-host`). Other Heroes currently use the class baseline mapping:

- Warrior -> `commanding-shout`
- Mage -> `fireblast`
- Hunter -> `bestial-wrath`
- Paladin -> `blessing-of-light`
- Shaman -> `ancestral-strength`
- Rogue -> `deadly-poison`
- Druid -> `mark-of-the-wild`
- Warlock -> `fel-infusion`
- Priest -> `power-word-fortitude`
- Demon Hunter -> `metamorphosis`
- Death Knight -> `death-coil`
- Monk -> `inner-balance`
- Evoker -> `draconic-legacy`

Additional distinct Hero mechanics should continue to reuse generic engine primitives where possible, adding new Core capabilities only when a real playable mechanic cannot be expressed cleanly.
