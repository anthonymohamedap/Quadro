const S=new URLSearchParams(location.search).get('s')||'hoofd';
const m=document.getElementById('m');
const kop=(o={})=>`
 <div class="kop">
  <div><div class="sec sm">◀ Verkopen</div><h1>Verkoop 423 · Sofie Claes${o.tag||''}</h1>
   <div class="sec">Leuvensestraat 12, Aarschot · 0478 12 34 56 · bevestigd 2/11/2026 · afhalen ${o.afhaal??'<b style="color:#222">di 10/11</b> <span class="sec sm">(= laatste plandag)</span> <span class="btn sm2" style="padding:1px 7px">wijzigen</span>'}</div></div>
  <span class="sp"></span>
  <div class="r"><div class="sec sm">Totaal · prijs vast</div><div style="font-size:22px;font-weight:700">€ 245,00</div><div class="pill" style="background:var(--orBg);color:var(--or)">nog te betalen € 145,00</div></div>
  <span class="btn">⋯</span>
 </div>
 <div class="row" style="flex-wrap:wrap"><span class="sec sm">In deze verkoop:</span>
  <span class="chip">Offerte 423</span><span class="chip">Werkbon 423</span><span class="chip ${o.bonweg?'weg':''}">Bestelbon 2026-24</span><span class="chip">Voorschot € 100,00</span></div>
 <div class="balk" ${o.grijs?'style="filter:grayscale(1);opacity:.55"':''}>${['Offerte','Werkbon','In productie','Klaar om af te halen','Afgehaald & betaald'].map((t,i)=>{const c=i==0?'k':i==1?'h':'';return `<div class="st ${c}"><div class="b">${i==0?'✓':i+1}</div><div class="t">${t}</div><div class="d">${i==0?'2/11':i==1?(o.grijs?'geannuleerd':'hier ben je'):''}</div></div>`}).join('')}</div>`;

const dagen=[['ma','2/11',0.55,'vandaag'],['di','3/11',0.9],['wo','4/11',0.3],['do','5/11',0.75],['vr','6/11',0.2],['ma','9/11',0.4],['di','10/11',0.95],['wo','11/11',0.15],['do','12/11',0.5],['vr','13/11',0.1]];
const kalender=(sel,plan,afh=6)=>`<div class="card"><div class="row" style="margin-bottom:10px"><h2>Planning</h2><span class="sp"></span><span class="seg"><span class="on">2 weken</span><span>Maand</span></span><span class="btn sm2">◀</span><span class="btn sm2">Vandaag</span><span class="btn sm2">▶</span></div>
  <div class="kal">${dagen.map((d,i)=>{const p=plan&&plan[i];const vol=Math.min(1,d[2]+(p?p[1]:0));return `<div class="dag ${i==sel?'sel':''} ${i==0?'vd':''}"><div class="row" style="gap:4px"><b>${d[0]} ${d[1]}</b><span class="sp"></span><span class="sec sm">${Math.round(vol*8*10)/10} u</span></div>
   <div class="bar"><i style="width:${d[2]*100}%"></i>${p?`<i class="mine" style="width:${p[1]*100}%"></i>`:''}</div>
   ${p?`<div class="taak">${p[0]}</div>`:''}${i===afh?'<div class="afh">afhalen</div>':''}</div>`}).join('')}</div>
  <div class="row sm sec" style="margin-top:8px;gap:14px"><span><i class="lg"></i> ander werk</span><span><i class="lg mine"></i> deze verkoop</span><span>max. 8 u per dag · niet in het verleden</span><span class="sp"></span><span class="btn sm2" title="Zet de verkopen van die week op In productie">🗓 Weeklijst afdrukken</span></div></div>`;

