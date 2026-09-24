"""Package genuine engine captures; no generated animation frames."""
from pathlib import Path
import json, subprocess
from PIL import Image,ImageDraw,ImageFont
import imageio_ffmpeg
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/claude-girl-modular'
font_path=ROOT/'client/Assets/AboutMe/Resources/AboutMeFont.otf'
font=ImageFont.truetype(str(font_path),25)
items=[('idle','待机'),('walk','行走'),('run','奔跑'),('jump','跳跃'),('light','轻击'),('heavy','重击'),('grab','抓取'),('eat','吞噬'),('hurt','受击'),('fall','倒地'),('recover','恢复')]
canvas=Image.new('RGB',(1600,1500),'#E9ECF1');draw=ImageDraw.Draw(canvas)
for i,(key,label) in enumerate(items):
 im=Image.open(OUT/'review'/('action-'+key+'.png'));im.thumbnail((400,450));x=(i%4)*400+(400-im.width)//2;y=(i//4)*500;canvas.paste(im,(x,y));draw.text(((i%4)*400+20,y+457),label,fill='#263765',font=font)
canvas.save(OUT/'review/actions-contact-sheet.jpg',quality=92)
ffmpeg=imageio_ffmpeg.get_ffmpeg_exe()
for folder,name,fps in [('physics-frames','physics-motion.mp4',12.5)]:
 subprocess.run([ffmpeg,'-y','-framerate',str(fps),'-i',str(OUT/folder/'%04d.png'),'-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',str(OUT/name)],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
print('Rendered contact sheet and physics-motion.mp4')
