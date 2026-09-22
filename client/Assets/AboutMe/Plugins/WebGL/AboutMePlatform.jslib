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
  AM_Screenshot: function(){
    var canvas=Module.canvas;
    if(typeof wx!=='undefined'&&canvas.toTempFilePath){canvas.toTempFilePath({success:function(result){wx.saveImageToPhotosAlbum({filePath:result.tempFilePath});}});return;}
    var a=document.createElement('a');a.download='AboutMe-result.png';a.href=canvas.toDataURL('image/png');a.click();
  }
});
