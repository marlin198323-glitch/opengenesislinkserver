// SPDX-License-Identifier: MPL-2.0

using System;
using System.Net;
using System.Text;
using OpenSim.Framework.Servers.HttpServer;

namespace NexVerse.Server.Api
{
    internal sealed class NexApiDocsPage
    {
        public void Handle(
            IOSHttpRequest request,
            IOSHttpResponse response)
        {
            response.KeepAlive = false;

            if (request == null ||
                !string.Equals(
                    request.HttpMethod,
                    "GET",
                    StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode =
                    (int)HttpStatusCode.MethodNotAllowed;
                response.ContentType =
                    "text/plain; charset=utf-8";
                response.RawBuffer =
                    Encoding.UTF8.GetBytes(
                        "GET ist erforderlich.\n");
                return;
            }

            response.StatusCode =
                (int)HttpStatusCode.OK;
            response.ContentType =
                "text/html; charset=utf-8";
            response.AddHeader(
                "Cache-Control",
                "no-store");
            response.AddHeader(
                "X-Content-Type-Options",
                "nosniff");
            response.AddHeader(
                "Referrer-Policy",
                "no-referrer");
            response.AddHeader(
                "Content-Security-Policy",
                "default-src 'self'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; connect-src 'self'");
            response.RawBuffer =
                Encoding.UTF8.GetBytes(s_Html);
        }

        private const string s_Html = """
<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>OpenGenesisLINK – World API Control Center</title>
<style>
:root{color-scheme:dark;--bg:#071019;--panel:#0f1a26;--panel2:#142335;--line:#24384d;--text:#e9f1f8;--muted:#8fa5b8;--accent:#66c7ff;--accent2:#8be0c2;--good:#62d49b;--warn:#ffd166;--danger:#ff7d7d;--shadow:0 18px 55px rgba(0,0,0,.28)}
*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:radial-gradient(circle at 85% 0,#112b3d 0,transparent 34%),var(--bg);color:var(--text);font:14px/1.5 system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif}
button,input,textarea{font:inherit}button{cursor:pointer}
.shell{min-height:100vh;display:grid;grid-template-columns:250px 1fr}
nav{position:sticky;top:0;height:100vh;padding:24px 18px;border-right:1px solid var(--line);background:rgba(8,15,24,.94);backdrop-filter:blur(12px)}
.brand{padding:0 8px 24px}.brand strong{display:block;font-size:19px;letter-spacing:.2px}.brand span{color:var(--muted);font-size:12px}
.navbtn{width:100%;text-align:left;margin:4px 0;padding:10px 12px;border:1px solid transparent;border-radius:9px;color:var(--muted);background:transparent}
.navbtn:hover,.navbtn.active{color:var(--text);background:var(--panel2);border-color:var(--line)}
.navmeta{position:absolute;left:26px;right:26px;bottom:24px;color:var(--muted);font-size:12px}
main{min-width:0;padding:34px}.page{display:none;max-width:1280px;margin:0 auto}.page.active{display:block}
.hero{display:flex;justify-content:space-between;gap:24px;align-items:flex-end;margin-bottom:24px}.eyebrow{color:var(--accent);font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.12em}.hero h1{font-size:32px;line-height:1.15;margin:7px 0 8px}.hero p{margin:0;color:var(--muted);max-width:760px}
.pill{display:inline-flex;align-items:center;gap:7px;border:1px solid var(--line);border-radius:999px;padding:6px 10px;background:var(--panel);font-size:12px;color:var(--muted)}.dot{width:8px;height:8px;border-radius:50%;background:var(--warn)}.dot.good{background:var(--good)}.dot.bad{background:var(--danger)}
.cards{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:14px;margin:20px 0 26px}.card,.section{border:1px solid var(--line);background:linear-gradient(180deg,rgba(20,35,53,.95),rgba(13,24,35,.95));border-radius:12px;box-shadow:var(--shadow)}
.card{padding:16px}.card .label{color:var(--muted);font-size:12px}.card .value{font-size:21px;font-weight:700;margin-top:6px;word-break:break-word}.card .sub{color:var(--muted);font-size:12px;margin-top:4px}
.section{padding:20px;margin-bottom:18px}.section h2{margin:0 0 5px;font-size:19px}.sectionlead{color:var(--muted);margin:0 0 16px}
.changegrid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.change{border:1px solid var(--line);background:#0b1621;border-radius:10px;padding:14px}.changehead{display:flex;justify-content:space-between;gap:12px;align-items:start}.change h3{font-size:15px;margin:0 0 7px}.change p{color:var(--muted);margin:0}.meta{font-size:12px;color:var(--muted);margin-top:10px}.tag{display:inline-block;border-radius:999px;padding:3px 7px;margin:2px 4px 2px 0;background:#1b3044;color:#cfe7f8;font-size:11px}.tag.good{background:#153727;color:#aaf0ca}
.filters{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:14px}.filter{border:1px solid var(--line);background:#0b1621;color:var(--muted);border-radius:999px;padding:6px 10px}.filter.active{border-color:var(--accent);color:var(--text)}
.version{display:grid;grid-template-columns:150px 1fr;gap:16px;padding:14px 0;border-bottom:1px solid var(--line)}.version:last-child{border-bottom:0}.version strong{display:block}.version ul{margin:6px 0 0;padding-left:18px;color:var(--muted)}
.roadmap-summary{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;margin-bottom:18px}.roadmap-list{display:grid;gap:12px}.milestone{border:1px solid var(--line);background:#0b1621;border-radius:11px;padding:16px}.milestone.current{border-color:var(--accent);box-shadow:0 0 0 1px rgba(102,199,255,.12)}.milestonehead{display:flex;justify-content:space-between;gap:16px;align-items:flex-start}.milestone h3{margin:0 0 4px;font-size:16px}.milestone p{margin:8px 0;color:var(--muted)}.milestone .evidence{margin:8px 0 0;padding-left:18px;color:var(--muted)}.status{display:inline-block;border:1px solid var(--line);border-radius:999px;padding:3px 8px;font-size:11px;text-transform:uppercase;letter-spacing:.06em}.status-released{color:#bdf4dc;border-color:#2d7053;background:#113326}.status-active{color:#b9ecff;border-color:#2c6f91;background:#102c3e}.status-advanced{color:#bdf4dc;border-color:#2d7053;background:#113326}.status-started,.status-foundation{color:#ffe6a6;border-color:#7c6131;background:#342914}.status-planned{color:#aebdca;background:#111b25}.progressline{margin-top:12px}.progressmeta{display:flex;justify-content:space-between;gap:12px;color:var(--muted);font-size:12px;margin-bottom:5px}.progressbar{height:7px;background:#07111b;border-radius:999px;overflow:hidden;border:1px solid var(--line)}.progressbar span{display:block;height:100%;background:linear-gradient(90deg,var(--accent),var(--accent2))}
.explorer{display:grid;grid-template-columns:minmax(290px,32%) 1fr;gap:16px}.endpointlist,.detail{min-width:0}.search{width:100%;background:#08131e;color:var(--text);border:1px solid var(--line);border-radius:9px;padding:10px;margin-bottom:10px}
.endpoint{padding:10px;margin:7px 0;border:1px solid var(--line);border-radius:9px;background:#0b1621;cursor:pointer}.endpoint:hover,.endpoint.active{border-color:var(--accent);background:var(--panel2)}
.method{display:inline-block;min-width:58px;font-weight:800;color:var(--accent)}.path{font-family:ui-monospace,SFMono-Regular,Menlo,monospace;font-size:12px}.small{font-size:12px;color:var(--muted)}
input,textarea,select{width:100%;background:#08131e;color:var(--text);border:1px solid var(--line);border-radius:8px;padding:9px}textarea{min-height:150px;font-family:ui-monospace,SFMono-Regular,Menlo,monospace;resize:vertical}
.grid2{display:grid;grid-template-columns:1fr 1fr;gap:12px}.label2{display:block;color:var(--muted);margin:8px 0 5px}pre{white-space:pre-wrap;word-break:break-word;background:#06101a;border:1px solid var(--line);padding:12px;border-radius:8px;max-height:350px;overflow:auto}
.action{background:#15324a;color:var(--text);border:1px solid #315a78;border-radius:8px;padding:8px 12px}.action:hover{border-color:var(--accent)}.row{display:flex;gap:10px;align-items:center}.row>*{flex:1}.hidden{display:none}.status-good{color:var(--good)}.status-bad{color:var(--danger)}
.empty{color:var(--muted);padding:18px;border:1px dashed var(--line);border-radius:9px;text-align:center}
.portalgrid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}.portalcard{border:1px solid var(--line);background:#0b1621;border-radius:11px;padding:18px}.portalcard h3{margin:0 0 7px;font-size:17px}.portalcard p{margin:0 0 12px;color:var(--muted)}.portalactions{display:flex;gap:8px;flex-wrap:wrap}.actionlink{display:inline-block;text-decoration:none;flex:0 0 auto}.portalmeta{margin-top:12px;color:var(--muted);font-size:12px}
.tablewrap{overflow:auto;border:1px solid var(--line);border-radius:9px}.datatable{width:100%;border-collapse:collapse;min-width:720px}.datatable th,.datatable td{padding:10px 12px;text-align:left;border-bottom:1px solid var(--line);white-space:nowrap}.datatable th{font-size:12px;color:var(--muted);background:#0a1520}.datatable tr:last-child td{border-bottom:0}.datatable td:first-child{font-weight:600}.authnote{margin-top:10px;color:var(--muted);font-size:12px}
.planner-controls{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px}.planner-actions{display:flex;gap:8px;flex-wrap:wrap;margin-top:12px}.planner-actions .action{flex:0 0 auto}.gridlegend{display:flex;gap:8px;flex-wrap:wrap;margin:10px 0}.legenditem{display:inline-flex;gap:6px;align-items:center;color:var(--muted);font-size:12px}.legendswatch{width:13px;height:13px;border-radius:3px;border:1px solid var(--line)}.gridboardwrap{overflow:auto;max-height:680px;border:1px solid var(--line);border-radius:10px;background:#06101a;padding:12px}.gridboard{--cell:30px;display:grid;gap:2px;width:max-content;min-width:100%}.gridcell{width:var(--cell);height:var(--cell);min-width:var(--cell);padding:0;border:1px solid #22384b;border-radius:3px;background:#0c1a26;color:transparent;position:relative}.gridcell:hover{outline:2px solid var(--accent);z-index:2}.gridcell.free{background:#123326}.gridcell.occupied{background:#1d4c6a}.gridcell.reserved{background:#5a461c}.gridcell.conflict{background:#6a2323}.gridcell.selected{outline:2px solid #fff;z-index:3}.gridcell.preview-ok{box-shadow:inset 0 0 0 2px var(--good)}.gridcell.preview-bad{box-shadow:inset 0 0 0 2px var(--danger)}.gridcell.dimmed{opacity:.22}.gridcell[data-region-name]:after{content:'';position:absolute;inset:35%;border-radius:50%;background:rgba(255,255,255,.72)}.gridinfo{display:grid;grid-template-columns:1fr 1fr;gap:12px}.gridinfo pre{margin:0;min-height:150px}.gridcoord{font-family:ui-monospace,SFMono-Regular,Menlo,monospace}.planner-note{padding:10px 12px;border:1px solid var(--line);border-radius:8px;background:#0a1520;color:var(--muted);font-size:12px}.noderow{cursor:pointer}.noderow:hover{background:var(--panel2)}.node-state-online{color:var(--good)}.node-state-stale{color:var(--warn)}.node-state-offline{color:var(--danger)}
@media(max-width:1100px){.cards,.roadmap-summary{grid-template-columns:repeat(2,1fr)}.changegrid{grid-template-columns:1fr}.explorer{grid-template-columns:1fr}.endpointlist{max-height:420px;overflow:auto}.planner-controls{grid-template-columns:repeat(2,1fr)}}
@media(max-width:760px){.portalgrid{grid-template-columns:1fr}.shell{display:block}nav{position:static;height:auto;border-right:0;border-bottom:1px solid var(--line)}.navmeta{display:none}.navbtn{display:inline-block;width:auto}.brand{padding-bottom:12px}main{padding:22px 14px}.hero{display:block}.hero .pill{margin-top:14px}.cards,.roadmap-summary{grid-template-columns:1fr}.grid2,.version,.gridinfo{grid-template-columns:1fr}.planner-controls{grid-template-columns:1fr 1fr}.milestonehead{display:block}.milestonehead .status{margin-top:8px}}

/* OpenGenesisLINK OGL Control Center */
:root{--bg:#060b16;--panel:#121f35;--panel2:#182c47;--line:rgba(128,187,243,.24);--text:#eff8ff;--muted:#9bb0cd;--accent:#75e7fa;--accent2:#ad92fb;--good:#55e6b7;--warn:#ffcb7f;--danger:#ff8199;--shadow:0 26px 90px rgba(0,0,0,.34);color-scheme:dark}
body{background:#060b16;color:var(--text);font-family:Inter,ui-sans-serif,system-ui,-apple-system,"Segoe UI",sans-serif;position:relative}
body:before{content:"";position:fixed;inset:-28%;z-index:-2;pointer-events:none;background:radial-gradient(ellipse 35% 29% at 23% 21%,rgba(44,178,232,.17),transparent 92%),radial-gradient(ellipse 30% 34% at 82% 55%,rgba(107,70,222,.17),transparent 91%),radial-gradient(ellipse 33% 25% at 48% 92%,rgba(17,178,169,.07),transparent 92%);animation:ogl-aurora 23s ease-in-out infinite alternate}
body:after{content:"";position:fixed;inset:0;z-index:-1;pointer-events:none;opacity:.26;background-image:linear-gradient(rgba(124,157,204,.12) 1px,transparent 1px),linear-gradient(90deg,rgba(124,157,204,.12) 1px,transparent 1px);background-size:66px 66px;mask-image:linear-gradient(110deg,transparent 2%,black 48%,transparent 94%)}
@keyframes ogl-aurora{to{transform:translate3d(3.4%,-2.8%,0) rotate(5deg)}}
.shell{grid-template-columns:270px 1fr}
nav{padding:29px 18px;background:linear-gradient(177deg,rgba(15,26,47,.97),rgba(6,13,27,.99));border-right:1px solid rgba(113,175,236,.22);box-shadow:18px 0 70px rgba(0,0,0,.14);z-index:3}
nav:before{content:"";position:absolute;top:0;left:0;right:0;height:2px;background:linear-gradient(90deg,transparent,rgba(113,236,255,.9),rgba(177,133,255,.8),transparent);box-shadow:0 0 25px #59bdfa54}
.brand{padding:0 8px 27px;margin-bottom:17px;position:relative;border-bottom:1px solid rgba(123,174,228,.16);display:grid;grid-template-columns:45px minmax(0,1fr);column-gap:11px;align-items:center}
.brand .ogl-logo{grid-row:span 2;height:45px;width:45px;border-radius:13px;border:1px solid #69dafa88;box-shadow:0 0 30px #4de7fe23,inset 0 0 20px #4da4e02d;background:linear-gradient(133deg,#15334a,#312049);color:#c7f8ff;font-size:17px;font-weight:900;letter-spacing:-1px;display:grid;place-items:center}
.brand strong{font-size:16px;letter-spacing:.005em;line-height:1.3;align-self:end;white-space:nowrap}
.brand span{font-size:9px;font-weight:750;letter-spacing:.13em;color:#89abc9;align-self:start;margin-top:3px}
.navbtn{border-radius:11px;padding:12px 13px;display:flex;gap:12px;align-items:center;font-size:12px;letter-spacing:.01em;border:1px solid transparent;margin:5px 0;transition:.23s;background:transparent;color:#98aecb}
.navbtn:before{content:"⌁";display:inline-grid;place-items:center;flex:0 0 23px;height:24px;border-radius:7px;background:rgba(100,140,204,.08);color:#88b3df;font-size:18px}
.navbtn[data-page=overview]:before{content:"◈"}.navbtn[data-page=statistics]:before{content:"▥"}.navbtn[data-page=grid]:before{content:"⊞"}.navbtn[data-page=nodes]:before{content:"⬡"}.navbtn[data-page=changes]:before{content:"✦"}.navbtn[data-page=versions]:before{content:"⌘"}.navbtn[data-page=roadmap]:before{content:"↗"}.navbtn[data-page=chatgpt]:before{content:"◇"}.navbtn[data-page=adminlogin]:before{content:"♙"}.navbtn[data-page=secrets]:before{content:"⚿"}.navbtn[data-page=explorer]:before{content:"◎"}
.navbtn:hover{transform:translateX(3px);border-color:rgba(96,194,255,.23);background:rgba(42,73,120,.17);color:#e5f7ff}
.navbtn.active{border:1px solid rgba(92,218,254,.37);color:#dff7ff;background:linear-gradient(100deg,rgba(30,110,158,.3),rgba(105,71,167,.13));box-shadow:inset 3px 0 #57deff,0 0 21px rgba(88,199,247,.05)}
.navbtn.active:before{background:rgba(61,181,224,.2);color:#86f1ff}
.navmeta{font-size:10px;line-height:1.6;color:#8ba3c3;left:25px;right:20px;border-top:1px solid rgba(117,165,220,.17);padding-top:15px}
main{padding:36px clamp(24px,3.6vw,69px)}.page{max-width:1550px}
.hero{position:relative;overflow:hidden;border-radius:19px;border:1px solid rgba(99,192,248,.22);padding:28px 31px;margin-bottom:27px;min-height:155px;align-items:center;background:linear-gradient(114deg,rgba(17,52,82,.89),rgba(28,31,66,.93) 53%,rgba(13,28,48,.91));box-shadow:0 20px 65px rgba(0,0,0,.2),inset 0 1px rgba(176,234,255,.1)}
.hero:before{content:"";position:absolute;right:-65px;top:-210px;width:470px;height:470px;border:1px solid rgba(96,214,255,.21);border-radius:50%;box-shadow:0 0 0 45px rgba(71,158,232,.035),0 0 0 103px rgba(71,158,232,.029),inset 0 0 55px rgba(72,157,255,.065);pointer-events:none}
.hero:after{content:"";position:absolute;left:0;right:0;top:0;height:2px;background:linear-gradient(90deg,rgba(80,242,255,.85),rgba(160,119,255,.5),transparent)}
.eyebrow{font-size:11px;letter-spacing:.18em;color:#83e7ff}
.hero h1{font-weight:780;letter-spacing:-.047em;font-size:clamp(26px,3vw,42px);margin:9px 0}
.hero h1 em{font-style:normal;background:linear-gradient(95deg,#86eeff,#d6b6ff);color:transparent;background-clip:text}
.hero p{color:#b0c1d7;line-height:1.7;font-size:13px}
.pill{background:rgba(14,27,48,.68);border:1px solid rgba(130,189,244,.25);padding:9px 13px;box-shadow:inset 0 1px rgba(255,255,255,.03);backdrop-filter:blur(12px)}
.dot.good{box-shadow:0 0 11px #44e7ac80}.dot.bad{box-shadow:0 0 11px #ff6a9380}
.cards{gap:16px}
.card,.section,.change{position:relative;border:1px solid rgba(113,165,226,.2);border-radius:15px;background:linear-gradient(147deg,rgba(24,40,64,.91),rgba(13,23,42,.95));box-shadow:0 14px 45px rgba(0,0,0,.18),inset 0 1px rgba(197,236,255,.028);backdrop-filter:blur(13px)}
.card:before,.section:before{content:"";position:absolute;inset:-1px;border-radius:inherit;padding:1px;background:linear-gradient(145deg,rgba(101,214,252,.28),transparent 33%,transparent 75%,rgba(151,104,249,.2));mask:linear-gradient(#000 0 0) content-box,linear-gradient(#000 0 0);mask-composite:exclude;pointer-events:none}
.card{padding:20px;transition:transform .24s,border-color .24s}.card:hover{transform:translateY(-3px);border-color:rgba(98,209,254,.46);box-shadow:0 17px 54px rgba(0,0,0,.29),0 0 30px rgba(99,165,250,.07)}
.card .label{font-size:11px;letter-spacing:.11em;text-transform:uppercase;color:#97aecb}.card .value{font-size:23px;letter-spacing:-.03em;margin:12px 0 4px}.card .sub{color:#9aabc3}
.section{padding:24px 26px;margin-bottom:20px}.section h2{font-size:20px;letter-spacing:-.025em;margin-bottom:8px}.sectionlead{font-size:13px;line-height:1.7;color:#97adc8}
.change{background:rgba(11,23,43,.76);border-radius:13px;transition:border-color .2s,transform .2s}.change:hover{transform:translateY(-2px);border-color:rgba(90,220,247,.35)}
input,select,textarea{background:rgba(6,16,33,.89)!important;color:#e9f7ff!important;border:1px solid rgba(123,168,223,.27)!important;border-radius:10px!important;box-shadow:inset 0 1px 4px rgba(0,0,0,.12)}
input:focus,select:focus,textarea:focus{outline:2px solid rgba(99,217,255,.43)!important;outline-offset:1px}
button.action,.actionlink,.filter{background:linear-gradient(107deg,rgba(33,81,125,.77),rgba(53,41,112,.65));border:1px solid rgba(105,177,238,.35)!important;color:#e5f7ff!important;border-radius:10px;transition:.2s}
button.action:hover,.actionlink:hover,.filter:hover{border-color:rgba(114,224,252,.68)!important;box-shadow:0 0 23px rgba(62,156,236,.13);filter:brightness(1.15)}
a{color:#83dcff}a:hover{color:#bcb0ff}
::-webkit-scrollbar{height:9px;width:9px}::-webkit-scrollbar-thumb{background:#314761;border-radius:7px}::-webkit-scrollbar-track{background:#0b1729}
@media(max-width:1100px){.shell{grid-template-columns:233px 1fr}nav{padding:20px 12px}.brand{padding:0 6px 20px}.brand strong{font-size:14px}.brand span{font-size:8px}main{padding:26px 23px}}
@media(max-width:760px){.shell{display:block}nav{position:relative;height:auto;display:flex;flex-wrap:wrap;align-items:center;gap:4px;padding:15px;overflow:visible}.brand{width:100%;border:0;margin:0;padding:0 6px 12px}.navbtn{width:auto;margin:1px;font-size:11px;padding:8px 9px;gap:5px}.navbtn:before{height:21px;width:19px;font-size:14px}.navmeta{display:none}main{padding:22px 15px}.hero{padding:23px 18px;display:block}.hero .pill{margin-top:16px}.section{padding:19px 15px}}
@media(prefers-reduced-motion:reduce){body:before{animation:none}.navbtn,.card,.change{transition:none}}


/* OGL CONTROL CENTER 2026 | Visible redesign, no external dependencies */
:root { --ogl-ice:#8af4ff; --ogl-violet:#bfa1ff; }
body { background:#050915; }
.shell { grid-template-columns:282px minmax(0,1fr); }
nav { box-shadow:24px 0 86px rgba(0,0,0,.25);background:linear-gradient(155deg,rgba(7,15,32,.99),rgba(20,20,46,.98));border-right:1px solid rgba(115,220,255,.27); }
nav .brand { background:linear-gradient(130deg,rgba(17,59,86,.56),rgba(75,39,103,.3));border-radius:18px;padding:13px;margin:0 0 20px;border:1px solid rgba(113,206,250,.2);box-shadow:inset 0 1px rgba(182,237,255,.15),0 18px 30px rgba(0,0,0,.15)}
.navsection-label { margin:19px 14px 7px; font-size:10px; letter-spacing:.21em;color:rgba(145,190,224,.72);font-weight:800; }
nav .navbtn { margin:5px 2px; border-radius:12px; padding:12px; }
nav .navbtn.active {background:linear-gradient(95deg,rgba(25,118,165,.45),rgba(89,65,184,.22));box-shadow:inset 3px 0 #8cf5ff,0 0 30px rgba(55,185,234,.12);}
main { padding:31px clamp(24px,4vw,74px);background:radial-gradient(ellipse at 85% -15%,rgba(74,39,180,.12),transparent 60%); }
main:before {content:"";position:fixed;inset:0;pointer-events:none;opacity:.17;z-index:-1;background:repeating-linear-gradient(0deg,transparent,transparent 63px,rgba(122,185,255,.11) 64px),repeating-linear-gradient(90deg,transparent,transparent 63px,rgba(122,185,255,.11) 64px);}
.hero { min-height:220px; padding:42px clamp(25px,4vw,59px);overflow:hidden;border:1px solid rgba(136,235,255,.38);border-radius:22px;background:linear-gradient(115deg,rgba(17,57,89,.97),rgba(35,34,83,.96) 58%,rgba(7,25,45,.96));box-shadow:0 32px 82px rgba(0,0,0,.3),0 0 42px rgba(95,154,245,.07),inset 0 1px rgba(255,255,255,.1); }
.hero:before{width:580px;height:580px;right:-95px;top:-310px;animation:ogl-orbit 25s linear infinite;border:1px solid rgba(131,237,255,.4);box-shadow:0 0 0 51px rgba(79,160,225,.05),0 0 0 117px rgba(99,89,228,.06),0 0 0 179px rgba(76,167,230,.025);}
.hero:after {height:3px;background:linear-gradient(90deg,rgba(93,238,255,.95),rgba(184,115,255,.95),transparent 86%);box-shadow:0 0 32px rgba(77,187,255,.5);}
@keyframes ogl-orbit {to{transform:rotate(360deg)}}
.hero h1 {font-size:clamp(33px,3.5vw,57px);letter-spacing:-.052em;max-width:800px;line-height:1.08;}
.hero p {max-width:630px;font-size:14px;color:#b9cce4;}
.hero .eyebrow {letter-spacing:.25em;font-size:11px;text-shadow:0 0 20px rgba(64,220,255,.65);}
.hero .pill {border:1px solid rgba(148,227,250,.35);padding:11px 18px;background:rgba(11,28,54,.72);backdrop-filter:blur(12px);}
.ogl-live-strip{display:flex;align-items:center;gap:11px;min-height:47px;margin:19px 0 27px;padding:9px 18px;border-radius:12px;background:linear-gradient(95deg,rgba(16,56,70,.8),rgba(28,31,70,.8));border:1px solid rgba(88,216,233,.19);font-size:11px;letter-spacing:.08em;color:#a6c7da;}
.ogl-live-strip strong{color:#a8ffe1;letter-spacing:.15em;}
.ogl-live-strip .live-version{margin-left:auto;letter-spacing:0;color:#c4d9f2;}
.livepulse{height:9px;width:9px;border-radius:50%;background:#5de7b8;box-shadow:0 0 0 5px rgba(41,202,143,.12),0 0 14px #56e1b2;animation:ogl-pulse 2.6s ease-in-out infinite;}
@keyframes ogl-pulse{50%{box-shadow:0 0 0 9px rgba(41,202,143,.04),0 0 20px #56e1b2;}}
.ogl-section-head{display:flex;justify-content:space-between;align-items:baseline;gap:12px;margin:27px 2px 13px;font-size:18px;font-weight:800;letter-spacing:-.02em;}
.ogl-section-head small{font-size:10px;color:#83abc9;letter-spacing:.19em;font-weight:700}
.card {min-height:141px;border-radius:18px;padding:26px 22px;background:linear-gradient(140deg,rgba(24,43,72,.92),rgba(13,25,45,.93));border:1px solid rgba(135,207,255,.24);box-shadow:0 18px 45px rgba(0,0,0,.18);}
.card .value{font-size:clamp(22px,2vw,32px);color:#ecfbff;letter-spacing:-.05em;white-space:normal;overflow-wrap:anywhere;}
.card .label{color:#a1c1dc;letter-spacing:.16em;font-size:10px;}
.card:hover{transform:translateY(-5px);border-color:#78dffd;}
.section{border-radius:18px;background:linear-gradient(144deg,rgba(22,38,65,.93),rgba(11,22,41,.97));border-color:rgba(122,198,252,.25);padding:27px;}
.section h2{font-size:22px;}
.filter {padding:10px 14px;}
@media(max-width:850px){.shell{grid-template-columns:1fr;}nav{position:relative;height:auto;display:flex;flex-wrap:wrap;align-items:center;padding:15px;gap:5px;}nav .brand{width:100%;}.navsection-label{width:100%;margin:12px 8px 2px}.navbtn{width:auto;}.navmeta{display:none}main{padding:18px}.ogl-live-strip{flex-wrap:wrap}.ogl-live-strip .live-version{margin-left:0;width:100%}.hero{padding:27px 20px}}
@media(prefers-reduced-motion:reduce){.hero:before,.livepulse{animation:none!important}}


.navbtn[data-page=diagnostics]:before{content:"⚙";}
.ogl-diagnostic-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:12px;margin-top:23px;}
.ogl-diagnostic-result{padding:18px;border:1px solid rgba(121,177,235,.22);background:rgba(9,22,42,.67);border-radius:13px;min-height:102px;}
.ogl-diagnostic-result strong{font-size:14px;display:block;margin-bottom:11px;color:#ecf8ff;}
.ogl-diagnostic-result small{color:#9bb6d0;font-size:12px;line-height:1.5;display:block;overflow-wrap:anywhere;}
.ogl-diagnostic-result.good{border-color:rgba(82,210,164,.32);box-shadow:inset 3px 0 rgba(82,230,187,.65);}
.ogl-diagnostic-result.bad{border-color:rgba(255,135,158,.32);box-shadow:inset 3px 0 rgba(255,135,158,.7);}
.ogl-diagnostic-result.optional{border-color:rgba(253,203,113,.3);box-shadow:inset 3px 0 rgba(253,203,113,.65);}
.ogl-triage-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:13px;margin-top:18px;}
.ogl-triage-tile{border:1px solid rgba(127,185,235,.2);background:rgba(8,20,38,.7);border-radius:13px;padding:17px 18px;}
.ogl-triage-tile strong{color:#aff1ff;}
.ogl-triage-tile p{font-size:12px;color:#a4b8d4;line-height:1.6;}
.ogl-triage-tile code{overflow-wrap:anywhere;}

</style>
</head>
<body>
<div class="shell">
<nav>
<div class="brand"><div class="ogl-logo" aria-hidden="true">OGL</div><strong>OpenGenesisLINK</strong><span>WORLD API · CONTROL CENTER</span></div>
<div class="navsection-label">CONTROL CENTER</div>
<button class="navbtn active" data-page="overview">Übersicht</button>
<button class="navbtn" data-page="statistics">Statistik</button>
<button class="navbtn" data-page="diagnostics">Systemdiagnose</button>
<button class="navbtn" data-page="grid">Grid-Planer</button>
<button class="navbtn" data-page="nodes">Simulatoren</button>
<button class="navbtn" data-page="changes">Was ist neu?</button>
<button class="navbtn" data-page="versions">Versionen</button>
<button class="navbtn" data-page="roadmap">Entwicklungsplan</button>
<div class="navsection-label">INTEGRATIONEN</div>
<button class="navbtn" data-page="chatgpt">ChatGPT / Portale</button>
<button class="navbtn" data-page="adminlogin">Administrator</button>
<button class="navbtn" data-page="secrets">Schlüssel &amp; Einrichtung</button>
<button class="navbtn" data-page="explorer">API-Endpunkte</button>
<div class="navmeta" id="navmeta">Live-Daten werden geladen…</div>
</nav>
<main>
<section class="page active" id="page-overview">
<div class="hero">
<div><div class="eyebrow">OpenGenesisLINK / WORLD API</div><h1>Deine Welt. <em>Deine Kontrolle.</em></h1><p>Live-Übersicht über Serverversion, API-Vertrag, aktuelle Erweiterungen und verfügbare Endpunkte.</p></div>
<div class="pill"><span class="dot" id="healthDot"></span><span id="healthText">Prüfe API-Status…</span></div>
</div>
<div class="ogl-live-strip"><span class="livepulse"></span><strong>OGL LIVE</strong><span>SERVER &amp; WORLD API</span><span class="live-version" id="oglActiveRelease">OpenGenesisLINK · Status wird geladen</span></div>
<div class="ogl-section-head"><span>Systemstatus</span><small>AKTUELLE SERVERDATEN</small></div>
<div class="cards">
<div class="card"><div class="label">Server</div><div class="value" id="serverVersion">–</div><div class="sub" id="milestone">Meilenstein –</div></div>
<div class="card"><div class="label">API</div><div class="value" id="apiVersion">–</div><div class="sub">Aktueller OpenAPI-3.1-Vertrag</div></div>
<div class="card"><div class="label">Endpunkte</div><div class="value" id="endpointCount">–</div><div class="sub" id="operationCount">– Operationen</div></div>
<div class="card"><div class="label">Letzte Änderung</div><div class="value" id="lastChangeDate">–</div><div class="sub" id="lastChangeTitle">Keine Versionshinweise</div></div>
</div>
<div class="section">
<h2>Neu in der aktuellen Version</h2><p class="sectionlead">Die Einträge werden direkt aus <code>x_nexverse_changelog</code> im laufenden OpenAPI-Dokument gelesen und deutsch dargestellt.</p>
<div class="changegrid" id="overviewChanges"></div>
</div>
<div class="ogl-section-head"><span>Arbeitsbereiche</span><small>VERWALTUNG &amp; ENTWICKLUNG</small></div>
<div class="section">
<h2>Schnellzugriff</h2><p class="sectionlead">Die wichtigsten technischen Oberflächen des laufenden Servers.</p>
<div class="filters">
<button class="filter" data-goto="changes">Alle Neuerungen</button>
<button class="filter" data-goto="versions">Versionshistorie</button>
<button class="filter" data-goto="roadmap">Entwicklungsplan / Projektstatus</button>
<button class="filter" data-goto="chatgpt">ChatGPT / Portale</button>
<button class="filter" data-goto="explorer">Endpunktübersicht</button>
<button class="filter" data-goto="adminlogin">Admin-Anmeldung</button>
<button class="filter" data-goto="secrets">Schlüssel &amp; Einrichtung</button>
</div>
</div>
</section>


<section class="page" id="page-diagnostics">
<div class="hero"><div><div class="eyebrow">OGL CONTROL PLANE / DIAGNOSTICS</div><h1>Systemdiagnose</h1><p>Prüfe die tatsächlich erreichbaren World-API-Dienste dieses OGL-Servers. Kein Demo-Status, keine Datenbankänderungen.</p></div><div class="pill"><span class="dot" id="diagDot"></span><span id="diagSummary">Noch nicht geprüft</span></div></div>
<div class="section"><h2>Live-Selbsttest</h2><p class="sectionlead">Diese Prüfung liest ausschließlich den aktuellen Health-/Versions-/OpenAPI-Zustand, die ChatGPT-Anweisung und den optionalen Node-Lizenzdienst. Keine schreibenden Aktionen.</p>
<button class="action" type="button" id="diagnosticsRefresh">Prüfung jetzt starten</button>
<div class="ogl-diagnostic-grid" id="diagnosticsResults">
<div class="ogl-diagnostic-result">Wähle „Prüfung jetzt starten“, um Live-Endpunkte zu testen.</div>
</div>
</div>
<div class="section"><h2>Robust &amp; Simulator – Log-Diagnosen</h2><p class="sectionlead">Beschädigte JPEG2000-Texturen, ungültige XML-RPC-Anfragen und Hypergrid-IM-Zustellversuche werden serverseitig protokolliert. Dieser Browser kann keine ungefilterten Serverlogs lesen.</p>
<div class="ogl-triage-grid"><div class="ogl-triage-tile"><strong>JPEG2000 / World Map</strong><p>LunaTexture zeigt defekte Textur-UUIDs und betroffene Prims:</p><code>nex texture status</code><p><code>nex texture inspect &lt;UUID&gt;</code></p></div>
<div class="ogl-triage-tile"><strong>Hypergrid IM</strong><p>Offline-/Location-Lookups werden weiterhin ausgeführt; identische fehlgeschlagene Debugmeldungen werden höchstens einmal je UUID in 60 Sekunden ausgegeben.</p></div>
<div class="ogl-triage-tile"><strong>XML-RPC</strong><p>Ungültige XML-RPC-Requests werden mit HTTP 400 abgewiesen. Einzelne fremde Requests sind kein Nachweis eines Serverfehlers.</p></div></div>
</div>
</section>

<section class="page" id="page-statistics">
<div class="hero">
<div><div class="eyebrow">Live-Statistik</div><h1>Grid-Statistik</h1><p>Aktuelle Einwohner-, Aktivitäts-, Anwesenheits- und Hypergrid-Kennzahlen direkt aus den laufenden OGL-Diensten.</p></div>
<div class="pill"><span class="dot" id="statsDot"></span><span id="statsStatus">Noch nicht geladen</span></div>
</div>
<div class="section">
<h2>Zugriff</h2><p class="sectionlead">Aggregierte Kennzahlen sind ohne Anmeldung verfügbar. Personenbezogene Online-, Regions- und Hypergrid-Details bleiben geschützt und erfordern ein Zugriffstoken oder einen API-Schlüssel mit <code>statistics:read</code> über einen geschützten Transport.</p>
<div class="grid2">
<div><label class="label2">Zugriffstoken (Bearer)</label><input id="statsBearer" type="password" autocomplete="off" placeholder="Optional"></div>
<div><label class="label2">X-NexVerse-Api-Key</label><input id="statsApiKey" type="password" autocomplete="off" placeholder="Optional"></div>
</div>
<div class="row" style="margin-top:10px"><button class="action" id="loadStats">Statistik laden</button><button class="action" id="clearStats">Zugangsdaten löschen</button></div>
<div class="authnote">Die Zugangsdaten bleiben nur in dieser geöffneten Seite und werden nicht gespeichert.</div>
</div>
<div class="cards">
<div class="card"><div class="label">Registriert</div><div class="value" id="statRegistered">–</div><div class="sub">lokale Einwohnerkonten</div></div>
<div class="card"><div class="label">Gerade online</div><div class="value" id="statOnlineTotal">–</div><div class="sub">Einwohner + Hypergrid</div></div>
<div class="card"><div class="label">Einwohner online</div><div class="value" id="statResidentsOnline">–</div><div class="sub">auf diesem Grid</div></div>
<div class="card"><div class="label">Aktiv letzte 7 Tage</div><div class="value" id="statActive7">–</div><div class="sub">eindeutige Einwohner</div></div>
<div class="card"><div class="label">Aktiv letzte 30 Tage</div><div class="value" id="statActive30">–</div><div class="sub">eindeutige Einwohner</div></div>
<div class="card"><div class="label">Hypergrid online</div><div class="value" id="statHgOnline">–</div><div class="sub">fremde Besucher jetzt</div></div>
<div class="card"><div class="label">HG-Besucher bekannt</div><div class="value" id="statHgKnown">–</div><div class="sub">eindeutige Besucher</div></div>
<div class="card"><div class="label">HG letzte 30 Tage</div><div class="value" id="statHg30">–</div><div class="sub">eindeutige Besucher</div></div>
</div>
<div class="section">
<h2>Gerade online</h2><p class="sectionlead">Lokale Einwohner und Hypergrid-Besucher mit aktuell ermittelter Region.</p>
<div class="tablewrap"><table class="datatable"><thead><tr><th>Name</th><th>Typ</th><th>Region</th><th>Heimat-Grid</th><th>Login</th></tr></thead><tbody id="statsOnlineBody"></tbody></table></div>
</div>
<div class="section">
<h2>Hypergrid-Herkunft</h2><p class="sectionlead">Bekannte eindeutige Besucher nach Heimat-Grid. Dies sind keine kumulierten Besuchssessions.</p>
<div class="tablewrap"><table class="datatable"><thead><tr><th>Heimat-Grid</th><th>Bekannt</th><th>Online</th><th>7 Tage</th><th>30 Tage</th></tr></thead><tbody id="statsHgBody"></tbody></table></div>
</div>
<div class="section">
<h2>Regionen jetzt</h2><p class="sectionlead">Aktuelle Belegung nach Region, getrennt nach lokalen Einwohnern und Hypergrid-Besuchern.</p>
<div class="tablewrap"><table class="datatable"><thead><tr><th>Region</th><th>Gesamt</th><th>Einwohner</th><th>Hypergrid</th></tr></thead><tbody id="statsRegionBody"></tbody></table></div>
</div>
<div class="section">
<h2>Datenbasis</h2><pre id="statsQuality">Statistik noch nicht geladen.</pre>
</div>
</section>

<section class="page" id="page-grid">
<div class="hero">
<div><div class="eyebrow">Region Control Plane</div><h1>Interaktiver Grid-Planer</h1><p>Rasteransicht des angeschlossenen Grids auf 256-Meter-Basis. Belegte, freie, reservierte und widersprüchliche Zellen werden direkt aus der World API gelesen.</p></div>
<div class="pill"><span class="dot" id="gridDot"></span><span id="gridStatus">Noch nicht geladen</span></div>
</div>
<div class="section">
<h2>Zugriff</h2><p class="sectionlead">Raster und Placement-Vorschau benötigen <code>regions:read</code>. Erstellen, Verschieben und Lifecycle benötigen zusätzlich <code>regions:manage</code>. Die komfortable Estate-Auswahl verwendet <code>estates:read</code>; eine bekannte positive Estate-ID kann weiterhin manuell eingetragen werden. Alle privilegierten Funktionen setzen TLS oder einen gleichwertig geschützten Transport voraus.</p>
<div class="grid2">
<div><label class="label2">Zugriffstoken (Bearer)</label><input id="gridBearer" type="password" autocomplete="off" placeholder="Optional"></div>
<div><label class="label2">X-NexVerse-Api-Key</label><input id="gridApiKey" type="password" autocomplete="off" placeholder="Optional"></div>
</div>
<div class="planner-actions"><button class="action" id="loadGrid">Grid laden</button><button class="action" id="clearGridCredentials">Zugangsdaten löschen</button></div>
<div class="authnote">Zugangsdaten werden nicht gespeichert. Bei deaktivierten privilegierten Endpunkten bleibt diese Ansicht sichtbar, kann aber keine Grid-Daten abrufen.</div>
</div>
<div class="section">
<h2>Region suchen und anspringen</h2>
<p class="sectionlead">Durchsuche das gesamte registrierte Grid nach Regionsname oder UUID. Ein Treffer setzt den Viewport direkt auf die Region und markiert ihre Ursprungzelle.</p>
<div class="planner-controls">
<div><label class="label2">Regionssuche</label><input id="gridRegionSearch" maxlength="128" placeholder="mindestens 2 Zeichen oder UUID"></div>
<div><label class="label2">Treffer</label><select id="gridRegionResults"><option value="">Noch keine Suche ausgeführt</option></select></div>
</div>
<div class="planner-actions">
<button class="action" id="searchGridRegions">Regionen suchen</button>
<button class="action" id="jumpGridRegion">Treffer anspringen</button>
</div>
<pre id="gridRegionSearchDetail">Noch keine Regionssuche ausgeführt.</pre>
</div>
<div class="section">
<h2>Viewport und Platzierung</h2><p class="sectionlead">Der API-Viewport ist auf maximal 128 × 128 Zellen begrenzt. Eine Zelle entspricht exakt 256 × 256 Metern.</p>
<div class="planner-controls">
<div><label class="label2">Min. Grid X</label><input id="gridMinX" type="number" min="0" step="1" value="1000"></div>
<div><label class="label2">Min. Grid Y</label><input id="gridMinY" type="number" min="0" step="1" value="1000"></div>
<div><label class="label2">Breite in Zellen</label><input id="gridWidth" type="number" min="1" max="128" step="1" value="24"></div>
<div><label class="label2">Höhe in Zellen</label><input id="gridHeight" type="number" min="1" max="128" step="1" value="18"></div>
<div><label class="label2">Vorschau Breite (m)</label><input id="gridRegionSizeX" type="number" min="256" max="4096" step="256" value="256"></div>
<div><label class="label2">Vorschau Höhe (m)</label><input id="gridRegionSizeY" type="number" min="256" max="4096" step="256" value="256"></div>
<div><label class="label2">Zellgröße Anzeige</label><input id="gridCellPixels" type="range" min="18" max="48" step="2" value="30"></div>
<div><label class="label2">Statusfilter</label><select id="gridFilter"><option value="all">Alle</option><option value="free">Frei</option><option value="occupied">Belegt</option><option value="reserved">Reserviert</option><option value="conflict">Konflikt</option></select></div>
</div>
<div class="planner-actions">
<button class="action" id="gridWest">← West</button><button class="action" id="gridEast">Ost →</button><button class="action" id="gridSouth">↓ Süd</button><button class="action" id="gridNorth">Nord ↑</button><button class="action" id="validateGridPlacement">Platzierung prüfen</button>
</div>
</div>
<div class="section">
<h2>Raster</h2>
<div class="gridlegend">
<span class="legenditem"><span class="legendswatch" style="background:#123326"></span>Frei</span>
<span class="legenditem"><span class="legendswatch" style="background:#1d4c6a"></span>Belegt</span>
<span class="legenditem"><span class="legendswatch" style="background:#5a461c"></span>Reserviert</span>
<span class="legenditem"><span class="legendswatch" style="background:#6a2323"></span>Konflikt</span>
</div>
<div class="planner-actions">
<label class="legenditem"><input id="gridShowWarp3d" type="checkbox" checked> Warp3D-Kartenbilder über belegten Zellen anzeigen</label>
<a class="action actionlink" href="/api/v1/deutschland/view" target="_blank" rel="noopener noreferrer">Deutschland-Karte öffnen ↗</a>
<a class="action actionlink" href="/api/v1/deutschland" target="_blank" rel="noopener noreferrer">Öffentliche Karten-API ↗</a>
</div>
<p class="sectionlead" id="gridMapTileStatus">Kartenkacheln werden bei Bedarf aus der öffentlichen Deutschland-API nachgeladen.</p>
<div class="gridboardwrap"><div class="gridboard" id="gridBoard"><div class="empty">Noch keine Grid-Daten geladen.</div></div></div>
</div>
<div class="section">
<h2>Zell- und Placement-Details</h2>
<div class="gridinfo">
<div><div class="small" style="margin-bottom:6px">Ausgewählte Zelle</div><pre id="gridCellDetail">Keine Zelle ausgewählt.</pre></div>
<div><div class="small" style="margin-bottom:6px">Placement-Prüfung</div><pre id="gridPlacementDetail">Noch keine Platzierung geprüft.</pre></div>
</div>
</div>
<div class="section">
<h2>Region erstellen / verschieben / Lifecycle</h2>
<p class="sectionlead">Die ausgewählte Rasterzelle wird als Zielkoordinate verwendet. Erstellung ist nur nach erfolgreicher Placement-Prüfung möglich. Create, Move und Lifecycle erfordern einen online erreichbaren Node mit <code>managed_region_commands=true</code>; gestoppte Regionen können über UUID und Ziel-Node wieder gestartet werden.</p>
<div class="planner-controls">
<div><label class="label2">Regionsname</label><input id="gridCreateName" maxlength="128" placeholder="z. B. Freiburg Nord"></div>
<div><label class="label2">Simulator-Node</label><input id="gridCreateNodeId" list="gridNodeOptions" maxlength="128" placeholder="NodeId"><datalist id="gridNodeOptions"></datalist></div>
<div><label class="label2">Estate</label><input id="gridCreateEstateId" type="number" min="1" step="1" list="gridEstateOptions" placeholder="Estate laden oder ID eingeben"><datalist id="gridEstateOptions"></datalist></div>
<div><label class="label2">Region-UUID (optional)</label><input id="gridCreateRegionId" placeholder="leer = automatisch"></div>
<div><label class="label2">Region-UUID für Move</label><input id="gridMoveRegionId" placeholder="bei belegter Zelle automatisch übernommen"></div>
<div><label class="label2">Region-UUID für Lifecycle</label><input id="gridLifecycleRegionId" placeholder="bei belegter Zelle automatisch übernommen"></div>
<div><label class="label2">Ziel-Node für Start</label><input id="gridLifecycleNodeId" list="gridNodeOptions" maxlength="128" placeholder="bei Start erforderlich"></div>
<div><label class="label2">Idempotency-Key (optional)</label><input id="gridMutationIdempotency" maxlength="128" autocomplete="off" placeholder="für sichere Wiederholung"></div>
</div>
<div class="planner-actions">
<button class="action" id="loadGridNodes">Managed Nodes laden</button>
<button class="action" id="loadGridEstates">Estates laden</button>
<button class="action" id="createGridRegion">Region erstellen</button>
<button class="action" id="moveGridRegion">Region verschieben</button>
</div>
<div class="planner-actions">
<button class="action" id="startGridRegion">Region starten</button>
<button class="action" id="stopGridRegion">Region stoppen</button>
<button class="action" id="restartGridRegion">Region neu starten</button>
</div>
<div class="authnote">Managed Mutationen sind serverseitig standardmäßig deaktiviert. <code>ManagedRegionCommands=true</code> darf erst bei authentifiziertem, geschütztem bidirektionalem NexBus aktiviert werden. Stop/Restart werden bei aktiven Root-Agents verweigert; Start benötigt einen Ziel-Node.</div>
<pre id="gridMutationDetail">Noch keine Regionsmutation ausgeführt.</pre>
</div>

<div class="section">
<h2>Estate-Verwaltung</h2>
<p class="sectionlead">Diese Funktionen benötigen <code>estates:manage</code>. Verwaltungslisten werden bewusst nicht über <code>estates:read</code> veröffentlicht.</p>
<div class="planner-controls">
<div><label class="label2">Estate-ID</label><input id="estateManageId" type="number" min="1" step="1" placeholder="bei Create leer lassen"></div>
<div><label class="label2">Name</label><input id="estateManageName" maxlength="64" placeholder="Estate-Name"></div>
<div><label class="label2">Owner UUID</label><input id="estateManageOwner" placeholder="lokaler Grid-Account"></div>
<div><label class="label2">Parent-Estate-ID</label><input id="estateManageParent" type="number" min="0" step="1" value="1"></div>
<div><label class="label2">Manager UUIDs</label><input id="estateManageManagers" placeholder="Komma oder Leerzeichen getrennt"></div>
<div><label class="label2">Erlaubte Residents</label><input id="estateManageAllowed" placeholder="UUIDs, Komma oder Leerzeichen getrennt"></div>
<div><label class="label2">Gesperrte Residents</label><input id="estateManageBanned" placeholder="UUIDs, Komma oder Leerzeichen getrennt"></div>
<div><label class="label2">Erlaubte Gruppen</label><input id="estateManageGroups" placeholder="Gruppen-UUIDs"></div>
<div><label class="label2">Region UUID zuordnen</label><input id="estateManageRegionId" placeholder="Region UUID"></div>
</div>
<div class="planner-controls">
<label><input id="estatePolicyPublic" type="checkbox" checked> Public Access</label>
<label><input id="estatePolicyVoice" type="checkbox" checked> Voice erlaubt</label>
<label><input id="estatePolicyDirectTp" type="checkbox" checked> Direct Teleport</label>
<label><input id="estatePolicySkipScripts" type="checkbox"> Scripts überspringen</label>
<label><input id="estatePolicyDenyAnonymous" type="checkbox"> Anonymous verweigern</label>
<label><input id="estatePolicyDenyMinors" type="checkbox"> Minors verweigern</label>
<label><input id="estatePolicyEnvironment" type="checkbox"> Environment Override</label>
</div>
<div class="planner-actions">
<button class="action" id="loadEstateManagement">Estate laden</button>
<button class="action" id="createEstateManagement">Estate erstellen</button>
<button class="action" id="updateEstateManagement">Estate speichern</button>
<button class="action" id="assignEstateRegion">Region zuordnen</button>
</div>
<div class="authnote">Region→Estate-Zuordnungen werden sofort in der Estate-Datenbank gespeichert. Eine laufende Region muss danach neu gestartet werden, damit ihre live EstateSettings neu geladen werden.</div>
<pre id="estateManagementDetail">Noch keine Estate-Verwaltungsaktion ausgeführt.</pre>
</div>

<div class="planner-note">Create/Move/Start/Stop/Restart laufen asynchron über Robust → NexBus → Ziel-NodeAgent. Estate-Änderungen werden synchron über die autoritative Estate-Datenschicht gespeichert und auditiert.</div>
</section>

<section class="page" id="page-nodes">
<div class="hero">
<div><div class="eyebrow">Simulator Control Plane</div><h1>Simulator-Nodes</h1><p>Live-Zustand der durch den OGL NodeAgent beobachteten Simulatorprozesse und ihrer aktuell gemeldeten Regionen.</p></div>
<div class="pill"><span class="dot" id="nodesDot"></span><span id="nodesStatus">Noch nicht geladen</span></div>
</div>
<div class="section">
<h2>Zugriff</h2><p class="sectionlead">Node-, Host- und Prozessinformationen sind geschützt und benötigen <code>simulators:read</code>. Die Ansicht führt keine Verwaltungsaktionen aus.</p>
<div class="grid2">
<div><label class="label2">Zugriffstoken (Bearer)</label><input id="nodesBearer" type="password" autocomplete="off" placeholder="Optional"></div>
<div><label class="label2">X-NexVerse-Api-Key</label><input id="nodesApiKey" type="password" autocomplete="off" placeholder="Optional"></div>
</div>
<div class="planner-actions"><button class="action" id="loadNodes">Simulatoren laden</button><button class="action" id="clearNodesCredentials">Zugangsdaten löschen</button></div>
<div class="authnote">Zugangsdaten bleiben nur in dieser geöffneten Seite. Regions-Lifecycle wird im Grid Planner gesteuert; ein Start/Stop/Restart des gesamten Simulatorprozesses ist hier weiterhin bewusst nicht verfügbar.</div>
</div>
<div class="cards">
<div class="card"><div class="label">Beobachtet</div><div class="value" id="nodesCount">–</div><div class="sub">NodeAgent-Registrierungen</div></div>
<div class="card"><div class="label">Online</div><div class="value" id="nodesOnline">–</div><div class="sub">Heartbeat aktuell</div></div>
<div class="card"><div class="label">Stale</div><div class="value" id="nodesStale">–</div><div class="sub">Heartbeat überfällig</div></div>
<div class="card"><div class="label">Offline</div><div class="value" id="nodesOffline">–</div><div class="sub">explizit abgemeldet</div></div>
</div>
<div class="section">
<h2>Node-Liste</h2><p class="sectionlead" id="nodesTransport">NexBus-Transportstatus noch nicht geladen.</p>
<div class="tablewrap"><table class="datatable"><thead><tr><th>Node</th><th>Host</th><th>Status</th><th>Version</th><th>Regionen</th><th>Avatare</th><th>Uptime</th><th>RAM</th><th>Letztes Signal</th></tr></thead><tbody id="nodesBody"></tbody></table></div>
</div>
<div class="section">
<h2>Node-Details</h2><pre id="nodeDetail">Noch kein Node ausgewählt.</pre>
</div>
<div class="section">
<h2><span class="icon">✦</span> OGL Feature Control · Simulator-Lizenzen</h2>
<p class="sectionlead">Grid-Inhaber vergeben hier die Archivfreigaben der per NexBus registrierten Simulatoren.
Die Richtlinie wird automatisch signiert und zur NodeID übertragen. Nur für World-API-Administratoren mit
<code>simulators:manage</code> und <code>UserLevel ≥ 200</code>. Ohne Freigabe gilt bei aktivierter
<code>[OGLFeatureControl]</code>-Pflicht eine Sperre.</p>
<div class="planner-controls">
<div><label class="label2">Simulator-NodeID</label><select id="licenseNode" style="width:100%;padding:11px"><option value="">Simulatoren laden …</option></select></div>
<div><label class="label2">Lizenzkennung (optional, wird nur als Prüfsumme gespeichert)</label>
<input id="licenseKey" type="password" autocomplete="off" placeholder="Bestehende Kennung unverändert lassen"></div>
</div>
<div class="planner-controls">
<label><input id="licenseOarImport" type="checkbox"> OAR importieren</label>
<label><input id="licenseOarExport" type="checkbox"> OAR exportieren</label>
<label><input id="licenseIarImport" type="checkbox"> IAR importieren</label>
<label><input id="licenseIarExport" type="checkbox"> IAR exportieren</label>
</div>
<div class="planner-actions">
<button class="action" id="loadFeatureLicenses">Freigaben laden</button>
<button class="action" id="saveFeatureLicense">Richtlinie speichern &amp; übertragen</button>
<button class="action" id="revokeFeatureLicense">Alle vier Funktionen sperren</button>
</div>
<div class="authnote">Die Funktion benötigt eine gültige Simulator-Richtlinienkonfiguration in Robust und NodeAgent.
Sperren greifen beim nächsten erfolgreichen NexBus-Ereignis; spätestens mit Ablauf des kurzlebigen Tokens.
Archivprüfungen bleiben auch für freigegebene Importe aktiv.</div>
<pre id="licenseStatus">Noch keine Lizenzinformationen geladen.</pre>
</div>
</section>

<section class="page" id="page-changes">
<div class="hero"><div><div class="eyebrow">Änderungsprotokoll</div><h1>Was ist neu?</h1><p>Nachvollziehbare, maschinenlesbare Änderungen der World API und ihrer Plattform-Grundlagen.</p></div></div>
<div class="section">
<div class="filters" id="changeFilters"></div>
<div class="changegrid" id="allChanges"></div>
</div>
</section>

<section class="page" id="page-versions">
<div class="hero"><div><div class="eyebrow">Kompatibilität</div><h1>Versionen</h1><p>API-Versionen, Server-Linien, Codenamen und veröffentlichte Highlights.</p></div></div>
<div class="section" id="versionList"></div>
</section>

<section class="page" id="page-roadmap">
<div class="hero"><div><div class="eyebrow">Projektstatus</div><h1>OpenGenesisLINK Entwicklungsplan</h1><p>Meilenstein-Status auf Basis des veröffentlichten Entwicklungsplans und konkreter Implementierungsnachweise. Prozentwerte werden nur angezeigt, wenn der Meilenstein tatsächlich eine gepflegte Checkbox-Checkliste besitzt.</p></div></div>
<div class="roadmap-summary">
<div class="card"><div class="label">Aktueller Meilenstein</div><div class="value" id="roadmapCurrent">–</div><div class="sub" id="roadmapCodename">–</div></div>
<div class="card"><div class="label">Aktiv</div><div class="value" id="roadmapActiveCount">–</div><div class="sub">aktueller Produkt-Meilenstein</div></div>
<div class="card"><div class="label">Vorgezogen / begonnen</div><div class="value" id="roadmapStartedCount">–</div><div class="sub">vorgezogen, begonnen oder Grundlage</div></div>
<div class="card"><div class="label">Geplant</div><div class="value" id="roadmapPlannedCount">–</div><div class="sub">noch nicht als implementiert beansprucht</div></div>
</div>
<div class="section">
<h2>Statusmodell</h2>
<p class="sectionlead">Die Statusbegriffe vermeiden erfundene Gesamtfortschritte. „Vorgezogen“ bedeutet, dass Arbeit aus einem späteren Meilenstein bereits früher umgesetzt wurde, nicht dass der Meilenstein vollständig abgeschlossen ist.</p>
<div class="filters" id="roadmapLegend"></div>
</div>
<div class="roadmap-list" id="roadmapList"></div>
</section>

<section class="page" id="page-chatgpt">
<div class="hero">
<div><div class="eyebrow">KI-Integration</div><h1>ChatGPT-Anweisungen &amp; Portaltrennung</h1><p>Downloadbare Arbeitsanweisungen fuer ChatGPT und API-Agenten. Die Trennung zwischen Buergerportal, Adminbereich und technischen Diensten folgt den Zielgruppen- und Scope-Metadaten des laufenden OpenAPI-Vertrags. Interne Simulator-Bridges (z. B. Experiences) sind keine direkt benutzbaren Einwohner-Endpunkte.</p></div>
<div class="pill"><span class="dot good"></span><span>OpenAPI-gebunden</span></div>
</div>

<div class="section">
<h2>Gesamtanweisung</h2>
<p class="sectionlead">Enthaelt gemeinsame Sicherheitsregeln, API-Basis, Authentifizierung, Fehlerbehandlung sowie getrennte Kapitel fuer Buergerportal, Adminbereich und Service-Integrationen.</p>
<div class="portalactions">
<a class="action actionlink" href="/api/v1/chatgpt-instructions.md" download>Gesamtanweisung als MD</a>
<a class="action actionlink" href="/api/v1/openapi.json" target="_blank" rel="noreferrer">OpenAPI JSON oeffnen</a>
<button class="action" data-goto="explorer">API-Explorer oeffnen</button>
</div>
<div class="portalmeta">Technische Quelle der Wahrheit bleibt immer der aktuelle OpenAPI-3.1-Vertrag des laufenden Servers. Dokumentierte optionale Endpunkte koennen je nach Robust-Konfiguration deaktiviert sein. Service-only Routen erfordern gesonderte berechtigte Simulator-Schluessel; OpenAPI-Zugriff allein erteilt keine Rechte.</div>
</div>

<div class="portalgrid">
<div class="portalcard">
<div class="eyebrow">Einwohner</div>
<h3>Buergerportal</h3>
<p>Self-Service fuer das eigene Konto und explizit autorisierte Einwohnerfunktionen. Keine stillschweigende Rechteausweitung auf fremde Konten oder Adminfunktionen.</p>
<div class="portalactions">
<a class="action actionlink" href="/api/v1/chatgpt-buergerportal.md" download>Buergerportal-MD</a>
<button class="action" data-audience-target="citizen">Einwohner-Endpunkte</button>
</div>
<div class="portalmeta"><span id="citizenEndpointCount">–</span> Operationen sind aktuell fuer die Zielgruppe Einwohner markiert.</div>
</div>

<div class="portalcard">
<div class="eyebrow">Administration</div>
<h3>Adminbereich</h3>
<p>Konten, Grid, Regionen, Simulatoren, Estates, Security, Audit und administrative Economy-/Discovery-Funktionen. Nur mit ausdruecklich autorisiertem Admin-Principal.</p>
<div class="portalactions">
<a class="action actionlink" href="/api/v1/chatgpt-admin.md" download>Adminbereich-MD</a>
<button class="action" data-audience-target="admin">Admin-Endpunkte</button>
</div>
<div class="portalmeta"><span id="adminEndpointCount">–</span> Operationen sind aktuell fuer die Zielgruppe Administration markiert.</div>
</div>
</div>

<div class="section">
<h2>Automatische Zuordnung</h2>
<p class="sectionlead">ChatGPT soll nicht anhand des URL-Namens raten. Jede Operation wird anhand der im OpenAPI-Dokument veroeffentlichten Metadaten eingeordnet.</p>
<div class="changegrid">
<div class="change"><h3>x-nexverse-audience</h3><p>Ordnet eine Operation Einwohner, Administration und/oder Dienst zu.</p></div>
<div class="change"><h3>x-nexverse-scope</h3><p>Definiert den benoetigten Berechtigungsumfang.</p></div>
<div class="change"><h3>x-nexverse-ai-instruction</h3><p>Enthaelt die operation-spezifische Anweisung fuer ChatGPT/API-Agenten.</p></div>
<div class="change"><h3>x-nexverse-security-constraints</h3><p>Definiert Sicherheits-, Secret-, Audit- und Autorisierungsvorgaben.</p></div>
</div>
</div>
</section>



<section class="page" id="page-adminlogin">
<div class="hero">
<div><div class="eyebrow">Geschützter Verwaltungszugang</div>
<h1>Als OGL-Administrator anmelden</h1>
<p>Dieses Control Center erlaubt die Anmeldung ausschließlich für lokale Grid-Benutzer mit <code>UserLevel >= 200</code> und <code>admin:*</code>. Andere Einwohner können die öffentliche API-Dokumentation weiter lesen, erhalten jedoch keine Administratorsitzung.</p></div>
<div class="pill"><span class="dot" id="adminLoginDot"></span><span id="adminLoginState">Nicht angemeldet</span></div>
</div>
<div class="section">
<h2>Administrator-Konto</h2>
<p class="sectionlead">Melde dich mit den lokalen Avatar-Zugangsdaten an. Benutzername: <code>Vorname.Nachname</code> (oder kurzer Resident-Name). Bei aktivierter Zwei-Faktor-Authentifizierung ist der TOTP-Code erforderlich.</p>
<div class="grid2">
<div><label class="label2" for="adminLoginUsername">Benutzername</label><input id="adminLoginUsername" spellcheck="false" autocomplete="username" placeholder="Vorname.Nachname"></div>
<div><label class="label2" for="adminLoginPassword">Avatar-Passwort</label><input id="adminLoginPassword" type="password" autocomplete="current-password"></div>
<div><label class="label2" for="adminLoginTotp">TOTP (falls aktiviert)</label><input id="adminLoginTotp" autocomplete="one-time-code" inputmode="numeric" placeholder="Optional"></div>
</div>
<div class="row" style="margin-top:16px">
<button class="action" id="adminLoginSubmit">Als Administrator anmelden</button>
<button class="action" id="adminLoginLogout" disabled>Abmelden</button>
</div>
<p class="authnote" id="adminLoginMessage" role="status">Der Login prüft Grid-Benutzername, Passwort und UserLevel ab 200 direkt am OGL-Server. Das Access-Token wird nur im Arbeitsspeicher dieser Seite gehalten, nicht als Cookie oder im Browserspeicher abgelegt.</p>
</div>
</section>

<section class="page" id="page-secrets">
<div class="hero"><div><div class="eyebrow">Systemverwaltung</div><h1>API-Schlüssel &amp; NexBus einrichten</h1>
<p>Erzeuge auf dieser Seite dedizierte Maschinen-API-Schlüssel und einen gemeinsamen NexBus-Signaturschlüssel. Kopiere anschließend die fertigen Zeilen in die geschützte <code>/etc/nexverse/nexverse.env</code> auf den beteiligten Servern.</p></div></div>
<div class="section">
<h2>1. Geschützte Administrator-Sitzung</h2>
<p class="sectionlead">Für die Schlüsselverwaltung muss der GridUser mit UserLevel mindestens 200 angemeldet sein. Einzelne Verwaltungsaktionen behalten ihre bestehenden API-Berechtigungen.</p>
<button class="action" data-goto="adminlogin">Admin-Login öffnen</button>
</div>
<div class="section">
<h2>2. Erforderliche Schlüssel auswählen</h2>
<p class="sectionlead">Bereits eingerichtete Schlüssel nicht unnötig neu erzeugen. Neue API-Schlüssel bleiben zusätzlich zu bestehenden registriert, bis sie ausdrücklich deaktiviert werden.</p>
<div class="changegrid">
<label class="change"><input type="checkbox" id="secretEconomy" style="width:auto" /> <strong>Economy / NV$ inkl. Gruppenhistorie</strong><p><code>economy:read</code>, <code>economy:transfer</code> und <code>groups:read</code>. Für neue Simulator-Schlüssel; bestehende Schlüssel für die Firestorm-Gruppenhistorie gezielt rotieren und erst nach dem Umstellen deaktivieren.</p></label>
<label class="change"><input type="checkbox" id="secretGroupLand" style="width:auto" /> <strong>NexGroups / Gridweite Landabfrage</strong><p><code>groups:read</code>. Eigener Simulator-Dienstschlüssel ausschließlich für die gruppeneigene Parzellensuche über die World API.</p></label>
<label class="change"><input type="checkbox" id="secretExperiences" checked style="width:auto" /> <strong>Experiences / Script</strong><p><code>experiences:script</code>. Eigener Schlüssel für die Simulator-LSL-Funktionen.</p></label>
<label class="change"><input type="checkbox" id="secretExperiencePermissions" style="width:auto" /> <strong>Experiences / Firestorm-Rechte</strong><p><code>experiences:viewer:permissions</code>. Separater Service-Schlüssel für Firestorm Allow/Block/Forget; nur für kontrolliert aktivierte Permission-CAPS.</p></label>
<label class="change"><input type="checkbox" id="secretExperienceManage" style="width:auto" /> <strong>Experiences / Firestorm-Erwerb und Editor</strong><p><code>experiences:viewer:manage</code>. Eigenständiger privilegierter Simulator-Service-Schlüssel für „Erlebnis erwerben“ und die Bearbeitung eigener Experiences; nicht mit anderen Scopes kombinieren.</p></label>
<label class="change"><input type="checkbox" id="secretRegister" style="width:auto" /> <strong>Stadtportal / Avatarregistrierung</strong><p><code>users:register</code>. Dedizierter Schlüssel ausschließlich für die sichere Server-zu-Server-Registrierung durch das Stadtportal-Backend.</p></label>
<label class="change"><input type="checkbox" id="secretNexBus" checked style="width:auto" /> <strong>NexBus / NodeAgent</strong><p>Gemeinsamer zufälliger HMAC-Signaturschlüssel für Robust und alle Simulator-Nodes.</p></label>
</div>
<div class="row" style="margin-top:14px;flex-wrap:wrap">
<button class="action" id="secretGenerateRegistration">Nur Registrierungs-API-Key erstellen</button>
<button class="action" id="secretGenerateGroupLand">Nur Gruppenland-API-Key erstellen</button>
<button class="action" id="secretGeneratePortalIm">NexPortalIM-HMAC-Schlüssel erstellen</button>
</div>
<p class="authnote">Für die Stadtportal-Registrierung genügt dieser einzelne Schlüssel. Der Button ignoriert die Vorauswahl für Experiences und NexBus und verlangt keine NexBus-URLs. <code>NEXVERSE_REGISTER_API_KEY</code> gehört ausschließlich in die geschützte Umgebung des Stadtportal-Backends, niemals in Browser-JavaScript oder auf öffentliche Seiten.</p>
<p class="authnote">Der Gruppenland-Button erstellt ausschließlich <code>groups:read</code>, unabhängig von den anderen Häkchen und ohne NexBus-URLs. <code>NEXVERSE_GROUP_LAND_API_KEY</code> gehört nur in die geschützte Environment-Datei der Simulatoren. Bereits aktive Schlüssel werden vor einer Neuausstellung geprüft.</p>
<p class="authnote"><strong>NexPortalIM:</strong> Dieser HMAC-Schlüssel ist kein Maschinen-API-Key. Der separate Button erzeugt nach Admin-Prüfung einen kryptografisch zufälligen 48-Byte-Schlüssel direkt im Browser. Speichere <code>NEX_PORTAL_IM_SECRET</code> auf ROBUST und allen beteiligten Simulatoren mit identischem Wert. Er wird weder im Browser dauerhaft gespeichert noch an die World API übertragen; verlorene Werte lassen sich nicht wiederherstellen. Bei Rotation alle Hosts koordiniert umstellen.</p>
<p class="authnote">Der NexBus-SharedKey ist kein World-API-Key. Er wird lokal im Browser per kryptografisch sicherem Zufall erzeugt und muss auf allen beteiligten Nodes identisch sein.</p>
<div class="grid2">
<div><label class="label2" for="secretPeerUrl">Simulator → Robust (NexBus-URL)</label><input id="secretPeerUrl" autocomplete="off" spellcheck="false" placeholder="https://intern.example/internal/nexbus/v1/events"></div>
<div><label class="label2" for="secretPeers">Robust → Simulator(en), kommagetrennt</label><input id="secretPeers" autocomplete="off" spellcheck="false" placeholder="https://sim-intern.example/internal/nexbus/v1/events"></div>
</div>
<p class="authnote">Diese beiden URLs sind nur für NexBus erforderlich. Trage die tatsächlich erreichbaren internen Endpunkte ein. Erlaubt ist HTTPS oder HTTP auf Loopback (127.0.0.1 / ::1); schütze die Endpunkte zusätzlich durch Firewall oder privates Netzwerk. Port und Proxy-Weiterleitung müssen zu deiner Installation passen.</p>
<div class="row" style="margin-top:14px;flex-wrap:wrap">
<button class="action" id="secretGenerate">Ausgewählte Schlüssel erstellen</button>
<button class="action" id="secretCopy" disabled>Konfiguration kopieren</button>
<button class="action" id="secretClear">Geheimnisse aus dieser Seite löschen</button>
</div>
<div class="authnote" id="secretStatus" role="status">Noch keine Schlüssel erstellt.</div>
</div>
<div class="section">
<h2>3. In Secret-Datei übernehmen</h2>
<p class="sectionlead">Die Secret-Datei verwendet <code>NAME=WERT</code>, keine INI-Sektionen. Die OGL-Serverdatei heißt üblicherweise <code>/etc/nexverse/nexverse.env</code> (nicht <code>.ini</code>). Bestehende Einträge, insbesondere dein Economy-Key, müssen erhalten bleiben. <strong>Wichtig:</strong> <code>NEXVERSE_REGISTER_API_KEY</code> ausschließlich in die <code>.env</code> des Stadtportal-Backends eintragen – nicht auf ROBUST oder den Simulatoren erforderlich.</p>
<textarea id="secretOutput" readonly autocomplete="off" spellcheck="false" style="min-height:210px" placeholder="Die einmalig angezeigten Schlüssel erscheinen nach erfolgreicher Erzeugung hier."></textarea>
<p class="authnote">Die vollständigen API-Keys zeigt die World API nur beim Erstellen an. Eine Seite oder den Browser erst verlassen, wenn du die neuen Werte sicher übernommen hast. Sie werden weder dauerhaft im Browserspeicher noch in Cookies oder URLs abgelegt.</p>
<p class="authnote">Setze für die Secret-Datei <code>chmod 600 /etc/nexverse/nexverse.env</code>. Starte zunächst Robust mit dem neuen NexBus-Key und dem PR-#104-Code neu, danach die Simulatoren. Bereits vorhandene Prozessvariablen haben Vorrang vor der Datei.</p>
</div>
<div class="section">
<h2>4. Vorhandene API-Schlüssel verwalten</h2>
<p class="sectionlead">Bestehende Schlüssel lassen sich jetzt um Berechtigungen wie <code>statistics:read</code> erweitern, ohne ihren Schlüsselwert auszutauschen. Klicke beim gewünschten Schlüssel auf <strong>Berechtigungen bearbeiten</strong>. Die Liste zeigt nur zulässige Maschinen-Scopes; <code>admin:*</code> und interaktive Scopes bleiben gesperrt. Es erscheinen ausschließlich ID, Name, Scopes und letzte Nutzung, niemals geheime Werte.</p>
<div class="row" style="margin-bottom:14px"><button class="action" id="secretLoadKeys">Schlüsselbestand aktualisieren</button></div>
<div class="authnote" id="secretListStatus" role="status">Als Administrator anmelden, dann Schlüsselbestand laden.</div>
<div id="secretKeyList" class="changegrid"></div>
<p class="authnote">Scope-Änderungen gelten ab der nächsten API-Authentifizierung mit dem <em>gleichen</em> Schlüssel. Der vollständige Secret-Wert wird nicht erneut angezeigt. Die Bearbeitung sowie Aktivierung, Deaktivierung und Löschung verlangen Administratorrechte und werden protokolliert. Für dauerhafte Löschung zuerst deaktivieren.</p>
</div>
<div class="section" id="worldPersonalAuthorization">
<h2>5. Persönliche World-API-Berechtigungen (keine Maschinen-Schlüssel)</h2>
<p class="sectionlead"><strong>Automatisch für alle Bewohner:</strong> Die World API berechnet beim nächsten Login die erforderlichen Lese-Scopes aus der Einwohnerrolle; Administratoren erhalten zusätzlich ihre administrativen Lese-Scopes. Das gilt für bestehende und neue Konten ohne Massenbearbeitung oder 100.000 einzelne API-Aufrufe. Dieser Bereich dient nur zur Prüfung und gezielten Ausnahmen. Maschinen-API-Schlüssel sind unabhängig. Individuelle Änderungen verlangen <code>security:manage</code>; Administrator-Zielkonten zusätzlich <code>admin:*</code>.</p>
<div class="grid2">
<div><label class="label2" for="worldAuthPrincipal">Bewohner-UUID / Principal-ID</label><input id="worldAuthPrincipal" maxlength="36" autocomplete="off" spellcheck="false" placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"></div>
<div><label class="label2">Ausgewähltes Konto</label><div class="authnote" id="worldAuthIdentity">Noch kein Benutzerkonto geladen.</div></div>
</div>
<div class="row" style="margin-top:12px;flex-wrap:wrap">
<button class="action" id="worldAuthUseMe">Eigenes Admin-Konto verwenden</button>
<button class="action" id="worldAuthLoad">Berechtigungen aus World laden</button>
<button class="action" id="worldAuthSave" disabled>Ausgewählte Lese-Scopes speichern</button>
</div>
<div class="authnote" id="worldAuthStatus" role="status">Zuerst eine UUID auswählen und die aktuellen Benutzerberechtigungen laden. Vor dem Speichern wird die Zuweisung erneut geprüft.</div>
<p class="authnote" id="worldAuthExisting">Automatische Lese-Scopes werden als „Durch Rolle zugewiesen“ markiert und können hier nicht deaktiviert werden. Nur zusätzliche explizite Berechtigungen sind bearbeitbar. Rollen und sonstige bestehende Zuweisungen bleiben unangetastet.</p>
<div class="changegrid" id="worldAuthChoices"></div>
<p class="authnote">Die expliziten automatischen Lese-Scopes erscheinen erst bei einem <strong>neu ausgestellten</strong> persönlichen World-Token. <code>admin:*</code> bleibt daneben eine eigene Berechtigung und verleiht Bürgern keine Admin-Rechte. Die World API prüft weiterhin Eigentümer-, Gruppen-, Audience- und Service-Regeln. Nach manuellen Änderungen sind alte Sitzungen ungültig und eine erneute Anmeldung erforderlich.</p>
</div>
</section>

<section class="page" id="page-explorer">
<div class="hero"><div><div class="eyebrow">OpenAPI 3.1</div><h1>API-Endpunkte prüfen</h1><p>Durchsuche den aktuellen Vertrag, prüfe Berechtigungsumfänge und führe autorisierte Anfragen direkt gegen denselben Server aus.</p></div></div>
<div class="explorer">
<div class="endpointlist section">
<div class="filters" id="endpointAudienceFilters">
<button class="filter active" data-endpoint-audience="all">Alle</button>
<button class="filter" data-endpoint-audience="citizen">Einwohner</button>
<button class="filter" data-endpoint-audience="admin">Administration</button>
<button class="filter" data-endpoint-audience="service">Dienst</button>
</div>
<input class="search" id="search" placeholder="Endpunkt, Methode, Berechtigung suchen…">
<div id="endpoints"></div>
</div>
<div class="detail">
<div class="section hidden" id="detail">
<div class="row"><h2 id="title"></h2><span id="deprecated"></span></div>
<p id="summary" class="sectionlead"></p>
<div id="badges"></div>
<div class="grid2">
<div><label class="label2">Anfrage-URL</label><input id="url"></div>
<div><label class="label2">HTTP-Methode</label><input id="method" readonly></div>
</div>
<h3>Parameter</h3><pre id="parameters"></pre>
<h3>Anfrageschema</h3><pre id="requestSchema"></pre>
<h3>KI-/Automatisierungshinweise</h3><pre id="aiInstruction"></pre>
<h3>Antworten / Fehler</h3><pre id="responses"></pre>
</div>
<div class="section">
<h2>Authentifizierung</h2>
<div class="grid2">
<div><label class="label2">Zugriffstoken (Bearer)</label><input id="bearer" type="password" autocomplete="off" placeholder="Optional"></div>
<div><label class="label2">X-NexVerse-Api-Key</label><input id="apiKey" type="password" autocomplete="off" placeholder="Optional"></div>
</div>
<label class="label2">Idempotency-Key</label><input id="idem" autocomplete="off" placeholder="Optional">
</div>
<div class="section hidden" id="runner">
<h2>Live-Anfrage</h2>
<label class="label2">JSON-Anfrageinhalt</label><textarea id="body" spellcheck="false"></textarea>
<div class="row" style="margin-top:10px"><button class="action" id="run">Anfrage ausführen</button><button class="action" id="clear">Zugangsdaten löschen</button></div>
<h3 style="margin-top:18px">Antwort</h3><div id="status" class="small"></div><pre id="output">Noch keine Anfrage ausgeführt.</pre>
</div>
</div>
</div>
</section>
</main>
</div>

<script>
'use strict';
let spec=null,entries=[],selected=null,changes=[],gridLayout=null,gridSelected=null,gridValidation=null,gridSearchResults=[],endpointAudience='all';
let adminSessionToken='',adminSessionExpiry=0,adminNextPage='secrets';
function adminIsActive(){
  return Boolean(adminSessionToken&&Date.now()<adminSessionExpiry);
}
function adminAuthToken(){
  if(adminIsActive())return adminSessionToken;
  adminSessionToken='';
  adminSessionExpiry=0;
  return '';
}
function adminUpdateStatus(message,ok){
  $('adminLoginState').textContent=message;
  $('adminLoginDot').className='dot'+(ok===true?' good':ok===false?' bad':'');
  $('adminLoginLogout').disabled=!adminIsActive();
  $('adminLoginMessage').textContent=message;
}
function adminLogout(){
  adminSessionToken='';
  adminSessionExpiry=0;
  ['adminLoginPassword','adminLoginTotp','bearer','gridBearer','nodesBearer','statsBearer'].forEach(id=>{$(id).value=''});
  secretClear();
  worldAuthReset();
  worldAuthSelfId='';
  adminUpdateStatus('Abgemeldet',null);
  showPage('adminlogin');
  if(window.parent!==window)window.parent.postMessage({type:'ogl-logout'},window.parent.location.origin);
}
async function adminLogin(){
  const username=$('adminLoginUsername').value.trim();
  const password=$('adminLoginPassword').value;
  const totp=$('adminLoginTotp').value.trim();
  if(!username||!password){adminUpdateStatus('Benutzername und Passwort erforderlich.',false);return}
  $('adminLoginSubmit').disabled=true;
  try{
    const body={username,password};
    if(totp)body.totp=totp;
    const res=await fetch('/api/v1/docs',{
      method:'POST',headers:{'Content-Type':'application/json','Accept':'application/json'},
      body:JSON.stringify(body),cache:'no-store',credentials:'same-origin'
    });
    const data=await res.json().catch(()=>null);
    if(!res.ok)throw new Error('HTTP '+res.status+' ('+(data&&data.error||'login_failed')+')');
    if(!data||typeof data!=='object'||Array.isArray(data)||!Number.isInteger(data.user_level))
      throw new Error('Die Anmeldung konnte nicht bestätigt werden. Der Server liefert keine gültige Benutzerantwort.');
    if(Number(data.user_level)<200)
      throw new Error('Zugriff verweigert: UserLevel 200 erforderlich.');
    if(typeof data.access_token!=='string'||data.access_token.length<24)
      throw new Error('Anmeldung nicht möglich: Der Server konnte keine Sitzung erstellen.');
    adminSessionToken=data.access_token;
    adminSessionExpiry=Date.now()+Math.max(0,Number(data.expires_in||0)-30)*1000;
    if(!adminIsActive())throw new Error('Administratorsitzung bereits abgelaufen.');
    adminUpdateStatus('Angemeldet · UserLevel '+data.user_level,true);
    showPage(adminNextPage||'secrets');
  }catch(err){
    adminSessionToken='';adminSessionExpiry=0;
    adminUpdateStatus('Anmeldung fehlgeschlagen: '+String(err.message||err),false);
  }finally{
    $('adminLoginPassword').value='';
    $('adminLoginTotp').value='';
    $('adminLoginSubmit').disabled=false;
  }
}

const $=id=>document.getElementById(id);
const methods=new Set(['get','post','put','patch','delete','options','head']);
const statusLabels={released:'Veröffentlicht',active:'Aktiv',advanced:'Vorgezogen',started:'Begonnen',foundation:'Grundlage',planned:'Geplant',implemented:'Umgesetzt',development:'Entwicklung',release:'Veröffentlicht'};
const categoryLabels={analytics:'Statistik',observability:'Beobachtbarkeit',security:'Sicherheit',api:'API',identity:'Identität',platform:'Plattform',runtime:'Laufzeit',scripting:'Skripting',regions:'Regionen'};
const audienceLabels={citizen:'Einwohner',admin:'Administration',service:'Dienst'};
const languageLabels={'de-DE':'Deutsch (Deutschland)'};
const accountStateLabels={active:'aktiv',locked:'gesperrt',banned:'gebannt',deactivated:'deaktiviert',provisioning:'Provisionierung',provisioning_failed:'Provisionierung fehlgeschlagen'};
function statusLabel(value){return statusLabels[value]||value||''}
function categoryLabel(value){return categoryLabels[value]||value||''}
function audienceLabel(value){return audienceLabels[value]||value||''}
function languageLabel(value){return languageLabels[value]||value||''}

function showPage(name){
  if(['secrets','nodes','grid'].includes(name)&&!adminIsActive()){
    adminNextPage=name;
    name='adminlogin';
    adminUpdateStatus('Anmeldung mit UserLevel >= 200 erforderlich.',null);
  }
  document.querySelectorAll('.page').forEach(x=>x.classList.toggle('active',x.id==='page-'+name));
  document.querySelectorAll('.navbtn').forEach(x=>x.classList.toggle('active',x.dataset.page===name));
  window.scrollTo({top:0,behavior:'smooth'});
}
document.querySelectorAll('.navbtn').forEach(x=>x.addEventListener('click',()=>showPage(x.dataset.page)));
document.querySelectorAll('[data-goto]').forEach(x=>x.addEventListener('click',()=>showPage(x.dataset.goto)));

function setEndpointAudience(audience){
  endpointAudience=audience||'all';
  document.querySelectorAll('[data-endpoint-audience]').forEach(x=>x.classList.toggle('active',x.dataset.endpointAudience===endpointAudience));
  renderEndpointList();
}
function openAudienceEndpoints(audience){
  $('search').value='';
  setEndpointAudience(audience);
  showPage('explorer');
}
document.querySelectorAll('[data-endpoint-audience]').forEach(x=>x.addEventListener('click',()=>setEndpointAudience(x.dataset.endpointAudience)));
document.querySelectorAll('[data-audience-target]').forEach(x=>x.addEventListener('click',()=>openAudienceEndpoints(x.dataset.audienceTarget)));

function resolveSchema(schema){
  if(!schema)return null;
  if(schema.$ref){
    const name=schema.$ref.split('/').pop();
    return spec.components?.schemas?.[name]||schema;
  }
  return schema;
}
function exampleFor(schema,depth=0){
  if(!schema||depth>5)return null;
  if(schema.$ref)return exampleFor(resolveSchema(schema),depth+1);
  if(schema.example!==undefined)return schema.example;
  if(schema.enum?.length)return schema.enum[0];
  const type=Array.isArray(schema.type)?schema.type.find(x=>x!=='null'):schema.type;
  if(type==='object'||schema.properties){
    const o={};
    for(const [k,v] of Object.entries(schema.properties||{})){
      if((schema.required||[]).includes(k)||depth<2)o[k]=exampleFor(v,depth+1);
    }
    return o;
  }
  if(type==='array')return [];
  if(type==='boolean')return false;
  if(type==='integer'||type==='number')return schema.minimum??0;
  if(schema.format==='uuid')return '00000000-0000-0000-0000-000000000000';
  if(schema.format==='date-time')return new Date(0).toISOString();
  return '';
}
function requestSchema(op){return op.requestBody?.content?.['application/json']?.schema||null}
function responseSchemas(op){
  const result={};
  for(const [code,response] of Object.entries(op.responses||{})){
    result[code]={description:response.description||'',schema:resolveSchema(response.content?.['application/json']?.schema)};
  }
  return result;
}
function buildEntries(){
  entries=[];
  for(const [path,item] of Object.entries(spec.paths||{})){
    for(const [method,op] of Object.entries(item||{})){
      if(!methods.has(method.toLowerCase()))continue;
      entries.push({path,method:method.toUpperCase(),op});
    }
  }
  entries.sort((a,b)=>a.path.localeCompare(b.path)||a.method.localeCompare(b.method));
}
function renderEndpointList(){
  const q=$('search').value.trim().toLowerCase();
  const host=$('endpoints');host.replaceChildren();
  for(const entry of entries){
    const scope=entry.op['x-nexverse-scope']||'';
    const audiences=entry.op['x-nexverse-audience']||[];
    const hay=[entry.method,entry.path,entry.op.summary||'',scope,...audiences].join(' ').toLowerCase();
    if(endpointAudience!=='all'&&!audiences.includes(endpointAudience))continue;
    if(q&&!hay.includes(q))continue;
    const div=document.createElement('div');div.className='endpoint'+(selected===entry?' active':'');
    const top=document.createElement('div');
    const m=document.createElement('span');m.className='method';m.textContent=entry.method;
    const p=document.createElement('span');p.className='path';p.textContent=entry.path;
    top.append(m,p);div.append(top);
    const summary=document.createElement('div');summary.className='small';summary.textContent=entry.op.summary||'';
    div.append(summary);div.addEventListener('click',()=>selectEntry(entry));host.append(div);
  }
}
function selectEntry(entry){
  selected=entry;renderEndpointList();$('detail').classList.remove('hidden');$('runner').classList.remove('hidden');
  $('title').textContent=entry.method+' '+entry.path;$('summary').textContent=entry.op.summary||'';$('method').value=entry.method;$('url').value=entry.path;
  $('deprecated').textContent=entry.op.deprecated?'VERALTET':'';$('deprecated').className=entry.op.deprecated?'tag':'';
  const badges=$('badges');badges.replaceChildren();
  const scope=entry.op['x-nexverse-scope'];
  if(scope){const b=document.createElement('span');b.className='tag';b.textContent='Berechtigung: '+scope;badges.append(b)}
  for(const audience of entry.op['x-nexverse-audience']||[]){const b=document.createElement('span');b.className='tag';b.textContent='Zielgruppe: '+audienceLabel(audience);badges.append(b)}
  for(const sec of entry.op.security||[]){const names=Object.keys(sec);if(names.length){const b=document.createElement('span');b.className='tag';b.textContent='Authentifizierung: '+names.join(' oder ');badges.append(b)}}
  $('parameters').textContent=JSON.stringify(entry.op.parameters||[],null,2);
  const req=requestSchema(entry.op);$('requestSchema').textContent=req?JSON.stringify(resolveSchema(req),null,2):'Kein JSON-Anfrageinhalt.';
  $('aiInstruction').textContent=JSON.stringify({zweck:entry.op['x-nexverse-purpose']||entry.op.summary||'',zielgruppe:(entry.op['x-nexverse-audience']||[]).map(audienceLabel),anweisung:entry.op['x-nexverse-ai-instruction']||'',sicherheitsvorgaben:entry.op['x-nexverse-security-constraints']||[],veraltet:!!entry.op.deprecated},null,2);
  $('responses').textContent=JSON.stringify(responseSchemas(entry.op),null,2);
  const ex=exampleFor(req);$('body').value=req&&ex!==null?JSON.stringify(ex,null,2):'';
}
function makeChange(item){
  const box=document.createElement('article');box.className='change';
  const head=document.createElement('div');head.className='changehead';
  const title=document.createElement('h3');title.textContent=item.title||'Änderung';
  const status=document.createElement('span');status.className='tag '+(item.status==='implemented'?'good':'');status.textContent=statusLabel(item.status);
  head.append(title,status);box.append(head);
  const summary=document.createElement('p');summary.textContent=item.summary||'';box.append(summary);
  const tags=document.createElement('div');tags.className='meta';
  for(const value of [item.date,item.version]){if(value){const t=document.createElement('span');t.className='tag';t.textContent=value;tags.append(t)}}if(item.category){const t=document.createElement('span');t.className='tag';t.textContent=categoryLabel(item.category);tags.append(t)}
  box.append(tags);
  if(item.endpoints?.length){const eps=document.createElement('div');eps.className='small';eps.style.marginTop='8px';eps.textContent='Betrifft: '+item.endpoints.join(', ');box.append(eps)}
  return box;
}
function renderChanges(category){
  const list=category&&category!=='all'?changes.filter(x=>x.category===category):changes;
  const all=$('allChanges');all.replaceChildren();
  if(!list.length){all.innerHTML='<div class="empty">Keine Änderungen für diesen Filter.</div>';return}
  list.forEach(x=>all.append(makeChange(x)));
}
function renderChangeFilters(){
  const host=$('changeFilters');host.replaceChildren();
  const cats=['all',...new Set(changes.map(x=>x.category).filter(Boolean))];
  cats.forEach((cat,index)=>{
    const b=document.createElement('button');b.className='filter'+(index===0?' active':'');b.textContent=cat==='all'?'Alle':categoryLabel(cat);
    b.addEventListener('click',()=>{host.querySelectorAll('.filter').forEach(x=>x.classList.remove('active'));b.classList.add('active');renderChanges(cat)});
    host.append(b);
  });
}
function renderVersions(){
  const host=$('versionList');host.replaceChildren();
  const history=spec['x_nexverse_version_history']||[];
  if(!history.length){host.innerHTML='<div class="empty">Keine Versionshistorie veröffentlicht.</div>';return}
  history.forEach(item=>{
    const row=document.createElement('div');row.className='version';
    const left=document.createElement('div');left.innerHTML='<strong></strong><span class="small"></span>';left.querySelector('strong').textContent='API '+(item.api_version||'');left.querySelector('span').textContent=(item.published?'Veröffentlicht: '+item.published:'Status: '+statusLabel(item.status));
    const right=document.createElement('div');const h=document.createElement('strong');h.textContent=[item.server_line,item.codename].filter(Boolean).join(' · ');right.append(h);
    const p=document.createElement('div');p.className='small';p.textContent=item.compatibility||'';right.append(p);
    if(item.highlights?.length){const ul=document.createElement('ul');item.highlights.forEach(v=>{const li=document.createElement('li');li.textContent=v;ul.append(li)});right.append(ul)}
    row.append(left,right);host.append(row);
  });
}
function renderRoadmap(){
  const roadmap=spec['x_nexverse_roadmap'];
  const host=$('roadmapList');host.replaceChildren();
  if(!roadmap?.milestones?.length){host.innerHTML='<div class="empty">Kein maschinenlesbarer Entwicklungsplan-Status veröffentlicht.</div>';return}
  $('roadmapCurrent').textContent=roadmap.current_milestone||'–';$('roadmapCodename').textContent=roadmap.current_codename?('Codename: '+roadmap.current_codename):'Aktive Entwicklung';
  const milestones=roadmap.milestones;
  $('roadmapActiveCount').textContent=milestones.filter(x=>x.status==='active').length;
  $('roadmapStartedCount').textContent=milestones.filter(x=>['advanced','started','foundation'].includes(x.status)).length;
  $('roadmapPlannedCount').textContent=milestones.filter(x=>x.status==='planned').length;
  const legend=$('roadmapLegend');legend.replaceChildren();
  for(const [key,value] of Object.entries(roadmap.status_model||{})){
    const item=document.createElement('span');item.className='tag';item.textContent=statusLabel(key)+' — '+value;legend.append(item);
  }
  milestones.forEach(item=>{
    const box=document.createElement('article');box.className='milestone'+(item.version===roadmap.current_milestone?' current':'');
    const head=document.createElement('div');head.className='milestonehead';
    const title=document.createElement('div');const h=document.createElement('h3');h.textContent=item.version+' · '+item.title;title.append(h);
    if(item.codename){const code=document.createElement('div');code.className='small';code.textContent='Codename: '+item.codename;title.append(code)}
    const status=document.createElement('span');status.className='status status-'+item.status;status.textContent=statusLabel(item.status);
    head.append(title,status);box.append(head);
    const p=document.createElement('p');p.textContent=item.summary||'';box.append(p);
    if(item.checklist&&item.checklist.total>0){
      const wrap=document.createElement('div');wrap.className='progressline';
      const meta=document.createElement('div');meta.className='progressmeta';
      const done=document.createElement('span');done.textContent='Entwicklungsplan-Checkliste: '+item.checklist.completed+' / '+item.checklist.total+' erledigt';
      const open=document.createElement('span');open.textContent=item.checklist.open+' offen';meta.append(done,open);
      const bar=document.createElement('div');bar.className='progressbar';const fill=document.createElement('span');
      const pct=Math.max(0,Math.min(100,(item.checklist.completed/item.checklist.total)*100));fill.style.width=pct+'%';bar.append(fill);wrap.append(meta,bar);box.append(wrap);
    }
    if(item.evidence?.length){
      const ul=document.createElement('ul');ul.className='evidence';item.evidence.forEach(v=>{const li=document.createElement('li');li.textContent=v;ul.append(li)});box.append(ul);
    }
    host.append(box);
  });
}

function statText(id,value){$(id).textContent=value===null||value===undefined?'–':String(value)}
function appendStatsRow(hostId,values){
  const host=$(hostId),tr=document.createElement('tr');
  values.forEach(value=>{const td=document.createElement('td');td.textContent=value===null||value===undefined||value===''?'–':String(value);tr.append(td)});
  host.append(tr);
}
function formatStatDate(value){
  if(!value)return '–';
  const d=new Date(value);
  return Number.isNaN(d.getTime())?String(value):d.toLocaleString('de-DE');
}
function renderStatistics(data){
  const residents=data.residents||{},hg=data.hypergrid||{},online=data.online||{};
  const protectedDetails=data.protected_details===true;
  statText('statRegistered',residents.registered_total);
  statText('statOnlineTotal',online.total);
  statText('statResidentsOnline',residents.online_now);
  statText('statActive7',residents.active_last_7_days);
  statText('statActive30',residents.active_last_30_days);
  statText('statHgOnline',hg.online_now);
  statText('statHgKnown',hg.known_visitors_total);
  statText('statHg30',hg.visitors_last_30_days);
  $('statsOnlineBody').replaceChildren();
  (online.users||[]).forEach(user=>appendStatsRow('statsOnlineBody',[
    user.name,
    user.type==='hypergrid'?'Hypergrid':'Einwohner',
    user.region_name||user.region_id,
    user.home_grid||'lokal',
    formatStatDate(user.login_at)
  ]));
  if(!(online.users||[]).length)appendStatsRow('statsOnlineBody',[protectedDetails?'Niemand online':'Geschützte Details – statistics:read erforderlich','','','','']);
  $('statsHgBody').replaceChildren();
  (data.hypergrid_home_grids||[]).forEach(item=>appendStatsRow('statsHgBody',[
    item.home_grid,item.known_visitors,item.online_now,item.last_7_days,item.last_30_days
  ]));
  if(!(data.hypergrid_home_grids||[]).length){
    const originStatus=(data.data_quality||{}).hypergrid_origin_status;
    const originLabel=!protectedDetails?'Geschützte Details – statistics:read erforderlich':
      originStatus==='unavailable'?'GridUser-Datenquelle nicht verfügbar':
      originStatus==='incomplete_home_uri'?'Gespeicherte HG-Besucher ohne zuordenbare HomeURI':
      originStatus==='no_recorded_visitors'?'Noch keine HG-Besucher in GridUser erfasst':
      'Keine auswertbaren Hypergrid-Herkunftszeilen';
    appendStatsRow('statsHgBody',[originLabel,'','','','']);
  }
  $('statsRegionBody').replaceChildren();
  (data.regions||[]).forEach(item=>appendStatsRow('statsRegionBody',[
    item.region_name||item.region_id,item.online_total,item.residents,item.hypergrid
  ]));
  if(!(data.regions||[]).length)appendStatsRow('statsRegionBody',[protectedDetails?'Keine Online-Belegung':'Geschützte Details – statistics:read erforderlich','','','']);
  const states=Object.fromEntries(Object.entries(data.account_states||{}).map(([k,v])=>[accountStateLabels[k]||k,v]));
  const quality=data.data_quality||{};
  const qualityValues={
    'UserAccountService aggregate':'UserAccountService-Aggregat',
    'GridUser/UserAccount fallback':'GridUser-/UserAccount-Ersatzquelle',
    'PresenceService':'PresenceService',
    'GridUser Online flag with five-day stale guard':'GridUser-Online-Status mit Fünf-Tage-Schutz gegen veraltete Daten',
    'Unique identities use the latest GridUser Login/Logout timestamps.':'Eindeutige Identitäten verwenden die neuesten GridUser-Zeitstempel für Anmeldung/Abmeldung.',
    'Known Hypergrid counts are distinct visitor/home-grid identities, not cumulative visit sessions.':'Bekannte Hypergrid-Zahlen sind eindeutige Besucher-/Heimat-Grid-Identitäten und keine kumulierten Besuchssitzungen.'
  };
  const qv=value=>qualityValues[value]||value;
  $('statsQuality').textContent=JSON.stringify({
    erzeugt_am:data.generated_at,
    kontostatus:states,
    datenqualitaet:{
      vollstaendig:quality.complete,
      griduser_datenquelle:quality.griduser_source,
      hg_herkunftsstatus:quality.hypergrid_origin_status,
      hg_herkunftszeilen:quality.hypergrid_origin_rows,
      hg_ohne_heimat_uri:quality.hypergrid_identities_without_home_uri,
      fehlende_kennzahlen:quality.missing_metrics||[],
      registrierungsquelle:qv(quality.registered_source),
      online_quelle:qv(quality.online_source),
      historische_aktivitaet:qv(quality.historical_activity),
      hypergrid_historie:qv(quality.hypergrid_history),
      exakte_besuchssitzungen_verfuegbar:quality.exact_visit_sessions_available
    }
  },null,2);
}
async function loadStatistics(){
  const headers={'Accept':'application/json'};
  // Do not reuse arbitrary credentials typed into the API Explorer:
  // expired/incorrect bearer or API keys turn otherwise public aggregates
  // into 401/403. Only the verified admin session or explicit stats
  // credentials should be sent to the statistics endpoint.
  const bearer=$('statsBearer').value.trim()||adminAuthToken();
  const key=$('statsApiKey').value.trim();
  if(bearer)headers.Authorization='Bearer '+bearer;
  if(key)headers['X-NexVerse-Api-Key']=key;
  $('statsStatus').textContent='Lade Statistik…';$('statsDot').className='dot';
  try{
    const res=await fetch('/api/v1/statistics/summary',{headers,cache:'no-store',credentials:'same-origin'});
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    if(!res.ok){
      $('statsStatus').textContent=res.status===401||res.status===403?'Authentifizierung erforderlich':'Statistik nicht verfügbar';
      $('statsDot').className='dot bad';
      $('statsQuality').textContent=data?JSON.stringify(data,null,2):txt;
      return;
    }
    renderStatistics(data||{});
    $('statsStatus').textContent='Live · '+(data.data_quality&&data.data_quality.complete===false?'Teilweise (Datenquelle fehlt)':data.protected_details?'Details':'Aggregiert')+' · '+formatStatDate(data.generated_at);
    $('statsDot').className='dot good';
  }catch(err){
    $('statsStatus').textContent='Statistik nicht erreichbar';$('statsDot').className='dot bad';$('statsQuality').textContent=String(err);
  }
}



function nodeHeaders(){
  const headers={'Accept':'application/json'};
  const bearer=$('nodesBearer').value.trim()||$('gridBearer').value.trim()||$('bearer').value.trim()||adminAuthToken();
  const key=$('nodesApiKey').value.trim()||$('gridApiKey').value.trim()||$('apiKey').value.trim();
  if(bearer)headers.Authorization='Bearer '+bearer;
  if(key)headers['X-NexVerse-Api-Key']=key;
  return headers;
}
function formatBytes(value){
  const n=Number(value||0);
  if(!Number.isFinite(n)||n<=0)return '0 B';
  const units=['B','KiB','MiB','GiB','TiB'];let v=n,i=0;
  while(v>=1024&&i<units.length-1){v/=1024;i++}
  return (i===0?Math.round(v):v.toFixed(v>=10?1:2))+' '+units[i];
}
function formatDuration(seconds){
  let s=Math.max(0,Number(seconds||0));
  const d=Math.floor(s/86400);s-=d*86400;
  const h=Math.floor(s/3600);s-=h*3600;
  const m=Math.floor(s/60);
  return (d?d+'d ':'')+(h?h+'h ':'')+m+'m';
}
function setNodesStatus(text,kind){
  $('nodesStatus').textContent=text;
  $('nodesDot').className='dot'+(kind?' '+kind:'');
}
function renderNodes(data){
  const nodes=data.nodes||[];
  $('nodesCount').textContent=nodes.length;
  $('nodesOnline').textContent=nodes.filter(x=>x.state==='online').length;
  $('nodesStale').textContent=nodes.filter(x=>x.state==='stale').length;
  $('nodesOffline').textContent=nodes.filter(x=>x.state==='offline').length;
  $('nodesTransport').textContent=(data.transport_enabled?'Verteilter NexBus-Transport aktiv':'Verteilter NexBus-Transport deaktiviert')+' · Stale nach '+(data.stale_after_seconds||'–')+' Sekunden ohne Heartbeat.';
  const body=$('nodesBody');body.replaceChildren();
  if(!nodes.length){
    const tr=document.createElement('tr');const td=document.createElement('td');td.colSpan=9;td.className='small';td.textContent=data.transport_enabled?'Noch keine NodeAgent-Heartbeats empfangen.':'NexBus-Transport ist deaktiviert; keine Remote-NodeAgents werden empfangen.';tr.append(td);body.append(tr);
  }
  nodes.forEach(node=>{
    const tr=document.createElement('tr');tr.className='noderow';
    const vals=[
      node.node_id||'',
      node.hostname||'',
      node.state||'',
      node.server_version||'',
      node.region_count??0,
      node.agent_count??0,
      formatDuration(node.uptime_seconds),
      formatBytes(node.working_set_bytes),
      node.last_seen?new Date(node.last_seen).toLocaleString('de-DE'):'–'
    ];
    vals.forEach((value,index)=>{
      const td=document.createElement('td');td.textContent=String(value);
      if(index===2)td.className='node-state-'+(node.state||'');
      tr.append(td);
    });
    tr.addEventListener('click',()=>{$('nodeDetail').textContent=JSON.stringify(node,null,2)});
    body.append(tr);
  });
  setNodesStatus('Live · '+nodes.length+' Nodes','good');
}
async function loadNodes(){
  setNodesStatus('Lade Simulatoren…','');
  try{
    const res=await fetch('/api/v1/nodes',{headers:nodeHeaders(),cache:'no-store',credentials:'same-origin'});
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    if(!res.ok){
      setNodesStatus(res.status===401||res.status===403?'simulators:read erforderlich':'Node-Registry nicht verfügbar','bad');
      $('nodeDetail').textContent=data?JSON.stringify(data,null,2):txt;
      return;
    }
    renderNodes(data||{});
  }catch(err){
    setNodesStatus('Node-Registry nicht erreichbar','bad');$('nodeDetail').textContent=String(err);
  }
}

let oglLicenseNodes=[];
function selectedLicense(){
  const node=$('licenseNode').value;
  return oglLicenseNodes.find(x=>x.node_id===node);
}
function showFeatureLicense(){
  const entry=selectedLicense();
  for(const [key,field] of [['oar_import','licenseOarImport'],['oar_export','licenseOarExport'],
        ['iar_import','licenseIarImport'],['iar_export','licenseIarExport']])
    $(field).checked=!!entry?.[key];
  $('licenseKey').value='';
  $('licenseStatus').textContent=entry?
    'Node: '+entry.node_id+' · '+entry.state+' · Revision '+entry.revision+
    (entry.assigned?' · Richtlinie gespeichert':' · noch keine Richtlinie'): 'Kein Simulator gewählt.';
}
async function loadFeatureLicenses(){
  const status=$('licenseStatus');status.textContent='Lade Freigaben …';
  try{
    const response=await fetch('/api/v1/feature-control/nodes',{
      method:'GET',headers:nodeHeaders(),cache:'no-store',credentials:'same-origin'});
    const data=await response.json().catch(()=>({}));
    if(response.status===404 && (data.error==='not_found'||!data.error)){
      oglLicenseNodes=[];
      $('licenseNode').replaceChildren();
      status.textContent='Lizenzmodul nicht aktiviert. Für signierte OAR/IAR-Node-Freigaben [OGLFeatureControl] Enabled = true und einen gültigen ES256-Schlüssel auf Robust konfigurieren. Ohne aktiviertes Modul ist dieser Bereich nicht verfügbar.';
      return;
    }
    if(response.status===401||response.status===403){
      status.textContent='Lizenzdienst vorhanden, aber Zugriff verweigert. Die Freigabe benötigt einen API-Zugang mit simulators:read und GridUser UserLevel 200 oder höher.';
      return;
    }
    if(!response.ok)throw Error(data.message||data.error||('HTTP '+response.status));
    oglLicenseNodes=data.nodes||[];
    const selection=$('licenseNode'),previous=selection.value;
    selection.replaceChildren();
    oglLicenseNodes.forEach(node=>{
      const option=document.createElement('option');option.value=node.node_id;
      option.textContent=node.node_id+' · '+node.state;selection.append(option);
    });
    if(oglLicenseNodes.some(x=>x.node_id===previous))selection.value=previous;
    showFeatureLicense();
    if(!oglLicenseNodes.length)status.textContent='Noch keine Simulator-Nodes registriert.';
  }catch(error){status.textContent='Lizenzdienst nicht verfügbar: '+String(error)}
}
async function saveFeatureLicense(revoke=false){
  const node=$('licenseNode').value,status=$('licenseStatus');
  if(!node){status.textContent='Zuerst eine NodeID auswählen.';return;}
  if(revoke && !confirm('Alle Archivfunktionen für '+node+' sperren?'))return;
  const body={
    oar_import:!revoke&&$('licenseOarImport').checked,
    oar_export:!revoke&&$('licenseOarExport').checked,
    iar_import:!revoke&&$('licenseIarImport').checked,
    iar_export:!revoke&&$('licenseIarExport').checked
  };
  if($('licenseKey').value.trim()&&!revoke)body.license_key=$('licenseKey').value.trim();
  status.textContent='Speichere Richtlinie für '+node+' …';
  try{
    const headers={...nodeHeaders(),'Content-Type':'application/json'};
    const response=await fetch('/api/v1/feature-control/nodes/'+encodeURIComponent(node),{
      method:'PATCH',headers,credentials:'same-origin',body:JSON.stringify(body)});
    const data=await response.json();
    if(!response.ok)throw Error(data.message||data.error||('HTTP '+response.status));
    await loadFeatureLicenses();
    status.textContent='Richtlinie für '+node+' aktualisiert und per NexBus übertragen.';
  }catch(error){status.textContent='Freigabe abgelehnt: '+String(error)}
}

function gridHeaders(){
  const headers={'Accept':'application/json'};
  const bearer=$('gridBearer').value.trim()||$('bearer').value.trim()||adminAuthToken();
  const key=$('gridApiKey').value.trim()||$('apiKey').value.trim();
  if(bearer)headers.Authorization='Bearer '+bearer;
  if(key)headers['X-NexVerse-Api-Key']=key;
  return headers;
}
function setGridStatus(text,kind){
  $('gridStatus').textContent=text;
  $('gridDot').className='dot'+(kind?' '+kind:'');
}
function gridViewport(){
  const minX=Math.max(0,parseInt($('gridMinX').value||'0',10)||0);
  const minY=Math.max(0,parseInt($('gridMinY').value||'0',10)||0);
  const width=Math.min(128,Math.max(1,parseInt($('gridWidth').value||'24',10)||24));
  const height=Math.min(128,Math.max(1,parseInt($('gridHeight').value||'18',10)||18));
  $('gridMinX').value=minX;$('gridMinY').value=minY;$('gridWidth').value=width;$('gridHeight').value=height;
  return {minX,minY,maxX:minX+width-1,maxY:minY+height-1,width,height};
}
function gridCellKey(x,y){return x+':'+y}
// Tile images are decorative; the authoritative occupancy/placement data
// remains the authenticated Grid Control API. Cap requests for large viewports.
function applyGridImagery(){
  const enabled=$('gridShowWarp3d').checked;
  let loaded=0, skipped=0;
  document.querySelectorAll('#gridBoard .gridcell').forEach(cell=>{
    if(enabled&&cell.classList.contains('occupied')){
      if(loaded<200){
        const x=Number(cell.dataset.x),y=Number(cell.dataset.y);
        cell.style.backgroundImage='linear-gradient(rgba(6,18,32,.22),rgba(6,18,32,.22)),url("/api/v1/deutschland/tiles/1/'+x+'/'+y+'.jpg")';
        cell.style.backgroundSize='cover';
        loaded++;
      }else{cell.style.backgroundImage='';skipped++;}
    }else cell.style.backgroundImage='';
  });
  $('gridMapTileStatus').textContent=!enabled?'Kartenbilder ausgeblendet.'
    :loaded+' Warp3D-Kacheln eingeblendet.'+(skipped?' Für große Ausschnitte sind maximal 200 Bilder gleichzeitig aktiv.':'');
}
function applyGridFilter(){
  const filter=$('gridFilter').value;
  document.querySelectorAll('#gridBoard .gridcell').forEach(cell=>{
    cell.classList.toggle('dimmed',filter!=='all'&&!cell.classList.contains(filter));
  });
}
function clearGridPreview(){
  document.querySelectorAll('#gridBoard .gridcell').forEach(cell=>cell.classList.remove('preview-ok','preview-bad','selected'));
}
function renderGridCellDetail(cell){
  $('gridCellDetail').textContent=JSON.stringify({
    grid:{x:cell.grid_x,y:cell.grid_y},
    welt_meter:{x:cell.world_x,y:cell.world_y},
    status:cell.status,
    region_id:cell.region_id||null,
    region_name:cell.region_name||null,
    region_ids:cell.region_ids||[]
  },null,2);
}
function markGridPreview(validation){
  document.querySelectorAll('#gridBoard .gridcell').forEach(cell=>cell.classList.remove('preview-ok','preview-bad'));
  if(!validation?.footprint)return;
  const cls=validation.valid?'preview-ok':'preview-bad';
  for(let y=validation.footprint.min_y;y<=validation.footprint.max_y;y++){
    for(let x=validation.footprint.min_x;x<=validation.footprint.max_x;x++){
      const cell=document.querySelector('#gridBoard .gridcell[data-key="'+gridCellKey(x,y)+'"]');
      if(cell)cell.classList.add(cls);
    }
  }
}
async function validateGridPlacement(){
  if(!gridSelected){$('gridPlacementDetail').textContent='Bitte zuerst eine Rasterzelle auswählen.';return null}
  const sizeX=parseInt($('gridRegionSizeX').value||'256',10);
  const sizeY=parseInt($('gridRegionSizeY').value||'256',10);
  if(sizeX<256||sizeX>4096||sizeX%256||sizeY<256||sizeY>4096||sizeY%256){
    $('gridPlacementDetail').textContent='Regionsgrößen müssen zwischen 256 und 4096 Metern liegen und durch 256 teilbar sein.';return null;
  }
  const qs=new URLSearchParams({x:gridSelected.grid_x,y:gridSelected.grid_y,size_x:sizeX,size_y:sizeY});
  $('gridPlacementDetail').textContent='Prüfe Platzierung…';
  try{
    const res=await fetch('/api/v1/grid/validate-placement?'+qs,{headers:gridHeaders(),cache:'no-store',credentials:'same-origin'});
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    if(!res.ok){gridValidation=null;$('gridPlacementDetail').textContent=data?JSON.stringify(data,null,2):txt;return null}
    gridValidation=data;markGridPreview(data);
    $('gridPlacementDetail').textContent=JSON.stringify({
      gueltig:data.valid,
      grund:data.reason,
      ursprung:data.origin,
      groesse:data.size,
      footprint:data.footprint,
      konflikte:(data.conflicts||[]).map(x=>({region_id:x.region_id,name:x.name,occupied:x.occupied}))
    },null,2);
    return data;
  }catch(err){gridValidation=null;$('gridPlacementDetail').textContent=String(err);return null}
}
function selectGridCell(cell){
  gridSelected=cell;
  document.querySelectorAll('#gridBoard .gridcell').forEach(x=>x.classList.remove('selected'));
  const el=document.querySelector('#gridBoard .gridcell[data-key="'+gridCellKey(cell.grid_x,cell.grid_y)+'"]');
  if(el)el.classList.add('selected');
  renderGridCellDetail(cell);
  if(cell.region_id){
    $('gridMoveRegionId').value=cell.region_id;
    $('gridLifecycleRegionId').value=cell.region_id;
    const region=(gridLayout?.regions||[]).find(x=>x.region_id===cell.region_id);
    if(region?.node_id)$('gridLifecycleNodeId').value=region.node_id;
  }
  validateGridPlacement();
}
function renderGridLayout(data){
  gridLayout=data;gridSelected=null;gridValidation=null;
  $('gridCellDetail').textContent='Keine Zelle ausgewählt.';
  $('gridPlacementDetail').textContent='Noch keine Platzierung geprüft.';
  const board=$('gridBoard');board.replaceChildren();
  const bounds=data.bounds||{};
  const cells=new Map((data.cells||[]).map(cell=>[gridCellKey(cell.grid_x,cell.grid_y),cell]));
  const width=(bounds.max_x??-1)-(bounds.min_x??0)+1;
  board.style.gridTemplateColumns='repeat('+Math.max(1,width)+', var(--cell))';
  board.style.setProperty('--cell',$('gridCellPixels').value+'px');
  for(let y=bounds.max_y;y>=bounds.min_y;y--){
    for(let x=bounds.min_x;x<=bounds.max_x;x++){
      const cell=cells.get(gridCellKey(x,y));
      if(!cell)continue;
      const b=document.createElement('button');
      b.type='button';b.className='gridcell '+cell.status;b.dataset.key=gridCellKey(x,y);b.dataset.x=x;b.dataset.y=y;
      if(cell.region_name)b.dataset.regionName=cell.region_name;
      b.title='Grid '+x+'/'+y+' · Welt '+cell.world_x+'/'+cell.world_y+' m · '+cell.status+(cell.region_name?' · '+cell.region_name:'');
      b.addEventListener('mouseenter',()=>renderGridCellDetail(cell));
      b.addEventListener('click',()=>selectGridCell(cell));
      board.append(b);
    }
  }
  applyGridFilter();
  applyGridImagery();
  const counts=data.counts||{};
  setGridStatus('Live · '+(counts.regions||0)+' Regionen · '+(counts.free||0)+' frei · '+(counts.occupied||0)+' belegt','good');
}
async function loadGridLayout(){
  const v=gridViewport();
  const qs=new URLSearchParams({min_x:v.minX,max_x:v.maxX,min_y:v.minY,max_y:v.maxY});
  setGridStatus('Lade Raster…','');
  try{
    const res=await fetch('/api/v1/grid/layout?'+qs,{headers:gridHeaders(),cache:'no-store',credentials:'same-origin'});
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    if(!res.ok){
      setGridStatus(res.status===401||res.status===403?'regions:read erforderlich':'Grid nicht verfügbar','bad');
      $('gridBoard').innerHTML='<div class="empty"></div>';$('gridBoard').firstChild.textContent=data?.message||data?.error||txt||('HTTP '+res.status);
      return null;
    }
    renderGridLayout(data||{});
    return data||{};
  }catch(err){
    setGridStatus('Grid nicht erreichbar','bad');$('gridBoard').innerHTML='<div class="empty"></div>';$('gridBoard').firstChild.textContent=String(err);
    return null;
  }
}
function panGrid(dx,dy){
  const v=gridViewport();
  $('gridMinX').value=Math.max(0,v.minX+dx*v.width);
  $('gridMinY').value=Math.max(0,v.minY+dy*v.height);
  loadGridLayout();
}

async function searchGridRegions(){
  const query=$('gridRegionSearch').value.trim();
  if(query.length<2){$('gridRegionSearchDetail').textContent='Bitte mindestens zwei Zeichen oder eine Region-UUID eingeben.';return}
  $('gridRegionSearchDetail').textContent='Suche Regionen…';
  try{
    const qs=new URLSearchParams({q:query,limit:'100',offset:'0',sort:'name',order:'asc'});
    const res=await fetch('/api/v1/regions?'+qs,{headers:gridHeaders(),cache:'no-store',credentials:'same-origin'});
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    if(!res.ok){gridSearchResults=[];$('gridRegionSearchDetail').textContent=data?JSON.stringify(data,null,2):txt;return}
    gridSearchResults=data?.regions||[];
    const select=$('gridRegionResults');select.replaceChildren();
    if(!gridSearchResults.length){
      const option=document.createElement('option');option.value='';option.textContent='Keine Treffer';select.append(option);
      $('gridRegionSearchDetail').textContent='Keine passende Region gefunden.';
      return;
    }
    gridSearchResults.forEach(region=>{
      const option=document.createElement('option');
      option.value=region.region_id;
      option.textContent=(region.name||region.region_id)+' · Grid '+region.grid_x+'/'+region.grid_y+' · '+region.size_x+'×'+region.size_y+' m';
      select.append(option);
    });
    $('gridRegionSearchDetail').textContent=JSON.stringify({
      treffer:gridSearchResults.length,
      pagination:data?.pagination||null,
      regionen:gridSearchResults.map(x=>({region_id:x.region_id,name:x.name,grid_x:x.grid_x,grid_y:x.grid_y,size_x:x.size_x,size_y:x.size_y}))
    },null,2);
  }catch(err){gridSearchResults=[];$('gridRegionSearchDetail').textContent=String(err)}
}
async function jumpGridRegion(){
  const regionId=$('gridRegionResults').value;
  const region=gridSearchResults.find(x=>x.region_id===regionId);
  if(!region){$('gridRegionSearchDetail').textContent='Bitte zuerst einen Suchtreffer auswählen.';return}
  const width=Math.min(128,Math.max(1,parseInt($('gridWidth').value||'24',10)||24));
  const height=Math.min(128,Math.max(1,parseInt($('gridHeight').value||'18',10)||18));
  $('gridMinX').value=Math.max(0,region.grid_x-Math.floor(width/2));
  $('gridMinY').value=Math.max(0,region.grid_y-Math.floor(height/2));
  $('gridLifecycleRegionId').value=region.region_id;
  $('gridMoveRegionId').value=region.region_id;
  const data=await loadGridLayout();
  if(!data)return;
  const cell=(data.cells||[]).find(x=>
    x.grid_x===region.grid_x&&
    x.grid_y===region.grid_y&&
    (x.region_id===region.region_id||(x.region_ids||[]).includes(region.region_id)))||null;
  if(cell){
    selectGridCell(cell);
    const el=document.querySelector('#gridBoard .gridcell[data-key="'+gridCellKey(cell.grid_x,cell.grid_y)+'"]');
    if(el)el.scrollIntoView({block:'nearest',inline:'nearest'});
  }else{
    $('gridRegionSearchDetail').textContent='Region wurde gefunden, ist aber im geladenen Viewport nicht als belegte Zelle enthalten.';
  }
}

async function loadGridManagedNodes(){
  $('gridMutationDetail').textContent='Lade Managed Nodes…';
  try{
    const res=await fetch('/api/v1/nodes',{headers:gridHeaders(),cache:'no-store',credentials:'same-origin'});
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    if(!res.ok){$('gridMutationDetail').textContent=data?JSON.stringify(data,null,2):txt;return}
    const nodes=(data?.nodes||[]).filter(x=>x.state==='online'&&x.managed_region_commands===true);
    const list=$('gridNodeOptions');list.replaceChildren();
    nodes.forEach(node=>{const option=document.createElement('option');option.value=node.node_id;option.label=(node.hostname||node.node_id)+' · '+(node.region_count??0)+' Regionen';list.append(option)});
    if(nodes.length===1&&!$('gridCreateNodeId').value)$('gridCreateNodeId').value=nodes[0].node_id;
    if(nodes.length===1&&!$('gridLifecycleNodeId').value)$('gridLifecycleNodeId').value=nodes[0].node_id;
    $('gridMutationDetail').textContent=nodes.length?('Managed Nodes verfügbar: '+nodes.map(x=>x.node_id).join(', ')):'Kein online Node mit managed_region_commands=true gefunden.';
  }catch(err){$('gridMutationDetail').textContent=String(err)}
}
async function loadGridEstates(){
  $('gridMutationDetail').textContent='Lade Estates…';
  try{
    const qs=new URLSearchParams({limit:'100',offset:'0'});
    const res=await fetch('/api/v1/estates?'+qs,{headers:gridHeaders(),cache:'no-store',credentials:'same-origin'});
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    if(!res.ok){$('gridMutationDetail').textContent=data?JSON.stringify(data,null,2):txt;return}
    const estates=data?.estates||[];
    const list=$('gridEstateOptions');list.replaceChildren();
    estates.forEach(estate=>{
      const option=document.createElement('option');
      option.value=String(estate.estate_id);
      option.label=(estate.name||('Estate '+estate.estate_id))+' · '+(estate.region_count??0)+' Regionen';
      list.append(option);
    });
    if(estates.length===1&&!$('gridCreateEstateId').value)$('gridCreateEstateId').value=String(estates[0].estate_id);
    $('gridMutationDetail').textContent=estates.length
      ?('Estates verfügbar: '+estates.map(x=>(x.name||'Estate')+' (#'+x.estate_id+')').join(', '))
      :'Keine Estates gefunden.';
  }catch(err){$('gridMutationDetail').textContent=String(err)}
}

function parseUuidList(value){
  return (value||'').split(/[\s,;]+/).map(x=>x.trim()).filter(Boolean);
}
function estateMutationBody(){
  const body={
    name:$('estateManageName').value.trim(),
    owner_id:$('estateManageOwner').value.trim(),
    parent_estate_id:parseInt($('estateManageParent').value||'0',10),
    managers:parseUuidList($('estateManageManagers').value),
    allowed_residents:parseUuidList($('estateManageAllowed').value),
    banned_residents:parseUuidList($('estateManageBanned').value),
    allowed_groups:parseUuidList($('estateManageGroups').value),
    public_access:$('estatePolicyPublic').checked,
    allow_voice:$('estatePolicyVoice').checked,
    allow_direct_teleport:$('estatePolicyDirectTp').checked,
    estate_skip_scripts:$('estatePolicySkipScripts').checked,
    deny_anonymous:$('estatePolicyDenyAnonymous').checked,
    deny_minors:$('estatePolicyDenyMinors').checked,
    allow_environment_override:$('estatePolicyEnvironment').checked
  };
  return body;
}
function renderEstateManagement(estate){
  if(!estate)return;
  $('estateManageId').value=estate.estate_id??'';
  $('estateManageName').value=estate.name||'';
  $('estateManageOwner').value=estate.owner_id||'';
  $('estateManageParent').value=estate.parent_estate_id??0;
  $('estateManageManagers').value=(estate.managers||[]).join(', ');
  $('estateManageAllowed').value=(estate.allowed_residents||[]).join(', ');
  $('estateManageBanned').value=(estate.banned_residents||[]).join(', ');
  $('estateManageGroups').value=(estate.allowed_groups||[]).join(', ');
  $('estatePolicyPublic').checked=estate.policies?.public_access!==false;
  $('estatePolicyVoice').checked=estate.policies?.allow_voice!==false;
  $('estatePolicyDirectTp').checked=estate.policies?.allow_direct_teleport!==false;
  $('estatePolicySkipScripts').checked=estate.policies?.estate_skip_scripts===true;
  $('estatePolicyDenyAnonymous').checked=estate.policies?.deny_anonymous===true;
  $('estatePolicyDenyMinors').checked=estate.policies?.deny_minors===true;
  $('estatePolicyEnvironment').checked=estate.policies?.allow_environment_override===true;
}
async function loadEstateManagement(){
  const estateId=parseInt($('estateManageId').value||'0',10);
  if(estateId<=0){$('estateManagementDetail').textContent='Positive Estate-ID erforderlich.';return}
  $('estateManagementDetail').textContent='Lade Estate-Verwaltungsdaten…';
  try{
    const res=await fetch('/api/v1/estates/'+estateId+'/management',{headers:gridHeaders(),cache:'no-store',credentials:'same-origin'});
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    $('estateManagementDetail').textContent=data?JSON.stringify(data,null,2):txt;
    if(!res.ok)return;
    renderEstateManagement(data?.estate);
  }catch(err){$('estateManagementDetail').textContent=String(err)}
}
async function createEstateManagement(){
  const body=estateMutationBody();
  if(!body.name||!body.owner_id){$('estateManagementDetail').textContent='Name und Owner UUID sind erforderlich.';return}
  $('estateManagementDetail').textContent='Estate wird erstellt…';
  try{
    const res=await fetch('/api/v1/estates',{method:'POST',headers:gridMutationHeaders(),body:JSON.stringify(body),credentials:'same-origin'});
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    $('estateManagementDetail').textContent=data?JSON.stringify(data,null,2):txt;
    if(!res.ok)return;
    renderEstateManagement(data?.estate);
    await loadGridEstates();
  }catch(err){$('estateManagementDetail').textContent=String(err)}
}
async function updateEstateManagement(){
  const estateId=parseInt($('estateManageId').value||'0',10);
  if(estateId<=0){$('estateManagementDetail').textContent='Positive Estate-ID erforderlich.';return}
  const body=estateMutationBody();
  $('estateManagementDetail').textContent='Estate wird gespeichert…';
  try{
    const res=await fetch('/api/v1/estates/'+estateId,{method:'PATCH',headers:gridMutationHeaders(),body:JSON.stringify(body),credentials:'same-origin'});
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    $('estateManagementDetail').textContent=data?JSON.stringify(data,null,2):txt;
    if(!res.ok)return;
    renderEstateManagement(data?.estate);
    await loadGridEstates();
  }catch(err){$('estateManagementDetail').textContent=String(err)}
}
async function assignEstateRegion(){
  const estateId=parseInt($('estateManageId').value||'0',10);
  const regionId=$('estateManageRegionId').value.trim()||$('gridLifecycleRegionId').value.trim()||$('gridMoveRegionId').value.trim();
  if(estateId<=0||!regionId){$('estateManagementDetail').textContent='Estate-ID und Region UUID sind erforderlich.';return}
  if(!window.confirm('Region '+regionId+' wirklich Estate #'+estateId+' zuordnen?'))return;
  $('estateManagementDetail').textContent='Regionszuordnung wird gespeichert…';
  try{
    const res=await fetch('/api/v1/estates/'+estateId+'/regions/'+encodeURIComponent(regionId),{method:'PUT',headers:gridMutationHeaders(),credentials:'same-origin'});
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    $('estateManagementDetail').textContent=data?JSON.stringify(data,null,2):txt;
    if(!res.ok)return;
    await loadEstateManagement();
  }catch(err){$('estateManagementDetail').textContent=String(err)}
}

function gridMutationHeaders(){
  const headers=gridHeaders();
  headers['Content-Type']='application/json';
  const idem=$('gridMutationIdempotency').value.trim();
  if(idem)headers['Idempotency-Key']=idem;
  return headers;
}
async function watchRegionOperation(operationId){
  if(!operationId)return;
  for(let attempt=0;attempt<40;attempt++){
    try{
      const res=await fetch('/api/v1/region-operations/'+encodeURIComponent(operationId),{headers:gridHeaders(),cache:'no-store',credentials:'same-origin'});
      const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
      if(!res.ok){$('gridMutationDetail').textContent=data?JSON.stringify(data,null,2):txt;return}
      $('gridMutationDetail').textContent=JSON.stringify(data,null,2);
      const state=data?.operation?.state;
      if(state==='completed'||state==='failed'){
        if(state==='completed')await loadGridLayout();
        return;
      }
    }catch(err){$('gridMutationDetail').textContent=String(err);return}
    await new Promise(resolve=>setTimeout(resolve,750));
  }
  $('gridMutationDetail').textContent+='\n\nStatusabfrage beendet; operation_id kann später erneut über die API geprüft werden.';
}
async function createGridRegion(){
  if(!gridSelected){$('gridMutationDetail').textContent='Bitte zuerst eine Zielzelle auswählen.';return}
  const validation=await validateGridPlacement();
  if(!validation?.valid){$('gridMutationDetail').textContent='Erstellung abgebrochen: Placement ist nicht frei.';return}
  const name=$('gridCreateName').value.trim(),nodeId=$('gridCreateNodeId').value.trim(),estateId=parseInt($('gridCreateEstateId').value||'0',10);
  if(!name||!nodeId||estateId<=0){$('gridMutationDetail').textContent='Regionsname, Simulator-Node und positive Estate-ID sind erforderlich.';return}
  const body={
    name,
    node_id:nodeId,
    estate_id:estateId,
    grid_x:gridSelected.grid_x,
    grid_y:gridSelected.grid_y,
    size_x:parseInt($('gridRegionSizeX').value||'256',10),
    size_y:parseInt($('gridRegionSizeY').value||'256',10)
  };
  const regionId=$('gridCreateRegionId').value.trim();if(regionId)body.region_id=regionId;
  $('gridMutationDetail').textContent='Regionserstellung wird eingereiht…';
  try{
    const res=await fetch('/api/v1/regions',{method:'POST',headers:gridMutationHeaders(),body:JSON.stringify(body),credentials:'same-origin'});
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    $('gridMutationDetail').textContent=data?JSON.stringify(data,null,2):txt;
    if(!res.ok)return;
    await watchRegionOperation(data?.operation?.operation_id);
  }catch(err){$('gridMutationDetail').textContent=String(err)}
}
async function moveGridRegion(){
  if(!gridSelected){$('gridMutationDetail').textContent='Bitte zuerst die Zielzelle auswählen.';return}
  const regionId=$('gridMoveRegionId').value.trim();
  if(!regionId){$('gridMutationDetail').textContent='Region-UUID für den Move fehlt. Eine belegte Quellzelle anklicken oder UUID manuell eintragen.';return}
  $('gridMutationDetail').textContent='Regionsverschiebung wird eingereiht…';
  try{
    const res=await fetch('/api/v1/regions/'+encodeURIComponent(regionId)+'/placement',{
      method:'PATCH',
      headers:gridMutationHeaders(),
      body:JSON.stringify({grid_x:gridSelected.grid_x,grid_y:gridSelected.grid_y}),
      credentials:'same-origin'
    });
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    $('gridMutationDetail').textContent=data?JSON.stringify(data,null,2):txt;
    if(!res.ok)return;
    await watchRegionOperation(data?.operation?.operation_id);
  }catch(err){$('gridMutationDetail').textContent=String(err)}
}

async function runGridLifecycle(action){
  const regionId=$('gridLifecycleRegionId').value.trim();
  if(!regionId){$('gridMutationDetail').textContent='Region-UUID für die Lifecycle-Aktion fehlt.';return}
  const nodeId=$('gridLifecycleNodeId').value.trim();
  if(action==='start'&&!nodeId){$('gridMutationDetail').textContent='Für Start ist der Ziel-Node erforderlich.';return}
  if((action==='stop'||action==='restart')&&!window.confirm(action==='stop'?'Region wirklich stoppen?':'Region wirklich neu starten?'))return;
  const body={action};
  if(nodeId)body.node_id=nodeId;
  const labels={start:'Start',stop:'Stop',restart:'Neustart'};
  $('gridMutationDetail').textContent=(labels[action]||action)+' wird eingereiht…';
  try{
    const res=await fetch('/api/v1/regions/'+encodeURIComponent(regionId)+'/lifecycle',{
      method:'POST',
      headers:gridMutationHeaders(),
      body:JSON.stringify(body),
      credentials:'same-origin'
    });
    const txt=await res.text();let data=null;try{data=JSON.parse(txt)}catch{}
    $('gridMutationDetail').textContent=data?JSON.stringify(data,null,2):txt;
    if(!res.ok)return;
    await watchRegionOperation(data?.operation?.operation_id);
  }catch(err){$('gridMutationDetail').textContent=String(err)}
}

async function execute(){
  if(!selected)return;
  const headers={'Accept':'application/json'},bearer=$('bearer').value.trim()||adminAuthToken(),key=$('apiKey').value.trim(),idem=$('idem').value.trim();
  if(bearer)headers.Authorization='Bearer '+bearer;if(key)headers['X-NexVerse-Api-Key']=key;if(idem)headers['Idempotency-Key']=idem;
  const body=$('body').value.trim(),opts={method:selected.method,headers};
  if(body&&!['GET','HEAD'].includes(selected.method)){headers['Content-Type']='application/json';opts.body=body}
  $('status').textContent='Anfrage läuft…';$('output').textContent='';
  try{
    const res=await fetch($('url').value,{...opts,credentials:'same-origin'}),txt=await res.text();
    $('status').textContent=res.status+' '+res.statusText+' | Korrelations-ID: '+(res.headers.get('X-Correlation-Id')||'-');$('status').className=res.ok?'small status-good':'small status-bad';
    let formatted=txt;try{formatted=JSON.stringify(JSON.parse(txt),null,2)}catch{}
    const headerLines=[];res.headers.forEach((v,k)=>headerLines.push(k+': '+v));$('output').textContent=headerLines.join('\n')+'\n\n'+formatted;
  }catch(err){$('status').textContent='Anfrage fehlgeschlagen';$('status').className='small status-bad';$('output').textContent=String(err)}
}

/* Issue #289: Personal World-Bearer authorization – never machine-key grants.
   Read current assignment before editing and again immediately before saving.
   Server-side ownership, account privileges and required scopes stay authoritative. */
const worldReadScopeChoices=[
 'statistics:read','regions:read','simulators:read','estates:read',
 'economy:read','inventory:read','groups:read',
 'experiences:read','discovery:read'
];
let worldAuthLoaded=null,worldAuthSelfId='';
function worldAuthReset(){
  worldAuthLoaded=null;
  $('worldAuthSave').disabled=true;
  $('worldAuthChoices').replaceChildren();
  $('worldAuthIdentity').textContent='Noch kein Benutzerkonto geladen.';
  $('worldAuthExisting').textContent='Automatische Lese-Scopes werden anhand des aktuellen World-Rollenmodells erkannt. Nur zusätzliche persönliche Einträge lassen sich ändern.';
}
function worldAuthArray(values){
  return [...new Set((Array.isArray(values)?values:[]).map(x=>String(x).toLowerCase()))].sort();
}
function worldAuthSame(left,right){
  return JSON.stringify(worldAuthArray(left))===JSON.stringify(worldAuthArray(right));
}
async function worldAuthUseMe(){
  try{
    const token=await secretAdminToken();
    const me=await secretJson('/api/v1/users/me',{
      headers:{'Authorization':'Bearer '+token}});
    if(typeof me.principal_id!=='string')throw new Error('World lieferte keine eigene Principal-ID.');
    worldAuthSelfId=me.principal_id.toLowerCase();
    $('worldAuthPrincipal').value=me.principal_id;
    worldAuthReset();
    await worldAuthLoad();
  }catch(err){$('worldAuthStatus').textContent='Eigene UUID nicht geladen: '+String(err.message||err);}
}
async function worldAuthLoad(){
  worldAuthReset();
  const status=$('worldAuthStatus');
  try{
    if(!window.isSecureContext)throw new Error('Diese Funktion erfordert HTTPS.');
    const id=$('worldAuthPrincipal').value.trim().toLowerCase();
    if(!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(id))
      throw new Error('Bitte eine vollständige gültige Bewohner-UUID eingeben.');
    const token=await secretAdminToken();
    const authHeaders={'Authorization':'Bearer '+token};
    const [model,existing]=await Promise.all([
      secretJson('/api/v1/auth/authorization-model',{headers:authHeaders}),
      secretJson('/api/v1/users/'+encodeURIComponent(id)+'/authorization',{headers:authHeaders})
    ]);
    const allowed=new Set(Array.isArray(model.scopes)?model.scopes:[]);
    const original=worldAuthArray(existing.explicit_scopes);
    const readable=worldReadScopeChoices.filter(scope=>allowed.has(scope));
    if(!readable.length)throw new Error('World gibt keine bearbeitbaren Lese-Scopes frei.');
    if(!Array.isArray(model.default_scopes)||!Array.isArray(model.roles))
      throw new Error('World unterstützt noch keine automatischen Scope-Modelldaten. ROBUST aktualisieren.');
    const inherited=new Set(worldAuthArray(model.default_scopes));
    const roles=worldAuthArray(existing.roles);
    if(existing.legacy_admin)roles.push('administrator');
    for(const role of roles){
      const config=model.roles.find(item=>item.id===role);
      if(config)for(const scope of worldAuthArray(config.scopes))inherited.add(scope);
    }
    $('worldAuthIdentity').textContent='Principal-ID: '+existing.principal_id+
      ' · UserLevel: '+existing.legacy_user_level;
    $('worldAuthExisting').textContent='Rollen: '+(existing.roles||[]).join(', ')+
      ' | Automatisch: '+([ ...inherited ].sort().join(', ')||'keine')+
      ' | Individuell gespeichert: '+(original.join(', ')||'keine');
    const grid=$('worldAuthChoices');
    let mutable=0;
    for(const scope of readable){
      const byRole=inherited.has(scope);
      const label=document.createElement('label');label.className='change';
      const input=document.createElement('input');input.type='checkbox';
      input.value=scope;
      input.checked=byRole||original.includes(scope);
      input.disabled=byRole;
      input.style.width='auto';
      const name=document.createElement('strong');name.textContent=' '+scope;
      const note=document.createElement('p');
      note.textContent=byRole?
        'Automatisch durch Einwohnerrolle / Benutzerrolle / Administratorlevel gewährt. Keine Einzelzuweisung erforderlich.':
        original.includes(scope)?
          'Zusätzliche explizite Zuweisung – Abwahl entfernt nur diesen Eintrag.':
          'Nur für eine begründete individuelle Ausnahme zusätzlich gewähren.';
      if(!byRole)mutable++;
      label.append(input,name,note);grid.append(label);
    }
    worldAuthLoaded={id,roles:worldAuthArray(existing.roles),original,
      inherited:[...inherited]};
    $('worldAuthSave').disabled=mutable===0;
    status.textContent='World-Berechtigungen geladen. '+(mutable?
      'Automatische Grants sind gesperrt, nur zusätzliche individuelle Grants bearbeitbar.':
      'Alle verfügbaren Lese-Scopes werden automatisch vergeben – keine Einzelzuweisung nötig.');
  }catch(err){
    status.textContent='Benutzerberechtigungen nicht geladen: '+String(err.message||err);
  }
}
async function worldAuthSave(){
  const button=$('worldAuthSave');button.disabled=true;
  const status=$('worldAuthStatus');
  try{
    if(!window.isSecureContext)throw new Error('Diese Funktion erfordert HTTPS.');
    const old=worldAuthLoaded;
    if(!old||$('worldAuthPrincipal').value.trim().toLowerCase()!==old.id)
      throw new Error('Bitte Bewohnerberechtigungen erneut laden.');
    const token=await secretAdminToken();
    const toggled=[...$('worldAuthChoices').querySelectorAll('input[type=checkbox]')];
    const merged=new Set(old.original);
    for(const input of toggled){
      if(input.disabled)continue; // Cannot revoke inherited role defaults.
      if(input.checked)merged.add(input.value);
      else merged.delete(input.value);
    }
    const next=worldAuthArray([...merged]);
    const added=next.filter(scope=>!old.original.includes(scope));
    const removed=old.original.filter(scope=>!next.includes(scope));
    if(!added.length&&!removed.length){
      status.textContent='Keine Scope-Änderung ausgewählt.';
      return;
    }
    // A stale browser must not silently overwrite a newer assignment.
    const url='/api/v1/users/'+encodeURIComponent(old.id)+'/authorization';
    const latest=await secretJson(url,{headers:{'Authorization':'Bearer '+token}});
    if(!worldAuthSame(latest.explicit_scopes,old.original)||
        !worldAuthSame(latest.roles,old.roles))
      throw new Error('Die World-Rechte wurden seit dem Laden verändert. Neu laden, bevor du speicherst.');
    const details='Hinzufügen: '+(added.join(', ')||'keine')+
      '\nEntfernen: '+(removed.join(', ')||'keine');
    if(!window.confirm('PERSÖNLICHE Berechtigungen für '+old.id+
      ' ändern?\n'+details+
      '\n\nAlle bestehenden Rollen und übrigen Fachscopes bleiben erhalten. Bisherige Sitzungen müssen neu angemeldet werden.'))return;
    const res=await secretJson(url,{
      method:'PATCH',
      headers:{'Authorization':'Bearer '+token,'Content-Type':'application/json'},
      body:JSON.stringify({scopes:next,expected_scopes:old.original,
        expected_roles:old.roles})
    });
    if(res.reauthentication_required!==true)
      throw new Error('World hat die Neuanmeldung nicht bestätigt.');
    if(res.authorization&&!worldAuthSame(res.authorization.explicit_scopes,next))
      throw new Error('World hat eine abweichende Scope-Liste bestätigt. Bestand neu laden.');
    worldAuthReset();
    status.textContent='Persönliche Scopes erfolgreich gespeichert. Bewohner im Stadtportal vollständig abmelden und erneut anmelden. Falls dein eigenes Konto geändert wurde, hier ebenfalls neu anmelden.';
    // Editing this administrator's own account invalidates the current
    // security-stamped bearer. Clear it instead of leaving a stale token
    // in memory and require a fresh Control Center login.
    if(old.id===worldAuthSelfId){
      adminLogout();
      $('adminLoginMessage').textContent=
        'Eigene Berechtigungen geändert. Bitte neu als Administrator anmelden.';
    }
  }catch(err){
    status.textContent='Nicht gespeichert oder Abnahme unklar: '+String(err.message||err)+
      ' – Berechtigungen neu laden und den aktuellen World-Stand prüfen.';
  }finally{
    button.disabled=worldAuthLoaded===null ||
      !$('worldAuthChoices').querySelector('input[type=checkbox]:not(:disabled)');
  }
}
/* Secrets console: admin-only World API key issuance; no browser persistence. */
function secretStatus(message, ok) {
  const node=$('secretStatus');
  node.textContent=message;
  node.className='authnote'+(ok===true?' status-good':ok===false?' status-bad':'');
}
function secretClear() {
  ['secretOutput'].forEach(id=>{$(id).value=''});
  $('secretCopy').disabled=true;
  secretStatus('Geheimnisse aus der geöffneten Seite gelöscht.',true);
}
function secretRandomHmac() {
  if(!window.isSecureContext||!window.crypto?.getRandomValues)
    throw new Error('Für die sichere Zufallsgenerierung ist HTTPS erforderlich.');
  const bytes=new Uint8Array(48);
  window.crypto.getRandomValues(bytes);
  return Array.from(bytes,b=>b.toString(16).padStart(2,'0')).join('');
}
function secretValidateEndpoint(text) {
  let url;
  try{url=new URL(text)}catch{throw new Error('NexBus-Peer-URL ist ungültig: '+text)}
  if(url.pathname!=='/internal/nexbus/v1/events'||url.search||url.hash||url.username||url.password)
    throw new Error('NexBus-URL muss exakt auf /internal/nexbus/v1/events enden.');
  const loopback=['localhost','127.0.0.1','[::1]'].includes(url.hostname);
  if(url.protocol!=='https:'&&!(url.protocol==='http:'&&loopback))
    throw new Error('NexBus erfordert HTTPS oder HTTP auf localhost.');
  return url.href;
}
function secretBuildLines(values) {
  const names=['NEXVERSE_REGISTER_API_KEY','NEXVERSE_ECONOMY_API_KEY',
    'NEXVERSE_GROUP_LAND_API_KEY','NEXVERSE_EXPERIENCES_API_KEY',
    'NEXVERSE_EXPERIENCES_VIEWER_PERMISSIONS_API_KEY',
    'NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY',
    'NEX_PORTAL_IM_SECRET','NEXVERSE_NEXBUS_SHARED_KEY','NEXVERSE_NEXBUS_PEER_URL','NEXVERSE_NEXBUS_PEERS'];
  $('secretOutput').value=names.filter(name=>Object.hasOwn(values,name)).map(name=>name+'='+values[name]).join('\n')+'\n';
  $('secretCopy').disabled=!Object.keys(values).length;
}
/* Only metadata is available after issuance; server never re-exposes secrets. */
function secretKeyFingerprint(key) {
  return JSON.stringify([(key.name||'').trim().toLowerCase(),
    [...(key.scopes||[])].map(x=>String(x).toLowerCase()).sort()]);
}
function secretRenderKeyInventory(keys, supportedScopes) {
  const host=$('secretKeyList');host.replaceChildren();
  const activeCounts=new Map();
  keys.filter(k=>k.enabled).forEach(k=>{
    const fingerprint=secretKeyFingerprint(k);
    activeCounts.set(fingerprint,(activeCounts.get(fingerprint)||0)+1);
  });
  const duplicates=[...activeCounts.values()].filter(count=>count>1).length;
  $('secretListStatus').textContent=keys.length+' Schlüssel registriert. '+
    duplicates+' aktive Duplikatgruppe(n) gefunden. Kein Schlüssel wurde automatisch verändert.';
  for(const key of keys){
    const card=document.createElement('div');card.className='change';
    const title=document.createElement('strong');
    title.textContent=(key.name||'Unbenannt')+(key.enabled?' · Aktiv':' · Deaktiviert');
    card.append(title);
    const fp=secretKeyFingerprint(key),repeated=key.enabled&&activeCounts.get(fp)>1;
    if(repeated){
      const warning=document.createElement('p');
      warning.textContent='Duplikat: Gleicher aktiver Name und identische Scopes (Geheimwerte können abweichen).';
      warning.className='status-bad';card.append(warning);
    }
    const description=document.createElement('p');
    description.textContent='ID: '+key.key_id+' | Scopes: '+(key.scopes||[]).join(', ')+
      ' | Letzte Nutzung: '+(key.last_used_at?
        new Date(key.last_used_at*1000).toLocaleString('de-DE'):'noch nicht erfasst');
    description.style.overflowWrap='anywhere';card.append(description);
    const controls=document.createElement('div');controls.className='row';
    const toggle=document.createElement('button');toggle.className='action';
    toggle.textContent=key.enabled?'Deaktivieren':'Aktivieren';
    toggle.addEventListener('click',()=>secretManageKey(key,'toggle'));
    controls.append(toggle);
    if(!key.enabled){
      const remove=document.createElement('button');remove.className='action';
      remove.textContent='Endgültig löschen';
      remove.addEventListener('click',()=>secretManageKey(key,'delete'));
      controls.append(remove);
    }
    const edit=document.createElement('button');edit.className='action';
    edit.textContent='Berechtigungen bearbeiten';
    controls.append(edit);
    card.append(controls);
    const editor=document.createElement('div');
    editor.hidden=true;
    editor.style.marginTop='12px';
    const editDescription=document.createElement('p');
    editDescription.textContent='Berechtigungen für diesen Schlüssel auswählen. Der Schlüsselwert bleibt erhalten.';
    editor.append(editDescription);
    const choices=document.createElement('div');choices.className='changegrid';
    const allowed=new Set(supportedScopes||[]);
    const allScopes=[...new Set([...(supportedScopes||[]),...(key.scopes||[])])].sort();
    const boxes=[];
    for(const scope of allScopes){
      const label=document.createElement('label');label.className='change';
      const check=document.createElement('input');check.type='checkbox';
      check.value=scope;check.style.width='auto';
      check.checked=(key.scopes||[]).includes(scope);
      if(!allowed.has(scope))check.disabled=true;
      const caption=document.createElement('span');caption.textContent=' '+scope+
        (!allowed.has(scope)?' (nicht mehr zuweisbar)':'');
      label.append(check,caption);choices.append(label);
      boxes.push(check);
    }
    editor.append(choices);
    const actions=document.createElement('div');actions.className='row';
    actions.style.marginTop='10px';
    const save=document.createElement('button');save.className='action';
    save.textContent='Berechtigungen speichern';
    save.addEventListener('click',async()=>{
      const selected=boxes.filter(box=>box.checked&&!box.disabled).map(box=>box.value);
      save.disabled=true;
      try{await secretEditKeyScopes(key,selected);}
      finally{save.disabled=false;}
    });
    const cancel=document.createElement('button');cancel.className='action';
    cancel.textContent='Abbrechen';
    cancel.addEventListener('click',()=>{editor.hidden=true;edit.textContent='Berechtigungen bearbeiten';});
    actions.append(save,cancel);
    editor.append(actions);
    edit.addEventListener('click',()=>{
      editor.hidden=!editor.hidden;
      edit.textContent=editor.hidden?'Berechtigungen bearbeiten':'Editor schließen';
    });
    card.append(editor);
    host.append(card);
  }
}
async function secretEditKeyScopes(key,scopes) {
  try{
    if(!window.isSecureContext)throw new Error('Nur über HTTPS verfügbar.');
    if(!scopes.length)throw new Error('Mindestens eine Berechtigung auswählen.');
    const previous=key.scopes||[];
    const added=scopes.filter(scope=>!previous.includes(scope));
    const removed=previous.filter(scope=>!scopes.includes(scope));
    if(!added.length&&!removed.length){
      $('secretListStatus').textContent='Keine Änderungen an den Berechtigungen.';
      return;
    }
    const details='Hinzufügen: '+(added.join(', ')||'keine')+
      '\nEntfernen: '+(removed.join(', ')||'keine');
    if(!window.confirm('Berechtigungen von '+key.name+' ('+key.key_id+
      ') ändern, ohne den Schlüssel zu erneuern?\n'+details))return;
    const token=await secretAdminToken();
    await secretJson('/api/v1/auth/api-keys',{
      method:'PATCH',
      headers:{'Authorization':'Bearer '+token,'Content-Type':'application/json'},
      body:JSON.stringify({key_id:key.key_id,scopes:scopes,expected_scopes:previous})
    });
    await secretLoadKeys();
    $('secretListStatus').textContent='Berechtigungen für '+key.name+
      ' aktualisiert. Derselbe API-Schlüssel bleibt gültig.';
  }catch(error){
    $('secretListStatus').textContent='Berechtigungen konnten nicht gespeichert werden: '+
      String(error.message||error)+'. Bestand gegebenenfalls aktualisieren.';
  }
}
async function secretLoadKeys() {
  try{
    if(!window.isSecureContext)throw new Error('Nur über HTTPS verfügbar.');
    const token=await secretAdminToken();
    const response=await secretJson('/api/v1/auth/api-keys',{
      headers:{'Authorization':'Bearer '+token}});
    secretRenderKeyInventory(response.keys||[],response.supported_scopes||[]);
  }catch(error){
    $('secretListStatus').textContent='Schlüsselbestand konnte nicht geladen werden: '+
      String(error.message||error);
    $('secretKeyList').replaceChildren();
  }
}
async function secretManageKey(key,action) {
  try{
    if(!window.isSecureContext)throw new Error('Nur über HTTPS verfügbar.');
    const token=await secretAdminToken();
    const verb=action==='delete'?'endgültig löschen':key.enabled?'deaktivieren':'aktivieren';
    if(!window.confirm('Maschinen-API-Schlüssel '+key.key_id+' ('+key.name+') '+verb+
       '? Vorher alle nexverse.env-Dateien und aktive Simulatoren prüfen.'))return;
    if(action==='delete'){
      if(key.enabled)throw new Error('Aktive Schlüssel zuerst deaktivieren.');
      await secretJson('/api/v1/auth/api-keys/'+encodeURIComponent(key.key_id),{
        method:'DELETE',headers:{'Authorization':'Bearer '+token}});
    }else{
      await secretJson('/api/v1/auth/api-keys',{
        method:'PATCH',
        headers:{'Authorization':'Bearer '+token,'Content-Type':'application/json'},
        body:JSON.stringify({key_id:key.key_id,enabled:!key.enabled})});
    }
    await secretLoadKeys();
  }catch(error){
    $('secretListStatus').textContent='Aktion nicht abgeschlossen: '+String(error.message||error);
  }
}
async function secretJson(url,opts) {
  const response=await fetch(url,{cache:'no-store',credentials:'same-origin',...opts});
  let data={};
  try{data=await response.json()}catch{}
  if(!response.ok)throw new Error('HTTP '+response.status+' ('+(data.error||'api_error')+')');
  return data;
}
async function secretAdminToken() {
  const token=adminAuthToken();
  if(!token)throw new Error('Eine aktive Admin-Sitzung mit UserLevel 200 ist erforderlich.');
  return token;
}
async function secretGeneratePortalIm() {
  if(secretIssuePending)return;
  if(!window.isSecureContext) {
    secretStatus('HMAC-Schlüssel dürfen ausschließlich unter HTTPS generiert werden.',false);
    return;
  }
  secretIssuePending=true;
  $('secretGeneratePortalIm').disabled=true;
  try {
    const token=await secretAdminToken();
    await secretJson('/api/v1/auth/admin/session',{
      headers:{'Authorization':'Bearer '+token}});
    if(!window.confirm('Neuen NexPortalIM-Schlüssel erstellen? Ein bestehender Schlüssel wird NICHT automatisch auf ROBUST oder Simulatoren umgestellt.'))return;
    secretBuildLines({NEX_PORTAL_IM_SECRET:secretRandomHmac()});
    secretStatus('Neuen NexPortalIM-HMAC-Schlüssel erzeugt. Jetzt sicher auf ROBUST und allen Simulatoren mit identischem Wert eintragen.',true);
  }catch(error) {
    secretStatus('NexPortalIM-Schlüssel nicht erzeugt: '+String(error.message||error),false);
  }finally {
    secretIssuePending=false;
    $('secretGeneratePortalIm').disabled=false;
  }
}
let secretIssuePending=false;
async function secretGenerate(onlyRegistration=false, onlyGroupLand=false) {
  if(secretIssuePending)return;
  const onlyOne=onlyRegistration||onlyGroupLand;
  const wantRegister=onlyRegistration||(!onlyOne&&$('secretRegister').checked);
  const wantGroupLand=onlyGroupLand||(!onlyOne&&$('secretGroupLand').checked);
  const wantEconomy=!onlyOne&&$('secretEconomy').checked;
  const wantExperiences=!onlyOne&&$('secretExperiences').checked;
  const wantPermissions=!onlyOne&&$('secretExperiencePermissions').checked;
  const wantManage=!onlyOne&&$('secretExperienceManage').checked;
  const wantBus=!onlyOne&&$('secretNexBus').checked;
  if(!wantRegister&&!wantGroupLand&&!wantEconomy&&!wantExperiences&&!wantPermissions&&!wantManage&&!wantBus){
    secretStatus('Bitte mindestens einen Schlüssel auswählen.',false);return;
  }
  secretIssuePending=true;
  $('secretGenerate').disabled=true;
  $('secretGenerateRegistration').disabled=true;
  $('secretGenerateGroupLand').disabled=true;
  const values={};
  let peers=[];
  let peerUrl='';
  try {
    if(wantBus){
      peerUrl=secretValidateEndpoint($('secretPeerUrl').value.trim());
      peers=$('secretPeers').value.split(',').map(x=>x.trim()).filter(Boolean).map(secretValidateEndpoint);
      if(!peers.length)throw new Error('Mindestens eine Robust-zu-Simulator-URL angeben.');
    }
    if(!window.isSecureContext)throw new Error('Schlüssel nur über HTTPS generieren.');
    const token=await secretAdminToken();
    const plan=[
      {wanted:wantRegister,name:'OGL Stadtportal Registrierung',
       scopes:['users:register'],env:'NEXVERSE_REGISTER_API_KEY'},
      {wanted:wantGroupLand,name:'OGL Simulator Group Land',
       scopes:['groups:read'],env:'NEXVERSE_GROUP_LAND_API_KEY'},
      {wanted:wantEconomy,name:'OGL Simulator Economy',
       scopes:['economy:read','economy:transfer','groups:read'],env:'NEXVERSE_ECONOMY_API_KEY'},
      {wanted:wantExperiences,name:'OGL Simulator Experiences',
       scopes:['experiences:script'],env:'NEXVERSE_EXPERIENCES_API_KEY'},
      {wanted:wantPermissions,name:'OGL Simulator Experience Permissions',
       scopes:['experiences:viewer:permissions'],
       env:'NEXVERSE_EXPERIENCES_VIEWER_PERMISSIONS_API_KEY'},
      {wanted:wantManage,name:'OGL Simulator Experience Management',
       scopes:['experiences:viewer:manage'],
       env:'NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY'}
    ].filter(item=>item.wanted);
    if(plan.length){
      const inventory=await secretJson('/api/v1/auth/api-keys',{
        headers:{'Authorization':'Bearer '+token}});
      const existing=inventory.keys||[];
      const match=plan.find(item=>existing.some(key=>key.enabled &&
        key.name?.trim().toLowerCase()===item.name.toLowerCase() &&
        JSON.stringify([...(key.scopes||[])].map(s=>s.toLowerCase()).sort())===
        JSON.stringify([...item.scopes].sort())));
      if(match)throw new Error('Ein aktiver Schlüssel für '+match.name+
        ' existiert bereits. Unter Abschnitt 4 prüfen und bei nötiger Rotation gezielt deaktivieren.');
    }
    if(!window.confirm(onlyRegistration?
      'Neuen dedizierten Registrierungs-API-Key (nur users:register) für das Stadtportal-Backend erstellen?':
      onlyGroupLand?
      'Neuen dedizierten Gruppenland-API-Key (nur groups:read) für die Simulatoren erstellen?':
      'Neue Maschinen-API-Schlüssel werden sofort serverseitig registriert. Erstellung starten?'))return;
    $('secretCopy').disabled=true;
    $('secretOutput').value='';
    secretStatus('Schlüssel werden registriert…',null);
    if(wantBus){
      values.NEXVERSE_NEXBUS_SHARED_KEY=secretRandomHmac();
      values.NEXVERSE_NEXBUS_PEER_URL=peerUrl;
      values.NEXVERSE_NEXBUS_PEERS=peers.join(',');
      secretBuildLines(values);
    }
    if(plan.length){
      for(const item of plan){
        const data=await secretJson('/api/v1/auth/api-keys',{
          method:'POST',
          headers:{'Content-Type':'application/json','Authorization':'Bearer '+token},
          body:JSON.stringify({name:item.name,scopes:item.scopes})
        });
        if(!/^nxk_[0-9a-f]{32}\.[A-Za-z0-9_-]{64}$/.test(data.api_key||''))
          throw new Error('Die API antwortete ohne vollständigen Maschinen-API-Schlüssel.');
        values[item.env]=data.api_key;
        secretBuildLines(values);
      }
    }
    secretStatus(onlyRegistration?
      'Registrierungs-API-Key erstellt. NEXVERSE_REGISTER_API_KEY jetzt ausschließlich ins Stadtportal-Backend kopieren.':
      onlyGroupLand?
      'Gruppenland-API-Key erstellt. NEXVERSE_GROUP_LAND_API_KEY jetzt in die geschützte Simulator-Environment-Datei kopieren.':
      'Alle ausgewählten Schlüssel erstellt. Konfigurationsblock jetzt sicher kopieren.',true);
    if(plan.length)await secretLoadKeys();
  }catch(err){
    secretBuildLines(values);
    secretStatus('Einrichtung nicht vollständig: '+String(err.message||err)+
      '. Bereits erstellte Schlüssel stehen im Ausgabefeld; kopieren, bevor du erneut startest.',false);
  }finally{
    secretIssuePending=false;
    $('secretGenerate').disabled=false;
    $('secretGenerateRegistration').disabled=false;
    $('secretGenerateGroupLand').disabled=false;
  }
}
async function secretCopy() {
  if(!adminAuthToken()){secretClear();showPage('adminlogin');return}
  const contents=$('secretOutput').value;
  if(!contents.trim())return;
  try{
    await navigator.clipboard.writeText(contents);
    secretStatus('Konfigurationszeilen in die Zwischenablage kopiert. Sicher in nexverse.env einfügen.',true);
  }catch{
    $('secretOutput').focus();
    $('secretOutput').select();
    secretStatus('Zwischenablage nicht verfügbar. Ausgabe markieren und manuell kopieren.',false);
  }
}


async function runOglDiagnostics(){
  const list=$('diagnosticsResults');
  list.replaceChildren();
  $('diagSummary').textContent='Prüfe Live-Dienste …';
  $('diagDot').className='dot';
  $('diagnosticsRefresh').disabled=true;
  const checks=[
    {label:'API Health',path:'/api/v1/health',expected:'application/json'},
    {label:'Serverversion',path:'/api/v1/version',expected:'application/json'},
    {label:'OpenAPI 3.1',path:'/api/v1/openapi.json',expected:'application/json'},
    {label:'ChatGPT-Anweisung',path:'/api/v1/chatgpt-instructions.md',expected:'text/markdown'},
    {label:'OGL Feature Control (optional)',path:'/api/v1/feature-control/nodes',expected:'application/json',optional:true},
  ];
  let passed=0,optional=0;
  for(const item of checks){
    let kind='bad',desc='Nicht erreichbar';
    try{
      const headers={'Accept':item.expected};
      if(item.optional && adminAuthToken())headers.Authorization='Bearer '+adminAuthToken();
      const response=await fetch(item.path,{method:'GET',headers,cache:'no-store'});
      if(response.ok){
        passed++;kind='good';
        if(item.path.endsWith('/version')){
          const result=await response.json();desc=String(result.server_version||'Version unbekannt');
        }else if(item.path.endsWith('/health')){
          const result=await response.json();desc=String(result.status||'Antwort erhalten');
        }else desc='HTTP '+response.status+' – verfügbar';
      }else if(item.optional && response.status===404){
        optional++;kind='optional';desc='Nicht aktiviert – optionales Modul fehlt';
      }else if(item.optional && (response.status===401||response.status===403)){
        optional++;kind='optional';desc='Aktiv, aber separate API-Berechtigung erforderlich (HTTP '+response.status+')';
      }else{desc='HTTP '+response.status;}
    }catch(error){desc='Netzwerkfehler: '+String(error.message||error);}
    const box=document.createElement('div');box.className='ogl-diagnostic-result '+kind;
    const label=document.createElement('strong');label.textContent=item.label;
    const message=document.createElement('small');message.textContent=desc;
    box.append(label,message);list.append(box);
  }
  $('diagSummary').textContent=passed+' / '+checks.length+' Dienste erreichbar'+(optional?' · '+optional+' optional/eingeschränkt':'');
  $('diagDot').className='dot '+(passed>=4?'good':'bad');
  $('diagnosticsRefresh').disabled=false;
}
document.getElementById('diagnosticsRefresh').addEventListener('click',runOglDiagnostics);
async function init(){
  try{
    const results=await Promise.all([
      fetch('/api/v1/openapi.json',{cache:'no-store'}).then(r=>r.json()),
      fetch('/api/v1/health',{cache:'no-store'}).then(r=>r.json()),
      fetch('/api/v1/version',{cache:'no-store'}).then(r=>r.json())
    ]);
    spec=results[0];const health=results[1],version=results[2];
    changes=spec['x_nexverse_changelog']||[];
    buildEntries();
    $('citizenEndpointCount').textContent=entries.filter(x=>(x.op['x-nexverse-audience']||[]).includes('citizen')).length;
    $('adminEndpointCount').textContent=entries.filter(x=>(x.op['x-nexverse-audience']||[]).includes('admin')).length;
    $('healthText').textContent=health.status==='ok'?'API online':'API Status: '+(health.status||'unbekannt');$('healthDot').className='dot '+(health.status==='ok'?'good':'bad');
    $('serverVersion').textContent=version.server_version||'–';$('oglActiveRelease').textContent=(version.server_version||'OpenGenesisLINK')+' · '+(version.milestone_title||'World API');$('milestone').textContent='Meilenstein '+(version.milestone||'–')+(version.milestone_title?' · '+version.milestone_title:'');$('apiVersion').textContent=version.api_version||spec.info?.version||'–';
    $('endpointCount').textContent=Object.keys(spec.paths||{}).length;$('operationCount').textContent=entries.length+' Operationen';
    $('navmeta').textContent=(version.server_version||'OpenGenesisLINK')+' · '+(version.api_version||'API')+' · '+languageLabel(version.language||spec['x_nexverse_language']||'de-DE');
    if(changes.length){$('lastChangeDate').textContent=changes[0].date||'–';$('lastChangeTitle').textContent=changes[0].title||''}
    const overview=$('overviewChanges');overview.replaceChildren();changes.slice(0,4).forEach(x=>overview.append(makeChange(x)));if(!changes.length)overview.innerHTML='<div class="empty">Noch keine Versionshinweise veröffentlicht.</div>';
    renderChangeFilters();renderChanges('all');renderVersions();renderRoadmap();renderEndpointList();
    loadStatistics();
  }catch(err){
    $('healthText').textContent='API-Metadaten konnten nicht geladen werden';$('healthDot').className='dot bad';$('navmeta').textContent=String(err);
  }
}
$('adminLoginSubmit').addEventListener('click',adminLogin);
$('adminLoginLogout').addEventListener('click',adminLogout);
$('adminLoginPassword').addEventListener('keydown',event=>{if(event.key==='Enter'){event.preventDefault();adminLogin()}});
$('adminLoginTotp').addEventListener('keydown',event=>{if(event.key==='Enter'){event.preventDefault();adminLogin()}});
$('secretGenerate').addEventListener('click',()=>secretGenerate());
$('secretGenerateRegistration').addEventListener('click',()=>secretGenerate(true));
$('secretGenerateGroupLand').addEventListener('click',()=>secretGenerate(false,true));
$('secretGeneratePortalIm').addEventListener('click',secretGeneratePortalIm);
$('secretCopy').addEventListener('click',secretCopy);
$('secretClear').addEventListener('click',secretClear);
$('secretLoadKeys').addEventListener('click',secretLoadKeys);
$('worldAuthUseMe').addEventListener('click',worldAuthUseMe);
$('worldAuthLoad').addEventListener('click',worldAuthLoad);
$('worldAuthSave').addEventListener('click',worldAuthSave);
$('worldAuthPrincipal').addEventListener('input',worldAuthReset);
$('search').addEventListener('input',renderEndpointList);$('run').addEventListener('click',execute);$('clear').addEventListener('click',()=>{$('bearer').value='';$('apiKey').value='';$('idem').value=''});
$('loadStats').addEventListener('click',loadStatistics);$('clearStats').addEventListener('click',()=>{$('statsBearer').value='';$('statsApiKey').value=''});
$('loadGrid').addEventListener('click',loadGridLayout);$('clearGridCredentials').addEventListener('click',()=>{$('gridBearer').value='';$('gridApiKey').value='';$('gridMutationIdempotency').value=''});
$('searchGridRegions').addEventListener('click',searchGridRegions);$('jumpGridRegion').addEventListener('click',jumpGridRegion);$('gridRegionSearch').addEventListener('keydown',event=>{if(event.key==='Enter'){event.preventDefault();searchGridRegions()}});
$('loadGridNodes').addEventListener('click',loadGridManagedNodes);$('loadGridEstates').addEventListener('click',loadGridEstates);$('createGridRegion').addEventListener('click',createGridRegion);$('moveGridRegion').addEventListener('click',moveGridRegion);
$('startGridRegion').addEventListener('click',()=>runGridLifecycle('start'));$('stopGridRegion').addEventListener('click',()=>runGridLifecycle('stop'));$('restartGridRegion').addEventListener('click',()=>runGridLifecycle('restart'));
$('loadEstateManagement').addEventListener('click',loadEstateManagement);$('createEstateManagement').addEventListener('click',createEstateManagement);$('updateEstateManagement').addEventListener('click',updateEstateManagement);$('assignEstateRegion').addEventListener('click',assignEstateRegion);
$('licenseNode').addEventListener('change',showFeatureLicense);
$('loadFeatureLicenses').addEventListener('click',loadFeatureLicenses);
$('saveFeatureLicense').addEventListener('click',()=>saveFeatureLicense(false));
$('revokeFeatureLicense').addEventListener('click',()=>saveFeatureLicense(true));
$('loadNodes').addEventListener('click',loadNodes);$('clearNodesCredentials').addEventListener('click',()=>{$('nodesBearer').value='';$('nodesApiKey').value=''});
$('gridWest').addEventListener('click',()=>panGrid(-1,0));$('gridEast').addEventListener('click',()=>panGrid(1,0));$('gridSouth').addEventListener('click',()=>panGrid(0,-1));$('gridNorth').addEventListener('click',()=>panGrid(0,1));
$('validateGridPlacement').addEventListener('click',validateGridPlacement);$('gridRegionSizeX').addEventListener('change',()=>{if(gridSelected)validateGridPlacement()});$('gridRegionSizeY').addEventListener('change',()=>{if(gridSelected)validateGridPlacement()});
$('gridCellPixels').addEventListener('input',()=>{$('gridBoard').style.setProperty('--cell',$('gridCellPixels').value+'px')});$('gridFilter').addEventListener('change',applyGridFilter);$('gridShowWarp3d').addEventListener('change',applyGridImagery);

window.addEventListener('message',event=>{
  if(event.origin!==window.parent.location.origin || event.source!==window.parent ||
     !event.data || event.data.type!=='ogl-auth')return;
  const access=event.data.access_token,expiry=Number(event.data.expires_at);
  if(typeof access!=='string'||access.length<24||!Number.isFinite(expiry)||expiry<=Date.now())return;
  adminSessionToken=access;
  adminSessionExpiry=expiry;
  adminUpdateStatus('OpenGenesisLINK · Administrator angemeldet',true);
  showPage('overview');
});
init();
</script>
</body>
</html>
""";
    }
}
