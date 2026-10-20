// SPDX-License-Identifier: MPL-2.0
namespace NexVerse.Server.Api
{
    /// <summary>
    /// Stadt NexVerse MapLibre GL JS view. All virtual Grid coordinates use a
    /// documented reversible synthetic Mercator mapping; not real geography.
    /// </summary>
    internal static class NexCitizenWorldMapPage
    {
        internal const string Html = """
<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Stadt NexVerse · MapLibre Weltkarte</title>
<link href="https://unpkg.com/maplibre-gl@6.15.0/dist/maplibre-gl.css" rel="stylesheet">
<style>
:root{font:14px/1.5 system-ui,-apple-system,"Segoe UI",Arial,sans-serif;color:#eaf2fa;background:#07111e;color-scheme:dark}
*{box-sizing:border-box}body{margin:0}button,input{font:inherit}
header{padding:14px 20px;background:#0c1728;border-bottom:1px solid #28405a;display:flex;justify-content:space-between;align-items:center;gap:15px}
header h1{font-size:1.15rem;margin:0}header small{color:#9db4c9}
main{height:calc(100dvh - 64px);min-height:420px;display:grid;grid-template-columns:290px minmax(0,1fr)}
aside{padding:16px;background:#0b1a2c;border-right:1px solid #28405a;overflow:auto}
form{display:flex;gap:6px}input{min-width:0;width:100%;padding:10px;background:#11243a;color:#fff;border:1px solid #35536f;border-radius:7px}
button{cursor:pointer;color:#fff;background:#205079;border:1px solid #4683a8;padding:9px 12px;border-radius:7px}
#results{display:grid;gap:7px;margin-top:13px}.result{text-align:left;width:100%;background:#122943}
#state{min-height:38px;margin:12px 0;color:#aed2ec}#details{display:none;margin-top:18px;padding:13px;background:#132c42;border:1px solid #38607e;border-radius:10px}
#details h2{margin:0 0 6px;font-size:1rem}#teleport{color:#80dcff}#notes{color:#8faac0;font-size:.86rem;margin-top:16px}
#map{width:100%;height:100%;background:#10263c}#coords{position:absolute;right:14px;bottom:17px;background:#09172ee8;padding:6px 9px;border-radius:6px;z-index:2;pointer-events:none}
.maplibregl-ctrl-group{background:#11253d;color:white}.maplibregl-ctrl-group button{background:#152b45}
@media(max-width:700px){main{grid-template-columns:1fr;grid-template-rows:200px minmax(450px,1fr);height:auto}aside{padding:11px}#notes{display:none}header small{display:none}#map{min-height:65vh}}
</style>
</head>
<body>
<header><h1>Stadt NexVerse · Interaktive Weltkarte</h1><small>MapLibre · Originale Warp3D-Regionen · Suche · Teleport</small></header>
<main>
<aside>
<form id="searchForm"><input id="search" maxlength="80" placeholder="Region suchen …" aria-label="Region suchen"><button type="submit">Suchen</button></form>
<div id="state" role="status" aria-live="polite">Karte wird geladen …</div>
<div id="results"></div>
<div id="details"><h2 id="name"></h2><p id="desc"></p><a id="teleport" target="_blank" rel="noopener noreferrer">In diese Region teleportieren ↗</a></div>
<p id="notes">Eigene Kartendaten von OpenGenesisLINK. Rasterkoordinaten sind virtuell und keine realen geographischen Standorte. Kartenbilder werden ausschließlich vom eigenen ROBUST-MapImageService geliefert.</p>
</aside>
<div style="position:relative;min-height:420px"><div id="map"></div><div id="coords">–</div></div>
</main>
<script type="module">
import * as maplibregl from 'https://unpkg.com/maplibre-gl@6.15.0/dist/maplibre-gl.mjs';
const $=id=>document.getElementById(id);
const apiRoot=location.pathname.toLowerCase().startsWith('/api/v1/deutschland')
  ?'/api/v1/deutschland':'/api/v1/world-map';
const AXIS=2**24,SHIFT=2**22,MAX=8388606;
const toCoords=(x,y)=>{
  const mx=(x+SHIFT+.5)/AXIS,my=1-(y+SHIFT+.5)/AXIS;
  return [mx*360-180,Math.atan(Math.sinh(Math.PI*(1-2*my)))*180/Math.PI];
};
const toGrid=(lng,lat)=>{
  const v=Math.log(Math.tan(Math.PI/4+Math.max(-85,Math.min(85,lat))*Math.PI/360));
  return {x:Math.round((lng+180)/360*AXIS-SHIFT),
    y:Math.round((1-(1-v/Math.PI)/2)*AXIS-SHIFT)};
};
let map,revision=0,selected=null;
function state(v){$('state').textContent=v;}
function select(r){
  selected=r;
  $('details').style.display='block';
  $('name').textContent=r.name;
  $('desc').textContent='Grid '+r.grid_x+' / '+r.grid_y+' · '+r.size_x+' × '+r.size_y+' Meter';
  const a=$('teleport');
  if(r.teleport_uri){a.href=r.teleport_uri;a.style.display='inline';}
  else{a.removeAttribute('href');a.style.display='none';}
}
function geo(data){
  const features=(data.regions||[]).map(r=>{
    const x=r.grid_x,y=r.grid_y,ex=x+r.cells_x,ey=y+r.cells_y;
    const corners=[[x,y],[ex,y],[ex,ey],[x,ey],[x,y]]
      .map(p=>toCoords(p[0]-.5,p[1]-.5));
    return {type:'Feature',properties:{name:r.name,region_id:r.region_id,
      grid_x:x,grid_y:y,description:r.size_x+' × '+r.size_y+' m',teleport_uri:r.teleport_uri||''},
      geometry:{type:'Polygon',coordinates:[corners]}};
  });
  map.getSource('regions').setData({type:'FeatureCollection',features});
}
async function viewport(){
  if(!map)return;
  const id=++revision,center=toGrid(...map.getCenter().toArray());
  const bounds=map.getBounds(),sw=toGrid(bounds.getWest(),bounds.getSouth()),
    ne=toGrid(bounds.getEast(),bounds.getNorth());
  // Public endpoint is deliberately limited to a bounded 16x16 viewport.
  const axis=16,minX=Math.max(0,Math.min(MAX-axis,Math.floor((sw.x+ne.x)/2)-axis/2)),
    minY=Math.max(0,Math.min(MAX-axis,Math.floor((sw.y+ne.y)/2)-axis/2));
  const qs=new URLSearchParams({min_x:minX,max_x:minX+axis-1,
    min_y:minY,max_y:minY+axis-1});
  state('Lade Regionen im Kartenausschnitt …');
  try{
    const response=await fetch('/api/v1/deutschland?'+qs,{cache:'no-store'});
    if(!response.ok)throw Error('HTTP '+response.status);
    const data=await response.json();if(id!==revision)return;
    geo(data);state((data.regions||[]).length+' Regionen im 16×16-Rasterausschnitt.');
    $('coords').textContent='Grid '+center.x+' / '+center.y+' · Zoom '+map.getZoom().toFixed(1);
  }catch(e){if(id===revision)state('Kartenabfrage fehlgeschlagen: '+e.message);}
}
async function search(e){
  e.preventDefault();const q=$('search').value.trim();if(q.length<2){state('Mindestens zwei Zeichen eingeben.');return;}
  $('results').replaceChildren();state('Suche läuft …');
  try{
    const r=await fetch(apiRoot+'?q='+encodeURIComponent(q),{cache:'no-store'});
    if(!r.ok)throw Error('HTTP '+r.status);
    const data=await r.json();state((data.regions||[]).length+' Regionen gefunden.');
    for(const reg of data.regions||[]){
      const b=document.createElement('button');b.className='result';b.type='button';
      b.textContent=reg.name+' · '+reg.grid_x+'/'+reg.grid_y;
      b.onclick=()=>{select(reg);map.flyTo({center:toCoords(reg.grid_x,reg.grid_y),zoom:22});};
      $('results').append(b);
    }
  }catch(e){state('Suche fehlgeschlagen: '+e.message);}
}
$('searchForm').addEventListener('submit',search);
const base={version:8,sources:{
  warp:{type:'raster',tiles:['/api/v1/deutschland/xyz/{z}/{x}/{y}.jpg'],
    tileSize:256,minzoom:17,maxzoom:24,attribution:'© Stadt NexVerse · OpenGenesisLINK'},
  regions:{type:'geojson',data:{type:'FeatureCollection',features:[]}}
},layers:[
  {id:'water',type:'background',paint:{'background-color':'#123247'}},
  {id:'warp3d',type:'raster',source:'warp',paint:{'raster-opacity':1}},
  {id:'region-fill',type:'fill',source:'regions',paint:{'fill-color':'#19b1e4','fill-opacity':.07}},
  {id:'region-outline',type:'line',source:'regions',paint:{'line-color':'#8bdffd','line-width':2}},
  {id:'region-label',type:'symbol',source:'regions',layout:{'text-field':['get','name'],
    'text-size':12,'text-allow-overlap':false},paint:{'text-color':'#fff','text-halo-color':'#09182a','text-halo-width':2}}
]};
async function init(){
  let cx=1000,cy=1010;
  try{
    const r=await fetch(apiRoot,{cache:'no-store'});
    if(r.ok){const data=await r.json();if(data.bounds){cx=Math.floor((data.bounds.min_x+data.bounds.max_x)/2);
      cy=Math.floor((data.bounds.min_y+data.bounds.max_y)/2);}}
  }catch{}
  map=new maplibregl.Map({container:'map',style:base,center:toCoords(cx,cy),
    zoom:22,minZoom:17,maxZoom:24,renderWorldCopies:false,
    dragRotate:false,pitchWithRotate:false,attributionControl:true});
  map.addControl(new maplibregl.NavigationControl({showCompass:false}),'top-right');
  map.on('load',viewport);map.on('moveend',viewport);
  map.on('mousemove',e=>{const g=toGrid(e.lngLat.lng,e.lngLat.lat);
    $('coords').textContent='Grid '+g.x+' / '+g.y+' · Zoom '+map.getZoom().toFixed(1);});
  map.on('click','region-fill',e=>{
    const p=e.features?.[0]?.properties;if(p)select({...p,name:p.name,
      size_x:p.description,size_y:'',grid_x:p.grid_x,grid_y:p.grid_y});
  });
}
init().catch(e=>state('MapLibre konnte nicht gestartet werden: '+e.message));
</script></body></html>
""";
    }
}
