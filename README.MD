# 铁血攻城 (IronBloodSiege)

## 简介
铁血攻城是一个《骑马与砍杀2：霸主》(Mount & Blade II: Bannerlord) 的mod，旨在增强攻城战的体验。原版攻城战斗中，攻城士兵动不动无脑逃跑，经常出现人数碾压守军仍然溃败，导致对于深度高难度玩家的守城战斗体验较差 ，本mod可有效防止攻城士兵无脑逃跑，并动态调用函数调整士气值，确保攻城持续性。
- 1.2+版本以上攻城的攻城方机制是士气结合战斗指挥系统而控制，其他包括防止溃军的mod在攻城战中大多数无法生效，所以本mod结合战斗指挥系统，动态调用函数调整士气值，士气值的调整是一个基本参考。
- 本人仅测试了1.2.12版本，基本上没有什么问题，其他版本未测试。

## 功能
- 防止攻城士兵动不动逃跑
- 动态调用函数调整士气值，确保攻城持续性
- 完全可配置的设置选项
- 实时战场状态反馈

## 计算战场优势比率
- 使用公式：strengthRatio = attackerCount / defenderCount
- 如果防守方人数为0，则默认设置为2.0（表示极大优势）
- 这个比率反映了攻守双方的数量对比

## 动态调整士气阈值
- 当攻方数量优势明显时(比率 >= 1.5)，会降低士气阈值
- 具体做法是将原始阈值乘以0.6
- 这意味着在优势情况下，系统对士气的要求会更宽松

## 安装要求
- 游戏版本：Mount & Blade II: Bannerlord 1.2.12
- MCM (Mod Configuration Menu) v5+

## 安装方法
1. 确保已安装MCM (Mod Configuration Menu)
2. 将mod文件复制到游戏的Modules文件夹中
3. 启动启动器，在Mods选项中启用"铁血攻城"
4. 确保加载顺序中MCM在本mod之前

## 设置说明
在游戏中可通过MCM菜单调整以下设置：

### 基础设置
- 启用Mod：开启/关闭mod功能

### 战斗设置
- 攻城士兵士气阈值 (20-80)：
  - 当士兵士气低于此值时会被提升
  - 默认值：70
  - 建议：优势时设置较低，劣势时设置较高

- 士气提升速率 (5-30)：
  - 每次提升士气的幅度
  - 默认值：15
  - 建议：根据战况调整，通常15-20较为合适

## 使用提示
- 在攻城战开始时，mod会自动生效
- 可以在战斗中随时通过ESC菜单调整设置
- 多mod中，建议本mod在最后加载

## 版本历史
- v1.0.0
  - 初始发布
  - 实现基本功能
  - 添加MCM配置支持

## 联系方式
- 作者：Ahao
- 邮箱：2285813721@qq.com

## 许可
本mod仅供个人使用，禁止用于商业用途。

# IronBlood Siege

## Introduction
IronBlood Siege is a mod for Mount & Blade II: Bannerlord that enhances the siege battle experience. In vanilla siege battles, attacking troops often retreat mindlessly, sometimes even when they vastly outnumber the defenders, leading to poor siege experience for players seeking challenging gameplay. This mod effectively prevents troops from retreating unnecessarily and dynamically adjusts morale values to ensure siege sustainability.

## Features
- Prevents troops from retreating unnecessarily during sieges
- Dynamically adjusts morale values to ensure siege sustainability
- Fully configurable settings
- Real-time battlefield status feedback

## Battle Advantage Ratio Calculation
- Uses formula: strengthRatio = attackerCount / defenderCount
- If defender count is 0, defaults to 2.0 (indicating significant advantage)
- This ratio reflects the numerical comparison between attackers and defenders

## Dynamic Morale Threshold Adjustment
- When attackers have significant numerical advantage (ratio >= 1.5), morale threshold is reduced
- Specifically, the original threshold is multiplied by 0.6
- This means the system has more lenient morale requirements in advantageous situations

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

## Version History
- v1.0.0
  - Initial release
  - Implemented core features
  - Added MCM configuration support

## Contact
- Author: Ahao
- Email: 2285813721@qq.com

## License
This mod is for personal use only. Commercial use is prohibited.