const regels=(st)=>`<div class="card"><div class="row" style="margin-bottom:10px"><h2>Werkbon · inlijstingen</h2><span class="sp"></span><span class="sec sm">${st=='gepland'?'alles ingepland':'2 nog te plannen'}</span></div>
   <div class="inl ${st=='leeg'?'sel':''}"><div class="row"><span class="chk" style="padding:0"><i class="${st=='leeg'?'v':''}">${st=='leeg'?'✓':''}</i></span><span class="t">Trouwfoto · 50 × 70</span><span class="sp"></span><span class="sec sm">± 33 min</span></div>
    <div class="sec sm" style="margin-left:24px">L-2045 eik naturel · ontspiegeld glas · pp wit 6 cm · rug MDF</div>
    <div class="row sm" style="margin:6px 0 0 24px;gap:8px">${st=='gepland'?'<span class="pill" style="background:var(--grBg);color:var(--gr)">gepland di 10/11</span><span class="btn sm2" style="padding:1px 8px">herplannen</span><span class="btn sm2" style="padding:1px 6px" title="Taak verwijderen">✕</span>':'<span class="pill" style="background:#F3F4F6;color:#374151">nog niet gepland</span>'}
     <span class="sp"></span>${st=='besteld'||st=='gepland'?'<span class="pill" style="background:var(--blueBg);color:var(--blue)">lijst besteld 2/11 · Nielsen</span>':'<span class="pill" style="background:var(--orBg);color:var(--or)">lijst: 3,1 m nodig · 1,8 m voorraad</span><span class="btn sm2" style="padding:1px 8px;border-color:var(--yellow)">Bestellen…</span>'}</div></div>
   <div class="inl ${st=='leeg'?'sel':''}"><div class="row"><span class="chk" style="padding:0"><i class="${st=='leeg'?'v':''}">${st=='leeg'?'✓':''}</i></span><span class="t">Poster A3 · standaardkader C2</span><span class="sp"></span><span class="sec sm">± 15 min</span></div>
    <div class="sec sm" style="margin-left:24px">Alu zwart 30 × 42 · gewoon glas</div>
    <div class="row sm" style="margin:6px 0 0 24px;gap:8px">${st=='gepland'?'<span class="pill" style="background:var(--grBg);color:var(--gr)">gepland di 10/11</span><span class="btn sm2" style="padding:1px 8px">herplannen</span><span class="btn sm2" style="padding:1px 6px" title="Taak verwijderen">✕</span>':'<span class="pill" style="background:#F3F4F6;color:#374151">nog niet gepland</span>'}
     <span class="sp"></span><span class="pill" style="background:var(--grBg);color:var(--gr)">uit voorraad · gereserveerd (3 over)</span></div></div>
   ${st=='gepland'?'':`<div class="row" style="margin-top:8px"><span class="sec sm">${st=='leeg'?'2 geselecteerd · ± 48 min':'Selecteer inlijstingen en kies een dag, of sleep ze naar de kalender.'}</span><span class="sp"></span><span class="btn">Plan per regel</span><span class="btn ${st=='leeg'?'pri':''}">Plan op di 10/11</span></div>`}
   <div class="sec sm" style="margin-top:10px">Opmerking Trouwfoto: passe-partout 2 mm over de foto laten vallen.</div></div>`;

const voet=(dis,why)=>`<div class="voet"><span class="btn">◀ Vorige stap</span><span class="sec" style="white-space:nowrap">Stap 2 van 5</span><span class="sp"></span>${why?`<span class="sm" style="color:#B91C1C;max-width:260px;text-align:right">${why}</span>`:''}<span class="btn">🖨 Werkbon afdrukken</span><span class="btn">⤼ Stap overslaan</span><span class="btn pri" ${dis?'style="opacity:.45"':''}>Start productie ▶</span></div>`;

