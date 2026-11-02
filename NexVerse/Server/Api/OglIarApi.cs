// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using NexVerse.Core.ControlPlane;
using NexVerse.Core.Jobs;
using NexVerse.Core.Messaging;
using NexVerse.Core.Security;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    internal sealed class OglIarApi
    {
        private readonly OglFeatureControlApi m_Features;
        private readonly NexApiAuthenticator m_Authenticator;
        private readonly NexNodeRegistry m_Nodes;
        private readonly IUserAccountService m_Accounts;
        private readonly INexEventBus m_Bus;
        private readonly OglJobEventBridge m_Jobs;
        private readonly IGridUserService m_GridUsers;

        public OglIarApi(NexApiAuthenticator authenticator, NexNodeRegistry nodes, IUserAccountService accounts, INexEventBus bus, OglJobEventBridge jobs, OglFeatureControlApi features = null, IGridUserService gridUsers = null)
        {
            m_Authenticator=authenticator; m_Nodes=nodes; m_Accounts=accounts; m_Bus=bus; m_Jobs=jobs; m_Features=features; m_GridUsers=gridUsers;
        }

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            string path=(request?.UriPath??string.Empty).TrimEnd('/');
            // Personal inventory access must NEVER inherit the administrator
            // region-management scope or accept a user_id from the caller.
            if(path.StartsWith("/api/v1/iar/me/",StringComparison.OrdinalIgnoreCase))
            {
                HandlePersonal(request,response,path);
                return;
            }
            if(!Authenticate(request,response)) return;
            if(path.Equals("/api/v1/iar/export",StringComparison.OrdinalIgnoreCase)){Start(request,response,"export");return;}
            if(path.Equals("/api/v1/iar/import",StringComparison.OrdinalIgnoreCase)){Start(request,response,"import");return;}
            const string prefix="/api/v1/iar/operations/";
            if(path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&Guid.TryParse(path.Substring(prefix.Length),out Guid id))
            {
                if(!request.HttpMethod.Equals("GET",StringComparison.OrdinalIgnoreCase)){Error(response,HttpStatusCode.MethodNotAllowed,"method_not_allowed","GET ist erforderlich.");return;}
                OglJobSnapshot job=m_Jobs.Get(id.ToString()); if(job==null){Error(response,HttpStatusCode.NotFound,"operation_not_found","IAR-Vorgang wurde nicht gefunden.");return;}
                Json(response,HttpStatusCode.OK,job);return;
            }
            Error(response,HttpStatusCode.NotFound,"not_found","Unbekannter IAR-Endpunkt.");
        }

        private void Start(IOSHttpRequest request, IOSHttpResponse response, string action)
        {
            if(!request.HttpMethod.Equals("POST",StringComparison.OrdinalIgnoreCase)){Error(response,HttpStatusCode.MethodNotAllowed,"method_not_allowed","POST ist erforderlich.");return;}
            JsonDocument doc;
            try{using StreamReader r=new(request.InputStream,Encoding.UTF8,true,1024,true);doc=JsonDocument.Parse(r.ReadToEnd());}
            catch{Error(response,HttpStatusCode.BadRequest,"invalid_json","Ungueltiger JSON-Request.");return;}
            using(doc)
            {
                JsonElement root=doc.RootElement;
                string regionId=Str(root,"region_id"), userId=Str(root,"user_id"), fileName=Str(root,"file_name"), invPath=Str(root,"inventory_path");
                bool merge=root.TryGetProperty("merge",out JsonElement m)&&m.ValueKind==JsonValueKind.True;
                bool dryRun=root.TryGetProperty("dry_run",out JsonElement d)&&d.ValueKind==JsonValueKind.True;
                if(!Guid.TryParse(regionId,out _)){Error(response,HttpStatusCode.BadRequest,"invalid_region_id","region_id muss eine gueltige UUID sein.");return;}
                if(!UUID.TryParse(userId,out UUID uid)){Error(response,HttpStatusCode.BadRequest,"invalid_user_id","user_id muss eine gueltige UUID sein.");return;}
                if(string.IsNullOrWhiteSpace(fileName)||string.IsNullOrWhiteSpace(invPath)){Error(response,HttpStatusCode.BadRequest,"missing_fields","file_name und inventory_path sind erforderlich.");return;}
                UserAccount user=m_Accounts.GetUserAccount(UUID.Zero,uid);
                if(user==null){Error(response,HttpStatusCode.NotFound,"user_not_found","Benutzer wurde nicht gefunden.");return;}
                NexNodeSnapshot node=m_Nodes.FindNodeForRegion(regionId);
                if(node==null||!node.State.Equals("online",StringComparison.OrdinalIgnoreCase)){Error(response,HttpStatusCode.ServiceUnavailable,"region_node_unavailable","Simulator der Region ist nicht erreichbar.");return;}

                int capability=string.Equals(action,"import",StringComparison.OrdinalIgnoreCase)
                    ? OglArchiveFeatureGate.IarImport : OglArchiveFeatureGate.IarExport;
                if(m_Features!=null && !m_Features.Permitted(node.NodeId,capability))
                {
                    Error(response,HttpStatusCode.Forbidden,"feature_not_licensed",
                        OglArchiveFeatureGate.DeniedMessage(capability));return;
                }
                Guid id=Guid.NewGuid(); OglJobSnapshot rec=m_Jobs.RegisterOperation("archive.iar."+action,id.ToString(),"world-api",id.ToString(),new Dictionary<string,string>{{"region_id",regionId},{"user_id",userId},{"node_id",node.NodeId},{"action",action},{"file_name",fileName},{"inventory_path",invPath},{"merge",merge.ToString()},{"dry_run",dryRun.ToString()}});
                m_Bus.Publish(new NexEvent("archive.iar.requested","opengenesislink.world-api",new Dictionary<string,string>{
                    ["operation_id"]=id.ToString(),["target_node_id"]=node.NodeId,["region_id"]=regionId,["user_id"]=userId,
                    ["action"]=action,["file_name"]=fileName,["inventory_path"]=invPath,["merge"]=merge.ToString(),["dry_run"]=dryRun.ToString()
                },id.ToString()));
                Json(response,HttpStatusCode.Accepted,rec);
            }
        }

        // R632: secure, read-only first slice of the personal IAR contract.
        // A personal export/import job and file transfer are NOT implemented;
        // do not redirect resident requests to the privileged admin endpoints.
        private void HandlePersonal(IOSHttpRequest request, IOSHttpResponse response, string path)
        {
            response.AddHeader("Cache-Control","private, no-store");
            response.AddHeader("X-Content-Type-Options","nosniff");
            string scope = request.HttpMethod.Equals("GET",StringComparison.OrdinalIgnoreCase)
                ? NexScopes.InventoryRead : NexScopes.InventoryWrite;
            if (path.Equals("/api/v1/iar/me/export",StringComparison.OrdinalIgnoreCase))
                scope = NexScopes.InventoryRead;
            if (!m_Authenticator.TryAuthenticate(request,scope,out NexPrincipal principal,
                    out UserAccount account,out int status,out string error))
            {
                response.AddHeader("WWW-Authenticate","Bearer");
                Error(response,(HttpStatusCode)status,error,"IAR erfordert eine persönliche World-Anmeldung mit Inventarberechtigung.");
                return;
            }
            // API keys and service Bearers can authenticate with inventory scopes
            // but have no resident owner. Never let them act as a resident.
            if (account == null || account.PrincipalID == UUID.Zero ||
                !string.Equals(principal.Subject,account.PrincipalID.ToString(),StringComparison.OrdinalIgnoreCase))
            {
                Error(response,HttpStatusCode.Forbidden,"personal_iar_resident_required","Eine persönliche World-Anmeldung ist erforderlich.");
                return;
            }
            const string prefix = "/api/v1/iar/me/operations/";
            if (path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))
            {
                string suffix=path.Substring(prefix.Length);
                if (!Guid.TryParse(suffix,out Guid operationId) || suffix.Length!=36 ||
                    !string.Equals(operationId.ToString(),suffix,StringComparison.OrdinalIgnoreCase))
                {
                    Error(response,HttpStatusCode.NotFound,"operation_not_found","IAR-Auftrag nicht gefunden.");
                    return;
                }
                if(!request.HttpMethod.Equals("GET",StringComparison.OrdinalIgnoreCase))
                {
                    Error(response,HttpStatusCode.NotImplemented,"personal_iar_mutation_unavailable","Persönliche IAR-Verwaltung ist noch nicht verfügbar.");
                    return;
                }
                OglJobSnapshot job=m_Jobs.Get(operationId.ToString());
                if(job==null || !(string.Equals(job.Type,"archive.iar.export",StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(job.Type,"archive.iar.import",StringComparison.OrdinalIgnoreCase)) ||
                    job.Metadata==null || !job.Metadata.TryGetValue("user_id",out string ownerId) ||
                    !string.Equals(ownerId,account.PrincipalID.ToString(),StringComparison.OrdinalIgnoreCase) ||
                    // Administrator-initiated IAR jobs are not personal backups.
                    !job.Metadata.TryGetValue("personal_iar_owner",out string personalOwnerId) ||
                    !string.Equals(personalOwnerId,account.PrincipalID.ToString(),StringComparison.OrdinalIgnoreCase))
                {
                    // Unauthenticated/foreign job IDs must never disclose existence,
                    // job metadata, filesystem names, error details or operation logs.
                    Error(response,HttpStatusCode.NotFound,"operation_not_found","IAR-Auftrag nicht gefunden.");
                    return;
                }
                Json(response,HttpStatusCode.OK,new {
                    ok=true, operation_id=operationId.ToString(),
                    status=job.State.ToString().ToLowerInvariant(),
                    progress=Math.Clamp(job.ProgressPercent,0,100)
                });
                return;
            }
            if(path.Equals("/api/v1/iar/me/export",StringComparison.OrdinalIgnoreCase))
            {
                StartPersonalExport(request,response,account);
                return;
            }
            if(path.Equals("/api/v1/iar/me/import",StringComparison.OrdinalIgnoreCase))
            {
                if(!request.HttpMethod.Equals("POST",StringComparison.OrdinalIgnoreCase))
                {
                    Error(response,HttpStatusCode.MethodNotAllowed,"method_not_allowed","POST ist erforderlich.");
                    return;
                }
                Error(response,HttpStatusCode.ServiceUnavailable,"personal_iar_backend_unavailable",
                    "Persönliche IAR-Archivierung ist serverseitig noch nicht angeschlossen. Es wurde kein Backup erstellt.");
                return;
            }
            Error(response,HttpStatusCode.NotFound,"not_found","Unbekannter persönlicher IAR-Endpunkt.");
        }

        // R633: the World server, never the browser, determines the resident's
        // last/home simulator. The simulator runs its existing real GZip/TAR
        // archiver, but file delivery is deliberately a separate security gate.
        private static bool OnlyConfirmation(JsonElement root)
        {
            int count=0;
            foreach(JsonProperty property in root.EnumerateObject())
            {
                if(++count>1 || !string.Equals(property.Name,"confirm",StringComparison.Ordinal))return false;
            }
            return count==1;
        }

        private void StartPersonalExport(IOSHttpRequest request, IOSHttpResponse response, UserAccount owner)
        {
            if(!request.HttpMethod.Equals("POST",StringComparison.OrdinalIgnoreCase))
            {
                Error(response,HttpStatusCode.MethodNotAllowed,"method_not_allowed","POST ist erforderlich.");
                return;
            }
            if(!string.Equals(request.Headers?["Content-Type"]?.Split(';')[0].Trim(),
                "application/json",StringComparison.OrdinalIgnoreCase))
            {
                Error(response,HttpStatusCode.UnsupportedMediaType,"json_required","JSON ist erforderlich.");
                return;
            }
            bool confirm=false;
            try
            {
                using StreamReader reader = new(request.InputStream,Encoding.UTF8,true,1024,true);
                // Never buffer an unbounded HTTP request in the ROBUST process.
                char[] buffer=new char[257];
                int read=reader.ReadBlock(buffer,0,buffer.Length);
                if(read>256 || reader.Read()!=-1)throw new JsonException();
                using JsonDocument document=JsonDocument.Parse(new string(buffer,0,read));
                JsonElement root=document.RootElement;
                confirm=root.ValueKind==JsonValueKind.Object &&
                    root.TryGetProperty("confirm",out JsonElement value) && value.ValueKind==JsonValueKind.True &&
                    OnlyConfirmation(root);
            }
            catch(Exception e) when (e is JsonException || e is IOException)
            {
                Error(response,HttpStatusCode.BadRequest,"invalid_confirmation","Bitte den Inventarexport bestätigen.");
                return;
            }
            if(!confirm)
            {
                Error(response,HttpStatusCode.BadRequest,"confirmation_required","Bitte den Inventarexport bestätigen.");
                return;
            }
            // A missing GridUser service must not fall back to a random region.
            GridUserInfo info;
            try { info=m_GridUsers?.GetGridUserInfo(owner.PrincipalID.ToString()); }
            catch { info=null; }
            if(info==null || !string.Equals(info.UserID,owner.PrincipalID.ToString(),StringComparison.OrdinalIgnoreCase))
            {
                Error(response,HttpStatusCode.ServiceUnavailable,"personal_iar_griduser_unavailable","Deine World-Region konnte nicht ermittelt werden.");
                return;
            }
            NexNodeSnapshot node=null;
            string regionId=string.Empty;
            foreach(UUID candidate in new[]{info.LastRegionID,info.HomeRegionID})
            {
                if(candidate==UUID.Zero)continue;
                NexNodeSnapshot found=m_Nodes.FindNodeForRegion(candidate.ToString());
                if(found!=null && string.Equals(found.State,"online",StringComparison.OrdinalIgnoreCase) &&
                   !found.MaintenanceMode && !found.Draining &&
                   m_Features!=null && m_Features.Permitted(found.NodeId,OglArchiveFeatureGate.IarExport))
                {
                    node=found;regionId=candidate.ToString();break;
                }
            }
            if(node==null)
            {
                Error(response,HttpStatusCode.ServiceUnavailable,"personal_iar_node_unavailable",
                    "Für deine Inventarsicherung ist derzeit kein freigegebener Simulator erreichbar.");
                return;
            }
            Guid id=Guid.NewGuid();
            string fileName="resident-"+id.ToString("N")+".iar";
            string userId=owner.PrincipalID.ToString();
            m_Jobs.RegisterOperation("archive.iar.export",id.ToString(),
                "resident-iar",id.ToString(),new Dictionary<string,string>{
                    ["personal_iar_owner"]=userId,["user_id"]=userId,
                    ["region_id"]=regionId,["node_id"]=node.NodeId,
                    ["action"]="export",["file_name"]=fileName,["inventory_path"]="/"
                });
            m_Bus.Publish(new NexEvent("archive.iar.requested","opengenesislink.world-api",
                new Dictionary<string,string>{
                    ["operation_id"]=id.ToString(),["target_node_id"]=node.NodeId,
                    ["region_id"]=regionId,["user_id"]=userId,["action"]="export",
                    ["file_name"]=fileName,["inventory_path"]="/"
                },id.ToString()));
            Json(response,HttpStatusCode.Accepted,new {
                ok=true,operation_id=id.ToString(),status="queued",
                message="Die Inventarsicherung wurde beauftragt. Der private Download ist noch nicht verfügbar."
            });
        }

        private bool Authenticate(IOSHttpRequest req,IOSHttpResponse res)
        {
            if(m_Authenticator.TryAuthenticate(req,NexScopes.RegionsManage,out NexPrincipal _,out UserAccount _,out int status,out string error))return true;
            res.AddHeader("WWW-Authenticate","Bearer");Error(res,(HttpStatusCode)status,error,"Authentifizierung oder Berechtigung fehlgeschlagen.");return false;
        }
        private static string Str(JsonElement r,string n)=>r.TryGetProperty(n,out JsonElement v)&&v.ValueKind==JsonValueKind.String?v.GetString()?.Trim()??string.Empty:string.Empty;
        private static void Json(IOSHttpResponse r,HttpStatusCode s,object p){r.StatusCode=(int)s;r.ContentType="application/json; charset=utf-8";r.RawBuffer=JsonSerializer.SerializeToUtf8Bytes(p,new JsonSerializerOptions{WriteIndented=true});}
        private static void Error(IOSHttpResponse r,HttpStatusCode s,string e,string m)=>Json(r,s,new{error=e,message=m});
    }
}