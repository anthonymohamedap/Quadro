const S=new URLSearchParams(location.search).get('s')||'leeg';
const m=document.getElementById('m');
const eur=v=>'€ '+v.toFixed(2).replace('.',',');
const kop=(o)=>`
 <div class="kop">
  <div><div class="sec sm">◀ Verkopen</div><h1>Verkoop ${o.nr} · ${o.klant}</h1>
   <div class="sec">${o.adres} · gestart ${o.start} · afhalen ${o.afhaal}</div></div>
  <span class="sp"></span>
  <div class="r"><div class="sec sm">Totaal (rekent mee)</div><div style="font-size:22px;font-weight:700">${eur(o.tot)}</div>${o.rest!=null?`<div class="pill" style="background:var(--orBg);color:var(--or)">nog te betalen ${eur(o.rest)}</div>`:''}</div>
  <span class="btn" ${o.menu?'style="border-color:var(--yellow);background:#FFFBEB"':''}>⋯</span>
 </div>
 <div class="row" style="flex-wrap:wrap"><span class="sec sm">In deze verkoop:</span>
  <span class="chip">Offerte ${o.nr}</span><span class="chip weg">Werkbon</span>
  <span class="chip ${o.bon?'':'weg'}">${o.bon?'Bestelbon voorlopig (V'+o.nr+')':'Bestelbon'}</span><span class="chip ${o.vs?'':'weg'}">${o.vs?'Voorschot '+eur(o.vs):'Voorschot'}</span></div>
 <div class="balk">${['Offerte','Werkbon','In productie','Klaar om af te halen','Afgehaald & betaald'].map((t,i)=>`<div class="st ${i==0?'h':''}"><div class="b">${i+1}</div><div class="t">${t}</div><div class="d">${i==0?'hier ben je':''}</div></div>`).join('')}</div>`;
const O={nr:423,klant:'Sofie Claes',adres:'Leuvensestraat 12, Aarschot · 0478 12 34 56',start:'2/11/2026',afhaal:'<b style="color:#222">vr 13/11</b> <span class="btn sm2" style="padding:1px 7px">wijzigen</span>',tot:245,rest:145,bon:true,vs:100};
const inl=(sel,fout)=>`
   <div class="inl ${sel?'sel':''}" ${fout?'style="border-color:#DC2626;background:#FEF2F2"':''}><div class="row"><span class="t">${fout?'Schilderij':'Trouwfoto'}</span><span class="sp"></span><b>${fout?'—':'€ 185,00'}</b></div><div class="sec sm">${fout?'<span style="color:#B91C1C">40 × 50 cm · nog geen lijst gekozen</span>':'1 × 50 × 70 cm · lijst L-2045 eik naturel · ontspiegeld glas · passe-partout wit 6 cm'}</div></div>`;
const inl2=`<div class="inl"><div class="row"><span class="t">Poster A3</span><span class="sp"></span><b>€ 60,00</b></div><div class="sec sm">1 × standaardkader C2 – Alu zwart 30 × 42 · gewoon glas</div></div>`;
const som=(vs)=>`<div class="sum" style="margin-top:12px"><div><span class="sec">Inlijstingen (incl.)</span><span>€ 245,00</span></div><div><span class="sec">Korting op totaal · Meerprijs</span><span class="sec">0 % · € 0,00 <u>wijzigen</u></span></div>
   <div class="tot"><span>Totaal incl. btw</span><span>€ 245,00</span></div><div class="sec sm" style="justify-content:flex-end">waarvan btw 21 %: € 42,52 · excl. € 202,48</div>${vs?`<div><span class="sec">Voorschot (Bancontact, 2/11)</span><span>− € 100,00</span></div><div><b>Nog te betalen</b><b>€ 145,00</b></div>`:''}</div>`;