const scr={
hoofd:()=>kop({afhaal:'<span style="color:#9CA3AF">nog niet gekozen</span> <span class="sec sm">(wordt de laatste plandag)</span>'})+`<div class="g2" style="grid-template-columns:1.25fr 1fr">${kalender(6,null,-1)}${regels('open')}</div>${voet(true,'Plan eerst alle inlijstingen (2 nog te plannen)')}`,
inplannen:()=>kop()+`<div class="g2" style="grid-template-columns:1.25fr 1fr">${kalender(6,{6:['Trouwfoto · Poster',0.1]})}${regels('leeg')}</div>${voet()}
 <div class="toast">✓ 2 inlijstingen gepland op di 10/11 (± 48 min). Afhaaldatum wordt di 10/11 (laatste plandag) — <u>wijzigen</u></div>`,
bestellen:()=>kop()+`<div class="g2" style="grid-template-columns:1.25fr 1fr">${kalender(6,{6:['Trouwfoto · Poster',0.1]})}${regels('gepland').replace('lijst besteld 2/11 · Nielsen','lijst: 3,1 m nodig · 1,8 m voorraad')}</div>${voet()}
 <div class="overlay"><div class="dlg" style="width:600px">
  <h2 style="font-size:18px">Lijst bestellen bij de leverancier</h2>
  <div class="sum"><div><span class="sec">Lijst</span><b>L-2045 eik naturel 4 cm</b></div><div><span class="sec">Leverancier</span><span>Nielsen · levertijd ± 5 werkdagen</span></div>
   <div><span class="sec">Nodig voor Trouwfoto</span><span>2,80 m omtrek + verlies = <b>3,1 m</b></span></div><div><span class="sec">Vrij in voorraad</span><span>1,8 m (onder minimum)</span></div></div>
  <div><div class="lbl">Bestelwijze</div><span class="seg"><span class="on">Verstek (op maat gezaagd)</span><span>In lengte</span><span>Gemonteerd</span></span>
   <div class="sec sm" style="margin-top:4px">Verstek: de leverancier zaagt op maat voor deze inlijsting. In lengte: aanvullen van de eigen voorraad. Gemonteerd: kader komt klaar; nooit uit eigen voorraad.</div></div>
  <div class="g4" style="grid-template-columns:1fr 1fr"><div><div class="lbl">Besteldatum</div><div class="inp">02/11/2026</div></div><div><div class="lbl">Verwacht binnen</div><div class="inp" style="background:var(--muted)">± ma 9/11</div></div></div>
  <div class="note i">Komt in Leveranciers › bestellingen. Ontvangen boek je daar; dan springt "lijst binnen" in stap 3 vanzelf aan.</div>
  <div class="row" style="justify-content:flex-end"><span class="btn">Annuleren</span><span class="btn pri">Bestelling plaatsen</span></div></div></div>`,
gepland:()=>kop()+`<div class="g2" style="grid-template-columns:1.25fr 1fr">${kalender(6,{6:['Trouwfoto · Poster',0.1]})}${regels('gepland')}</div>${voet()}`,
start:()=>kop()+`<div class="g2" style="grid-template-columns:1.25fr 1fr">${kalender(6,{6:['Trouwfoto · Poster',0.1]})}${regels('gepland')}</div>${voet()}
 <div class="overlay"><div class="dlg" style="width:560px">
  <h2 style="font-size:18px">Start productie?</h2>
  <div class="tl sm"><div><b>Werkbon</b><span>gaat naar "In uitvoering"; de offerte volgt naar "In productie"</span></div>
   <div><b>Planning</b><span>2 van 2 inlijstingen gepland ✓ (verplicht)</span></div><div><b>Afhalen</b><span>di 10/11/2026 ✓ (verplicht)</span></div></div>
  <div class="note i">Gebeurt ook vanzelf wanneer de <b>weeklijst</b> van die week wordt afgedrukt.</div>
  <div class="note w">⚠ De lijst L-2045 is besteld maar nog niet binnen (verwacht ± ma 9/11). Starten mag wel (zoals nu).</div>
  <div class="row" style="justify-content:flex-end"><span class="btn">Nog niet</span><span class="btn pri">Start productie ▶</span></div></div></div>`,
afdrukken:()=>`<div class="pv">
  <div class="row" style="padding:12px 18px;background:#fff;border-bottom:1px solid var(--bd)"><h2>Werkbon 423 — voor het atelier</h2><span class="sp"></span><label class="sm row" style="gap:6px"><span class="chk" style="padding:0"><i></i></span>Prijzen tonen</label><span class="btn">Sluiten</span><span class="btn">⤓ Bewaren als PDF</span><span class="btn pri">🖨 Afdrukken…</span></div>
  <div style="display:grid;grid-template-columns:1fr 300px;height:calc(100vh - 62px)">
   <div style="background:#6B7280;display:grid;place-items:start center;padding:24px;overflow:hidden"><div class="paper" style="font-size:11.5px">
    <div class="row"><b style="font-size:16px">WERKBON 423</b><span class="sp"></span><span>afhalen <b>di 10/11/2026</b></span></div>
    <div class="sec" style="margin:4px 0 14px">Sofie Claes · 0478 12 34 56 · bevestigd 2/11 · bestelbon 2026-24</div>
    <div class="wb"><div class="row"><b>1 × Trouwfoto — 50 × 70 cm</b><span class="sp"></span>gepland di 10/11</div>
     <div>Lijst L-2045 eik naturel 4 cm · <i>besteld bij Nielsen, verstek</i></div><div>Glas: ontspiegeld · Passe-partout: wit 6 cm · Rug: MDF 3 mm</div>
     <div class="sec">Notitie: passe-partout 2 mm over de foto laten vallen.</div>
     <div class="row" style="gap:14px;margin-top:6px"><span>☐ lijst binnen</span><span>☐ gezaagd</span><span>☐ ingelijst</span><span>☐ nagekeken</span><span>initialen ____</span></div></div>
    <div class="wb"><div class="row"><b>1 × Poster A3 — standaardkader C2 Alu zwart 30 × 42</b><span class="sp"></span>gepland di 10/11</div>
     <div>uit voorraad · gewoon glas</div><div class="row" style="gap:14px;margin-top:6px"><span>☐ uit voorraad</span><span>☐ ingelijst</span><span>☐ nagekeken</span><span>initialen ____</span></div></div>
   </div></div>
   <div style="background:#fff;padding:18px;border-left:1px solid var(--bd);display:flex;flex-direction:column;gap:10px"><b>Waar komt het bestand?</b><div class="sec sm">Documenten › QuadroApp › Werkbonnen › <b style="color:#222">Werkbon-423.pdf</b></div>
    <div class="note i">Standaard zonder prijzen (voor het atelier). Afdrukken… opent het printvenster van het systeem.</div></div></div></div>`,
overslaan:()=>kop({afhaal:'<span style="color:#9CA3AF">nog niet gekozen</span>'})+`<div class="g2" style="grid-template-columns:1.25fr 1fr">${kalender(6,null,-1)}${regels('open')}</div>${voet()}
 <div class="overlay"><div class="dlg" style="width:600px">
  <h2 style="font-size:18px">Stap overslaan</h2>
  <div class="lbl">Je slaat over:</div>
  <div class="opt on"><span class="rad"></span><div><b>Werkbon én In productie → meteen Klaar om af te halen</b><div class="sec sm">Bv. standaardkader uit voorraad dat de klant meteen meeneemt. Bestelbon 2026-24 blijft; voorraad wordt verbruikt.</div></div></div>
  <div><div class="lbl">Reden</div><span class="seg"><span class="on">Standaardkader uit voorraad</span><span>Klant neemt meteen mee</span><span>Andere…</span></span></div>
  <div class="lbl">Zo ziet de stappenbalk er daarna uit</div>
  <div class="row" style="gap:6px">${['✓ Offerte','⤼ Werkbon','⤼ In productie','● Klaar','○ Afgehaald'].map((t,i)=>`<span class="chip" style="${i==1||i==2?'text-decoration:line-through;color:#9CA3AF':i==3?'background:#FEF3C7;border-color:var(--yellow)':''}">${t}</span>`).join('')}</div>
  <div class="note w">Trouwfoto heeft een lijst die nog besteld moet worden: overslaan kan, maar dan zonder bestelling via de app.</div>
  <div class="row" style="justify-content:flex-end"><span class="btn">Annuleren</span><span class="btn pri">Overslaan</span></div></div></div>`,
annuleren:()=>kop()+`<div class="g2" style="grid-template-columns:1.25fr 1fr">${kalender(6,{6:['Trouwfoto · Poster',0.1]})}${regels('gepland')}</div>${voet()}
 <div class="overlay"><div class="dlg" style="width:640px">
  <h2 style="font-size:18px">Verkoop 423 annuleren?</h2>
  <div><div class="lbl">Reden (verplicht)</div><span class="seg"><span class="on">Klant ziet af</span><span>Te duur</span><span>Per vergissing</span><span>Andere…</span></span></div>
  <div class="lbl" style="margin:4px 0 0">Wat er gebeurt</div>
  <div class="tl sm">
   <div><b>Werkbon</b><span>vervalt; de planning (di 10/11) wordt vrijgemaakt; momentopname in het archief</span></div>
   <div><b>Voorraad</b><span>gereserveerde stukken komen vrij (C2 30 × 42)</span></div>
   <div><b>Leverancier</b><span>open bestelling L-2045 bij Nielsen wordt geannuleerd</span></div>
   <div><b>Bestelbon</b><span>2026-24 wordt "Geannuleerd"; het nummer blijft voor de boekhouding</span></div>
   <div><b>Voorschot</b><span>€ 100,00 (Bancontact, 2/11) — kies hieronder</span></div></div>
  <div class="opt on"><span class="rad"></span><div><b>Voorschot terugbetalen</b><div class="sec sm">− € 100,00 in Overzicht betalingen.</div><div class="row" style="margin-top:6px"><span class="seg"><span class="on">Bancontact</span><span>Cash</span><span>Overschrijving</span></span></div></div></div>
  <div class="opt"><span class="rad"></span><div><b>Voorschot houden</b><div class="sec sm">Bv. als annuleringskost.</div></div></div>
  <div class="row" style="justify-content:flex-end"><span class="btn">Niet annuleren</span><span class="btn" style="background:#DC2626;border-color:#DC2626;color:#fff">Verkoop annuleren</span></div></div></div>`,
terugkijken:()=>kop().replace('<div class="d">2/11</div>','<div class="d" style="color:#1D4ED8;font-weight:600">terug gekeken</div>')+`
 <div class="note i">👁 Je bekijkt stap 1 Offerte (alleen-lezen). De klant heeft bevestigd op 2/11: de prijs staat vast. <span class="btn sm2" style="margin-left:8px">Terug naar stap 2 ▶</span></div>
 <div class="g2" style="opacity:.85"><div class="card"><h2 style="margin-bottom:10px">Inlijstingen</h2>
  <div class="inl"><div class="row"><span class="t">Trouwfoto</span><span class="sp"></span><b>€ 185,00</b></div><div class="sec sm">1 × 50 × 70 cm · lijst L-2045 eik naturel · ontspiegeld glas · passe-partout wit 6 cm</div></div>
  <div class="inl"><div class="row"><span class="t">Poster A3</span><span class="sp"></span><b>€ 60,00</b></div><div class="sec sm">1 × standaardkader C2 – Alu zwart 30 × 42 · gewoon glas</div></div>
  <div class="sum"><div class="tot"><span>Totaal incl. btw</span><span>€ 245,00</span></div></div></div>
  <div class="card"><h2 style="margin-bottom:8px">Iets wijzigen na het akkoord?</h2><div class="sec sm">Zoals nu: wijzigen kan, met "Prijzen herberekenen" (met bevestiging, US-100). De werkbon volgt de nieuwe inlijstingen.</div>
   <div class="row" style="margin-top:10px"><span class="btn">✎ Offerte aanpassen…</span></div></div></div>
 <div class="voet"><span class="sec">Terug gekeken · stap 1 van 5</span><span class="sp"></span><span class="btn pri">Terug naar stap 2 ▶</span></div>`
};
m.innerHTML=scr[S]();
