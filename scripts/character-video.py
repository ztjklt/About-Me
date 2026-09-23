"""Encode actual Tuanjie review renders. Requires optional imageio-ffmpeg and Pillow."""
from pathlib import Path
import json,re,subprocess
import imageio_ffmpeg
root=Path(__file__).resolve().parents[1]
out=root/'artifacts/characters-v1'
ffmpeg=imageio_ffmpeg.get_ffmpeg_exe()
subprocess.run([ffmpeg,'-y','-v','error','-framerate','30','-i',str(out/'motion-frames/%04d.png'),'-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',str(out/'character-motion.mp4')],check=True)
subprocess.run([ffmpeg,'-y','-v','error','-ss','2.8','-t','3.2','-i',str(out/'character-motion.mp4'),'-filter_complex','fps=15,scale=960:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse','-loop','0',str(out/'exaggerated-eat.gif')],check=True)
mesh_stats={}
for index in range(3):
 vertices=triangles=0
 for mesh in (root/f'client/Assets/AboutMe/Art/CharactersV1/Demo{index}').glob('Mesh-*.asset'):
  data=mesh.read_text();vertices+=int(re.search(r'm_VertexCount: (\d+)',data).group(1))
  indices=re.search(r'm_IndexBuffer: ([a-fA-F0-9]+)',data)
  if indices:triangles+=len(indices.group(1))//12
 mesh_stats[str(index)]={'vertices':vertices,'triangles':triangles,'rigidbodies':10,'body_skin_renderers':1,'morph_targets':3}
(out/'avatar-statistics.json').write_text(json.dumps(mesh_stats,indent=2))
print('Encoded Tuanjie motion review:',out/'character-motion.mp4')
print(json.dumps(mesh_stats))
