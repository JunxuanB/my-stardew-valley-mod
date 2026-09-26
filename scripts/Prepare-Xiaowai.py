"""Extract complete poses and add continuous side-view passing steps.

Detailed 96x128 frames render at 1x, retaining the former 24x32-at-4x world size.
The low-resolution sheet exists only for native NPC animation/collision bookkeeping.
"""
from collections import deque
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT/'Welcome/assets'
OUT = ROOT/'artifacts'
OUT.mkdir(exist_ok=True)
SOURCE = Image.open(ROOT/'docs/references/xiaowai-user-sprites.jpg').convert('RGB')
W, H = 96, 128
SCALE = 120/232
BG = '#e9e0ca'

# Native order: down, right, up, left. Keep the original left poses, not mirrored right poses.
BOXES = [
    [(200,8,358,252),(390,8,540,252),(574,8,724,252),(750,8,898,252)],
    [(232,252,384,509),(426,252,584,509),(620,252,780,509),(827,252,991,509)],
    [(224,768,386,1016),(429,768,593,1016),(630,768,794,1016),(829,768,996,1016)],
    [(214,517,377,765),(415,517,578,765),(618,517,780,765),(818,517,981,765)]
]
POSE_BOXES = [(78,1020,254,1254),(291,1020,465,1254),(505,1020,688,1254),
              (705,1010,893,1254),(901,1020,1182,1254)]
POSE_NAMES = ['idle','smile','sit','wave','sleep']


def extract(box):
    image = SOURCE.crop(box).convert('RGBA')
    pixels = image.load()
    width, height = image.size
    seen = set()
    todo = deque([(x,0) for x in range(width)] + [(x,height-1) for x in range(width)]
                 + [(0,y) for y in range(height)] + [(width-1,y) for y in range(height)])
    while todo:
        x,y = todo.popleft()
        if not (0<=x<width and 0<=y<height) or (x,y) in seen:
            continue
        seen.add((x,y))
        r,g,b,_ = pixels[x,y]
        if min(r,g,b)<55 or max(r,g,b)-min(r,g,b)>45:
            continue
        pixels[x,y] = (0,0,0,0)
        todo.extend(((x-1,y),(x+1,y),(x,y-1),(x,y+1)))
    # Reject small JPEG specks detached from the silhouette, but retain the sleep Zs.
    seen.clear()
    for y in range(height):
        for x in range(width):
            if not pixels[x,y][3] or (x,y) in seen:
                continue
            group=[]; todo=deque([(x,y)])
            while todo:
                xx,yy=todo.popleft()
                if not (0<=xx<width and 0<=yy<height) or (xx,yy) in seen or not pixels[xx,yy][3]:
                    continue
                seen.add((xx,yy)); group.append((xx,yy))
                todo.extend(((xx-1,yy),(xx+1,yy),(xx,yy-1),(xx,yy+1)))
            if len(group)<12:
                for point in group: pixels[point]=(0,0,0,0)
    return image.crop(image.getbbox())


def resize_pixels(image, size):
    # At the detailed resolution the original frame strokes survive intact. No
    # palette replacement, averaging or outline inflation changes the supplied face.
    return image.resize(size,Image.Resampling.NEAREST)


def align_walk(image):
    # Anchor the head rather than the overall bounds (which shift when a foot moves).
    head=image.crop((0,round(image.height*.18),image.width,round(image.height*.58))).getbbox()
    center=(head[0]+head[2])/2 if head else image.width/2
    size=(round(image.width*SCALE),round(image.height*SCALE))
    reduced=resize_pixels(image,size)
    x=round(W/2-center*SCALE)
    y=H-1-size[1]
    if x<0 or y<0 or x+size[0]>W:
        raise ValueError(f'Frame would be cropped: {(x,y,size)}')
    frame=Image.new('RGBA',(W,H))
    frame.alpha_composite(reduced,(x,y))
    return frame


frames=[align_walk(extract(box)) for row in BOXES for box in row]


def passing_step(frame, lifted_side):
    # The reference's four side poses all have spread feet. Warp each complete
    # silhouette continuously from the hips down to make a passing step, rather
    # than cutting off/replacing the legs (which previously left a waist seam).
    # Head, glasses, backpack and upper torso keep their original pixels.
    result=Image.new('RGBA',frame.size)
    src=frame.load()
    dst=result.load()
    hip,sole,center=102,118,49
    for y in range(H):
        for x in range(W):
            sy=float(y)
            for _ in range(5):
                t=max(0,min(1,(sy-hip)/(sole-hip)))
                ease=t*t*(3-2*t)
                sx=x+8*ease*max(-1,min(1,(x-center)/4))
                lift_weight=max(0,min(1,.5+lifted_side*(sx-center)/14))
                sy=y+5*ease*lift_weight
            t=max(0,min(1,(sy-hip)/(sole-hip)))
            ease=t*t*(3-2*t)
            sx=x+8*ease*max(-1,min(1,(x-center)/4))
            xx,yy=round(sx),round(sy)
            if 0<=xx<W and 0<=yy<H: dst[x,y]=src[xx,yy]
    return result


