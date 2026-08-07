# 官方 CSV 模板表头参考

> **来源**：官方 Mod 模板仓库 meowalive/apocalyptic-journey-mod-tutorial（MIT License, (c) 2026 MeowAlive）的 ModTemplate/Scripts/Lib/DataConfigs/。
> 每个表只提取「表头行 + 中文注释行」；需要完整数据或刷新时，git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git 查看。
> 若游戏版本更新，重新从仓库提取即可刷新本文件。

> 注意：游戏按**列名**（非列序）读取 CSV。写 Mod 的 CSV 时列名必须与下表完全一致；不要臆造 Cost / CardType / Damage / Defend / Magic / Heal / Buff / Exhaust / MaxLayer 等列。

---

## Data/

### Data/Achievement/achievement.csv
```
Id,ListenScript,Type,Reward,RewardType
id,,种类,,
```

### Data/Affection/Amelia.csv
```
Id,Character,Reward,InitScript,Target,Belong
,哪个角色,真理之晶奖励数,控制是否达成,达成数量,属于等级
```

### Data/Blessing/blessing.csv
```
Id,Weight,OwnScript,FightScript,Icon,Type,Source,Rarity
id,权重,本身脚本;,战斗脚本,图片名称,类型,选项 1,稀有度
```

### Data/Blessing/CrowdfundingBlessing.csv
```
Id,Weight,OwnScript,FightScript,Icon,Type,Source,Rarity,PackBelong
id,权重,本身脚本;,战斗脚本,图片名称,类型,选项 1,稀有度,cardpack id
```

### Data/Buff/buff.csv
```
Id,InitScript,ApplyScript,ClearScript,ReducePerTurn,ReducePerAttacked,ReducePerUse,UpperBound,Icon,Type,Rarity,Effects,SoundEffects,Action,CanZero
BUFF的ID（唯一英文）,,BUFF生效时的效buff_BUFF的ID（唯一英文）时的效果,清除时效果,层数每回合减少数,层数每受击减少数,层数每行动减少数,层数上限,图标路径,类型,稀有度,特效,,,
```

### Data/Buff/SpecialBuff.csv
```
Id,InitScript,ApplyScript,ClearScript,ReducePerTurn,ReducePerAttacked,ReducePerUse,UpperBound,Icon,Type,Rarity,Effects,SoundEffects,Action,CanZero
BUFF的ID（唯一英文）,,BUFF生效时的效果,清除时效果,层数每回合减少数,层数每受击减少数,层数每使用卡牌减少数,层数上限,图标路径,类型,,,,纯攻击动画,
```

### Data/Card/blood.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong
唯一的标识（不能重复）,稀有度,花费,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效路径,动作,
```

### Data/Card/burningcard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action
唯一的标识（不能重复）,稀有度,消耗的费用,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效,动作
```

### Data/Card/card.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action
唯一的标识（不能重复）,稀有度,消耗的费用,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效,动作
```

### Data/Card/careercard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action
唯一的标识（不能重复）,稀有度,消耗的费用,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效,动作
```

### Data/Card/combo.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong
唯一的标识（不能重复）,稀有度,花费,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效,动作,cardpack id
```

### Data/Card/counterattackcard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong
唯一的标识（不能重复）,稀有度,花费,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效,动作,
```

### Data/Card/Crowdfundingcard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong
唯一的标识（不能重复）,稀有度,花费,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效,动作,cardpack id
```

### Data/Card/cursecard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action
唯一的标识（不能重复）,稀有度,花费,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效路径,动作
```

### Data/Card/elementscard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong
唯一的标识（不能重复）,稀有度,消耗的费用,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效,动作,cardpack id
```

### Data/Card/healcard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action
唯一的标识（不能重复）,稀有度,消耗的费用,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效路径,动作
```

### Data/Card/luckycard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong
唯一的标识（不能重复）,稀有度,花费,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效路径,动作,cardpack id
```

### Data/Card/nocard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action
唯一的标识（不能重复）,稀有度,消耗的费用,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效,动作
```

### Data/Card/onlinecard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong
唯一的标识（不能重复）,稀有度,花费,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效路径,动作,cardpack id
```

### Data/Card/perceivecard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action
唯一的标识（不能重复）,稀有度,花费,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效路径,动作
```

