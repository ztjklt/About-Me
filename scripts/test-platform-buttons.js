const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('client/Assets/AboutMe/Plugins/WebGL/AboutMePlatform.jslib', 'utf8');
function setup() {
  const nodes=[],messages=[];
  function node(tag){return {tag,style:{},children:[],setAttribute(k,v){this[k]=v},appendChild(n){this.children.push(n)},remove(){this.removed=true},focus(){},click(){}};}
  const document={body:node('body'),createElement(tag){const n=node(tag);nodes.push(n);return n},getElementById(id){return nodes.find(n=>n.id===id&&!n.removed)}};
  const context={LibraryManager:{library:{}},mergeInto:Object.assign,UTF8ToString:x=>x,SendMessage:(...x)=>messages.push(x),Module:{canvas:{toDataURL:()=> 'data:image/png;base64,test',focus(){}}},document,navigator:{},location:{href:'http://localhost:8080/'},URL,URLSearchParams,setTimeout,console};
  context.globalThis=context;context.aboutMeReceiver='PlatformBridge';vm.createContext(context);vm.runInContext(source,context);return {api:context.LibraryManager.library,context,nodes,messages};
}
(async()=>{
  let t=setup();t.api.AM_Share('ABCDEF');assert.match(t.messages[0][2],/ABCDEF/);assert.equal(t.messages[0][1],'OnPlatformError');
  t=setup();t.context.navigator.clipboard={writeText:()=>Promise.reject(new Error('denied'))};t.api.AM_Share('ABCDEF');await Promise.resolve();assert.match(t.messages[0][2],/未成功/);
  t=setup();t.context.navigator.clipboard={writeText:()=>Promise.resolve()};t.api.AM_Share('ABCDEF');await Promise.resolve();assert.equal(t.messages[0][1],'OnPlatformStatus');
  t=setup();t.api.AM_Screenshot();assert.equal(t.nodes.find(n=>n.id==='aboutme-result-screenshot').role,'dialog');assert.equal(t.nodes.find(n=>n.tag==='a').download,'AboutMe-result.png');t.nodes.find(n=>n.tag==='button').onclick();assert.equal(t.nodes.find(n=>n.tag==='section').removed,true);
  t=setup();t.context.Module.canvas.toDataURL=()=>{throw new Error('capture failed')};t.api.AM_Screenshot();assert.equal(t.messages[0][1],'OnPlatformError');
  t=setup();let stopped=0;t.context.aboutMeRecorder={state:'inactive',stop(){stopped++}};t.api.AM_RecordStop();assert.equal(stopped,0);
  t=setup();let resolve,trackStops=0,created=0;t.context.navigator.mediaDevices={getUserMedia:()=>new Promise(r=>resolve=r)};t.context.MediaRecorder=function(){created++};t.api.AM_RecordStart('PlatformBridge');t.api.AM_RecordStop();resolve({getTracks:()=>[{stop(){trackStops++}}]});await Promise.resolve();assert.equal(trackStops,1);assert.equal(created,0);
  console.log('PLATFORM_BUTTON_TESTS_PASSED 7: clipboard absence/rejection/success, screenshot dialog/error, inactive stop, late microphone permission cancellation');
})().catch(e=>{console.error(e);process.exitCode=1});
