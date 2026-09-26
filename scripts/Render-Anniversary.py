"""Render the shipped layout with locally extracted vanilla textures (never game automation).

Usage: python scripts/Render-Anniversary.py --reference PATH
Vanilla textures remain local and are not included in the mod package.
"""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]


def render(reference, layout_path, output):
    layout = json.loads(layout_path.read_text(encoding='utf-8'))
    maps, textures = {}, {}

    def map_data(name):
        if name not in maps:
            data = json.loads((reference / (name.split('/')[-1] + '.json')).read_text(encoding='utf-8-sig'))
            sheets = {s['Id']: s for s in data['Sheets']}
            layers = {l['Id']: {(t['X'], t['Y']): (sheets[t['Sheet']]['Image'].replace('\\', '/'), t['Index']) for t in l['Tiles']} for l in data['Layers']}
            maps[name] = data, layers
        return maps[name]

    def texture(name):
        if name not in textures:
            path = ROOT / 'Welcome' / name if name.startswith('assets/') else reference / (name.split('/')[-1] + '.png')
            textures[name] = Image.open(path).convert('RGBA')
        return textures[name]

    town, source = map_data('Maps/Town')
    layers = {key: dict(value) for key, value in source.items()}
    for clear in layout['Clear']:
        x, y, w, h = clear['Area']
        for layer in clear['Layers']:
            for xx in range(x, x+w):
                for yy in range(y, y+h):
                    layers[layer].pop((xx, yy), None)
    for patch in layout['Copies']:
        _, src = map_data(patch['Map'])
        x, y, w, h = patch['Source']
        dx, dy = patch['Target']
        for layer in patch['Layers']:
            for xx in range(w):
                for yy in range(h):
                    t = src.get(layer, {}).get((x+xx, y+yy))
                    if t and (not patch.get('FestivalOnly') or t[0] == 'Maps/Festivals'):
                        layers[layer][dx+xx, dy+yy] = t
    for tile in layout['Tiles']:
        layers.setdefault(tile['Layer'], {})[tuple(tile['At'])] = (tile['Texture'], tile['Index'])

    canvas = Image.new('RGBA', (town['Width']*16, town['Height']*16), '#314f2a')
    for layer in ('Back', 'Buildings', 'Front', 'AlwaysFront'):
        for (x, y), (name, index) in layers.get(layer, {}).items():
            im = texture(name)
            columns = im.width // 16
            sx, sy = index % columns * 16, index // columns * 16
            canvas.alpha_composite(im.crop((sx, sy, sx+16, sy+16)), (x*16, y*16))

    for sprite in sorted(layout['Sprites'], key=lambda s: s['At'][1]+s['Source'][3]):
        x, y, w, h = sprite['Source']
        patch = texture(sprite['Texture']).crop((x, y, x+w, y+h))
        scale = sprite.get('Scale', 1)
        patch = patch.resize((int(w*scale),int(h*scale)), Image.Resampling.NEAREST)
        canvas.alpha_composite(patch, tuple(sprite['At']))
    # Static sample of the native FishPond: base, water tint, rim, ripple and net.
    pond = texture('Buildings/Fish Pond')
    px,py = (v*16 for v in layout['Pond'])
    base = pond.crop((0,80,80,160))
    from PIL import ImageChops
    base = ImageChops.multiply(base, Image.new('RGBA',base.size,(60,126,150,255)))
    canvas.alpha_composite(base,(px,py))
    canvas.alpha_composite(pond.crop((0,0,80,80)),(px,py))
    canvas.alpha_composite(pond.crop((16,160,64,167)),(px+16,py+11))
    canvas.alpha_composite(pond.crop((80,0,160,48)),(px,py-32))
    for actor in layout['Actors']:
        if actor['Name'] == 'Xiaowai':
            continue  # detail art is composited after the map's 2x scale below
        texture_name={'Leo':'ParrotBoy','Mister Qi':'MrQi','Old Mariner':'Mariner'}.get(actor['Name'],actor['Name'])
        name = 'Characters/' + texture_name
        im = texture(name)
        direction = actor.get('Facing', 2)
        row = {2: 0, 1: 1, 0: 2, 3: 3}[direction]
        fw = 32 if actor['Name']=='Clint' else 16
        fh=24 if actor['Name'] in ('Dwarf','Krobus') else 32
        index=9 if actor['Name']=='Clint' else row*4
        sx=index*fw%im.width
        sy=index*fw//im.width*fh
        frame = im.crop((sx, sy, sx+fw, sy+fh))
        x, y = actor['At']
        canvas.alpha_composite(frame, (x*16, y*16-(fh-16)-(4 if actor['Name']=='Clint' else 0)))

    x, y, w, h = layout['Preview']
    crop = canvas.crop((x*16, y*16, (x+w)*16, (y+h)*16)).resize((w*32, h*32), Image.Resampling.NEAREST)
    for actor in layout['Actors']:
        if actor['Name'] != 'Xiaowai':
            continue
        row={2:0,1:1,0:2,3:3}[actor.get('Facing',2)]
        frame=texture('assets/xiaowai-detail.png').crop((0,row*128,96,row*128+128))
        frame=frame.resize((48,64),Image.Resampling.NEAREST)
        crop.alpha_composite(frame,((actor['At'][0]-x)*32-8,(actor['At'][1]-y)*32-32))
    output.mkdir(parents=True, exist_ok=True)
    crop.convert('RGB').save(output / 'anniversary-map.png')
    # Annotations live outside the map, so they do not conceal the artwork.
    board = Image.new('RGB', (crop.width+340, crop.height+86), '#201f25')
    board.paste(crop, (0, 86))
    draw = ImageDraw.Draw(board)
    font_path = 'C:/Windows/Fonts/msyh.ttc'
    font = ImageFont.truetype(font_path, 18)
    title = ImageFont.truetype(font_path, 26)
    small = ImageFont.truetype(font_path, 15)
    draw.text((24, 14), '纪念日 · 小镇布置提案', font=title, fill='#ffe0a9')
    draw.text((24, 52), '原版地图图块合成预览 / 非实机截图 / 以进游戏验收为准', font=small, fill='#bcb2a6')
    bx = crop.width+20
    notes = [('01  巴士站方向出入口', '商店下方道路最左端 / 进出共用'), ('02  威利的临时鱼池', '酒吧南侧 / 原版鱼塘及动态水波'), ('03  皮埃尔与艾米丽', '两座完整原版节日摊位 / 马路北侧'), ('04  花拱与蛋糕长桌', '空白桌面，只摆一块蛋糕'), ('05  刘易斯的比赛区', '广场南侧，留出双人活动空间'), ('06  小外的地球调查', '诊所前 / 横向宽镜框与豆豆眼'), ('07  巨大草莓', '左下树底 / 展示框内只摆一颗'), ('08  西侧围栏内收', '沿道路南侧与树左侧封闭小区域')]
    yy = 116
    for heading, body in notes:
        draw.text((bx, yy), heading, font=font, fill='#ffe0a9')
        draw.text((bx, yy+30), body, font=small, fill='#c8c2b9')
        yy += 89
    draw.text((bx, yy+12), '0.6.8 · 打铁工位与固定朝向', font=small, fill='#b8cc9a')
    draw.text((bx, yy+38), '实机验收由玩家完成', font=small, fill='#b8cc9a')
    board.save(output / 'anniversary-layout.png')
    return layers


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--reference', type=Path, required=True)
    p.add_argument('--layout', type=Path, default=ROOT / 'Welcome/assets/festival-layout.json')
    p.add_argument('--output', type=Path, default=ROOT / 'artifacts')
    args = p.parse_args()
    render(args.reference, args.layout, args.output)