### Data/Card/ReturnAgain.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong
唯一的标识（不能重复）,稀有度,花费,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效路径,动作,cardpack id
```

### Data/Card/ritualcard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong
唯一的标识（不能重复）,稀有度,消耗的费用,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效,动作,cardpack id
```

### Data/Card/SpellCard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong
唯一的标识（不能重复）,稀有度,花费,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效路径,动作,
```

### Data/Card/timekeeper.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong
唯一的标识（不能重复）,稀有度,花费,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效路径,动作,cardpack id
```

### Data/Card/universalcard.csv
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong
唯一的标识（不能重复）,稀有度,花费,标签,卡牌初始化脚本,卡牌抽到后执行,卡牌使用后执行,卡牌进入弃牌堆后执行,图标资源的路径,特效,动作,cardpack id
```

### Data/Career/career.csv
```
Id,SanMax,SkillScript,Animation,Vocal,Skill1,Skill2,ChoiceIcon,DollIcon,Character,Avatar,CareerImage,ActionImage1,ActionImage2,Dialogue,EmojiPath,AttackEffect,SkillEffect,HitEffect,DefendEffect
唯一id,最大san,被动技能&初始化,动画文件夹路径,配音文件夹路径,技能卡牌，后续要改,,,玩偶路径,立绘路径,头像路径,职业图路径,主动技能图标1,,对话图路径，虽然其实用在statusui,表情包路径,,,,
```

### Data/Coin/coin.csv
```
Id,Type,NodeId,TokenType,TokenWeight
地图id,种类,对应种类的节点Id,代币类型,代币权重
```

### Data/Destiny/destiny.csv
```
Id,Rarity,OwnScript,FightScript,Icon,Type
Id（唯一标识）,稀有度,获得生效,完成生效,图片名称,类型
```

### Data/Dialogue/2Fight.csv
```
Id,BaseScript,EndScript,Roles,EventName,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",何时执行对话（事件名）,选项数量,选项脚本,
```

### Data/Dialogue/3Fight.csv
```
Id,BaseScript,EndScript,Roles,EventName,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",何时执行对话（事件名）,选项数量,选项脚本,
```

### Data/Dialogue/4Fight.csv
```
Id,BaseScript,EndScript,Roles,EventName,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",何时执行对话（事件名）,选项数量,选项脚本,
```

### Data/Dialogue/7Node.csv
```
Id,BaseScript,EndScript,Roles,EventName,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",何时执行对话（事件名）,选项数量,选项脚本,
```

### Data/Dialogue/ending.csv
```
Id,BaseScript,EndScript,Roles,EventName,ChoiceCount,ChoiceScript1
对话Id,,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",,选项数量,选项脚本
```

### Data/Dialogue/FirstBless.csv
```
Id,BaseScript,EndScript,Roles,EventName,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",何时执行对话（事件名）,选项数量,选项脚本,
```

### Data/Dialogue/FirstFight.csv
```
Id,BaseScript,EndScript,Roles,EventName,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",何时执行对话（事件名）,选项数量,选项脚本,
```

### Data/Dialogue/FirstShop.csv
```
Id,BaseScript,EndScript,Roles,EventName,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",何时执行对话（事件名）,选项数量,选项脚本,
```

### Data/Dialogue/Mapselect.csv
```
Id,BaseScript,EndScript,Roles,EventName,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",何时执行对话（事件名）,选项数量,选项脚本,
```

### Data/Dialogue/SecondAD.csv
```
Id,BaseScript,EndScript,Roles,EventName,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",何时执行对话（事件名）,选项数量,选项脚本,
```

### Data/Dialogue/StartTutorial.csv
```
Id,BaseScript,EndScript,Roles,EventName,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",何时执行对话（事件名）,选项数量,选项脚本,
```

### Data/Dialogue/WinChruch.csv
```
Id,BaseScript,EndScript,Roles,EventName,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",何时执行对话（事件名）,选项数量,选项脚本,
```

### Data/Effect/effect.csv
```
Id,InitScript,Timepoint,Script,Cost,DesValType
唯一的标识（不能重复）,初始化脚本,时点,脚本,负荷,占位符类型
```

### Data/EnchTag/Crowdfundingenchtag.csv
```
Id,Tag,LoadScript,DrawScript,DropScript,PreUseScript,UseScript,UnloadScript,Rarity,Icon,PackBelong
Id（唯一标识）,添加的标签,初始效果,抽到时的效果,进入弃牌堆的效果,,使用时的效果,卸下效果,稀有度,,cardpack id
```

### Data/EnchTag/enchtag.csv
```
Id,Tag,LoadScript,DrawScript,DropScript,PreUseScript,UseScript,UnloadScript,Rarity,Icon
Id（唯一标识）,添加的标签,初始效果,抽到时的效果,进入弃牌堆的效果,,使用时的效果,卸下效果,稀有度,
```

### Data/Enemy/enemy.csv
```
Id,Name,Hp,Attack,Defend,ActionCount,Rarity,InitScript,CardList,AttributeText,Animation
Id,名字,血量,攻击力,防御盾,行动次数,单体强度,初始buff,卡牌列表,"用;分开,不能加空格(现在只是给个演示,因为怪物要重写特性)",
```

### Data/EnemyBless/enemybless.csv
```
Id,Rarity,FightScript
id,权重,战斗脚本
```

### Data/EnemyCard/enemycard.csv
```
Id,InitScript,TargetScript,UseScript,BackIcon,Icon,Tag,Effects,Action
Id,卡牌初始化脚本（使用前执行）,卡牌出现时执行（设定目标）,卡牌使用后执行,行动底面,行动图标,唯一特性,特效,动作
```

### Data/EventList/event.csv
```
Id,1Script,2Script,3Script,4Script,InitScript,EntryScript
Id,选项1脚本,选项2脚本,选项3脚本;,选项4脚本,含所有选项的解锁条件,退出脚本，为后置事件准备的
```

### Data/Food/food.csv
```
Id,Icon,Hp,HPPercent,Rarity
,图片路径,恢复血量,提高血量百分比,占比
```

### Data/Hard/Hard.csv
```
Id,Belong,Level,UseScript,FightScript,MaxCount,Type
唯一Id,属于哪类,难度等级,对应脚本,,,
```

### Data/HouseDialogue/faildialog1.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/faildialog2.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/faildialog3.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog1.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog10.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog11.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog12.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog13.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog14.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog15.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog16.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog17.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog2.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog3.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog4.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog5.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog6.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog7.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog8.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogue/windialog9.csv
```
Id,BaseScript,EndScript,Roles,ChoiceCount,ChoiceScript1,ChoiceScript2
对话Id,初始脚本,对话结束时执行,"出现的角色Id，说话者用<>包括，用,分割",选项数量,选项脚本,
```

### Data/HouseDialogueConfig/fail.csv
```
Id,DialogueId,Build
唯一标识（数字）,对话前缀,建筑归属
```

### Data/HouseDialogueConfig/success.csv
```
Id,DialogueId,Build
唯一标识（数字）,对话前缀,建筑归属
```

### Data/Item/item.csv
```
Id,Rarity,Type,Icon
唯一的标识（不能重复）,稀有度,类型,图标资源的路径
```

### Data/Item/materials.csv
```
Id,Dimensions,Type,Icon,Rarity
（id）唯一标识,成分,类型,图标路径,稀有度
```

### Data/Level/level.csv
```
Id,EnemyIds,Note,Level,BGM
Id,敌人Id的数组,备注,出现层数,重载音乐
```

### Data/Map/map.csv
```
Id,Type,NodeId,Level
地图id,种类,对应种类的节点Id,对应出现层数
```

### Data/OutSideShop/outsideshop.csv
```
Id,PriceType,Price,TimePrice,Icon,Type,Toid,BuyScript,BuyCount,CanClose
这里唯一id,价格种类,真理价格,,图标,种类,对应类的id,购买后生效的脚本,购买次数,
```

### Data/Partner/Partner.csv
```
Id,InitScript,ChoiceIcon,Model,Animation,Bless,CareerImage
Id,初始buff,玩偶图标,怪物的模型路径,,给与的祝福,
```

### Data/PartnerCard/PartnerCard.csv
```
Id,InitScript,TargetScript,UseScript,Icon,Tag,Effects,Action
Id,卡牌初始化脚本（使用前执行）,出现时执行（选定目标）,卡牌使用后执行,行动图标,唯一特性,特效,动作
```

### Data/Relic/CrowdFundingRelic.csv
```
Id,Rarity,OwnScript,FightScript,Icon,PackBelong
id,稀有度,获取时的脚本,战斗脚本,图片路径,cardpack id
```

### Data/Relic/relic.csv
```
Id,Rarity,OwnScript,FightScript,Icon
id,稀有度,获取时的脚本,战斗脚本,图片路径
```

### Data/RoleData/role.csv
```
Id,Avatar,CharacterImage,HouseAvatar
唯一标识,头像路径,立绘路径,场景对话头像
```

### Data/SlotCal/slotCal.csv
```
Id,Type,NodeId
地图id,种类,对应种类的节点Id
```

### Data/SlotReward/slotReward.csv
```
Id,Type,NodeId
地图id,种类,对应种类的节点Id
```

### Data/Task/testTask.csv
```
Id,Reward,InitScript,Target,Belong
Id,真理之晶奖励数,控制是否达成,达成数量,属于哪类
```

### Data/Tutorial/tutorial.csv
```
Id,EventName,Initial
Id,触发时点,初始状态
```

---

## Text/

### Text/Achievement/achievement.csv
```
Id,Name,Description,Name_zh-Hant,Name_en,Name_ja,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,,描述,,,,描述,Description,説明
```

### Text/Affection/Amelia.csv
```
Id,Name,Name_zh-Hant,Name_en,Name_ja,NeedDes,NeedDes_zh-Hant,NeedDes_en,NeedDes_ja
,任务名字,任務名字,Task Name,任務名,完成条件描述,完成條件描述,Completion Requirement Description,達成条件説明
```

### Text/Announcement/Announcement.csv
```
Id,Note,Image,Name,Description,Name_zh-Hant,Name_en,Name_ja,Description_zh-Hant,Description_en,Description_ja,Ver,Date
Id,备注,图片路径,标题,正文,標題,Title,タイトル,タイトル,正文,本文,版本,日期
```

### Text/Blessing/blessing.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja,Tips,Tips_zh-Hant,Tips_en,Tips_ja
唯一的标识（不能重复）,备注,名称,名稱,Name,名称,描述,描述,Description,説明,剧情描述,劇情描述,Tips,ストーリー説明
```

