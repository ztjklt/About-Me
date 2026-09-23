"""Encode engine-rendered V4 frames with chapter labels; never invent animation frames."""
from PIL import Image, ImageDraw, ImageFont
import imageio_ffmpeg
from pathlib import Path
root=Path(__file__).resolve().parent.parent
base=root/'artifacts/characters-v4'
font=ImageFont.truetype(str(root/'client/Assets/AboutMe/Resources/AboutMeFont.otf'),24)
chapters=['走路 → 奔跑 → 停步','轻击与局部反馈','满蓄力重击 → 摔躺 → 起身','高处跌落：落地留在原处','撞墙后摔倒与恢复','夸张吞噬与受击打断','倒地拖动与起身释放','旧预设姿势 / 新物理姿态：同一击倒事件重放']
writer=imageio_ffmpeg.write_frames(str(base/'v4-actions-comparison.mp4'),(1280,800),fps=25,codec='libx264',pix_fmt_in='rgb24',pix_fmt_out='yuv420p',output_params=['-crf','19','-movflags','+faststart'])
writer.send(None)
for i in range(960):
    canvas=Image.new('RGB',(1280,800),'#F6EFDF');canvas.paste(Image.open(base/'showcase'/f'{i:04}.png'),(0,40));draw=ImageDraw.Draw(canvas)
    draw.text((24,7),chapters[i//120],font=font,fill='#243044')
    draw.text((24,765),'左：旧版预设代码重放  ·  右：V4 物理结果' if i>=840 else '左：正面斜视  ·  右：侧视  |  团结引擎实际物理姿态渲染',font=font,fill='#243044')
    writer.send(canvas.tobytes())
writer.close()
print(base/'v4-actions-comparison.mp4')