for row in (1,3):
    frames[row*4+1]=passing_step(frames[row*4+1],-1)
    frames[row*4+3]=passing_step(frames[row*4+3],1)

sheet=Image.new('RGBA',(W*4,H*4))
for index,frame in enumerate(frames):
    sheet.alpha_composite(frame,((index%4)*W,(index//4)*H))
sheet.save(ASSETS/'xiaowai-detail.png')
sheet.resize((96,128),Image.Resampling.NEAREST).save(ASSETS/'xiaowai.png')

# A close-up of the complete first pose, without changing facial features or body contours.
portrait=extract(BOXES[0][0])
portrait=portrait.crop((0,0,portrait.width,round(portrait.height*.75)))
factor=min(60/portrait.width,62/portrait.height)
portrait=resize_pixels(portrait,(round(portrait.width*factor),round(portrait.height*factor)))
portrait_sheet=Image.new('RGBA',(64,64))
portrait_sheet.alpha_composite(portrait,((64-portrait.width)//2,64-portrait.height))
portrait_sheet.save(ASSETS/'xiaowai-portrait.png')

poses=Image.new('RGBA',(800,160))
for index,box in enumerate(POSE_BOXES):
    image=extract(box)
    reduced=resize_pixels(image,(round(image.width*SCALE),round(image.height*SCALE)))
    poses.alpha_composite(reduced,(index*160+(160-reduced.width)//2,159-reduced.height))
poses.save(ASSETS/'xiaowai-home-detail.png')
poses.resize((200,40),Image.Resampling.NEAREST).save(ASSETS/'xiaowai-home-poses.png')

(ASSETS/'xiaowai-animation.json').write_text(json.dumps({
    'Walk':{'Texture':'xiaowai-detail.png','FrameWidth':96,'FrameHeight':128,'RenderScale':1,
            'FramesPerDirection':4,'Rows':['down','right','up','left'],
            'NativeProxyTexture':'xiaowai.png','NativeProxyFrameWidth':24,'NativeProxyFrameHeight':32},
    'Home':{'Texture':'xiaowai-home-detail.png','FrameWidth':160,'FrameHeight':160,'RenderScale':1,
            'Poses':{name:i for i,name in enumerate(POSE_NAMES)}},
    'Source':'User-approved reference; side passing steps use a continuous silhouette warp, no body-part splicing',
    'SideWalkCycle':[0,1,2,3],
    'VerticalWalkCycle':[0,1,2,3,2,1]
},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')


def matte(image):
    result=Image.new('RGBA',image.size,BG)
    result.alpha_composite(image)
    return result.convert('RGB')


matte(sheet.resize((768,1024),Image.Resampling.NEAREST)).save(OUT/'xiaowai-walksheet.png')
matte(frames[0].resize((192,256),Image.Resampling.NEAREST)).save(OUT/'xiaowai-front.png')
matte(poses.resize((1000,200),Image.Resampling.NEAREST)).save(OUT/'xiaowai-home-poses.png')
for row,name in ((1,'right'),(3,'left')):
    sequence=[matte(frames[row*4+i].resize((192,256),Image.Resampling.NEAREST)) for i in (0,1,2,3)]
    sequence[0].save(OUT/f'xiaowai-{name}-walk-0.6.1.gif',save_all=True,append_images=sequence[1:],duration=220,loop=0)

# Show reference and extracted art at equal display size for review.
board=Image.new('RGB',(864,364),BG)
draw=ImageDraw.Draw(board)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
for col,(index,label) in enumerate(((0,'原图正面'),(4,'原图右向'),(12,'原图左向'))):
    reference=extract(BOXES[index//4][index%4])
    factor=SCALE*1.5
    reference=reference.resize((round(reference.width*factor),round(reference.height*factor)),Image.Resampling.BOX)
    board.paste(reference,(col*288+4+(132-reference.width)//2,315-reference.height),reference)
    game=frames[index].resize((144,192),Image.Resampling.NEAREST)
    board.paste(game,(col*288+140,124),game)
    draw.text((col*288+16,16),label,font=font,fill='#393429')
    draw.text((col*288+154,16),'新版贴图',font=font,fill='#393429')
draw.text((18,333),'完整姿势取自原图；去除棋盘格、脚底对齐；游戏内大小保持不变。',font=font,fill='#393429')
board.save(OUT/'xiaowai-reference-comparison-0.6.1.png')
print('Prepared reference poses with side passing steps: detail 96x128 at 1x; native proxy 24x32.')