### Text/Blessing/CrowdfundingBlessing.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja,Tips,Tips_zh-Hant,Tips_en,Tips_ja
唯一的标识（不能重复）,备注,名称,名稱,Name,名称,描述,描述,Description,説明,剧情描述,劇情描述,Tips,ヒント
```

### Text/Buff/buff.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_ja,Description_en
唯一的标识（不能重复）,备注,名称,名稱,Name,名称,描述,描述,説明,Description
```

### Text/Buff/SpecialBuff.csv
```
Id,Note,Name,Name_en,Name_zh-Hant,Name_ja,Description,Description_en,Description_zh-Hant,Description_ja
唯一的标识（不能重复）,备注,名称,Name,名稱,名称,描述,Description,描述,説明
```

### Text/Card/blood.csv
```
Id,是否完成,Type,Note,Name,Name_en,Name_zh-Hant,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,FALSE,类型,备注,名称,Name,名稱,名称,描述,描述,Description,説明
```

### Text/Card/burningcard.csv
```
Id,Type,已完成,Name,Name_en,Name_zh-Hant,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja,
唯一的标识（不能重复）,卡牌类型,FALSE,名称,Name,名稱,名称,描述,描述,Description,説明,
```

### Text/Card/card.csv
```
Id,Note,Type,Name,Name_en,Name_zh-Hant,Name_ja,是否完成,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,,卡牌类型,名称,Name,名稱,名称,FALSE,描述,描述,Description,説明
```