const voet=(dis,why)=>`<div class="voet"><span class="sec" style="white-space:nowrap">Stap 1 van 5</span><span class="sp"></span>${why?`<span class="sm" style="color:#B91C1C;max-width:300px;text-align:right">${why}</span>`:''}<span class="btn">€ Voorschot ontvangen</span><span class="btn">🖨 Bestelbon afdrukken</span><span class="btn pri" ${dis?'style="opacity:.45"':''}>Klant akkoord: Bevestigen → Werkbon ▶</span></div>`;
const afw=(g,naam,waarde,leeg)=>`<div class="row" style="padding:2px 0;border-bottom:1px dashed var(--bd)"><span class="code">${g}</span><span style="width:120px" class="sec">${naam}</span><span class="inp" style="flex:1;padding:3px 10px;${leeg?'color:#9CA3AF':''}">${waarde}</span></div>`;
const editorMaat=`
  <div class="card"><div class="row" style="margin-bottom:12px"><h2>Trouwfoto</h2><span class="sp"></span><span class="btn sm2">⧉ Dupliceren</span><span class="btn sm2 dan">🗑</span></div>
   <div class="g4"><div style="grid-column:span 2"><div class="lbl">Titel (op de bestelbon)</div><div class="inp">Trouwfoto</div></div><div><div class="lbl">Aantal</div><div class="inp">1</div></div><div></div>
    <div><div class="lbl">Breedte</div><div class="inp">50 cm</div></div><div><div class="lbl">Hoogte</div><div class="inp">70 cm</div></div><div style="grid-column:span 2"><div class="lbl">Inleg (tweede kader)</div><div class="inp" style="color:#9CA3AF">— geen — ＋</div></div></div>
   <div style="margin:12px 0 6px"><div class="lbl">Soort</div><span class="seg"><span class="on">Lijst op maat</span><span>Standaardkader</span></span></div>
   <div class="lbl">Lijst</div><div class="inp" style="margin-bottom:10px">L-2045 · eik naturel · 4 cm · inkoop € 9,80/m · <span style="color:#B45309">voorraad 1,8 m — te bestellen in stap 2</span></div>
   <div class="lbl">Afwerkingen</div>
   ${afw('G','Glas','Ontspiegeld')}${afw('P','Passe-partout','Wit, 6 cm')}${afw('D','Diepte','— geen —',1)}${afw('O','Opkleven','— geen —',1)}${afw('R','Rug','MDF 3 mm')}
   <div class="g4" style="margin-top:12px"><div><div class="lbl">Extra werk</div><div class="inp">0 min</div></div><div><div class="lbl">Extra kost (excl.)</div><div class="inp">€ 0,00</div></div><div><div class="lbl">Berekend (incl.)</div><div class="inp" style="background:var(--muted)">€ 194,22 <span class="sec sm">ⓘ</span></div></div><div><div class="lbl">Afgesproken (incl.)</div><div class="inp" style="border-color:var(--yellow)">€ 185,00</div></div></div>
   <div class="row" style="margin-top:8px"><div style="width:160px"><div class="lbl">Korting op deze inlijsting</div><div class="inp" style="background:var(--muted);color:#9CA3AF">— %</div></div><div class="sec sm" style="flex:1">Afgesproken prijs ingevuld: die vervangt de berekening, de korting % telt dan niet. Alles wordt meteen bewaard.</div></div></div>`;
