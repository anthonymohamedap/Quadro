// Mock stap 1 Offerte — alle velden en knoppen van het huidige offertescherm (OfferteView), anders geordend.
const S=new URLSearchParams(location.search).get('s')||'maat';
const m=document.getElementById('m');
const eur=v=>'€ '+v.toFixed(2).replace('.',',');
const kop=(o)=>`
 <div class="kop">
  <div><div class="sec sm">◀ Verkopen</div><h1>Verkoop ${o.nr} · ${o.klant}${o.tag||''}</h1>
   <div class="sec">${o.adres} · afhalen ${o.afhaal} · <span class="pill" style="background:#F3F4F6;color:#374151">${o.status||'Concept'}</span> <span class="sec sm">${o.bewaard===false?'':'· bewaard ✓'}</span></div></div>
  <span class="sp"></span>
  <div class="r"><div class="sec sm">Totaal incl. BTW</div><div style="font-size:22px;font-weight:700">${eur(o.tot)}</div>${o.rest!=null?`<div class="pill" style="background:var(--orBg);color:var(--or)">rest te betalen ${eur(o.rest)}</div>`:''}</div>
  <span class="btn" ${o.menu?'style="border-color:var(--yellow);background:#FFFBEB"':''}>⋯</span>
 </div>
 <div class="row" style="flex-wrap:wrap"><span class="sec sm">In deze verkoop:</span>
  <span class="chip">Offerte ${o.nr}</span><span class="chip weg">Werkbon</span>
  <span class="chip ${o.bon?'':'weg'}">${o.bon?'Bestelbon voorlopig (V'+o.nr+')':'Bestelbon'}</span><span class="chip ${o.vs?'':'weg'}">${o.vs?'Voorschot '+eur(o.vs):'Voorschot'}</span></div>
 <div class="balk" ${o.grijs?'style="filter:grayscale(1);opacity:.55"':''}>${['Offerte','Werkbon','In productie','Klaar om af te halen','Afgehaald & betaald'].map((t,i)=>`<div class="st ${i==0?'h':''}"><div class="b">${i+1}</div><div class="t">${t}</div><div class="d">${i==0?(o.grijs?'geannuleerd':'hier ben je'):''}</div></div>`).join('')}</div>`;
const O={nr:423,klant:'Sofie Claes',adres:'Leuvensestraat 12, Aarschot · 0478 12 34 56',afhaal:'<span style="color:#9CA3AF">— (via planning)</span>',tot:245,rest:145,bon:true,vs:100};

// ── Linkerkolom: klant, inlijstingen, prijsaanpassingen, voorschot, totalen ──
const klant=`<div class="card"><div class="row"><h2>Klant</h2><span class="sp"></span><span class="btn sm2">＋ Nieuwe klant</span></div>
  <div class="row" style="margin-top:8px"><div style="flex:1"><b>Sofie Claes</b><div class="sec sm">0478 12 34 56 · sofie.claes@… · Aarschot</div></div><div class="inp" style="width:200px;color:#9CA3AF">🔍 Zoek klant…</div></div></div>`;
const regelItem=(t,sub,lijst,bedrag,sel,fout)=>`<div class="inl ${sel?'sel':''}" ${fout?'style="border-color:#DC2626;background:#FEF2F2"':''}><div class="row"><b>${sub}</b><span class="sp"></span><b>${bedrag}</b></div><div class="sec sm">${t}</div><div class="sm" ${fout?'style="color:#B91C1C"':'class="sec"'}>${lijst}</div></div>`;
const regels=(sel=1,fout)=>`<div class="card"><div class="row" style="margin-bottom:8px"><h2>Inlijstingen</h2><span class="sp"></span><span class="btn sm2">＋ Toevoegen</span><span class="btn sm2">Dupliceren</span><span class="btn sm2">– Verwijderen</span></div>
  ${regelItem('Trouwfoto','50 × 70 cm','Lijst: L-2045','€ 185,00',sel==1)}
  ${fout?regelItem('Schilderij','40 × 50 cm','nog geen lijst of standaardkader','—',sel==3,1):regelItem('Poster A3','30 × 42 cm','Standaardkader C2 – Alu zwart 30 × 42','€ 60,00',sel==2)}</div>`;
