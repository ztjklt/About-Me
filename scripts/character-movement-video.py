"""Encode actual Tuanjie locomotion review renders (no generated concept frames)."""
from pathlib import Path
import subprocess
import imageio_ffmpeg
out=Path(__file__).resolve().parents[1]/'artifacts/characters-v2'
ffmpeg=imageio_ffmpeg.get_ffmpeg_exe()
subprocess.run([ffmpeg,'-y','-v','error','-framerate','30','-i',str(out/'motion-frames/%04d.png'),'-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',str(out/'walk-run-eat.mp4')],check=True)
subprocess.run([ffmpeg,'-y','-v','error','-ss','0.4','-t','4.6','-i',str(out/'walk-run-eat.mp4'),'-filter_complex','fps=15,scale=960:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse','-loop','0',str(out/'walk-run.gif')],check=True)
print(out/'walk-run-eat.mp4')
