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
  <div class="card" style="margin-bottom:12px"><h2>⚠ Cheat alerts</h2><div class="meta" style="margin-bottom:6px">Heuristic only. Sustained climbing or speed gets flagged, which also catches players being carried, launched or lagging. Use it to decide who to watch, not as proof.</div><div id="alerts" class="log"></div></div>
  <div class="card" style="margin-bottom:12px"><h2>💬 Chat log</h2><div id="chat" class="log"></div></div>
  <div class="card"><h2>✏ Sign edits</h2><div id="signlocks" class="meta" style="margin-bottom:6px"></div><div id="signs" class="log"></div></div>
 </div>
 <div>
  <div class="card" style="margin-bottom:12px"><h2>⛔ Ban list</h2>
   <div style="display:flex;gap:6px;margin-bottom:8px"><input id="banId" placeholder="identifier to ban manually" style="flex:1"><button class="danger" onclick="cmd('ban',{id:el('banId').value});el('banId').value=''">ban</button></div>
   <div id="bans" class="log"></div>
  </div>
  <div class="card" style="margin-bottom:12px"><h2>👥 Everyone this session</h2><div id="roster" class="log"></div></div>
  <div class="card"><h2>📜 Events</h2><div id="events" class="log"></div></div>
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
 lastBans=d.bans;
 el('bans').innerHTML=d.bans.map((b,i)=>`<div><b>${esc(b.name)}</b> <span class="t">${esc(b.id).slice(0,24)} · ${esc(b.when)}</span> <button class="good" data-unban="${i}">unban</button></div>`).join('')||'<div class="meta">no bans</div>';
}
refresh();setInterval(refresh,1000);
</script></body></html>
""";
}
