"""Author the shared festival layout; original game textures remain runtime references.

Run Prepare-Xiaowai.py separately to normalize the user's approved sprite reference.
"""
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'Welcome/assets'
ASSETS.mkdir(parents=True, exist_ok=True)

Image.new('RGBA',(16,16)).save(ASSETS/'collision.png')

layout = {'Preview':[0,46,50,39], 'Entry':[1,54], 'Exit':[0,53,1,3], 'Pond':[36,76], 'Clear':[], 'Copies':[], 'Tiles':[], 'Sprites':[], 'Actors':[]}
layout['ClearBushes'] = [[35,73,8,10], [32,60,10,8], [40,58,5,4], [17,70,6,6], [43,62,4,3]]
layout['ClearTrees'] = [[18,72,3,3]]


def clear(area, layers=('Buildings','Front','AlwaysFront')):
    layout['Clear'].append({'Area':area,'Layers':list(layers)})


def copy(mapname, source, target, layers=('Buildings','Front','AlwaysFront'), festival_only=True):
    layout['Copies'].append({'Map':'Maps/'+mapname,'Source':source,'Target':target,'Layers':list(layers),'FestivalOnly':festival_only})


def tile(layer,x,y,texture,index,properties=None):
    entry={'Layer':layer,'At':[x,y],'Texture':texture,'Index':index}
    if properties: entry['Properties']=properties
    layout['Tiles'].append(entry)


def sprite(texture,source,at,scale=1):
    layout['Sprites'].append({'Texture':texture,'Source':source,'At':at,'Scale':scale})


def horizontal(x1,x2,y):
    for x in range(x1,x2+1):
        col = 4 if x==x1 else 6 if x==x2 else 5
        tile('Front',x,y,'Maps/Festivals',col)
        tile('Buildings',x,y+1,'Maps/Festivals',col+32)


def vertical(x,y1,y2):
    tile('Front',x,y1,'Maps/Festivals',7)
    for y in range(y1+1,y2+1):
        tile('Buildings',x,y,'Maps/Festivals',39 if (y-y1)%3==1 else 71)

# The northern cliff already blocks movement: only close the three-tile road.
# Remove the old fence end without touching the bench immediately below it.
clear([16,55,1,1], ('Front',))
clear([16,56,1,1], ('Buildings',))
clear([24,47,1,7])
horizontal(20,22,46)
horizontal(47,48,56)
horizontal(0,15,55)
horizontal(16,27,75)
horizontal(27,48,82)
vertical(27,75,82)
vertical(15,56,75)
vertical(48,57,82)
# Close the grass strip between Emily's booth and the clinic tree.
vertical(28,47,56)
# Apply the horizontal end last: both rails share its complete post and foot.
# Otherwise the vertical ribbon overwrites the horizontal post's Buildings tile.
horizontal(28,32,55)
# Two exact vanilla booths, deliberately placed side by side along the north.
copy('Town-EggFestival',[19,49,4,5],[17,48])
copy('Town-EggFestival',[19,49,4,5],[23,48])

# Empty original Winter Dining Table, keeping its tablecloth and legs intact.
clear([33,62,5,3])
for y in range(3):
    for x in range(5):
        tile('Buildings',33+x,62+y,'TileSheets/furniture',800+y*32+x)
for x,y in ((23,65),(41,59),(22,72),(35,71)):
    copy('Town-EggFestival',[38,57,3,2],[x,y])

# The pond is rendered by the actual FishPond.draw method, including moving water.
# Its native water is marked invisible to avoid a second world-water pass.
for y in range(5):
    for x in range(5):
        props={'Water':'T'} if 0<x<4 and 0<y<4 else {}
        tile('Buildings',36+x,76+y,'assets/collision.png',0,props)
        if 0<x<4 and 0<y<4:
            tile('Back',36+x,76+y,'Maps/spring_outdoorsTileSheet',1246,{'Water':'I'})

# Wedding arch and flower columns use the exact vanilla wedding source pixels.
sprite('LooseSprites/Cursors',[540,1196,98,79],[26*16,57*16+8])
for x in (26,31):
    tile('Buildings',x,61,'assets/collision.png',0)
for x,y in ((25,62),(32,62),(25,71),(32,71)):
    sprite('LooseSprites/Cursors',[527,1249,12,25],[x*16,y*16-9])
    tile('Buildings',x,y,'assets/collision.png',0)