const prijsaanp=(vs,km)=>`<div class="card"><h2 style="margin-bottom:8px">Prijsaanpassingen en voorschot</h2>
  <div class="g4" style="grid-template-columns:1fr 1fr"><div><div class="lbl">Korting op totaal (%)</div><div class="inp">${km?'5':'0'}</div></div><div><div class="lbl">Meerprijs (incl. BTW)</div><div class="inp">${km?'€ 12,10':'€ 0,00'}</div></div></div>
  <div class="lbl" style="margin-top:10px">Voorschot ontvangen</div>
  <div class="row"><div class="inp" style="width:110px">€ 0,00</div><div class="inp" style="flex:1">Bancontact ▾</div><div class="inp" style="width:120px">02/11/2026</div><span class="btn sm2">Registreren</span></div>
  ${vs?`<div class="row sm" style="margin-top:6px;padding:4px 8px;background:var(--muted);border-radius:6px"><span>02/11/2026</span><span class="sec">Bancontact</span><span class="sp"></span><b>€ 100,00</b><span class="btn sm2" style="padding:0 6px">✕</span></div>`:''}</div>`;
const totalen=(vs,km)=>`<div class="card sum"><div><span class="sec">Subtotaal</span><span>${km?'€ 202,36':'€ 202,48'}</span></div><div><span class="sec">BTW</span><span>${km?'€ 42,49':'€ 42,52'}</span></div>
  <div class="tot"><span>Totaal incl. BTW</span><span>${km?'€ 244,85':'€ 245,00'}</span></div>${vs?`<div><span class="sec">Voorschot betaald</span><span>− € 100,00</span></div><div><b>Rest te betalen</b><b>${km?'€ 144,85':'€ 145,00'}</b></div>`:''}</div>`;
const links=(o={})=>`<div class="col">${klant}${regels(o.sel??1,o.fout)}${prijsaanp(o.vs??1,o.km)}${o.fout?totalen(1).replace('€ 202,48','€ 152,89').replace('€ 42,52','€ 32,11').replace('€ 245,00','€ 185,00').replace('€ 145,00','€ 85,00'):totalen(o.vs??1,o.km)}</div>`;

