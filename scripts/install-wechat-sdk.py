"""Pin the official WXSDK in the Unity Package Manager manifest."""
import json
from pathlib import Path
path=Path(__file__).resolve().parent.parent/'client/Packages/manifest.json'
data=json.loads(path.read_text())
data['dependencies']['com.qq.weixin.minigame']='https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk.git#a09d4b29daa1dd8358b09b5b5639554ab08cfdc2'
path.write_text(json.dumps(data,indent=2)+'\n')
print('WXSDK pinned; open Tuanjie to resolve the package.')