# One cake on an otherwise empty native table.
sprite('Maps/springobjects',[80,144,16,16],[34*16+8,62*16],2)
# Wrapped gifts beside Xiaowai.
for x,y,index in ((34,60,729),(35,60,730),(36,60,731)):
    tile('Buildings',x,y,'Maps/Festivals',index)

# Two small vanilla props add investigation targets without changing town roads.
for y in range(2):
    for x in range(2):
        tile('Front',46+x,59+y,'Maps/Festivals',(13+y)*32+12+x)
tile('Buildings',46,61,'Maps/Festivals',36)
tile('Front',32,77,'Maps/Festivals',239)
tile('Buildings',32,78,'Maps/Festivals',271)

# Clint's native anvil below the balloons. The tool/sparks render over its steel face.
sprite('TileSheets/Craftables',[80,1152,16,32],[45*16,60*16+6])
layout['Sprites'][-1]['SortY']=61*16+7  # anvil behind the worker; never raise the NPC over map foreground
for y in (61,62):tile('Buildings',45,y,'assets/collision.png',0)

# User preference: the empty vanilla fair display, containing exactly one strawberry.
for y in range(3):
    for x in range(3):
        tile('Buildings',18+x,72+y,'Maps/Festivals',(8+y)*32+29+x,{'Action':'AnniversaryStrawberry'})
# Restore the complete top rail from the tile above the display's main panels.
for x in range(3):
    tile('Buildings',18+x,71,'Maps/Festivals',7*32+29+x)
sprite('Maps/springobjects',[256,256,16,16],[18*16+8,72*16+1],2)

# Small vanilla flowers and lanterns draw the eye along the perimeter.
for x,y in ((21,58),(27,58),(37,67),(38,73)):
    tile('Buildings',x,y,'Maps/Festivals',34)

# All 34 native friendship characters, plus eight visitors and Xiaowai.
actors=[('Pierre',18,51,2),('Emily',24,51,2),('Willy',42,75,2),('Gus',29,63,1),
        ('Xiaowai',35,59,2),('Lewis',29,71,2),('Caroline',22,58,2),('Robin',18,54,1),
        ('Evelyn',24,63,2),('Abigail',23,69,1),('Sam',33,70,3),('Penny',25,70,3),
        ('Haley',33,59,1),('Marnie',21,74,3),('Alex',38,59,1),('Elliott',44,73,3),
        ('Harvey',33,57,2),('Leah',33,73,0),('Maru',39,57,2),('Sebastian',31,70,1),
        ('Shane',46,74,3),('Clint',44,61,1),('Demetrius',26,54,3),('George',26,64,3),
        ('Jas',24,74,0),('Jodi',20,56,2),('Kent',23,56,3),('Krobus',45,80,3),
        ('Linus',18,58,1),('Pam',44,58,3),('Sandy',37,57,2),('Vincent',23,67,2),
        ('Wizard',29,78,1),('Dwarf',43,81,3),('Leo',40,74,1),('Marlon',33,81,0),
        ('Gunther',38,61,2),('Birdie',46,76,3),('Governor',42,57,2),('Morris',46,57,3),
        ('Mister Qi',29,80,1),('Bouncer',31,74,2),('Old Mariner',46,81,2)]
