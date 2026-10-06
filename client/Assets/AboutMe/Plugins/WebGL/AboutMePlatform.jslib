mergeInto(LibraryManager.library, {
  AM_PointerLocked: function(){return typeof document!=='undefined'&&document.pointerLockElement===Module.canvas?1:0;},
  AM_Init: function(receiver) {
    var target=UTF8ToString(receiver);globalThis.aboutMeReceiver=target;
    if(typeof wx!=='undefined'&&wx.getLaunchOptionsSync){
      var launch=wx.getLaunchOptionsSync();
      if(launch.query&&(launch.query.room||launch.query.scene))SendMessage(target,'OnLaunchRoom',decodeURIComponent(launch.query.room||launch.query.scene));
      wx.onShow(function(event){if(event.query&&(event.query.room||event.query.scene))SendMessage(target,'OnLaunchRoom',decodeURIComponent(event.query.room||event.query.scene));});
      wx.showShareMenu({menus:['shareAppMessage']});return 1;
    }
    if(typeof location!=='undefined'){
      var room=new URLSearchParams(location.search).get('room');if(room)SendMessage(target,'OnLaunchRoom',room);
    }
    return 0;
  },
  AM_Login: function(receiver){var target=UTF8ToString(receiver);if(typeof wx!=='undefined')wx.login({success:function(result){SendMessage(target,'OnWeChatLogin',result.code);},fail:function(){SendMessage(target,'OnPlatformError','微信登录失败');}});},
  AM_Share: function(code) {
    var room=UTF8ToString(code);
    if(typeof wx!=='undefined'&&wx.shareAppMessage){wx.shareAppMessage({title:'拿「自己」打朋友 · About Me',query:'room='+encodeURIComponent(room)});return;}
    var link=new URL(location.href);link.searchParams.set('room',room);
    var target=globalThis.aboutMeReceiver;
    var notify=function(method,text){if(target)SendMessage(target,method,text);};
    if(navigator.clipboard&&navigator.clipboard.writeText){
      navigator.clipboard.writeText(link.toString()).then(function(){notify('OnPlatformStatus','邀请链接已复制；房间码：'+room);},function(){notify('OnPlatformError','复制链接未成功，请手动分享房间码：'+room);});
    }else notify('OnPlatformError','浏览器不支持复制链接，请手动分享房间码：'+room);
  },
  AM_RecordStart: function(receiver) {
    var target=UTF8ToString(receiver);globalThis.aboutMeRecordingCancelled=false;
    var fail=function(error){SendMessage(target,'OnPlatformError','录音失败：'+String(error.errMsg||error.message||error));};
    if(typeof wx!=='undefined'&&wx.getRecorderManager){
      var recorder=wx.getRecorderManager();globalThis.aboutMeRecorder=recorder;
      if(globalThis.aboutMeWxStop)recorder.offStop(globalThis.aboutMeWxStop);
      globalThis.aboutMeWxStop=function(event){wx.getFileSystemManager().readFile({filePath:event.tempFilePath,encoding:'base64',success:function(data){SendMessage(target,'OnRecorded','mp3:'+data.data);},fail:fail});};
      recorder.onStop(globalThis.aboutMeWxStop);recorder.onError(fail);
      recorder.start({duration:60000,format:'mp3',sampleRate:16000,numberOfChannels:1});return;
    }
    if(!navigator.mediaDevices){fail('浏览器录音需要 HTTPS 和麦克风权限');return;}
    navigator.mediaDevices.getUserMedia({audio:true}).then(function(stream){
      if(globalThis.aboutMeRecordingCancelled){stream.getTracks().forEach(function(track){track.stop();});return;}
      var recorder;try{recorder=new MediaRecorder(stream);}catch(error){stream.getTracks().forEach(function(track){track.stop();});fail(error);return;}
      globalThis.aboutMeRecorder=recorder;var chunks=[];
      recorder.ondataavailable=function(event){if(event.data.size)chunks.push(event.data);};
      recorder.onstop=function(){stream.getTracks().forEach(function(track){track.stop();});var reader=new FileReader();reader.onload=function(){SendMessage(target,'OnRecorded',(recorder.mimeType.indexOf('mp4')>=0?'m4a:':'webm:')+reader.result.split(',')[1]);};reader.readAsDataURL(new Blob(chunks,{type:recorder.mimeType}));};
      recorder.start();setTimeout(function(){if(recorder.state==='recording')recorder.stop();},60000);
    }).catch(fail);
  },
  AM_RecordStop: function(){globalThis.aboutMeRecordingCancelled=true;var recorder=globalThis.aboutMeRecorder;if(recorder&&recorder.state!=='inactive')recorder.stop();},
  AM_Screenshot: function(){
    var canvas=Module.canvas,target=globalThis.aboutMeReceiver;
    var notify=function(method,text){if(target)SendMessage(target,method,text);};
    if(typeof wx!=='undefined'&&canvas.toTempFilePath){canvas.toTempFilePath({success:function(result){wx.saveImageToPhotosAlbum({filePath:result.tempFilePath,success:function(){notify('OnPlatformStatus','结算截图已保存到相册。');},fail:function(){notify('OnPlatformError','截图未保存，请检查相册权限后重试。');}});},fail:function(){notify('OnPlatformError','结算截图生成失败，请重试。');}});return;}
    try{
      var old=document.getElementById('aboutme-result-screenshot');if(old)old.remove();
      var data=canvas.toDataURL('image/png');
      var dialog=document.createElement('section');dialog.id='aboutme-result-screenshot';dialog.setAttribute('role','dialog');dialog.setAttribute('aria-label','保存结算截图');
      dialog.style.cssText='position:fixed;inset:16px;z-index:2147483647;background:#fff9ef;border:3px solid #222c3c;border-radius:16px;padding:16px;display:flex;flex-direction:column;align-items:center;gap:12px;overflow:auto;color:#222c3c;font:16px system-ui';
      var title=document.createElement('p');title.textContent='截图已准备。点击下载；若浏览器不支持下载，可以右键或长按图片保存。';dialog.appendChild(title);
      var image=document.createElement('img');image.src=data;image.alt='本局结算截图';image.style.cssText='max-width:100%;max-height:70%;object-fit:contain';dialog.appendChild(image);
      var link=document.createElement('a');link.href=data;link.download='AboutMe-result.png';link.textContent='下载截图';link.style.cssText='padding:12px 28px;background:#79cfc5;border-radius:8px;color:#222c3c';dialog.appendChild(link);
      var close=document.createElement('button');close.textContent='关闭截图';close.style.cssText='padding:10px 24px';close.onclick=function(){dialog.remove();canvas.focus();};dialog.appendChild(close);
      document.body.appendChild(dialog);close.focus();notify('OnPlatformStatus','结算截图已准备，请点击下载截图。');
    }catch(error){notify('OnPlatformError','结算截图生成失败，请稍后重试。');}
  },
  AM_InputOpen: function(receiver,value,placeholder,multiline,maxLength,x,y,width,height){
    var target=UTF8ToString(receiver),initial=UTF8ToString(value),hint=UTF8ToString(placeholder);
    var values=globalThis.aboutMeTextValues||(globalThis.aboutMeTextValues=Object.create(null));
    values[target]=initial;
    if(typeof wx!=='undefined'&&wx.showKeyboard){
      if(globalThis.aboutMeKeyboardInput&&wx.offKeyboardInput)wx.offKeyboardInput(globalThis.aboutMeKeyboardInput);
      globalThis.aboutMeKeyboardTarget=target;
      globalThis.aboutMeKeyboardInput=function(event){values[target]=event.value||'';SendMessage(target,'OnBrowserInput',values[target]);};
      wx.onKeyboardInput(globalThis.aboutMeKeyboardInput);
      wx.showKeyboard({defaultValue:initial,maxLength:maxLength,multiple:!!multiline,confirmHold:!!multiline,success:function(){}});return;
    }
    if(typeof document==='undefined')return;
    if(globalThis.aboutMeTextInput)globalThis.aboutMeTextInput.__aboutMeFinish(false);
    var canvas=Module.canvas,bounds=canvas.getBoundingClientRect();
    var input=document.createElement(multiline?'textarea':'input');globalThis.aboutMeTextInput=input;globalThis.aboutMeTextTarget=target;
    input.value=initial;input.placeholder=hint;input.setAttribute('aria-label',hint);input.maxLength=maxLength;input.autocomplete='off';input.autocapitalize='off';input.spellcheck=false;
    var style=input.style;style.position='fixed';style.zIndex='2147483647';style.left=(bounds.left+x*bounds.width)+'px';style.top=(bounds.top+(1-y-height)*bounds.height)+'px';
    style.width=Math.max(48,width*bounds.width)+'px';style.height=Math.max(28,height*bounds.height)+'px';style.boxSizing='border-box';style.opacity='1';style.background='#f5f7f4';style.color='#222c3c';style.border='2px solid #222c3c';style.borderRadius='8px';style.outline='0';style.padding='0 12px';style.resize='none';style.fontSize='18px';style.fontFamily='system-ui,sans-serif';
    // Unity's capture-phase keyboard handlers run before a textarea's own events.
    // Yield those engine handlers while editing, then restore them when the field closes.
    // This also works with Tuanjie builds that omit the WebGLInput managed API.
    var keyboardHandlers=typeof JSEvents!=='undefined'?JSEvents.eventHandlers.filter(function(handler){
      return /^(keydown|keypress|keyup)$/.test(handler.eventTypeString);
    }):[];
    keyboardHandlers.forEach(function(handler){handler.target.removeEventListener(handler.eventTypeString,handler.eventListenerFunc,handler.useCapture);});
    var closed=false;
    var finish=function(blurred){
      if(closed)return;
      closed=true;
      keyboardHandlers.forEach(function(handler){
        if(JSEvents.eventHandlers.indexOf(handler)!==-1)handler.target.addEventListener(handler.eventTypeString,handler.eventListenerFunc,handler.useCapture);
      });
      var finalValue=input.value;
      values[target]=finalValue;
      if(globalThis.aboutMeTextInput===input)globalThis.aboutMeTextInput=null;
      // Removing a focused input can synchronously fire blur in WebKit. Mark it
      // closed first so the blur handler cannot remove the same node again.
      if(input.parentNode)input.parentNode.removeChild(input);
      SendMessage(target,blurred?'OnBrowserBlur':'OnBrowserInput',finalValue);
    };
    input.__aboutMeFinish=finish;
    var update=function(){if(!closed){values[target]=input.value;SendMessage(target,'OnBrowserInput',input.value);}};
    input.addEventListener('input',update);input.addEventListener('compositionupdate',update);input.addEventListener('compositionend',update);input.addEventListener('change',update);
    input.addEventListener('keydown',function(event){event.stopPropagation();if(!multiline&&event.key==='Enter'&&!event.isComposing&&event.keyCode!==229){event.preventDefault();input.blur();}});
    input.addEventListener('keyup',function(event){event.stopPropagation();});input.addEventListener('keypress',function(event){event.stopPropagation();});
    input.addEventListener('blur',function(){finish(true);});
    document.body.appendChild(input);
    // Safari needs the focus attempt in the original pointer gesture. If it declines,
    // the visible HTML field remains tappable so the player can focus it directly.
    input.focus();try{input.setSelectionRange(input.value.length,input.value.length);}catch(error){}
  },
  AM_InputClose: function(receiver){
    var target=UTF8ToString(receiver);
    if(typeof wx!=='undefined'&&wx.hideKeyboard){wx.hideKeyboard();if(globalThis.aboutMeKeyboardInput&&wx.offKeyboardInput)wx.offKeyboardInput(globalThis.aboutMeKeyboardInput);globalThis.aboutMeKeyboardInput=null;return;}
    var input=globalThis.aboutMeTextInput;
    if(input&&globalThis.aboutMeTextTarget===target)input.__aboutMeFinish(false);
  },
  AM_InputValue: function(receiver,fallback){
    var target=UTF8ToString(receiver),defaultValue=UTF8ToString(fallback);
    var input=globalThis.aboutMeTextInput;
    if(input&&globalThis.aboutMeTextTarget===target)return stringToNewUTF8(input.value);
    var values=globalThis.aboutMeTextValues;
    return stringToNewUTF8(values&&Object.prototype.hasOwnProperty.call(values,target)?values[target]:defaultValue);
  },
  AM_InputStore: function(receiver,value){
    var target=UTF8ToString(receiver),text=UTF8ToString(value);
    var values=globalThis.aboutMeTextValues||(globalThis.aboutMeTextValues=Object.create(null));
    values[target]=text;
    var input=globalThis.aboutMeTextInput;if(input&&globalThis.aboutMeTextTarget===target)input.value=text;
  }
});
