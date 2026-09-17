namespace BigOrb;

internal static class Dashboard
{
    // {{TOKEN}} gets swapped for the api token when served
    internal const string Html = """
<!doctype html><html><head><meta charset="utf-8"><title>Big Orb</title>
<meta name="viewport" content="width=device-width,initial-scale=1">
<style>
:root{--bg:#12182c;--card:#1c2440;--card2:#232d52;--ink:#f0ead8;--sub:#9aa3bd;--red:#ff5d5d;--green:#37d597;--amber:#f4cc48;
 --line:rgba(255,255,255,.05);--dotline:rgba(240,234,216,.35);
 --okbg:#173a2c;--badbg:#3a1720;--badink:#ffb0b0;--platbg:#2a2a2a;--platink:#aaa;--steambg:#1b2838;--steamink:#66c0f4;
 --psnbg:#0b2a6b;--psnink:#8fb3ff;--lockbg:#1c3a52;--lockink:#7fd6ff;--youbg:#3a3352;--youink:#cbb8ff;--focus:#b610a7;--hover:1.25}
:root[data-theme=light]{--bg:#f3f1ea;--card:#ffffff;--card2:#e8e5db;--ink:#1c2136;--sub:#5d6478;--red:#c82f2f;--green:#1c8a5c;--amber:#9a6d00;
 --line:rgba(0,0,0,.07);--dotline:rgba(0,0,0,.25);
 --okbg:#d6f2e4;--badbg:#f8dada;--badink:#8c1f1f;--platbg:#e3e3e3;--platink:#555;--steambg:#dce8f5;--steamink:#1b4f7a;
 --psnbg:#dbe4fb;--psnink:#1e3f8f;--lockbg:#d9ecf7;--lockink:#1d5b82;--youbg:#e6e0f7;--youink:#4a3c8a;--hover:.93}
*{box-sizing:border-box;margin:0}
body{background:var(--bg);color:var(--ink);font:14px/1.45 "Segoe UI",system-ui,sans-serif;padding:18px}
h1{font-size:22px;letter-spacing:1px} h2{font-size:13px;color:var(--sub);text-transform:uppercase;letter-spacing:2px;margin:0 0 10px}
.top{display:flex;align-items:center;gap:14px;flex-wrap:wrap;margin-bottom:16px}
.pill{padding:4px 12px;border-radius:99px;background:var(--card);color:var(--sub);font-size:12px}
.pill.on{background:var(--okbg);color:var(--green)} .pill.off{background:var(--badbg);color:var(--red)}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:12px;margin-bottom:18px}
.card{background:var(--card);border-radius:14px;padding:12px}
.cols{display:grid;grid-template-columns:1.2fr 1fr;gap:14px} @media(max-width:1100px){.cols{grid-template-columns:1fr}}
.p{display:flex;gap:12px;align-items:center}
.dots{display:flex;flex-direction:column;gap:2px}
.dot{width:22px;height:22px;border-radius:50%;border:2px solid var(--dotline)}
.pname{font-weight:600;font-size:15px} .meta{color:var(--sub);font-size:12px}
.btns{display:flex;gap:6px;flex-wrap:wrap;margin-top:8px}
button{background:var(--card2);color:var(--ink);border:0;border-radius:8px;padding:5px 10px;font-size:12px;cursor:pointer}
button:hover{filter:brightness(var(--hover))}
button.danger{background:var(--badbg);color:var(--badink)} button.good{background:var(--okbg);color:var(--green)}
.badge{font-size:11px;padding:2px 8px;border-radius:99px;margin-left:6px}
.badge.plat{background:var(--platbg);color:var(--platink);text-transform:uppercase;font-size:10px} .badge.plat.steam{background:var(--steambg);color:var(--steamink)} .badge.plat.psn{background:var(--psnbg);color:var(--psnink)} .badge.plat.unset{background:var(--badbg);color:var(--badink)}
.badge.locked{background:var(--lockbg);color:var(--lockink)} .badge.banned{background:var(--badbg);color:var(--badink)} .badge.you{background:var(--youbg);color:var(--youink)}
.log{max-height:280px;overflow-y:auto;font-size:12.5px}
.log div{padding:3px 0;border-bottom:1px solid var(--line)}
.t{color:var(--sub);margin-right:6px;font-size:11px} .who{color:var(--amber);font-weight:600}
.alert{color:var(--red)} input{background:var(--card2);border:0;border-radius:8px;color:var(--ink);padding:5px 8px;font-size:12px} input:focus{outline:1px solid var(--focus)}
.dotsmall{display:inline-block;width:11px;height:11px;border-radius:50%;border:1px solid var(--dotline);margin-right:1px}
.stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:12px;margin-bottom:14px}
.stat{background:var(--card);border-radius:14px;padding:12px 14px}
.stat .n{font-size:30px;font-weight:700;line-height:1.1} .stat .n small{font-size:14px;color:var(--sub);font-weight:500}
.stat .l{color:var(--sub);font-size:11px;text-transform:uppercase;letter-spacing:1.5px;margin-top:2px}
.bar{height:6px;border-radius:99px;background:var(--card2);margin-top:8px;overflow:hidden;display:flex} .bar i{display:block;height:100%}
.tiles{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:7px}
.tile{background:var(--card2);border-radius:10px;padding:7px 9px;border-left:4px solid #3a4470;font-size:12px;line-height:1.3;display:flex;gap:7px;align-items:center;min-height:44px}
.tile .ic{font-size:16px;width:20px;text-align:center} .tile .nm{flex:1;overflow:hidden;text-overflow:ellipsis} .tile .sb{display:block;color:var(--sub);font-size:10.5px}
.tile.done{border-color:var(--green)} .tile.solved{border-color:var(--amber)} .tile.out{border-color:#7fd6ff} .tile.rest{opacity:.55}
.hub{background:var(--card2);border-radius:10px;padding:9px 11px;margin-bottom:7px}
.hub .hd{display:flex;align-items:center;gap:8px} .hub .hd b{flex:1}
.pips{display:flex;gap:4px;margin:6px 0 2px;align-items:center} .pip{width:16px;height:16px;border-radius:50%;border:2px solid #3a4470;display:flex;align-items:center;justify-content:center;font-size:9px} .pip.on{background:var(--green);border-color:var(--green);color:#0b2a1c}
.cuts{display:flex;gap:2px} .cuts i{display:block;width:10px;height:6px;border-radius:2px;background:#3a4470} .cuts i.on{background:var(--amber)}
.chip{font-size:11px;padding:2px 8px;border-radius:99px;background:#2a3358;color:var(--sub)} .chip.ok{background:#173a2c;color:var(--green)} .chip.warn{background:#3d3418;color:var(--amber)} .chip.info{background:#1c3a52;color:#7fd6ff}
.gourd{display:flex;gap:9px;align-items:center;padding:6px 0;border-bottom:1px solid rgba(255,255,255,.05)} .gourd:last-child{border:0} .gourd .ic{font-size:18px;width:24px;text-align:center}
.gate{display:flex;align-items:center;gap:8px;padding:3px 0} .gv{width:26px;text-align:center;color:var(--amber)}
.badge.frozen{background:#1c3a52;color:#7fd6ff} .badge.good{background:var(--okbg);color:var(--green)}
.legend{display:flex;gap:12px;flex-wrap:wrap;font-size:11px;color:var(--sub);margin:0 0 8px} .legend i{display:inline-block;width:10px;height:10px;border-radius:3px;margin-right:4px;vertical-align:-1px}
</style></head><body>
<div class="top">
 <h1>⚫ BIG ORB</h1>
 <span id="host" class="pill">...</span>
 <button id="btnTags"></button>
 <button id="btnChime"></button>
 <span class="pill" id="count"></span>
 <span class="pill" id="code" style="display:none">Session Join Code: <b id="codeVal" style="letter-spacing:2px"></b> <button id="btnCopy" style="padding:2px 8px;margin-left:4px">copy</button></span>
 <span class="pill" id="pw" style="display:none">Session Password: <b id="pwVal"></b> <button id="btnPwShow" style="padding:2px 8px;margin-left:4px">show</button> <button id="btnPwSet" style="padding:2px 8px">change</button></span>
 <span style="flex:1"></span>
 <button id="btnTheme" onclick="toggleTheme()"></button>
</div>

<h2>Players</h2><div id="players" class="grid"></div>

<div class="cols">
 <div>
  <div class="card" style="margin-bottom:12px"><h2>⚠ Cheat alerts</h2><div class="meta" style="margin-bottom:6px">fly / speed are heuristics (carried, launched or lagging players trip them too). idspoof, epicspoof, platspoof, eosspoof, eosanon and voiceflood are not: those are a client lying about who it is or flooding the voice relay, and with guard.autoBan on its connection is banned automatically.</div><div id="alerts" class="log"></div></div>
  <div class="card" style="margin-bottom:12px"><h2>💬 Chat log</h2><div id="chat" class="log"></div></div>
  <div class="card"><h2>✏ Sign edits</h2><div id="signlocks" class="meta" style="margin-bottom:6px"></div><div id="signs" class="log"></div></div>
 </div>
 <div>
  <div class="card" style="margin-bottom:12px"><h2>⛔ Ban list</h2>
   <div style="display:flex;gap:6px;margin-bottom:8px"><input id="banId" placeholder="identifier or connection address to ban manually" style="flex:1"><button class="danger" onclick="const v=el('banId').value.trim();if(/^[0-9a-f]{32}$/i.test(v))cmd('banaddr',{key:v,text:'manual'});else cmd('ban',{id:v});el('banId').value=''">ban</button></div>
   <div style="display:flex;gap:6px;margin-bottom:8px"><a href="/api/bans.csv?token={{TOKEN}}" download><button>⬇ export csv</button></a><button onclick="el('banImportBox').style.display=el('banImportBox').style.display==='none'?'':'none'">⬆ import csv</button></div>
   <div id="banImportBox" style="display:none;margin-bottom:8px"><textarea id="banCsv" rows="5" style="width:100%;box-sizing:border-box" placeholder="identifier,name,platformId,when,address&#10;paste a csv exported from another host; entries already on your list are skipped"></textarea><button onclick="banImport()">merge into my ban list</button></div>
   <div id="bans" class="log"></div>
  </div>
  <div class="card" style="margin-bottom:12px"><h2>👥 Everyone this session</h2><div id="roster" class="log"></div></div>
  <div class="card"><h2>📜 Events</h2><div id="events" class="log"></div></div>
 </div>
</div>

<div id="world" style="margin-top:16px">
 <h2 style="margin:0 0 10px">🌍 World</h2>
 <div class="meta" style="margin-bottom:10px">Puzzle, hub and finale state comes from the world's own state objects. Reset returns a puzzle to how it was when the world loaded — pieces back, vice closed, gourd back in it. Nothing here touches the save until the game writes it itself.</div>
 <div class="stats" id="prStats"></div>
 <div class="cols">
  <div>
   <div class="card" style="margin-bottom:12px">
    <h2>🧩 Puzzles</h2>
    <div class="legend"><span><i style="background:var(--green)"></i>gourd turned in</span><span><i style="background:#7fd6ff"></i>gourd out in the world</span><span><i style="background:var(--amber)"></i>solved, gourd still in vice</span><span><i style="background:#3a4470"></i>untouched</span></div>
    <div id="prPuzzles" class="tiles" style="margin-bottom:10px"></div>
    <div class="pname" style="margin:4px 0">🏆 Solves <button id="btnSolveChime" style="margin-left:6px"></button> <button onclick="cmd('solveclear')">clear</button></div>
    <div id="solves" class="log" style="max-height:140px;margin-bottom:10px"></div>
    <div id="puzzlemgmt" style="margin-bottom:10px"></div>
    <div style="margin:4px 0"><button onclick="cmd('bringgourds')">🥒 bring all puzzle gourds to me</button> <span class="meta">unpins every puzzle's gourd and drops them in front of you</span></div>
   </div>
   <div class="card" style="margin-bottom:12px"><h2>📦 Items</h2>
    <div style="display:flex;gap:6px;flex-wrap:wrap;align-items:center">
     <button onclick="cmd('propreset',{key:'near',val:30})">reset items near me (30m)</button>
     <button class="danger" onclick="if(confirm('Return every item in the world to where it was when the world loaded?'))cmd('propreset',{key:'all'})">reset ALL items</button>
     <input id="prKey" placeholder="prop group, e.g. RewardGourd" style="width:190px"><button onclick="cmd('propreset',{key:el('prKey').value})">reset group</button>
    </div>
    <div class="meta" style="margin-top:6px">Items go back to where they were when the world loaded (re-pinned into their home if they had one; anything being carried is dropped first). The 4-player world ships with a fresh-world baseline; other sizes are captured the first time you host them, so host a fresh save once for those.</div>
   </div>
  </div>
  <div>
   <div class="card" style="margin-bottom:12px"><h2>🗝 Hubs</h2><div id="prHubs"></div><div id="hubmgmt" style="margin-top:8px"></div></div>
   <div class="card" style="margin-bottom:12px"><h2>🥒 Gourds out in the world</h2><div id="prGourds"></div></div>
   <div class="card" style="margin-bottom:12px"><h2>🌑 Black tower finale</h2>
    <div style="margin-bottom:8px"><button class="danger" onclick="if(confirm('Reset the whole finale? Dream door, monument and black key, all seven gauntlet levels, final bell. Best with nobody inside the tower.'))cmd('finalereset',{key:'all'})">reset the whole finale</button></div>
    <div id="finalemgmt"></div>
   </div>
   <div id="scpanels"></div>
  </div>
 </div>
</div>

<script>
const TOKEN='{{TOKEN}}';
function applyTheme(){
 let t='dark';try{t=localStorage.getItem('theme')||'dark';}catch(e){}
 document.documentElement.dataset.theme=t;
 document.getElementById('btnTheme').textContent=t==='dark'?'☀ light mode':'☾ dark mode';
}
function toggleTheme(){
 const t=document.documentElement.dataset.theme==='dark'?'light':'dark';
 try{localStorage.setItem('theme',t);}catch(e){}
 applyTheme();
}
applyTheme();
const el=id=>document.getElementById(id);
const esc=s=>(s??'').toString().replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
let lastSnap=null,lastBans=[],lastRoster=[],lastSigns=[],showPw=false;
function copyText(t){
 if(navigator.clipboard){navigator.clipboard.writeText(t).catch(()=>{});return;}
 const ta=document.createElement('textarea');ta.value=t;document.body.appendChild(ta);ta.select();document.execCommand('copy');ta.remove();
}
el('btnCopy').onclick=()=>{copyText(el('codeVal').textContent);el('btnCopy').textContent='copied';setTimeout(()=>el('btnCopy').textContent='copy',1200);};
el('btnPwShow').onclick=()=>{showPw=!showPw;refresh();};
el('btnPwSet').onclick=()=>{const p=prompt('New session password (blank = no password):',lastSnap?.password||'');if(p===null)return;cmd('setpassword',{text:p});};
document.addEventListener('click',e=>{
 const se=e.target.closest('button[data-signerase]');
 if(se){const s=lastSigns[+se.dataset.signerase];if(s)cmd('signset',{key:s.net,text:''});return;}
 const sr=e.target.closest('button[data-signrestore]');
 if(sr){const s=lastSigns[+sr.dataset.signrestore];if(s)cmd('signset',{key:s.net,text:s.text});return;}
 const sl=e.target.closest('button[data-signlock]');
 if(sl){const s=lastSigns[+sl.dataset.signlock];if(s)cmd('signlock',{key:s.net,text:s.text});return;}
 const su=e.target.closest('button[data-signunlock]');
 if(su){cmd('signunlock',{key:su.dataset.signunlock});return;}
 const u=e.target.closest('button[data-unban]');
 if(u){const b=lastBans[+u.dataset.unban];if(b)cmd('unban',{id:b.id});return;}
 const rb=e.target.closest('button[data-rban]');
 if(rb){const r=lastRoster[+rb.dataset.rban];if(r&&confirm('Ban '+r.name+'? They will be kicked (if online) and auto-kicked on every future join.'))cmd('ban',{id:r.id});return;}
 const ru=e.target.closest('button[data-runban]');
 if(ru){const r=lastRoster[+ru.dataset.runban];if(r)cmd('unban',{id:r.id});return;}
 const b=e.target.closest('button[data-act]');
 if(!b||!lastSnap)return;
 const p=lastSnap.players[+b.dataset.i];
 if(!p)return;
 if(b.dataset.act==='ban'&&!confirm('Ban '+p.name+'? They will be kicked and auto-kicked on every future join.'))return;
 cmd(b.dataset.act,{id:p.id});
});
function cmd(action,extra={}){const q=new URLSearchParams({action,...extra});fetch('/api/cmd?'+q,{method:'POST',headers:{'X-Orb-Token':TOKEN}}).then(refresh);}
function row(e,html){return `<div><span class="t">${esc(e.t)}</span>${html}</div>`}
function banImport(){const t=el('banCsv').value;if(!t.trim())return;fetch('/api/cmd?action=banimport',{method:'POST',headers:{'X-Orb-Token':TOKEN,'Content-Type':'text/plain'},body:t}).then(()=>{el('banCsv').value='';el('banImportBox').style.display='none';refresh();});}
async function refresh(){
 let d; try{d=await (await fetch('/api/state',{headers:{'X-Orb-Token':TOKEN}})).json();}catch(e){el('host').textContent='plugin offline';el('host').className='pill off';return;}
 const s=d.snap;
 el('host').textContent=s.hosting?('HOSTING · '+(s.session||'')):'not hosting';
 el('host').className='pill '+(s.hosting?'on':'off');
 el('btnTags').textContent=s.nametags?'🏷 nametags: ON':'🏷 nametags: off';
 el('btnTags').className=s.nametags?'good':'';
 el('btnTags').onclick=()=>cmd('nametags',{val:s.nametags?0:1});
 el('btnChime').textContent=s.chime?'🔔 chime: ON':'🔕 chime: off';
 el('btnChime').className=s.chime?'good':'';
 el('btnChime').onclick=()=>cmd('chime',{val:s.chime?0:1});
 el('count').textContent=s.players.length+' walkers';
 el('code').style.display=s.hosting&&s.code?'':'none';
 el('codeVal').textContent=s.code||'';
 el('pw').style.display=s.hosting?'':'none';
 el('pwVal').textContent=s.password?(showPw?s.password:'•'.repeat(s.password.length)):'(none)';
 el('btnPwShow').textContent=showPw?'hide':'show';
 el('btnPwShow').style.display=s.password?'':'none';

 lastSnap=s;
 el('players').innerHTML=s.players.map((p,i)=>{
  const dots=p.colors.map(c=>`<div class="dot" style="background:${c}"></div>`).join('');
  const badges=`<span class="badge plat ${esc(p.platform)}">${esc(p.platform)}</span>`+(p.local?'<span class="badge you">you</span>':'')
   +(p.banned?'<span class="badge banned">banned</span>':'');
  const btns=p.local?'<span class="meta">this is you</span>':`
   <button data-i="${i}" data-act="kick" class="danger">kick</button>
   <button data-i="${i}" data-act="ban" class="danger">ban</button>`;
  const alt=[p.username,p.modName].filter(n=>n&&n!==p.name);
  return `<div class="card"><div class="p"><div class="dots">${dots}</div>
   <div><div class="pname">${esc(p.name)} ${badges}</div>
   ${alt.length?`<div class="meta">aka ${alt.map(esc).join(' · ')}</div>`:''}
   <div class="meta">${esc(p.id)}${p.platform==='steam'?'':' · platformId '+esc(p.platformId)}</div>
   <div class="meta">pos ${p.pos.join(', ')} · ${p.speed} m/s · climbing ${p.air}s</div></div></div>
   <div class="btns">${btns}</div></div>`;
 }).join('')||'<div class="meta">no players</div>';

 el('alerts').innerHTML=d.alerts.slice().reverse().map(e=>row(e,`<span class="alert">${esc(e.kind)}</span> <span class="who">${esc(e.name)}</span> ${esc(e.detail)}`)).join('')||'<div class="meta">none</div>';
 el('chat').innerHTML=d.chat.slice().reverse().map(e=>row(e,`<span class="who">${esc(e.name)}:</span> ${esc(e.msg)}`)).join('')||'<div class="meta">no chat yet</div>';
 lastSigns=d.signs;
 const locks=d.signlocks||[];const lockOf=n=>locks.find(l=>l.net===n);
 el('signlocks').innerHTML=locks.length?'🔒 locked: '+locks.map(l=>`<span class="badge locked"><i>${esc(l.key)}</i> #${l.net} "${esc(l.text)}" <button data-signunlock="${l.net}">unlock</button></span>`).join(' '):'no locked signs. Lock a sign from an edit below to stop guests changing it.';
 el('signs').innerHTML=d.signs.map((e,i)=>({e,i})).reverse().map(({e,i})=>{
  const lk=e.net?lockOf(e.net):null;
  const lockBtn=!e.net?'':lk?` <button data-signunlock="${e.net}">🔓 unlock</button>`:` <button data-signlock="${i}">🔒 lock this</button>`;
  const btns=e.net?` <button data-signerase="${i}">erase</button> <button data-signrestore="${i}">restore this</button>${lockBtn}`:'';
  return row(e,`<span class="who">${esc(e.byName)}</span> → <i>${esc(e.key)}</i>${lk?' 🔒':''}: "${esc(e.text)}"${btns}`);
 }).join('')||'<div class="meta">no sign edits yet</div>';
 el('events').innerHTML=d.events.slice().reverse().map(e=>row(e,`${esc(e.kind)} <span class="who">${esc(e.name)}</span> ${esc(e.detail??'')}`)).join('')||'<div class="meta">quiet so far</div>';

 lastRoster=d.roster||[];
 el('roster').innerHTML=lastRoster.map((r,i)=>{
  const dots=(r.colors||[]).map(c=>`<span class="dotsmall" style="background:${c}"></span>`).join('');
  const aka=r.username&&r.username!==r.name?` <span class="t">aka ${esc(r.username)}</span>`:'';
  const btn=r.banned?`<button class="good" data-runban="${i}">unban</button>`:`<button class="danger" data-rban="${i}">ban</button>`;
  return `<div>${dots} <span style="color:${r.online?'var(--green)':'var(--sub)'}">●</span> <b>${esc(r.name)}</b>${aka} <span class="t">joined ${esc(r.first)} · last ${esc(r.last||'?')}</span> ${btn}</div>`;
 }).join('')||'<div class="meta">nobody yet</div>';
 try{renderWorld(d);}catch(err){console.error(err);}
 lastBans=d.bans;
 el('bans').innerHTML=d.bans.map((b,i)=>`<div><b>${esc(b.name)}</b> <span class="t">${esc(b.id).slice(0,24)}${b.addr?' · addr '+esc(b.addr).slice(0,8)+'…':''} · ${esc(b.when)}</span> <button class="good" data-unban="${i}">unban</button></div>`).join('')||'<div class="meta">no bans</div>';
}

// ---- world: module panels ----
 window.panel_hubs=function(list){
  const box=el('hubmgmt'); if(!box) return '';
  box.innerHTML=(list||[]).map(h=>{
   const st=h.filled===0&&h.cuts===0&&h.keyWhere==='KeyStoneHome'?'<span class="badge good">at rest</span>':'<span class="badge frozen">in progress</span>';
   return `<div class="gate"><span class="gv">🗝</span><span style="flex:1"><b>${esc(h.label)}</b> ${st} <span class="t">slots ${h.filled}/${h.slots} · key ${esc(h.keyWhere)} · cuts ${h.cuts}/${h.cutsN}${h.complete?' (complete)':''} · ${esc(h.save)}=${h.saveVal===null?'unset':h.saveVal} · ${esc(h.note)}</span></span>
    <button class="danger" onclick="cmd('hubfill',{key:'${h.root}'})" title="cheat: pin puzzle gourds into every empty slot (takes them from their puzzles)">🔥 fill</button><button onclick="cmd('resethub',{key:'${h.root}',val:1})" title="gourds dropped at the hub">reset, keep gourds</button><button onclick="cmd('resethub',{key:'${h.root}'})" title="every gourd in a slot, loose nearby or carried goes back to its puzzle and that puzzle is reset; key blanked and back in its stone; whatever the key was activating is reset too">reset all</button></div>`;
  }).join('')||'<div class="meta">no hubs registered</div>';
  return '';
 };
 window.panel_mines=function(m){ return `<div class="card" style="margin-bottom:8px"><div class="pname">mines (${m.bombCount} bombs)</div><div>bombs: <b>${(m.bombs||[]).map(esc).join(', ')||'—'}</b></div><div class="t">safe: ${(m.safe||[]).map(esc).join(', ')||'—'}</div><button onclick="cmd('minecheat',{key:'MemoryBombs',val:1})">press all safe mines</button></div>`; };
 window.panel_finale=function(f){
  const box=el('finalemgmt'); if(!box) return '';
  const tr=el('dreamTrapped'); if(tr) tr.innerHTML=(f.trapped||[]).length?'🔁 looping: <b>'+f.trapped.map(esc).join(', ')+'</b>':'';
  box.innerHTML=(f.stages||[]).map(s=>{
   const pr=Object.entries(s.probe||{}).map(([k,v])=>`${esc(k)} ${v===null?'?':v}`).join(' · ');
   const done=!!s.done;
   return `<div class="gate"><span class="gv">${done?'🔓':'🔒'}</span><span style="flex:1"><b>${esc(s.label)}</b> ${done?'<span class="badge frozen">open</span>':'<span class="badge good">at rest</span>'} <span class="t">${pr}${f.held?` · holding ${f.held}`:''} · ${esc(s.note)}</span></span>
    <button onclick="cmd('finalereset',{key:'${s.key}'})">reset</button></div>`;
  }).join('')||'<div class="meta">no finale stages registered</div>';
  return '';
 };
 window.panel_solves=function(s){
  const b=el('btnSolveChime'); if(b){b.textContent='chime: '+(s.chime?'ON':'off'); b.className=s.chime?'good':''; b.onclick=()=>cmd('solvechime',{val:s.chime?0:1});}
  const ic={puzzle:'🏆',hub:'🗝',key:'🔑'};
  el('solves').innerHTML=(s.recent||[]).map(r=>`<div><span class="t">${esc(r.t)}</span> ${ic[r.kind]||'•'} <b>${esc(r.what)}</b>${r.who?' <span class="t">— '+esc(r.who)+'</span>':''}</div>`).join('')||'<div class="meta">nothing solved yet this session</div>';
  return '';
 };
 window.panel_gourds=function(){ return ''; }; // rendered on the progress tab
 window.panel_puzzles=function(list){
  el('puzzlemgmt').innerHTML=(list||[]).map(p=>{
   const solved=(p.vice===0)||(p.vice===null&&p.changed>0&&p.gourdsHome<p.gourds), home=p.gourdsHome===p.gourds;
   const st=solved?'<span class="badge frozen">solved</span>':(p.changed>0?'<span class="badge">'+p.changed+' state(s) off baseline</span>':'<span class="badge good">at rest</span>');
   return `<div class="gate"><span class="gv">${home?'🥒':'💨'}</span><span style="flex:1"><b>${esc(p.label)}</b> ${st} <span class="t">vice ${p.vice===null?'?':p.vice} · gourd ${home?'in vice':(esc(p.gourdWhere||'?'))} · save ${esc(p.save)}=${p.saveVal===null?'unset':p.saveVal} · ${esc(p.note)}</span></span>
    ${p.root==='PointersParadise'?`<input type="number" min="1" max="99" style="width:44px" id="pl_${p.root}" placeholder="len"><button onclick="cmd('progress',{key:'${p.root}',val:el('pl_${p.root}').value||0})" title="sequence length (requiredIncrements); blank = read current">length</button>`:''}    ${p.cheat?`<button class="danger" onclick="cmd('${p.cheat}',{key:'${p.root}'})">${p.cheat==='nhold'?'🔥 force start':p.cheat==='countcheat'?'🔢 fill answer':p.cheat==='minecheat'?'👁 reveal mines':'🔥 cheat solve'}</button>`:''}<button onclick="cmd('resetpuzzle',{key:'${p.root}'})">reset</button></div>`;
  }).join('')||'<div class="meta">no puzzles registered</div>';
  return '';
 };
function renderProgress(pn){
  const pz=pn.puzzles||[], hubs=pn.hubs||[], g=pn.gourds||{list:[]}, gl=g.list||[];
 const byRoot={}; gl.forEach(x=>{ if(x.root) byRoot[x.root]=x; });
 const short=l=>(l||'').replace(/\s*\(.*$/,'');
 let solved=0, launched=0, done=0;
 el('prPuzzles').innerHTML=pz.map(p=>{
  const gd=byRoot[p.root]||{};
  const isSolved=(p.vice===0)||(p.vice===null&&p.changed>0&&p.gourdsHome<p.gourds);
  const isLaunched=p.saveVal!==null&&p.saveVal!==0;
  const inSlot=gd.where==='slot', out=!!gd.where&&gd.where!=='puzzle'&&!inSlot;
  if(isSolved) solved++; if(isLaunched) launched++; if(inSlot) done++;
  const cls=inSlot?'done':out?'out':isSolved?'solved':'rest';
  const ic=inSlot?'🏛':gd.where==='held'?'🙌':gd.where==='backpack'?'🎒':out?'💨':isSolved?'🔓':'🥒';
  const sub=inSlot?('at '+short(gd.slotOf||'?')):(gd.where==='held'||gd.where==='backpack')?(gd.heldBy||'carried'):out?'dropped '+(gd.detail||'').replace(/^(loose|in [^·]+)\s*·\s*/,'').replace(/\s*·\s*at.*$/,''):isSolved?'solved, gourd waiting':(p.changed>0?p.changed+' state(s) touched':'at rest');
  return `<div class="tile ${cls}" title="${esc(p.label)} — ${esc(gd.detail||p.gourdWhere||'')}${isLaunched?' · launched':''}"><span class="ic">${ic}</span><span class="nm">${esc(short(p.label))}<span class="sb">${esc(sub)}</span></span></div>`;
 }).join('')||'<div class="meta">no puzzles registered (scripts loaded?)</div>';
 const out=gl.filter(x=>x.where!=='puzzle'&&x.where!=='slot');
 const hubsDone=hubs.filter(h=>h.complete&&h.keyWhere!=='KeyStoneHome'&&h.keyWhere!=='loose').length;
 const hubsFull=hubs.filter(h=>h.slots>0&&h.filled>=h.slots).length;
 const T=pz.length||1, pc=v=>Math.round(100*v/T);
 el('prStats').innerHTML=`
  <div class="stat"><div class="n">${done}<small> / ${pz.length}</small></div><div class="l">gourds turned in</div><div class="bar"><i style="width:${pc(done)}%;background:var(--green)"></i><i style="width:${pc(out.length)}%;background:#7fd6ff"></i></div></div>
  <div class="stat"><div class="n">${launched}<small> / ${pz.length}</small></div><div class="l">puzzles launched this save</div><div class="bar"><i style="width:${pc(launched)}%;background:var(--amber)"></i></div></div>
  <div class="stat"><div class="n">${solved}</div><div class="l">vices open right now</div></div>
  <div class="stat"><div class="n">${out.length}</div><div class="l">gourds loose or carried</div></div>
  <div class="stat"><div class="n">${hubsDone}<small> / ${hubs.length}</small></div><div class="l">keys placed · ${hubsFull} hubs full</div><div class="bar"><i style="width:${hubs.length?Math.round(100*hubsDone/hubs.length):0}%;background:var(--green)"></i></div></div>`;
 el('prGourds').innerHTML=out.map(x=>{
  const ic=x.where==='held'?'🙌':x.where==='backpack'?'🎒':'💨';
  const who=x.where==='held'?`<span class="chip info">carried by ${esc(x.heldBy||'?')}</span>`:x.where==='backpack'?`<span class="chip info">in ${esc(x.heldBy||'?')}'s backpack</span>`:`<span class="chip warn">dropped</span>`;
  const where=(x.where==='held'||x.where==='backpack')?'':(x.detail||'').replace(/^(loose|in [^·]+)\s*·\s*/,'');
  const tp=x.net?`<button onclick="cmd('bringnet',{key:${x.net}})" title="pull it to me (takes it off whoever holds it)" style="padding:3px 8px;font-size:11px">➡ to me</button>`:'';
  return `<div class="gourd"><span class="ic">${ic}</span><span style="flex:1"><b>${esc(short(x.label))}</b> ${who}<div class="meta">${esc(where)}${x.flag?' · map: '+esc(x.flag):''}</div></span>${tp}</div>`;
 }).join('')||'<div class="meta">every gourd is either in its puzzle or turned in ✨</div>';
 el('prHubs').innerHTML=hubs.map(h=>{
  const inSlots=gl.filter(x=>x.where==='slot'&&x.slotOf===h.label).map(x=>short(x.label));
  const rest=h.filled===0&&h.cuts===0&&h.keyWhere==='KeyStoneHome';
  const placed=h.complete&&h.keyWhere!=='KeyStoneHome'&&h.keyWhere!=='loose';
  const st=placed?'<span class="chip ok">key placed</span>':rest?'<span class="chip">untouched</span>':(h.filled>=h.slots&&h.slots>0)?'<span class="chip warn">full — key stage</span>':'<span class="chip warn">collecting</span>';
  const pips=Array.from({length:h.slots},(_,i)=>`<div class="pip ${i<h.filled?'on':''}" title="${i<h.filled?esc(inSlots[i]||'gourd'):'empty'}">${i<h.filled?'✓':''}</div>`).join('');
  const cuts=h.cutsN?`<span class="cuts" title="key cuts ${h.cuts}/${h.cutsN}">${Array.from({length:h.cutsN},(_,i)=>`<i class="${i<h.cuts?'on':''}"></i>`).join('')}</span>`:'';
  const key=h.keyWhere==='KeyStoneHome'?'key in stone':h.keyWhere==='loose'?'key loose (dropped)':h.keyWhere.startsWith('carried')?'key '+esc(h.keyWhere):'key → '+esc(h.keyWhere.replace(/^BigKeyPlinth\s*/,''));
  return `<div class="hub"><div class="hd"><span>${placed?'🏛':'🗝'}</span><b>${esc(short(h.label))}</b>${st}</div><div class="pips">${pips}<span class="meta" style="margin-left:6px">${h.filled}/${h.slots}</span></div><div class="meta" style="display:flex;gap:8px;align-items:center">${cuts}<span>${key}${h.complete?' · cut':''}</span></div>${inSlots.length?`<div class="meta" style="margin-top:3px">${inSlots.map(esc).join(' · ')}</div>`:''}</div>`;
 }).join('')||'<div class="meta">no hubs registered</div>';
}

function renderWorld(d){
 const pn=(d.modules||{}).panels||{};
 try{renderProgress(pn);}catch(err){console.error(err);}
 const extra=[];
 for(const [k,v] of Object.entries(pn)){
  const fn=window['panel_'+k];
  if(fn){try{const h=fn(v);if(h)extra.push(h);}catch(err){console.error(err);}}
 }
 el('scpanels').innerHTML=extra.join('');
}

refresh();setInterval(refresh,1000);
</script></body></html>
""";
}