dialogue={
    'Evelyn':'我把开得最好的一枝花留给了乔治。他说不需要，却一直没有放下。',
    'George':'我只是替艾芙琳拿着花。你看什么？……好吧，是挺好看的。',
    'Linus':'我也能在这里坐一会儿吧？一朵小花，就能让任何地方像家。',
    'Alex':'今天不比谁跑得快。我答应奶奶，陪她慢慢走。',
    'Elliott':'我写了很长一段祝词，后来只留下四个字：明天见面。',
    'Harvey':'我带了急救箱，不过今天最忙的可能是格斯。希望大家只是吃得太开心。',
    'Leah':'两根弯曲的树枝也能搭成拱门。不必长得一样，也可以一起撑住什么。',
    'Maru':'我想做一台记录拥抱的机器，结果发现，按按钮会耽误拥抱。',
    'Sebastian':'我只是来看看。山姆如果说他早就猜到了，你可别告诉他我笑了。',
    'Shane':'贾斯让我替她保管蝴蝶结。比照顾一群鸡还需要小心。',
    'Clint':'戒指可以量尺寸，心意却没有尺子。眼镜倒是能修，把破的交给我就好。',
    'Demetrius':'本来想记录大家的心跳变化，罗宾把记录板换成了我的手。',
    'Jas':'我给自己的布娃娃也留了蛋糕。它很有礼貌，一口都没有偷吃。',
    'Jodi':'今天不用赶着做饭，我反而不知道该把手放哪里了。肯特说，放在他手里就好。',
    'Kent':'这里的喧闹让我有些紧张。不过，听见家人的笑声就好多了。',
    'Krobus':'我挑了一处阴凉的位置。鲜花不介意我从哪里来，你们好像也不介意。',
    'Pam':'今天这杯不加酒。我答应潘妮，回去的路陪她一起走。',
    'Sandy':'沙漠里也会开花。艾米丽邀请我的时候，我就把今天从日历上圈出来了。',
    'Vincent':'如果我把两块蛋糕叠起来，是不是就只算一块了？潘妮老师说这道题不能这么算。',
    'Wizard':'维系两个人的未必是魔法。我观察了很久，似乎也可能是每天记得留一盏灯。',
    'Dwarf':'这些闪闪发亮的丝带能挖到吗？如果不能，我愿意用一块好石头交换。',
    'Leo':'我想把今天讲给鹦鹉听。它们可能先问：蛋糕的种子在哪里？',
    'Marlon':'今天不需要剑。我把最锋利的东西留在了家里，免得吓到来宾。',
    'Gunther':'不是每件值得记住的东西都能放进展柜。今天的笑声就是。',
    'Birdie':'海风把许多事带走，也会把新朋友送来。今天这一趟很值得。',
    'Governor':'今天我只是来宾。祝词短一点，大家就能早点吃蛋糕。',
    'Morris':'我今天没带优惠券。别这么看我，参加庆祝也不一定要谈生意。',
    'Mister Qi':'最难的挑战，有时是记住一个普通日子。今天的奖励，你们已经拿到了。',
    'Bouncer':'今天没有暗号，也不检查邀请函。大家都可以进来。',
    'Old Mariner':'有些人向海许愿，有些人把愿望亲口说给身边的人。后者不必等晴天。',
}
layout['Actors']=[{'Name':name,'At':[x,y],'Facing':direction,'Dialogue':dialogue.get(name,'')} for name,x,y,direction in actors]
layout['Interactions']=[{'Id':'Pierre','Area':[17,51,4,2]}, {'Id':'Emily','Area':[23,51,4,2]},
    {'Id':'Cake','Area':[33,62,5,3]}, {'Id':'Strawberry','Area':[18,71,3,4]}]

layout['SurveyTargets'] = []
def clue(key,name,area,a,b,observation='',npc=''):
    layout['SurveyTargets'].append({'Id':key,'Name':name,'Npc':npc,'Area':area,
        'ClueA':a,'ClueB':b,'Observation':observation})

clue('cake','蛋糕桌',[33,62,5,3],
     '调查笔记 A：我发现一座可以吃掉的粉色建筑。人类没有报修，反而准备把它分给大家。',
     '调查笔记 B：它的地基是一片白云，两把刀据说比一把更有仪式感。我建议先别拆屋顶。',
     '白色桌布上摆着大蛋糕，格斯把餐刀留在了桌边。')
clue('gifts','小外前面的礼物堆',[34,60,3,1],
     '调查笔记 A：人类想让对方得到一样东西，却先把它藏起来。这种交付方式效率很低，大家倒很开心。',
     '调查笔记 B：嫌疑物穿着漂亮外衣，还系了绳结。它们就在我的脚前；我本人没有被打包。',
     '小外面前摆着几只包好的礼物盒，丝带被认真地打成了结。')
clue('arch','花拱门',[26,59,6,3],
     '调查笔记 A：这里有一扇从来关不上的门。没有房间，也没有门锁，却总有人郑重地站进去。',
     '调查笔记 B：植物爬上了门框，门下正好放得下两个笑容。也许它通向某种共同生活。',
     '花拱下留着两个人的位置，白色花瓣落在石砖上。')
clue('pond','临时鱼池',[36,76,5,5],
     '调查笔记 A：一小块海被搬进了镇里。奇怪的是，里面住的不是鱼，而是惊喜。',
     '调查笔记 B：石头围着它，渔网陪着它。它在热闹酒馆的南边，但喝起来绝不会比汽水好。',
     '石沿里的水轻轻晃动，渔网挂在旁边，池里藏着小礼物。')
