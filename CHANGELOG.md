# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0](https://github.com/Slatyo/Valheim-Viking/compare/v1.0.0...v1.1.0) (2025-12-17)


### Features

* add server-authoritative equipment storage ([8a92a98](https://github.com/Slatyo/Valheim-Viking/commit/8a92a98d683ef0445e602364a4d4b9ba7b39ac4c))
* **hud:** add Prime-integrated player frame and XP bar ([21d0d46](https://github.com/Slatyo/Valheim-Viking/commit/21d0d46bde160dd6343a3a0e828c5a7241e2cc01))
* initial Viking release ([9e58092](https://github.com/Slatyo/Valheim-Viking/commit/9e58092be03771ebee8ccc5b37ce73cb8e17fcae))


### Bug Fixes

* **ui:** use vanilla getters for resource stats in character window ([1914f22](https://github.com/Slatyo/Valheim-Viking/commit/1914f225d71cfbcec9d1c95fa23b7791e2d3b316))


### Code Refactoring

* **equipment:** take full control of equip/unequip flow ([a2ab778](https://github.com/Slatyo/Valheim-Viking/commit/a2ab7780231fc0ac56bbc1fd8a5775a1c1807ae5))
* migrate from VitalDataStore to State.Store ([067b06b](https://github.com/Slatyo/Valheim-Viking/commit/067b06bef73a3d1f72cdaf1089dc09ab43728e9f))


### Documentation

* **ui:** clarify equipment inventory comments ([8c3738c](https://github.com/Slatyo/Valheim-Viking/commit/8c3738c61f49a7126a3a8c00c957e135aed6c775))

## [1.0.0] - 2025-12-06

### Added
- Talent tree with interconnected nodes
- Three starting archetypes (Warrior, Ranger, Mage)
- 8-slot ability bar with keybinds 1-8
- Server-authoritative talent allocation
- Backtrack system to undo recent allocations
- Full talent reset functionality
- Veneer integration for UI (talent tree window, ability bar, character window)
- Prime integration for stat modifiers
- Munin console commands
- Network sync for multiplayer
