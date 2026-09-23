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
  AM_InputOpen: function(receiver,value,multiline,maxLength,x,y,width,height){
    var target=UTF8ToString(receiver),initial=UTF8ToString(value);
    if(typeof wx!=='undefined'&&wx.showKeyboard){
      if(globalThis.aboutMeKeyboardInput&&wx.offKeyboardInput)wx.offKeyboardInput(globalThis.aboutMeKeyboardInput);
      globalThis.aboutMeKeyboardTarget=target;
      globalThis.aboutMeKeyboardInput=function(event){SendMessage(target,'OnBrowserInput',event.value||'');};
      wx.onKeyboardInput(globalThis.aboutMeKeyboardInput);
      wx.showKeyboard({defaultValue:initial,maxLength:maxLength,multiple:!!multiline,confirmHold:!!multiline,success:function(){}});return;
    }
    if(typeof document==='undefined')return;
    if(globalThis.aboutMeTextInput)globalThis.aboutMeTextInput.remove();
    var canvas=Module.canvas,bounds=canvas.getBoundingClientRect();
    var input=document.createElement(multiline?'textarea':'input');globalThis.aboutMeTextInput=input;globalThis.aboutMeTextTarget=target;
    input.value=initial;input.maxLength=maxLength;input.autocomplete='off';input.autocapitalize='off';input.spellcheck=false;
    var style=input.style;style.position='fixed';style.zIndex='2147483647';style.left=(bounds.left+x*bounds.width)+'px';style.top=(bounds.top+(1-y-height)*bounds.height)+'px';
    style.width=Math.max(48,width*bounds.width)+'px';style.height=Math.max(28,height*bounds.height)+'px';style.opacity='0.02';style.background='transparent';style.border='0';style.outline='0';style.resize='none';style.fontSize='18px';
    var update=function(){SendMessage(target,'OnBrowserInput',input.value);};input.addEventListener('input',update);input.addEventListener('compositionupdate',update);
    input.addEventListener('keydown',function(event){event.stopPropagation();if(!multiline&&event.key==='Enter'){event.preventDefault();input.blur();}});
    input.addEventListener('keyup',function(event){event.stopPropagation();});input.addEventListener('keypress',function(event){event.stopPropagation();});
    input.addEventListener('blur',function(){var finalValue=input.value;input.remove();if(globalThis.aboutMeTextInput===input)globalThis.aboutMeTextInput=null;SendMessage(target,'OnBrowserBlur',finalValue);});
    document.body.appendChild(input);setTimeout(function(){input.focus();input.setSelectionRange(input.value.length,input.value.length);},0);
  },
  AM_InputClose: function(receiver){
    var target=UTF8ToString(receiver);
    if(typeof wx!=='undefined'&&wx.hideKeyboard){wx.hideKeyboard();if(globalThis.aboutMeKeyboardInput&&wx.offKeyboardInput)wx.offKeyboardInput(globalThis.aboutMeKeyboardInput);globalThis.aboutMeKeyboardInput=null;return;}
    var input=globalThis.aboutMeTextInput;if(input&&globalThis.aboutMeTextTarget===target){globalThis.aboutMeTextInput=null;input.remove();}
  }
});