// ── Rechterkolom: regel details (alle velden van vandaag) ──
const sect=(t,inh)=>`<div class="sect"><div class="st2h">${t}</div>${inh}</div>`;
const veld=(l,v,st='')=>`<div><div class="lbl">${l}</div><div class="inp" style="${st}">${v}</div></div>`;
const zoekKeuze=(lbl,waarde,leegTekst,extra='')=>`<div><div class="lbl">${lbl}</div><div class="row" style="gap:6px"><div class="inp" style="width:170px;color:#9CA3AF;white-space:nowrap">Zoek artikelnummer…</div><div class="inp" style="flex:1;${waarde?'':'color:#9CA3AF'}">${waarde||leegTekst} ▾</div>${waarde?'<span class="btn sm2" style="padding:2px 7px">✕</span>':''}${extra}</div></div>`;
const afwRij=(g,naam,type,variant)=>`<div class="row" style="gap:6px;padding:2px 0"><span class="code">${g}</span><span class="sec sm" style="width:110px;white-space:nowrap">${naam}</span><div class="inp" style="flex:1;padding:3px 8px;${type?'':'color:#9CA3AF'}">${type||naam+' type…'} ▾</div><div class="inp" style="width:130px;padding:3px 8px;${variant?'':'color:#9CA3AF'}">${variant||'Variant…'} ▾</div>${type?'<span class="btn sm2" style="padding:1px 6px">✕</span>':'<span style="width:26px"></span>'}</div>`;
const detailsMaat=(o={})=>`<div class="card"><div class="row" style="margin-bottom:6px"><h2>Regel details · ${o.titel||'Trouwfoto'}</h2></div>
 ${sect('Algemeen',`<div class="g4" style="grid-template-columns:2fr 1fr 1.3fr">${veld('Titel (verschijnt op bon)',o.titel||'Trouwfoto')}${veld('Aantal stuks','1')}${veld('Afhaal datum','— nog niet gepland <span class="sec sm">(via planning)</span>','background:var(--muted)')}</div>`)}
 ${sect('Lijst',zoekKeuze('Type lijst',o.fout||o.kader?'':'L-2045 · eik naturel 4 cm','Kies een lijst…','<span class="btn sm2" title="Lijst bewerken">✏</span><span class="btn sm2" title="Lijstbeheer">📋</span>')+(o.fout?'<div class="sm" style="color:#B91C1C;margin-top:3px">Selecteer een TypeLijst.</div>':''))}
 ${sect('Standaardkader',`<div class="row" style="gap:6px"><div class="inp" style="width:170px;${o.kader?'border-color:var(--yellow)':'color:#9CA3AF'}">${o.kader?'🔍 c2 zwart':'Zoek…'}</div><div class="inp" style="flex:1;${o.kader?'':'color:#9CA3AF'}">${o.kader?'C2 – Alu zwart 30 × 42':'Geen standaardkader (op-maat lijst hierboven gebruiken)'} ▾</div>${o.kader?'<span class="btn sm2" style="padding:2px 7px">✕</span>':''}</div>`)}
 ${sect('Afmetingen en inleg',`<div class="g4" style="grid-template-columns:1fr 1fr 1fr 1fr">${veld('Breedte (cm)',o.kader?'30':o.fout?'40':'50')}${veld('Hoogte (cm)',o.kader?'42':o.fout?'50':'70')}${o.kader?'<div class="sec sm" style="grid-column:span 2;align-self:end">Inleg niet van toepassing bij een standaardkader</div>':veld('Inleg B (cm)','62')+veld('Inleg H (cm)','82')}</div>
   ${o.kader?'':`<div style="margin-top:6px">${zoekKeuze('Inleg nr.','','Geen inleg-nummer')}</div>`}`)}
 ${sect('Afwerkingen <span class="sec sm" style="font-weight:400">Glas – Passe-partout – Diepte – Opkleven – Rug</span>',
   afwRij('G','Glas',o.kader?'Gewoon':'Ontspiegeld',o.kader?'':'2 mm')+afwRij('P','Passe-partout 1',o.kader?'':'Passe-partout','Wit 6 cm'.slice(0,o.kader?0:9))+afwRij('P','Passe-partout 2','','')+afwRij('D','Diepte','','')+afwRij('O','Opkleven','','')+afwRij('R','Rug',o.kader?'':'MDF','3 mm'.slice(0,o.kader?0:4))
   +`<div class="row" style="gap:6px;margin-top:6px"><span class="sec sm">Afwerking (G P P D O R)</span><div class="inp" style="width:110px;padding:3px 8px">${o.kader?'G00000':'G2P100R1'}</div><span class="btn sm2">Toepassen</span><span class="btn sm2">Genereren</span></div>`)}
 ${sect('Prijs',`<div class="g4" style="grid-template-columns:1fr 1fr 1fr">${veld('Afgesproken prijs (incl. BTW)',o.kader?'—':o.fout?'—':'€ 185,00',o.kader||o.fout?'color:#9CA3AF':'border-color:var(--yellow)')}${veld('Korting op deze inlijsting (%)','0')}${veld('Prijs deze inlijsting (incl. BTW)',o.kader?'€ 60,00':o.fout?'—':'€ 185,00','background:var(--muted);font-weight:700')}</div>`)}
 ${sect('Notities',veld('Opmerking',o.kader?'':'Foto heeft een witte rand: passe-partout 2 mm over de foto laten vallen.',o.kader?'height:34px':''))}
 </div>`;

const voet=(dis,why)=>`<div class="voet"><span class="sec" style="white-space:nowrap">Stap 1 van 5</span><span class="sp"></span>${why?`<span class="sm" style="color:#B91C1C;max-width:300px;text-align:right">${why}</span>`:''}<span class="btn">🖨 Bestelbon (voorlopig)</span><span class="btn pri" ${dis?'style="opacity:.45"':''}>Bevestigen → Werkbon ▶</span></div>`;
const scherm=(o={},d={})=>kop({...O,...o})+`<div class="g2" style="grid-template-columns:1fr 1.25fr;align-items:start">${links(d)}${detailsMaat(d)}</div>`;