const scr={
nieuw:()=>`<div class="kop"><div><h1>Verkopen</h1></div></div><div class="card" style="height:600px;opacity:.5"></div>
 <div class="overlay"><div class="dlg" style="width:600px">
  <h2 style="font-size:18px">Nieuwe verkoop</h2>
  <div><div class="lbl">Klant</div><div class="inp" style="border-color:var(--yellow)">claes</div></div>
  <div class="lijst2">
   <div class="r2 on"><b>Sofie Claes</b><span class="sec sm">Leuvensestraat 12, Aarschot · 0478 12 34 56</span><span class="sec sm" style="margin-left:auto">3 eerdere verkopen</span></div>
   <div class="r2"><b>Jef Claes</b><span class="sec sm">Diestsesteenweg 4, Rillaar</span></div>
   <div class="r2"><b>Claes &amp; Co bv</b><span class="sec sm">BTW BE 0123.456.789 · Aarschot</span></div></div>
  <div class="row"><span class="btn sm2">＋ Nieuwe klant</span><span class="sec sm">Toonbankverkoop zonder klant? Gebruik <u>Winkelverkopen</u>.</span></div>
  <div class="g4" style="grid-template-columns:1fr 1fr"><div><div class="lbl">Afhaaldatum (mag later)</div><div class="inp" style="color:#9CA3AF">dd/mm/jjjj</div></div><div><div class="lbl">Aangenomen door</div><div class="inp">VK</div></div></div>
  <div class="note i">Verkoop krijgt meteen nummer <b>428</b> (nooit meer "offerte 0").</div>
  <div class="row" style="justify-content:flex-end"><span class="btn">Annuleren</span><span class="btn pri">Verkoop starten ▶</span></div></div></div>`,
leeg:()=>kop({...O,nr:428,klant:'Sofie Claes',tot:0,rest:null,bon:false,vs:0,start:'2/11/2026',afhaal:'<span style="color:#9CA3AF">nog niet gekozen</span> <span class="btn sm2" style="padding:1px 7px">kiezen</span>'})+`
 <div class="g2"><div class="card" style="display:grid;place-items:center;min-height:330px;text-align:center"><div><div style="font-size:40px">🖼</div><h2>Nog geen inlijstingen</h2><div class="sec" style="margin:6px 0 14px">Voeg toe wat de klant wil laten inlijsten.</div>
  <div class="row" style="justify-content:center"><span class="btn pri">＋ Inlijsting (lijst op maat)</span><span class="btn">＋ Standaardkader</span></div></div></div>
  <div class="card"><h2 style="margin-bottom:10px">Klant</h2><div class="sec sm">Sofie Claes · Leuvensestraat 12, 3200 Aarschot · 0478 12 34 56 · sofie.claes@…</div><div class="row" style="margin-top:8px"><span class="btn sm2">Klant wijzigen</span><span class="btn sm2">Gegevens aanpassen</span></div>
   <h2 style="margin:18px 0 8px">Eerdere verkopen</h2><div class="sec sm">411 · 03/2026 · Afgerond · € 96,00<br>396 · 11/2025 · Afgerond · € 312,50<br>360 · 05/2025 · Geannuleerd</div>
   <h2 style="margin:18px 0 8px">Opmerking</h2><div class="inp" style="color:#9CA3AF;height:56px">Notitie bij deze verkoop…</div></div></div>
 ${voet(true,'Voeg minstens één inlijsting toe')}`,
maat:()=>kop(O)+`<div class="g2"><div class="card"><div class="row" style="margin-bottom:10px"><h2>Inlijstingen</h2><span class="sp"></span><span class="btn sm2">＋ Inlijsting</span><span class="btn sm2">＋ Standaardkader</span></div>${inl(1)}${inl2}${som(1)}</div>${editorMaat}</div>${voet()}`,
lijstkiezen:()=>kop(O)+`<div class="g2"><div class="card"><div class="row" style="margin-bottom:10px"><h2>Inlijstingen</h2></div>${inl(1)}${inl2}${som(1)}</div>${editorMaat}</div>${voet()}
 <div class="overlay" style="background:rgba(17,24,39,.25)"><div class="dlg" style="width:640px;position:absolute;right:60px;top:330px">
  <div class="inp" style="border-color:var(--yellow)">🔍 eik 4</div>
  <div class="lijst2">${[['L-2045','eik naturel','4,0 cm','€ 9,80/m','1,8 m',1],['L-2046','eik gerookt','4,0 cm','€ 10,40/m','3,1 m'],['L-2310','eik wit gelakt','4,2 cm','€ 11,20/m','0 m — bestellen'],['L-1180','eik antiek','3,8 cm','€ 9,30/m','4,5 m']].map(r=>`<div class="r2 ${r[5]?'on':''}"><b style="width:70px">${r[0]}</b><span style="width:140px">${r[1]}</span><span class="sec sm" style="width:60px">${r[2]}</span><span style="width:90px">${r[3]}</span><span class="sm" style="margin-left:auto;color:${r[4].startsWith('0')?'#B91C1C':'var(--gr)'}">voorraad ${r[4]}</span></div>`).join('')}</div>
  <div class="sec sm">Inkoopprijs per meter. Zoekt op nummer, kleur, houtsoort en breedte. Te weinig voorraad: wordt bestelbaar in stap 2.</div></div></div>`,
kader:()=>kop(O)+`<div class="g2"><div class="card"><div class="row" style="margin-bottom:10px"><h2>Inlijstingen</h2></div>${inl(0)}<div class="inl sel"><div class="row"><span class="t">Poster A3</span><span class="sp"></span><b>€ 60,00</b></div><div class="sec sm">1 × standaardkader C2 – Alu zwart 30 × 42 · gewoon glas</div></div>${som(1)}</div>
  <div class="card"><h2 style="margin-bottom:12px">Poster A3</h2>
   <div class="g4"><div style="grid-column:span 3"><div class="lbl">Titel (op de bestelbon)</div><div class="inp">Poster A3</div></div><div><div class="lbl">Aantal</div><div class="inp">1</div></div></div>
   <div style="margin:12px 0 6px"><div class="lbl">Soort</div><span class="seg"><span>Lijst op maat</span><span class="on">Standaardkader</span></span></div>
   <div class="lbl">Standaardkader</div><div class="inp" style="border-color:var(--yellow)">🔍 c2 zwart</div>
   <div class="lijst2" style="margin:6px 0 12px">${[['C2','Alu zwart','30 × 42 (A3)','€ 49,59',1],['C2','Alu zwart','50 × 70','€ 52,07'],['C2','Alu zwart','40 × 50','€ 44,63']].map(r=>`<div class="r2 ${r[4]?'on':''}"><b style="width:40px">${r[0]}</b><span style="width:110px">${r[1]}</span><span style="width:120px">${r[2]}</span><span class="sec sm">excl. btw</span><span style="margin-left:auto">${r[3]}</span></div>`).join('')}</div>
   <div class="lbl">Afwerkingen (blijven mogelijk bij een standaardkader)</div>
   ${afw('G','Glas','Gewoon (zit in het kader)')}${afw('P','Passe-partout','— geen —',1)}${afw('R','Rug','zit in het kader')}
   <div class="g4" style="margin-top:12px"><div><div class="lbl">Berekend</div><div class="inp" style="background:var(--muted)">€ 60,00</div></div><div><div class="lbl">Afgesproken prijs</div><div class="inp" style="color:#9CA3AF">—</div></div><div><div class="lbl">Korting</div><div class="inp">0 %</div></div></div>
   <div class="sec sm" style="margin-top:8px">Geen afgesproken prijs: de berekende prijs telt. Maat komt van het kader. Op de bestelbon: "Standaardkader C2 – Alu zwart 30 × 42".</div></div></div>${voet()}`,
fouten:()=>kop({...O,tot:185,rest:85,afhaal:'<b style="color:#B45309">wo 28/10</b> <span class="btn sm2" style="padding:1px 7px">wijzigen</span>'})+`
 <div class="note w">⚠ De afhaaldatum (28/10) ligt in het verleden. Kies een nieuwe datum voor je bevestigt.</div>
 <div class="g2"><div class="card"><div class="row" style="margin-bottom:10px"><h2>Inlijstingen</h2></div>${inl(0)}${inl(1,1)}${som(1).replace('€ 202,48','€ 152,89').replace('€ 42,52','€ 32,11').replace('<span>€ 245,00</span>','<span>€ 185,00</span>').replace('<b>€ 145,00</b>','<b>€ 85,00</b>')}<div class="sec sm">"Schilderij" telt pas mee als er een lijst gekozen is.</div></div>
  <div class="card"><h2 style="margin-bottom:12px">Schilderij</h2>
   <div class="g4"><div><div class="lbl">Breedte werk</div><div class="inp">40 cm</div></div><div><div class="lbl">Hoogte werk</div><div class="inp">50 cm</div></div></div>
   <div style="margin:12px 0 6px"><div class="lbl">Soort</div><span class="seg"><span class="on">Lijst op maat</span><span>Standaardkader</span></span></div>
   <div class="lbl" style="color:#B91C1C">Lijst — verplicht</div><div class="inp" style="border-color:#DC2626;color:#9CA3AF">Kies een lijst…</div>
   <div class="sm" style="color:#B91C1C;margin-top:4px">Selecteer een lijst om de prijs te berekenen.</div></div></div>
 ${voet(true,'2 dingen te doen: lijst kiezen bij "Schilderij", afhaaldatum aanpassen')}`,
voorschot:()=>kop({...O,vs:0,rest:null})+`<div class="g2"><div class="card"><div class="row" style="margin-bottom:10px"><h2>Inlijstingen</h2></div>${inl(1)}${inl2}${som(0)}</div>${editorMaat}</div>${voet()}`+`<div class="overlay"><div class="dlg">
  <h2 style="font-size:18px">Voorschot ontvangen</h2><div class="sec sm">Verkoop 423 · totaal € 245,00 · al ontvangen € 0,00</div>
  <div><div class="lbl">Bedrag</div><div class="row"><div class="inp" style="border-color:var(--yellow);width:160px">€ 100,00</div><span class="btn sm2">25 % · € 61,25</span><span class="btn sm2">50 % · € 122,50</span><span class="btn sm2">Volledig</span></div></div>
  <div><div class="lbl">Betaalwijze</div><span class="seg"><span>Bancontact</span><span class="on">Cash</span><span>Credit card</span><span>Overschrijving</span></span></div>
  <div class="g4" style="grid-template-columns:1fr 1fr 1fr"><div><div class="lbl">Datum</div><div class="inp">02/11/2026</div></div><div><div class="lbl">Ontvangen (cash)</div><div class="inp">€ 100,00</div></div><div><div class="lbl">Terug te geven</div><div class="inp" style="background:var(--grBg);color:var(--gr)">€ 0,00</div></div></div>
  <div class="sec sm">Komt in Overzicht betalingen en op de bestelbon. Kan je later verwijderen zolang de verkoop niet afgerond is.</div>
  <div class="row" style="justify-content:flex-end"><span class="btn">Annuleren</span><span class="btn pri">Voorschot boeken</span></div></div></div>`,
afdrukken:()=>`<div class="pv">
  <div class="row" style="padding:12px 18px;background:#fff;border-bottom:1px solid var(--bd)"><h2>Bestelbon voorlopig (V423) — prijsvoorstel</h2><span class="sp"></span><span class="btn">Sluiten</span><span class="btn">⤓ Bewaren als PDF</span><span class="btn pri">🖨 Afdrukken…</span></div>
  <div style="display:grid;grid-template-columns:1fr 300px;gap:0;height:calc(100vh - 62px)">
   <div style="background:#6B7280;display:grid;place-items:start center;padding:24px;overflow:hidden"><div class="paper">
    <div class="row"><img src="../../../Assets/Quadro_logo2012_ZW1.jpg" style="width:90px"><span class="sp"></span><div style="text-align:right;font-size:11px">QUADRO inlijstatelier · Aarschot<br><b style="font-size:15px">BESTELBON</b><br>voorlopig · bij offerte 423<br>datum 02/11/2026</div></div>
    <div style="margin:18px 0 10px;font-size:11.5px">Sofie Claes<br>Leuvensestraat 12<br>3200 Aarschot</div>
    <table style="font-size:11px;white-space:normal" class="bon"><tr><th>Omschrijving</th><th>Aantal</th><th style="text-align:right">Bedrag</th></tr>
     <tr><td>Trouwfoto — 50 × 70 cm<br><span class="sec">lijst L-2045 eik naturel, ontspiegeld glas, pp wit 6 cm, rug MDF</span></td><td>1</td><td style="text-align:right">€ 185,00</td></tr>
     <tr><td>Poster A3 — standaardkader C2 Alu zwart 30 × 42<br><span class="sec">gewoon glas</span></td><td>1</td><td style="text-align:right">€ 60,00</td></tr></table>
    <div class="sum" style="font-size:11.5px;margin-top:10px;width:220px;margin-left:auto"><div><span>Totaal incl. btw</span><b>€ 245,00</b></div><div><span>Voorschot</span><span>− € 100,00</span></div><div><b>Nog te betalen</b><b>€ 145,00</b></div></div>
    <div style="font-size:10.5px;margin-top:18px;color:#555">Prijsvoorstel, geldig 30 dagen. Geen volgnummer zolang de klant niet bevestigd heeft.</div></div></div>
   <div style="background:#fff;padding:18px;border-left:1px solid var(--bd);display:flex;flex-direction:column;gap:10px"><b>Waar komt het bestand?</b><div class="sec sm">Documenten › QuadroApp › Bestelbonnen › <b style="color:#222">Bestelbon-V423.pdf</b></div>
    <div class="note i">Afdrukken… opent het printvenster van macOS/Windows: daar kies je printer en aantal exemplaren.</div>
    <div class="note w">Voorlopig: krijgt pas een volgnummer (2026-…) bij "Bevestigen → Werkbon". Telt niet mee voor de boekhouding.</div>
    <div class="sec sm">Na een wijziging aan de offerte wordt de voorlopige bon opnieuw opgebouwd bij het volgende afdrukken.</div></div></div></div>`,
bevestigen:()=>scr.maat()+`<div class="overlay"><div class="dlg" style="width:600px">
  <h2 style="font-size:18px">Klant gaat akkoord — bevestigen?</h2>
  <div class="sum"><div><span class="sec">Totaal</span><b>€ 245,00</b></div><div><span class="sec">Voorschot</span><span>€ 100,00 (Bancontact, 2/11)</span></div><div><span class="sec">Afhaaldatum</span><span>vr 13/11/2026</span></div></div>
  <div><b>Wat er gebeurt</b><div class="tl sm" style="margin-top:6px">
   <div><b>Werkbon</b>wordt aangemaakt voor het atelier (stap 2: inplannen en lijsten bestellen)</div>
   <div><b>Bestelbon</b><span>voorlopige bon V423 krijgt volgnummer <strong>2026-24</strong> en besteldatum vandaag</span></div>
   <div><b>Prijs</b>staat vanaf nu vast; niet meer automatisch herberekend (US-100)</div>
   <div><b>Voorraad</b>L-2045 3,2 m wordt gereserveerd</div>
   <div><b>Terug</b>naar "Offerte" kan daarna niet meer; annuleren wel</div></div></div>
  <div class="row" style="gap:8px"><span class="chk"><i class="v">✓</i></span><span class="sm">Bestelbon 2026-24 meteen afdrukken voor de klant</span></div>
  <div class="row" style="justify-content:flex-end"><span class="btn">Nog niet</span><span class="btn pri">Bevestigen → Werkbon ▶</span></div></div></div>`,
menu:()=>kop({...O,menu:1})+`<div class="g2"><div class="card">${inl(1)}${inl2}${som(1)}</div>${editorMaat}</div>${voet()}
 <div class="menu"><div>⧉ Dupliceren als nieuwe verkoop</div><div>👤 Andere klant kiezen</div><div style="border-top:1px solid var(--bd)">✕ Verkoop annuleren…<span class="sec sm">altijd mogelijk · reden + afhandeling voorschot en bon</span></div></div>`
,annuleren:()=>scr.maat()+`<div class="overlay"><div class="dlg" style="width:620px">
  <h2 style="font-size:18px">Verkoop 423 annuleren?</h2>
  <div><div class="lbl">Reden (verplicht)</div><span class="seg"><span class="on">Klant ziet af</span><span>Te duur</span><span>Per vergissing aangemaakt</span><span>Andere…</span></span></div>
  <div class="lbl" style="margin:4px 0 0">Wat er gebeurt</div>
  <div class="tl sm">
   <div><b>Offerte 423</b><span>wordt "Geannuleerd" en alleen-lezen; het nummer blijft (geen gat in de nummering)</span></div>
   <div><b>Bestelbon</b><span>de voorlopige bon V423 vervalt; hij had geen volgnummer, dus niets in de boekhouding</span></div>
   <div><b>Voorschot</b><span>€ 100,00 (Bancontact, 2/11) — kies hieronder</span></div></div>
  <div class="opt on"><span class="rad"></span><div><b>Voorschot terugbetalen</b><div class="sec sm">Er komt een terugbetaling − € 100,00 bij in Overzicht betalingen, gekoppeld aan deze verkoop.</div>
   <div class="row" style="margin-top:6px"><span class="seg"><span class="on">Bancontact</span><span>Cash</span><span>Overschrijving</span></span><span class="inp" style="padding:4px 10px">02/11/2026</span></div></div></div>
  <div class="opt"><span class="rad"></span><div><b>Voorschot houden</b><div class="sec sm">Bv. als annuleringskost. Blijft geboekt als ontvangst bij deze (geannuleerde) verkoop.</div></div></div>
  <div class="row" style="justify-content:flex-end;margin-top:4px"><span class="btn">Niet annuleren</span><span class="btn" style="background:#DC2626;border-color:#DC2626;color:#fff">Verkoop annuleren</span></div></div></div>`
,geannuleerd:()=>kop({...O,rest:null,afhaal:'<s>vr 13/11</s>'}).replace('Totaal (rekent mee)','Totaal')
  .replace('Verkoop 423 · Sofie Claes</h1>','Verkoop 423 · Sofie Claes <span class="pill" style="background:#F3F4F6;color:#374151;font-size:14px;vertical-align:middle">Geannuleerd</span></h1>')
  .replace('<span class="chip ">Bestelbon voorlopig (V423)</span>','<span class="chip weg">Bestelbon V423 (vervallen)</span>')
  .replace('<div class="balk">','<div class="balk" style="filter:grayscale(1);opacity:.55">').replace('hier ben je','geannuleerd')+`
 <div class="note" style="background:#F3F4F6;color:#374151;border:1px solid var(--bd)"><b>Geannuleerd op 2/11/2026 door VK</b> — reden: Klant ziet af. Voorschot € 100,00 terugbetaald (Bancontact, 2/11). Deze verkoop is alleen-lezen.</div>
 <div class="g2" style="opacity:.75"><div class="card"><h2 style="margin-bottom:10px">Inlijstingen</h2>${inl(0)}${inl2}${som(0)}</div>
  <div class="card"><h2 style="margin-bottom:8px">Betalingen</h2><div class="row sm"><span>2/11 · Voorschot · Bancontact</span><span class="sp"></span>€ 100,00</div><div class="row sm"><span>2/11 · Terugbetaling voorschot · Bancontact</span><span class="sp"></span>− € 100,00</div><div class="row sm" style="border-top:1px solid var(--bd);margin-top:6px;padding-top:6px"><b>Saldo</b><span class="sp"></span><b>€ 0,00</b></div></div></div>
 <div class="voet"><span class="sec">Geannuleerd</span><span class="sp"></span><span class="btn">⧉ Dupliceren als nieuwe verkoop</span><span class="btn">↺ Heropenen (beheerder)</span></div>`

,prijs:()=>kop({...O,tot:244.85,rest:144.85})+`<div class="g2"><div class="card"><div class="row" style="margin-bottom:10px"><h2>Inlijstingen</h2></div>${inl(1)}${inl2}
  <div class="sum" style="margin-top:12px">
   <div><span class="sec">Inlijstingen (incl.)</span><span>€ 245,00</span></div>
   <div style="align-items:center"><span>Korting op totaal</span><span class="row" style="gap:8px"><span class="inp" style="padding:3px 8px;width:70px;border-color:var(--yellow)">5 %</span><span style="width:80px;text-align:right">− € 12,25</span></span></div>
   <div style="align-items:center"><span>Meerprijs (incl.)</span><span class="row" style="gap:8px"><span class="inp" style="padding:3px 8px;width:70px;border-color:var(--yellow)">€ 12,10</span><span style="width:80px;text-align:right">+ € 12,10</span></span></div>
   <div class="sec sm" style="justify-content:flex-start">Meerprijs: bv. dringend, levering, ophangen. Staat niet als aparte regel op de bestelbon, wel in het totaal.</div>
   <div class="tot"><span>Totaal incl. btw</span><span>€ 244,85</span></div>
   <div class="sec sm" style="justify-content:flex-end">waarvan btw 21 %: € 42,49 · excl. € 202,36</div>
   <div><span class="sec">Voorschot (Bancontact, 2/11)</span><span>− € 100,00</span></div><div><b>Nog te betalen</b><b>€ 144,85</b></div></div></div>
  <div class="card"><div class="row" style="margin-bottom:10px"><h2>Prijsopbouw · Trouwfoto</h2><span class="sp"></span><span class="sec sm">bedragen incl. btw</span></div>
   <table class="po">
    <tr><td><b>Lijst L-2045</b><div class="sec sm">omtrek 2 × (50 + 70) + 10 × 4 cm = 2,80 m × € 9,80/m inkoop = € 27,44<br>× winstfactor 2,5 + afval 10 % + vaste kost € 3,00 + 15 min werk × € 48/u</div></td><td>€ 104,47</td></tr>
    <tr><td><b>G · Glas ontspiegeld</b><div class="sec sm">0,35 m² × € 48/m² × marge 2,0 + afval + € 2,00 + 5 min</div></td><td>€ 49,95</td></tr>
    <tr><td><b>P · Passe-partout wit 6 cm</b><div class="sec sm">0,35 m² × € 22/m² × marge 2,0 + afval + € 1,00 + 10 min</div></td><td>€ 30,46</td></tr>
    <tr><td><b>R · Rug MDF 3 mm</b><div class="sec sm">0,35 m² × € 8/m² × marge 1,8 + afval + 3 min</div></td><td>€ 9,34</td></tr>
    <tr><td class="sec">Inleg (tweede kader) · extra werk · extra kost</td><td class="sec">€ 0,00</td></tr>
    <tr class="st2"><td><b>Berekend</b></td><td><b>€ 194,22</b></td></tr>
    <tr><td>Korting op deze inlijsting</td><td class="sec">n.v.t.</td></tr>
    <tr class="st2" style="background:#FFFBEB"><td><b>Afgesproken prijs</b> <span class="sec sm">vervangt de berekening</span></td><td><b>€ 185,00</b></td></tr>
    <tr><td class="sec">× aantal 1</td><td><b>€ 185,00</b></td></tr>
   </table>
   <div class="sec sm" style="margin-top:8px">Uurloon, standaard winstfactor en afval komen uit Instellingen; prijs per meter, factor en werkminuten per lijst uit Lijsten; afwerkingen uit Afwerkingen.</div></div></div>${voet()}`

};
m.innerHTML=scr[S]();