### Text/Card/careercard.csv
```
Id,Type,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,卡牌类型,消耗费用,名称,名稱,Name,名称,描述,描述,Description,説明
```

### Text/Card/combo.csv
```
Id,Type,Note,Name,Description,Name_zh-Hant,Name_en,Description_zh-Hant,Description_en,Name_ja,Description_ja
唯一的标识（不能重复）,类型,备注,名称,描述,名稱,Name,描述,Description,名称,説明
```

### Text/Card/counterattackcard.csv
```
Id,Type,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja,第 1 列,第 2 列,第 3 列,第 4 列,第 5 列,第 6 列,第 7 列,第 8 列
唯一的标识（不能重复）,类型,备注,名称,Name_zh-Hant,Name_en,Name_ja,描述,Description_zh-Hant,Description_en,Description_ja,,,,,,,,
```

### Text/Card/Crowdfundingcard.csv
```
Id,Type,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,类型,备注,名称,Name_zh-Hant,Name_en,Name_ja,描述,Description_zh-Hant,Description_en,Description_ja
```

### Text/Card/cursecard.csv
```
Id,Type,已完成,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,类型,TRUE,备注,名称,名稱,Name,名称,描述,描述,Description,説明
```

### Text/Card/elementscard.csv
```
Id,Type,是否完成,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja,
唯一的标识（不能重复）,卡牌类型,FALSE,费用,名称,名稱,Name,名称,描述,描述,Description,説明,
```