const scr={
nieuw:()=>`<div class="kop"><div><h1>Verkopen</h1></div></div><div class="card" style="height:600px;opacity:.5"></div>
 <div class="overlay"><div class="dlg" style="width:560px">
  <h2 style="font-size:18px">Nieuwe verkoop</h2>
  <div><div class="lbl">Klant</div><div class="inp" style="border-color:var(--yellow)">claes</div></div>
  <div class="lijst2">
   <div class="r2 on"><b>Sofie Claes</b><span class="sec sm">0478 12 34 56 · Aarschot</span></div>
   <div class="r2"><b>Jef Claes</b><span class="sec sm">Rillaar</span></div></div>
  <div class="row"><span class="btn sm2">＋ Nieuwe klant</span></div>
  <div class="note i">De verkoop krijgt meteen nummer <b>428</b> (geen "offerte 0" meer, US-81).</div>
  <div class="row" style="justify-content:flex-end"><span class="btn">Annuleren</span><span class="btn pri">Verkoop starten ▶</span></div></div></div>`,
leeg:()=>kop({...O,nr:428,tot:0,rest:null,bon:false,vs:0})+`<div class="g2" style="grid-template-columns:1fr 1.25fr;align-items:start"><div class="col">${klant}
  <div class="card" style="text-align:center;padding:30px"><h2>Nog geen inlijstingen</h2><div class="sec" style="margin:6px 0 12px">Voeg toe wat de klant wil laten inlijsten.</div><span class="btn pri">＋ Toevoegen</span></div>${prijsaanp(0)}${totalen(0).replace('€ 202,48','€ 0,00').replace('€ 42,52','€ 0,00').replace('€ 245,00','€ 0,00')}</div>
  <div class="card" style="color:#9CA3AF;display:grid;place-items:center;min-height:300px">Kies of voeg een inlijsting toe om de details te zien.</div></div>${voet(true,'Voeg minstens één regel toe.')}`,
maat:()=>scherm({},{})+voet(),
kader:()=>scherm({},{sel:2,kader:1,titel:'Poster A3'})+voet(),
lijstkiezen:()=>scherm({},{})+voet()+`<div class="overlay" style="background:rgba(17,24,39,.25)"><div class="dlg" style="width:560px;position:absolute;right:70px;top:430px">
  <div class="inp" style="border-color:var(--yellow)">L-20</div>
  <div class="lijst2">${['L-2045','L-2046','L-2050','L-2060'].map((a,i)=>`<div class="r2 ${i==0?'on':''}"><b>${a}</b></div>`).join('')}</div>
  <div class="sec sm">Zoeken op artikelnummer, zoals nu.</div></div></div>`,
fouten:()=>kop({...O,tot:185,rest:85,afhaal:'<b style="color:#B45309">wo 28/10</b>'})+`<div class="note w">⚠ De afhaaldatum (28/10) ligt in het verleden.</div>
 <div class="g2" style="grid-template-columns:1fr 1.25fr;align-items:start">${links({sel:3,fout:1})}${detailsMaat({fout:1,titel:'Schilderij'})}</div>${voet(true,'Regel 2: Selecteer een TypeLijst.')}`,
voorschot:()=>scherm({vs:0,rest:null},{vs:0})+voet()+`<div class="hint" style="left:262px;top:600px">← voorschot: bedrag, betaalwijze, datum, Registreren (zoals nu)</div>`,
afdrukken:()=>`<div class="pv">
  <div class="row" style="padding:12px 18px;background:#fff;border-bottom:1px solid var(--bd)"><h2>Bestelbon voorlopig (V423)</h2><span class="sp"></span><span class="btn">Sluiten</span><span class="btn">⤓ Bewaren als PDF</span><span class="btn pri">🖨 Afdrukken…</span></div>
  <div style="display:grid;grid-template-columns:1fr 300px;height:calc(100vh - 62px)">
   <div style="background:#6B7280;display:grid;place-items:start center;padding:24px;overflow:hidden"><div class="paper">
    <div class="row"><img src="../../../Assets/Quadro_logo2012_ZW1.jpg" style="width:90px"><span class="sp"></span><div style="text-align:right;font-size:11px">QUADRO inlijstatelier · Aarschot<br><b style="font-size:15px">BESTELBON</b><br>voorlopig · bij offerte 423<br>datum 02/11/2026</div></div>
    <div style="margin:18px 0 10px;font-size:11.5px">Sofie Claes<br>Leuvensestraat 12<br>3200 Aarschot</div>
    <table style="font-size:11px" class="bon"><tr><th>Omschrijving</th><th>Aantal</th><th style="text-align:right">Bedrag</th></tr>
     <tr><td>Trouwfoto — 50 × 70 cm<br><span class="sec">lijst L-2045, glas ontspiegeld, pp wit 6 cm, rug MDF</span></td><td>1</td><td style="text-align:right">€ 185,00</td></tr>
     <tr><td>Poster A3 — standaardkader C2 Alu zwart 30 × 42</td><td>1</td><td style="text-align:right">€ 60,00</td></tr></table>
    <div class="sum" style="font-size:11.5px;margin-top:10px;width:220px;margin-left:auto"><div><span>Totaal incl. btw</span><b>€ 245,00</b></div><div><span>Voorschot</span><span>− € 100,00</span></div><div><b>Nog te betalen</b><b>€ 145,00</b></div></div></div></div>
   <div style="background:#fff;padding:18px;border-left:1px solid var(--bd);display:flex;flex-direction:column;gap:10px"><b>Zoals nu (US-82)</b><div class="sec sm">Documenten › QuadroApp › Bestelbonnen › Bestelbon-V423.pdf</div>
    <div class="note w">Voorlopig: geen volgnummer tot "Bevestigen → Werkbon".</div></div></div></div>`,
bevestigen:()=>scherm()+voet()+`<div class="overlay"><div class="dlg" style="width:580px">
  <h2 style="font-size:18px">Bevestigen → Werkbon?</h2>
  <div class="tl sm">
   <div><b>Werkbon</b><span>wordt aangemaakt (stap 2: inplannen)</span></div>
   <div><b>Bestelbon</b><span>voorlopige bon V423 krijgt volgnummer <strong>2026-24</strong></span></div>
   <div><b>Prijs</b><span>staat vast (US-100); "Prijzen herberekenen" blijft mogelijk</span></div>
   <div><b>Voorraad</b><span>lijsten worden gereserveerd</span></div></div>
  <div class="row" style="justify-content:flex-end"><span class="btn">Nog niet</span><span class="btn pri">Bevestigen → Werkbon ▶</span></div></div></div>`,
menu:()=>scherm({menu:1})+voet()+`<div class="menu"><div>→ Markeer als verzonden<span class="sec sm">zoals nu (Concept → Verzonden)</span></div><div>↩ Terug naar concept<span class="sec sm">zoals nu, zolang er geen werkbon is</span></div><div style="border-top:1px solid var(--bd)">✕ Verkoop annuleren…<span class="sec sm">reden + voorschot terugbetalen of houden</span></div></div>`,
annuleren:()=>scherm()+voet()+`<div class="overlay"><div class="dlg" style="width:600px">
  <h2 style="font-size:18px">Verkoop 423 annuleren?</h2>
  <div><div class="lbl">Reden (verplicht)</div><div class="inp">Klant ziet af</div></div>
  <div class="tl sm"><div><b>Offerte</b><span>wordt "Geannuleerd" (zoals nu); nummer blijft</span></div><div><b>Bestelbon</b><span>voorlopige bon V423 vervalt (geen volgnummer)</span></div><div><b>Voorschot</b><span>€ 100,00 — kies hieronder</span></div></div>
  <div class="opt on"><span class="rad"></span><div><b>Voorschot terugbetalen</b><div class="sec sm">− € 100,00 in Overzicht betalingen.</div></div></div>
  <div class="opt"><span class="rad"></span><div><b>Voorschot houden</b></div></div>
  <div class="row" style="justify-content:flex-end"><span class="btn">Niet annuleren</span><span class="btn" style="background:#DC2626;border-color:#DC2626;color:#fff">Verkoop annuleren</span></div></div></div>`,
geannuleerd:()=>kop({...O,rest:null,grijs:1,status:'Geannuleerd'})+`<div class="note" style="background:#F3F4F6;color:#374151;border:1px solid var(--bd)"><b>Geannuleerd op 2/11/2026</b> — reden: Klant ziet af. Voorschot € 100,00 terugbetaald. Alleen-lezen.</div>
 <div class="g2" style="grid-template-columns:1fr 1.25fr;align-items:start;opacity:.7">${links()}${detailsMaat()}</div>`
};
m.innerHTML=scr[S]();
