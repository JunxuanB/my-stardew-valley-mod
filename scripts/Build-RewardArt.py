"""Small pixel assets for reward items. Character art is taken from the approved user sprite."""
from pathlib import Path
from PIL import Image,ImageDraw
project=Path(__file__).resolve().parents[1]
root=project/'Welcome/assets'
sprites=Image.open(root/'xiaowai.png').convert('RGBA')
hats=Image.new('RGBA',(20,80))
for row,direction in enumerate((0,1,3,2)):
    antenna=sprites.crop((3,direction*32,21,direction*32+8))
    hats.alpha_composite(antenna,(1,row*20+2))
# The user approved the front view. Preserve it exactly while fixing hat row order.
hats.paste(Image.open(project/'docs/references/antennae-front-approved.png').convert('RGBA'),(0,0))
hats.save(root/'antennae.png')
nest=Image.new('RGBA',(32,32));d=ImageDraw.Draw(nest)
d.ellipse((1,15,30,30),fill='#514231',outline='#332b25')
d.ellipse((2,13,29,27),fill='#96804d',outline='#5b5631')
d.ellipse((5,15,26,24),fill='#abc064',outline='#73833f')
d.rectangle((9,17,22,21),fill='#c7d782')
d.line((5,25,13,28,22,27,27,24),fill='#c3a76a')
nest.save(root/'nest.png')
icons=Image.new('RGBA',(48,16));d=ImageDraw.Draw(icons)
for i,color in enumerate(('#698da9','#bb7564')):
    x=i*16;d.rectangle((x+1,3,x+14,13),fill='#41394a');d.rectangle((x+2,4,x+13,12),fill=color)
    d.line((x+3,11,x+12,11),fill='#e5d9ad');d.ellipse((x+4,5,x+9,9),fill='#e5d9ad')
    if i==0:d.ellipse((x+6,4,x+10,8),fill=color)
    else:d.line((x+3,9,x+11,5),fill='#85453e')
d.line((34,12,43,3),fill='#5b452b',width=3);d.line((35,12,44,3),fill='#ba9250')
d.polygon([(38,2),(41,1),(46,5),(45,8),(42,7),(41,4)],fill='#9ba9a2',outline='#394e4f')
icons.save(root/'reward-items.png')

detail=Image.open(root/'xiaowai-detail.png').convert('RGBA')
home=Image.open(root/'xiaowai-home-detail.png').convert('RGBA')
head=Image.new('RGBA',(20,80))
for hat_row,npc_row in enumerate((0,1,3,2)):
    face=detail.crop((0,npc_row*128,96,npc_row*128+94))
    face=face.crop(face.getbbox())
    face.thumbnail((20,20),Image.Resampling.NEAREST)
    head.alpha_composite(face,((20-face.width)//2,hat_row*20+20-face.height))
head.save(root/'xiaowai-head.png')

# Native shirt atlas: left half is artwork, right half is the optional dye mask.
shirt=Image.new('RGBA',(256,32));d=ImageDraw.Draw(shirt)
for row in range(4):
    y=row*8
    d.polygon([(2,y),(5,y),(7,y+2),(6,y+3),(5,y+2),(5,y+7),(2,y+7),(2,y+2),(1,y+3),(0,y+2)],fill='#86b43b',outline='#486b26')
    d.rectangle((3,y+3,4,y+6),fill='#b7cf62')
    if row==3:
        d.rectangle((2,y+2,5,y+6),fill='#343329');d.rectangle((3,y+3,4,y+5),fill='#755032')
    elif row in (1,2):
        x=1 if row==1 else 5;d.rectangle((x,y+2,x+1,y+5),fill='#343329')
shirt.save(root/'xiaowai-shirt.png')

shoes=Image.new('RGBA',(16,16));d=ImageDraw.Draw(shoes)
for x in (1,8):
    d.polygon([(x+1,4),(x+4,4),(x+4,8),(x+6,10),(x+6,13),(x,13),(x,7)],fill='#8db83f',outline='#324522')
    d.line((x+1,12,x+5,12),fill='#526f2a');d.line((x+2,5,x+3,8),fill='#bdd36c')
shoes.save(root/'xiaowai-shoes.png')
shoe_colors=Image.new('RGBA',(4,1))
shoe_colors.putdata([(37,51,27,255),(75,105,38,255),(139,178,58,255),(184,207,103,255)])
shoe_colors.save(root/'xiaowai-shoe-colors.png')

# Custom reading glasses, aligned to the native glasses hat's eye line.
# Hat directions are down, right, left, up; ordinary glasses aren't visible from behind.
glasses=Image.new('RGBA',(20,80));d=ImageDraw.Draw(glasses)
frame_color='#654735'
for left in (5,11):
    d.rounded_rectangle((left,11,left+3,14),radius=1,outline=frame_color)
    d.point((left+1,11),fill='#bb925a')
d.line((9,12,10,12),fill=frame_color)
side=Image.new('RGBA',(20,20));d=ImageDraw.Draw(side)
d.line((6,12,10,12),fill=frame_color)
d.rounded_rectangle((10,11,13,14),radius=1,outline=frame_color)
d.point((11,11),fill='#bb925a')
glasses.alpha_composite(side,(0,20))
glasses.alpha_composite(side.transpose(Image.Transpose.FLIP_LEFT_RIGHT),(0,40))
glasses.save(root/'reading-glasses.png')
preview=Image.new('RGBA',(80,20),'#e9e0ca')
for i in range(4):preview.alpha_composite(glasses.crop((0,i*20,20,(i+1)*20)),(i*20,0))
preview.resize((640,160),Image.Resampling.NEAREST).convert('RGB').save(project/'artifacts/reading-glasses-preview.png')

stickers=Image.new('RGBA',(96,48))
images=[home.crop((3*160,0,4*160,160)),home.crop((4*160,0,5*160,160)),detail.crop((0,0,96,128))]
for i,art in enumerate(images):
    art=art.crop(art.getbbox());art.thumbnail((30,44),Image.Resampling.NEAREST)
    stickers.alpha_composite(art,(i*32+(32-art.width)//2,2+(44-art.height)//2))
stickers.save(root/'xiaowai-stickers.png')

board=Image.new('RGBA',(640,320),'#e9e0ca')
board.alpha_composite(head.resize((80,320),Image.Resampling.NEAREST),(0,0))
board.alpha_composite(hats.resize((80,320),Image.Resampling.NEAREST),(100,0))
board.alpha_composite(stickers.resize((384,192),Image.Resampling.NEAREST),(240,0))
board.alpha_composite(shoes.resize((64,64),Image.Resampling.NEAREST),(260,230))
board.alpha_composite(shirt.crop((0,0,8,8)).resize((64,64),Image.Resampling.NEAREST),(360,230))
board.convert('RGB').save(project/'artifacts/xiaowai-new-rewards-0.6.0.png')
print('Reward textures generated, including costume, shoe palette and indoor stickers.')
