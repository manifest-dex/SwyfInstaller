import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';
const html=readFileSync(new URL('../src/Panel/index.html',import.meta.url),'utf8');
const script=html.split('<script nonce="__NONCE__">')[1].split('</script>')[0];
function fixture(configured=true){
 const nodes=new Map(),events=new Map(),calls=[];let opened=0,closed=0;
 let saved={enabled:true,useManifestDeX:false,baseUrl:'https://private.example/v1',model:'private-model',temperature:null,topP:null,maxTokens:null,outputMode:'schema',hasApiKey:true};
 const node=id=>nodes.get(id)||(()=>{const n={id,value:'',checked:false,disabled:false,hidden:false,dataset:{},children:[],textContent:'',addEventListener:(name,fn)=>events.set(id+':'+name,fn),replaceChildren(...v){this.children=v},append(...v){this.children.push(...v)},reportValidity:()=>true};nodes.set(id,n);return n})();
 const popup={closed:false,close(){closed++;this.closed=true},document:{title:'',body:node('popup'),createElement:tag=>({tag,textContent:''})}};
 const response=(status,body,header=null)=>({ok:status<400,status,headers:{get:()=>header},json:async()=>body});
 const context={console,URL,Date,Math,Number,Error,Promise,location:{hash:'',pathname:'/'},history:{replaceState(){}},sessionStorage:{getItem:()=>'',setItem(){}},setInterval(){},clearInterval(){},setTimeout(){},
 document:{hidden:false,getElementById:node,querySelectorAll:()=>[],createElement:tag=>({tag}),createTextNode:text=>({textContent:text}),addEventListener(){}},
 window:{open(){opened++;return popup}},
 fetch:async(path,options)=>{calls.push(path);if(path==='/api/config'){if(options?.method==='PUT'){const draft=JSON.parse(options.body);saved=draft.useManifestDeX?{...saved,enabled:draft.enabled,useManifestDeX:true}:{...saved,...draft}}return response(200,{...saved})}if(path==='/api/test')return response(200,{message:'Connection and game response schema verified.',elapsedMs:4200});if(path==='/api/status')return response(200,{completed:0});if(path==='/api/manifestdex/status')return response(200,{available:configured,connected:false,signInAvailable:configured,notice:configured?'':'OAuth is not configured.'});if(path.startsWith('/api/manifestdex/account'))return response(200,{connected:false});if(path==='/api/manifestdex/connect')return response(429,{error:'rate_limit',message:'Too many requests.',retryAfter:600},'600');throw Error(path)}};
 vm.runInNewContext(script,context);
 return {node,events,calls,popup,opened:()=>opened,closed:()=>closed};
}
const flush=()=>new Promise(resolve=>setImmediate(resolve));
const f=fixture();await flush();await flush();
f.events.get('connectManifest:click')();await flush();await flush();
assert.equal(f.opened(),1);assert.equal(f.closed(),0);
assert.equal(f.popup.document.title,'ManifestDeX sign-in');
assert.ok(f.node('popup').children.some(n=>n.textContent.includes('10 minute')));
assert.equal(f.node('connectManifest').disabled,true);
f.events.get('connectManifest:click')();await flush();
assert.equal(f.calls.filter(p=>p==='/api/manifestdex/connect').length,1);
const missing=fixture(false);await flush();await flush();
assert.equal(missing.node('connectManifest').disabled,true);
missing.events.get('connectManifest:click')();await flush();assert.equal(missing.opened(),0);
console.log('PASS: 429 keeps an explanatory tab open, preserves 600-second cooldown, rejects repeat clicks, and blocks unconfigured sign-in.');
const draft=fixture();await flush();await flush();
assert.match(draft.node('activeSettings').textContent,/Game uses: Private provider · private-model/);
assert.equal(draft.node('savedState').textContent,'Saved');
draft.node('manifestdex').checked=true;draft.events.get('manifestdex:change')();
assert.equal(draft.node('savedState').textContent,'Unsaved changes');
assert.match(draft.node('activeSettings').textContent,/Private provider.*not active/);
draft.events.get('test:click')();await flush();await flush();
assert.match(draft.node('notice').textContent,/Test passed for unsaved settings/);
assert.equal(draft.node('savedState').textContent,'Unsaved changes');
assert.equal(draft.calls.filter(p=>p==='/api/config').length,1,'test must not silently save settings');
draft.events.get('config:submit')({preventDefault(){}});await flush();await flush();
assert.equal(draft.node('savedState').textContent,'Saved');
assert.equal(draft.node('activeSettings').textContent,'Game uses: ManifestDeX AI.');
assert.equal(draft.node('baseUrl').value,'https://private.example/v1');
console.log('PASS: selecting and testing a draft keeps the saved game route visible; only Save activates it and private settings survive.');
