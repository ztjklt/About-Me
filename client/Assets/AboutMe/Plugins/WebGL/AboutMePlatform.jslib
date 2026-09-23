mergeInto(LibraryManager.library, {
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
    if(navigator.clipboard)navigator.clipboard.writeText(link.toString());
  },
  AM_RecordStart: function(receiver) {
    var target=UTF8ToString(receiver);
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
      var recorder=new MediaRecorder(stream);globalThis.aboutMeRecorder=recorder;var chunks=[];
      recorder.ondataavailable=function(event){if(event.data.size)chunks.push(event.data);};
      recorder.onstop=function(){stream.getTracks().forEach(function(track){track.stop();});var reader=new FileReader();reader.onload=function(){SendMessage(target,'OnRecorded',(recorder.mimeType.indexOf('mp4')>=0?'m4a:':'webm:')+reader.result.split(',')[1]);};reader.readAsDataURL(new Blob(chunks,{type:recorder.mimeType}));};
      recorder.start();setTimeout(function(){if(recorder.state==='recording')recorder.stop();},60000);
    }).catch(fail);
  },
  AM_RecordStop: function(){if(globalThis.aboutMeRecorder)globalThis.aboutMeRecorder.stop();},
  AM_PickIntroductionImage: function(receiver) {
    var target=UTF8ToString(receiver), fail=function(){SendMessage(target,'OnPlatformError','无法读取所选图片');};
    if(typeof wx!=='undefined'&&wx.chooseMedia){
      wx.chooseMedia({count:1,mediaType:['image'],sourceType:['album','camera'],success:function(result){var file=result.tempFiles&&result.tempFiles[0];if(!file){fail();return;}wx.getFileSystemManager().readFile({filePath:file.tempFilePath,encoding:'base64',success:function(data){SendMessage(target,'OnIntroductionImage',(file.fileType==='png'?'image/png':'image/jpeg')+':'+data.data);},fail:fail});},fail:function(){}});
      return;
    }
    var input=document.createElement('input');input.type='file';input.accept='image/jpeg,image/png,image/gif,image/webp';input.style.display='none';
    input.onchange=function(){var file=input.files&&input.files[0];input.remove();if(!file)return;if(file.size>10*1024*1024){SendMessage(target,'OnPlatformError','图片不能超过 10 MB');return;}var reader=new FileReader();reader.onload=function(){var value=String(reader.result),split=value.indexOf(',');if(split<0){fail();return;}SendMessage(target,'OnIntroductionImage',(file.type||'image/jpeg')+':'+value.substring(split+1));};reader.onerror=fail;reader.readAsDataURL(file);};
    document.body.appendChild(input);input.click();
  },
  AM_Screenshot: function(){
    var canvas=Module.canvas;
    if(typeof wx!=='undefined'&&canvas.toTempFilePath){canvas.toTempFilePath({success:function(result){wx.saveImageToPhotosAlbum({filePath:result.tempFilePath});}});return;}
    var a=document.createElement('a');a.download='AboutMe-result.png';a.href=canvas.toDataURL('image/png');a.click();
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
    var closed=false;
    var finish=function(blurred){
      if(closed)return;
      closed=true;
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
    input.addEventListener('keydown',function(event){event.stopPropagation();if(!multiline&&event.key==='Enter'){event.preventDefault();input.blur();}});
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
