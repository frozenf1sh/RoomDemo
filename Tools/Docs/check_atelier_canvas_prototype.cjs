// 文件：check_atelier_canvas_prototype.cjs
// 职责：执行交付HTML中的实际脚本，核对有限颜料、撤销与四层收笔；不替代浏览器或Unity验收。
const fs=require('fs'),vm=require('vm'),assert=require('assert'),path=require('path');
const root=path.resolve(__dirname,'../..');
const html=fs.readFileSync(path.join(root,'Docs/Design/全流程重设计20261019/交互原型/画布操作原型.html'),'utf8');
const code=html.match(/<script>([\s\S]*?)<\/script>/)[1];
const ctx={save(){},restore(){},translate(){},rotate(){},drawImage(){},clearRect(){},fillRect(){}};
const nodes={};
function el(id){return nodes[id]??=( {id,width:1024,height:820,value:id==='size'?'18':'0',hidden:id==='end',listeners:{},classList:{add(){}},addEventListener(n,f){this.listeners[n]=f;},getContext(){return ctx;},getBoundingClientRect(){return {left:0,top:0,width:1024,height:820};},setPointerCapture(){},hasPointerCapture(){return false;},releasePointerCapture(){}});}
const win={addEventListener(){}};
const sandbox={console,document:{getElementById:el},window:win,Image:class{set src(v){this.width=this.height=2048;queueMicrotask(()=>this.onload());}},Promise,Math,Number,Error};
vm.createContext(sandbox);vm.runInContext(code,sandbox);
const tick=()=>new Promise(r=>setImmediate(r));
const snap=()=>win.canvasPrototype.snapshot();
const event=(x=300,y=300)=>({button:0,pointerId:1,clientX:x,clientY:y});
function stroke(q){if(q!==undefined){el('size').value=q;el('size').listeners.input();}el('preview').listeners.pointerdown(event());el('preview').listeners.pointerup(event());}
(async()=>{await tick();assert(snap().loaded);assert(el('finish').disabled);el('finish').listeners.click();assert.equal(snap().layer,0);
 stroke();stroke();assert(el('finish').disabled);stroke();assert(!el('finish').disabled);const used=snap().remaining;el('undo').listeners.click();assert.equal(snap().count,2);assert(snap().remaining>used);stroke();el('finish').listeners.click();assert.equal(snap().layer,1);assert.equal(snap().remaining,100);assert.equal(snap().discarded[0],46);
 for(let phase=1;phase<4;phase++){const n=[6,5,5,6][phase];for(let i=0;i<n;i++){stroke(i%2?12:24);const s=snap();assert(s.remaining>=0);if(s.bounds.m)assert(s.bounds.lo>=12&&s.bounds.hi<=24);}assert.equal(snap().remaining,0);assert.equal(snap().count,n);el('finish').listeners.click();}
 assert.equal(el('end').hidden,false);const s=snap();for(let i=0;i<4;i++)assert.equal(s.layers[i].reduce((n,x)=>n+x.q,0)+s.discarded[i],100);
 el('again').listeners.click();assert.equal(snap().count,0);assert.equal(snap().remaining,100);
 const output={success:true,checked:['零至两笔禁止完成','三笔允许提前完成','撤销精确退量','阶段余量不转移','五六笔耗尽且末笔合法','四层账本守恒','重新试画清空'],boundary:'仅执行实际原型脚本的DOM替身检查；未做浏览器视觉检查，未验证Unity新流程'};
 fs.writeFileSync(path.join(root,'Docs/Design/全流程重设计20261019/参考资料/画布原型脚本校验.json'),JSON.stringify(output,null,2));console.log(output);
})().catch(e=>{console.error(e);process.exit(1);});