### Text/Card/healcard.csv
```
Id,Type,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,卡牌类型,备注,名称,名稱,Name,名称,描述,描述,Description,説明
```

### Text/Card/luckycard.csv
```
Id,Type,是否完成,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,类型,FALSE,备注,名称,名稱,Name,名称,描述,描述,Description,説明
```

### Text/Card/nocard.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Type,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,,名称,名稱,Name,名称,卡牌类型,描述,描述,Description,説明
```

### Text/Card/onlinecard.csv
```
Id,Type,Note,Name,Name_en,Name_zh-Hant,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,类型,备注,名称,Name,名稱,名称,描述,描述,Description,説明
```

### Text/Card/perceivecard.csv
```
Id,Note,Name,Type,Description,Name_en,Description_en,Name_zh-Hant,Description_zh-Hant,Name_ja,Description_ja,第 1 列
唯一的标识（不能重复）,备注,名称,类型,描述,Name,Description,名稱,描述,名称,説明,FALSE
```

### Text/Card/ReturnAgain.csv
```
Id,Type,是否完成,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,类型,FALSE,备注,名称,名稱,Name,名称,描述,描述,Description,説明
```

### Text/Card/ritualcard.csv
```
Id,Type,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,卡牌类型,费用,名称,名稱,Name,名称,描述,描述,Description,説明
```

### Text/Card/SpellCard.csv
```
Id,Type,是否完成,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,类型,FALSE,备注,名称,名稱,Name,名前,描述,描述,Description,説明
```

### Text/Card/timekeeper.csv
```
Id,是否完成,Type,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,FALSE,类型,备注,名称,名稱,Name,名称,描述,描述,Description,説明
```

### Text/Card/universalcard.csv
```
Id,第 1 列,Type,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,FALSE,类型,备注,名称,名稱,Name,名称,描述,描述,Description,説明
```

### Text/CardPack/cardpack.csv
```
Id,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja,Icon,Type
唯一的标识（不能重复）,,,,,描述,描述,Description,説明,图片路径,类型
```

### Text/Career/career.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Title,Title_zh-Hant,Title_en,Title_ja,Description,Description_zh-Hant,Description_en,Description_ja,Action1,Action1_zh-Hant,Action1_en,Action1_ja,Action2,Action2_zh-Hant,Action2_en,Action2_ja,Passive1,Passive1_zh-Hant,Passive1_en,Passive1_ja,Passive2,Passive2_zh-Hant,Passive2_en,Passive2_ja
唯一id,备注,称号,稱號,Title,称号,称号,稱號,Title,称号,描述,描述,Description,説明,主动1描述,主動1描述,Active Skill 1 Description,アクション1説明,主动2,主動2,Active Skill 2,アクション2,被动1,被動1,Passive Skill 1,パッシブ1,被动2,被動2,Passive Skill 2,パッシブ2
```

### Text/Coin/coin.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
地图id,备注,名字,名字,Name,名前,描述,描述,Description,説明
```

### Text/Destiny/destiny.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja,Tips,Tips_zh-Hant,Tips_en,Tips_ja
（Id）唯一标识,备注,名称,名稱,Name,名称,效果描述,效果描述,Effect Description,効果説明,剧情描述,劇情描述,Story Description,ストーリー説明
```

### Text/Dialogue/2Fight.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja,Notification,Notification_zh-Hant,Notification_en,Notification_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,,,,,
```

### Text/Dialogue/3Fight.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja,Notification,Notification_zh-Hant,Notification_en,Notification_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,,,,,
```

### Text/Dialogue/4Fight.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja,Notification,Notification_zh-Hant,Notification_en,Notification_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,,,,,
```

### Text/Dialogue/7Node.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja,Notification,Notification_zh-Hant,Notification_en,Notification_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,,,,,
```

### Text/Dialogue/ending.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,Notification,Notification_zh-Hant,Notification_en,Notification_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,,,,
```

### Text/Dialogue/FirstBless.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja,Notification,Notification_zh-Hant,Notification_en,Notification_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,,,,,
```

