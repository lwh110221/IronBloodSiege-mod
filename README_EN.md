# IronBlood Siege 🏰

<div align="center">
  <img src="https://img.shields.io/badge/Game-Bannerlord-red"/>
  <img src="https://img.shields.io/badge/Version-1.2.12-blue"/>
  <img src="https://img.shields.io/badge/Language-English%20%7C%20Chinese-green"/>

  [简体中文](README.md) | English
</div>

## Introduction
IronBlood Siege is a mod for Mount & Blade II: Bannerlord that enhances the siege battle experience. In vanilla siege battles, attacking troops often retreat mindlessly, sometimes even when they vastly outnumber the defenders, leading to poor siege experience for players seeking challenging gameplay. This mod effectively prevents troops from retreating unnecessarily and dynamically adjusts morale values to ensure siege sustainability.

- The siege attacker mechanism in version 1.2+ is controlled by the morale combined with the battle command system. Most other mods that prevent routing troops cannot take effect in siege battles, so this mod integrates with the battle command system and dynamically adjusts morale values.
- Only tested on version 1.2.12 with no major issues. Other versions are untested.

## Features
- Prevents troops from retreating unnecessarily during sieges
- Dynamically adjusts morale values to ensure siege sustainability
- Fully configurable settings
- Real-time battlefield status feedback

## Main Features
### Iron Will Attack Mechanism
- Prevents troops from retreating arbitrarily during siege battles
- Dynamically adjusts morale based on battlefield situation
- Automatically disables iron will status when troop numbers are insufficient
- Supports two retreat judgment methods:
  - Fixed number mode: Disables iron will when attacker troops fall below specified number
  - Ratio mode: Disables iron will when attacker troops fall below 70% of defenders
- Automatically restores iron will status if reinforcements arrive during countdown
- Disables iron will status when below troop threshold with no reinforcements

### Morale Adjustment
- Real-time monitoring and boosting of low morale units
- Boosts morale when troops fall below set threshold
- Configurable boost rate
- Displays number of inspired troops

## Battlefield Advantage Ratio Calculation
- Uses formula: strengthRatio = attackerCount / defenderCount
- If defender count is 0, defaults to 2.0 (indicating significant advantage)
- This ratio reflects the numerical comparison between attackers and defenders

## Dynamic Morale Threshold Adjustment
- When attackers have significant numerical advantage (ratio >= 1.5), morale threshold is reduced
- Specifically, the original threshold is multiplied by 0.6
- This means the system has more lenient morale requirements in advantageous situations

## Battlefield Information Feedback
- Battlefield status changes
- Key information prompts:
  - Number of inspired troops
  - Reinforcement arrival notifications
  - Retreat condition trigger notifications
- Supports localization in multiple languages, currently Chinese and English

## Configuration Options
- Adjustable in-game settings panel:
  - Enable/disable entire mod
  - Choose retreat judgment method (fixed number/ratio)
  - Set fixed number mode threshold
  - Adjust morale boost threshold
  - Set morale boost rate

## Important Notes
- Only one of the two retreat judgment methods can be selected
- Recommend choosing appropriate fixed number threshold based on battle scale
- Ratio mode is currently set to 70%
- Debug version outputs detailed logs

## Requirements
- Game Version: Mount & Blade II: Bannerlord 1.2.12
- MCM (Mod Configuration Menu) v5+

## Installation
1. Ensure MCM (Mod Configuration Menu) is installed
2. Copy mod files to the game's Modules folder
3. Enable "IronBlood Siege" in the launcher's Mods section
4. Ensure MCM loads before this mod in the load order

## Settings Guide
The following settings can be adjusted through the MCM menu in-game:

### Basic Settings
- Enable Mod: Toggle mod functionality on/off

### Combat Settings
- Siege Troop Morale Threshold (20-80):
  - Morale will be boosted when below this value
  - Default: 70
  - Recommendation: Set lower when advantaged, higher when disadvantaged

- Morale Boost Rate (5-30):
  - Amount of morale boost per update
  - Default: 15
  - Recommendation: 15-20 is typically suitable

## Usage Tips
- The mod automatically takes effect during siege battles
- Settings can be adjusted anytime through the ESC menu during battle
- In multi-mod setups, recommend loading this mod last

## Contact
- Author: Ahao
- Email: ahao221x@gmail.com

## License
This mod is for personal use only. Commercial use is prohibited. 