clue('balloons','彩色气球束',[46,59,2,3],
     '调查笔记 A：人类给空气穿上彩色衣服，它就立刻开始想逃跑。空气原来这么不爱上班。',
     '调查笔记 B：几根细线正在挽留这群逃跑者。它们悬在广场东边，既不像水果，也不能真的放生。',
     '红、绿、蓝的气球系在一起，细绳把它们留在了广场上。')
clue('lantern','池边小灯',[32,77,1,2],
     '调查笔记 A：太阳还没下班，地面上就已经有人准备了一颗备用的。体积小得很礼貌。',
     '调查笔记 B：它守着水边和花桶，等夜晚来时才算正式上岗。别拿它去孵小鸡。',
     '小灯静静亮着，照着池边草地和回去的路。')
for key,name,npc,a,b in [
    ('haley','海莉','Haley','调查笔记 A：一位人类让我对着镜头念蔬菜。我认真检查了厨房，才发现她想收集的是笑容。','调查笔记 B：她能把一瞬间装进小小的盒子。我的蔬菜发音显然还没有通过她的拍摄标准。'),
    ('evelyn','艾芙琳','Evelyn','调查笔记 A：一位人类把最漂亮的一枝花，送给了一个嘴上说不需要的人。观察发现，他的手却一直没有松开。','调查笔记 B：她大概早就熟悉那种嘴硬。她保存了许多个春天，今天仍然愿意把最好的一枝留给身边的人。'),
    ('abigail','阿比盖尔','Abigail','调查笔记 A：有人在研究一个严肃问题：长了爱心以后，逃跑技术会不会跟着进步？','调查笔记 B：大多数人觉得黏糊糊的比赛有点怪，她却像发现了新的冒险。可疑，但很适合加入调查。'),
    ('sam','山姆','Sam','调查笔记 A：一位人类预言，一个宣称不爱热闹的人最终还是会来。地球人的嘴和脚经常意见不一致。','调查笔记 B：他似乎很了解那位朋友，甚至愿意为这个预言下注。也许友情就是一种准确的天气预报。'),
    ('penny','潘妮','Penny','调查笔记 A：今天我采访到一个反对追赶时间的人。她认为逛得慢一点，反而不会错过今天。','调查笔记 B：她的建议里没有奖励、比分或截止时间。听她说完，我决定先把调查本合上一小会儿。'),
    ('marnie','玛妮','Marnie','调查笔记 A：有人提前给一个红色的大个子留了住处。它不用床，倒是很需要一块宽敞的地。','调查笔记 B：她望着那个大个子，像在夸自己照顾长大的孩子。找照顾它的人，不要把孩子直接摘走。'),
    ('george','乔治','George','调查笔记 A：我遇到一位嘴上拒绝植物、手上却保护植物的人。人类语言和肢体似乎使用两套密码。','调查笔记 B：他坚持说自己只是替身边的人拿着。我怀疑花已经成功说服了他，只是他还没通知自己的嘴。'),
    ('linus','莱纳斯','Linus','调查笔记 A：有人没有提出要大房子，只问自己能不能在这里坐一会儿。他给“家”设定的条件非常轻。','调查笔记 B：在他看来，一朵花就能让一个地方变得亲切。我想把这个发现带回去，飞船的行李箱正好装得下。'),
    ('jas','贾斯','Jas','调查笔记 A：我发现一位从头到尾不偷吃的来宾，礼貌得不像人类。有人认真替它留好了甜点。','调查笔记 B：请找带它来参加聚会的小主人。那位不用吃饭的来宾，大概是用布和棉花做的。'),
    ('vincent','文森特','Vincent','调查笔记 A：有人提出，把两个甜点上下重叠，就可以消除其中一个的数量。听起来是很有前途的空间技术。','调查笔记 B：这项研究遭到了老师反对。我决定找那位年轻研究员聊聊，而不是直接把实验材料吃掉。'),
    ('leo','雷欧','Leo','调查笔记 A：有位来宾准备把聚会讲给会飞的家人听。我担心他们对糖霜和面粉的理解不太一样。','调查笔记 B：他的听众会先寻找蛋糕里的种子。要采访的是替鸟儿记住今天的那个孩子。'),
    ('sandy','桑迪','Sandy','调查笔记 A：为了一个邀请，有人提前在日历上画了圈。人类居然可以先把还没到来的日子珍藏起来。','调查笔记 B：她来自很少下雨的地方，却告诉我那里也会开花。她今天要见的好朋友就在服饰摊位里。'),
    ('leah','莉亚','Leah','调查笔记 A：两根不一样的树枝也能支起同一扇门。一位人类认为，配合比长得一样更有用。','调查笔记 B：她的眼睛似乎能在木头里看见还不存在的形状。这次找的是观察树枝的人，不是那座花拱。'),
    ('maru','玛鲁','Maru','调查笔记 A：一项自动记录亲密动作的发明失败了，因为操作发明本身占用了进行亲密动作的双手。','调查笔记 B：发明者最后放下按钮。也许有些数据，直接记在心里比装进机器更快。'),
    ('harvey','哈维','Harvey','调查笔记 A：一位来宾带了能处理意外的小箱子，但他希望今天最忙的是负责甜点的人。','调查笔记 B：他担心大家太开心会忘了自己的胃。听起来不是在阻止聚会，而是在准备照顾聚会。'),
    ('clint','克林特','Clint','调查笔记 A：有人能把坚硬的金属做成合适的圆圈，却不知道怎么测量要放进圆圈里的心意。','调查笔记 B：他的工具能量出戒指的尺寸，敲打声也很熟练。今天他还在修一种能让世界重新清楚起来的小圆窗。'),
    ('jodi','乔迪','Jodi','调查笔记 A：一双终于不用准备饭菜的手，突然不知道应该放在哪里。另一位人类主动提供了位置。','调查笔记 B：她平时照顾家人，今天轮到有人牵着她。这份空出来的时间，好像就是一件礼物。'),
    ('kent','肯特','Kent','调查笔记 A：相同的热闹对不同人会发出不同声音。有个人先听见紧张，后来又从里面辨认出了安心。','调查笔记 B：改变声音的不是音量，而是家人的笑。我想找那位正在重新适应平静日子的来宾。'),
    ('demetrius','德米特里厄斯','Demetrius','调查笔记 A：某人的研究计划被另一位人类打断了。记录板离开了手，另一只手接替了它。','调查笔记 B：他本来要测量心跳，现在可以直接感受。邀请他跳过数据表的，是一位擅长木工的人。'),
    ('elliott','艾利欧特','Elliott','调查笔记 A：一份长篇祝词被删了又删，最后只剩下对下一次见面的期待。越短，重量却好像越大。','调查笔记 B：那个人很擅长和文字打交道。我想请教他：为什么对明天的约定，也能成为今天的礼物？'),
    ('shane','谢恩','Shane','调查笔记 A：一位照顾许多小动物的人，今天接到了一项更精细的保管任务。目标很轻，却不敢弄丢。','调查笔记 B：那是一条小姑娘交给他的蝴蝶结。我怀疑他在认真程度上，已经通过了这次地球调查。'),
    ('pam','潘姆','Pam','调查笔记 A：有人的杯子里少了一样平时喜欢的东西，回家的路上却会多出一个同行的人。','调查笔记 B：她为女儿换了今天的饮料。请找作出这个约定的人，不是寻找酒吧的菜单。'),
    ('wizard','法师','Wizard','调查笔记 A：一个最懂魔法的人，竟然建议用每天留灯这种普通办法维系关系。是不是越高深的技术越简单？','调查笔记 B：他已经观察了很久。请找说出这个发现的人，别去调查池边那盏真的灯。'),
    ('krobus','科罗布斯','Krobus','调查笔记 A：一位喜欢阴凉的来宾说，植物并不在意他的来处。这可能是地球花朵最先进的地方。','调查笔记 B：他的外表像一团温柔的影子。今天他发现，愿意接纳自己的不只有花。'),
    ('dwarf','矮人','Dwarf','调查笔记 A：一位来宾把漂亮的绳带当成了可能埋在地下的宝物，还认真提出用石头交换。','调查笔记 B：我猜他的购物经验主要来自矿洞。这次要找热爱岩石的小个子，不需要真的挥动镐子。'),
]:
    actor=next(a for a in layout['Actors'] if a['Name']==npc)
    clue(key,name,[*actor['At'],1,1],a,b,npc=npc)
(ASSETS/'festival-layout.json').write_text(json.dumps(layout,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Wrote pixel assets and the shared festival layout.')