### Text/Dialogue/FirstFight.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja,Notification,Notification_zh-Hant,Notification_en,Notification_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,,,,,
```

### Text/Dialogue/FirstShop.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja,Notification,Notification_zh-Hant,Notification_en,Notification_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,,,,,
```

### Text/Dialogue/Mapselect.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja,Notification,Notification_zh-Hant,Notification_en,Notification_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,,,,,
```

### Text/Dialogue/SecondAD.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja,Notification,Notification_zh-Hant,Notification_en,Notification_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,,,,,
```

### Text/Dialogue/StartTutorial.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja,Notification,Notification_zh-Hant,Notification_en,Notification_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,,,,,
```

### Text/Dialogue/WinChruch.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/EnchTag/Crowdfundingenchtag.csv
```
Id,Note,Name,Description,Name_zh-Hant,Name_en,Description_zh-Hant,Description_en,Name_ja,Description_ja
Id（唯一标识）,备注,名称,效果描述,名稱,Name,效果描述,Effect Description,名称,効果説明
```

### Text/EnchTag/enchtag.csv
```
Id,Note,Name,Description,Name_zh-Hant,Name_en,Description_zh-Hant,Description_en,Name_ja,Description_ja
Id（唯一标识）,备注,名称,效果描述,名稱,Name,效果描述,Effect Description,名称,効果説明
```

### Text/Enemy/enemy.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description1,Description1_zh-Hant,Description1_en,Description1_ja,Description2,Description2_zh-Hant,Description2_en,Description2_ja,Level
Id,备注,名字,名字,Name,名前,图鉴特性1,圖鑒特性1,Bestiary Trait 1,图鉴特性1,,,,,出没层数
```

### Text/EnemyBless/enemybless.csv
```
Id,Description,Description_zh-Hant,Description_en,Description_ja,Name,Name_zh-Hant,Name_en,Name_ja
Id,描述,描述,Description,説明,名字,名字,Name,名前
```

### Text/EnemyCard/enemycard.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯1标识,备注,名称,名稱,Name,名称,描述,描述,Description,説明
```

### Text/EventList/event.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,TotalDescribe,TotalDescribe_zh-Hant,TotalDescribe_en,TotalDescribe_ja,1Describe,1Describe_zh-Hant,1Describe_en,1Describe_ja,2Describe,2Describe_zh-Hant,2Describe_en,2Describe_ja,3Describe,3Describe_zh-Hant,3Describe_en,3Describe_ja,4Describe,4Describe_zh-Hant,4Describe_en,4Describe_ja,CompareUse,CompareUse_zh-Hant,CompareUse_en,CompareUse_ja
Id,备注,名字,名字,Name,名前,整体文本,整體文本,Full Text,整体文本,,,,,,,,,,,,,,,,,,,,
```

### Text/Hard/Hard.csv
```
Id,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja,第 1 列
唯一id,前缀名,前綴名,Prefix Name,前缀名,后缀描述,后綴描述,Suffix Description,后缀描述,
```

### Text/HouseDialogue/faildialog1.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/faildialog2.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/faildialog3.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog1.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog10.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog11.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog12.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog13.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog14.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog15.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog16.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog17.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog2.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog3.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog4.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog5.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog6.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog7.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog8.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/HouseDialogue/windialog9.csv
```
Id,Text,Text_zh-Hant,Text_en,Text_ja,ChoiceText1,ChoiceText1_zh-Hant,ChoiceText1_en,ChoiceText1_ja,ChoiceText2,ChoiceText2_zh-Hant,ChoiceText2_en,ChoiceText2_ja
对话Id,对话正文,對話正文,Dialogue Text,对话正文,选项1文本,選項1文本,Choice 1 Text,选项1文本,,,,
```

### Text/IllustratedBook/gameguide.csv
```
Id,Note,Chapter,Chapter_zh-Hant,Chapter_en,Chapter_ja,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja,Tip,Tip_zh-Hant,Tip_en,Tip_ja,Text,Text_zh-Hant,Text_en,Text_ja
Id,Note,Chapter,Chapter,Chapter_en,Chapter,Chapter,Name,Name,名前,Name_en,Name,Description,説明,Description,Description_en,Description,ヒント,Tip,Tip,Tip_en,テキスト
```

### Text/Item/item.csv
```
Id,Note,Name,Description,Name_zh-Hant,Name_en,Name_ja,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,备注,名称,描述,名稱,Name,名称,名称,描述,説明
```

### Text/Item/materials.csv
```
Id,Note,Name,Description,Name_zh-Hant,Name_en,Name_ja,Description_zh-Hant,Description_en,Description_ja
（id）唯一标识,备注,名称,描述,名稱,Name,名前,名称,描述,説明
```

### Text/KeyWordsDic/keyword.csv
```
Id,Note,Description,Keywords,Keywords_zh-Hant,Keywords_en,Description_zh-Hant,Description_en,Keywords_ja,Description_ja,ShouldShow
Id,备注,描述,关键词,關鍵詞,keyword,描述,description,Keywords_ja,Description_ja,
```

### Text/Map/map.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja,AttributeText,AttributeText_zh-Hant,AttributeText_en,AttributeText_ja
地图id,备注,名字,名字,Name,名前,描述,描述,Description,説明,,,,
```

### Text/Narration/narration.csv
```
Id,Time,Text,Text_zh-Hant,Text_en,Text_ja,Note,Path
唯一的标识（不能重复）,时点,文本,文本,Text,テキスト,日语文本,备注
```

### Text/OutSideShop/outsideshop.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja,Tag1,Tag1_zh-Hant,Tag1_en,Tag1_ja,Tag2,Tag2_zh-Hant,Tag2_en,Tag2_ja,Tag3,Tag3_zh-Hant,Tag3_en,Tag3_ja,Tag4,Tag4_zh-Hant,Tag4_en,Tag4_ja
Id（唯一标识）,备注,名字,名字,Name,名前,描述,描述,Description,説明,显示的tag,顯示的tag,Displayed Tag,显示的tag,,,,,,,,,,,,
```

### Text/Partner/Partner.csv
```
Id,Note,Name,Description,Name_zh-Hant,Name_en,Description_zh-Hant,Description_en,Name_ja,Description_ja,Passive1,Passive1_zh-Hant,Passive1_en,Passive1_ja
Id,备注,名字,描述,名字,Name,描述,Description,名前,説明,,,,
```

### Text/PartnerCard/PartnerCard.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一标识,备注,名称,名稱,Name,名称,描述,描述,Description,説明
```

### Text/Relic/CrowdFundingRelic.csv
```
Id,Note,Series,Tag,Name,Name_zh-Hant,Name_en,Name_ja,Tips,Tips_zh-Hant,Tips_en,Tips_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,备注,系列,标签,名称,名稱,Name,名称,剧情描述,劇情描述,Tips,ストーリー説明,描述,描述,Description,説明
```

### Text/Relic/relic.csv
```
Id,Note,Series,Tag,Name,Name_zh-Hant,Name_en,Name_ja,Tips,Tips_zh-Hant,Tips_en,Tips_ja,Description,Description_zh-Hant,Description_en,Description_ja
唯一的标识（不能重复）,备注,系列,标签,名称,名稱,Name,名称,剧情描述,劇情描述,Tips,ストーリー説明,描述,描述,描述,説明
```

### Text/RoleData/role.csv
```
Id,Name,Name_en,Name_zh-Hant,Name_ja,Title,Title_en,Title_zh-Hant,Title_ja,Dia,Dia_en,Dia_zh-Hant,Dia_ja
唯一标识,,,,,头衔,Title,頭銜,头衔,场景对话头衔,Scene dialogue title,場景對話頭銜,シーン会話の肩書き
```

### Text/SlotCal/slotCal.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
地图id,备注,名字,名字,Name,名前,描述,描述,Description,説明
```

### Text/SlotReward/slotReward.csv
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
地图id,备注,名字,名字,Name,名前,描述,描述,Description,説明
```

### Text/Task/testTask.csv
```
Id,Name,Name_zh-Hant,Name_en,Name_ja,Des,Des_zh-Hant,Des_en,Des_ja,NeedDes,NeedDes_zh-Hant,NeedDes_en,NeedDes_ja
Id,名字,名字,Name,名前,任务描述,任務描述,Task Description,任務説明,完成条件描述,完成條件描述,Completion Requirement Description,達成条件説明
```

### Text/Tutorial/tutorial.csv
```
Id,Note,Image,Name,Description,Name_zh-Hant,Name_en,Description_zh-Hant,Description_en,Name_ja,Description_ja
Id,备注,图片路径,标题,正文,標題,Title,正文,Description,タイトル,本文
```

