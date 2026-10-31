// SPDX-License-Identifier: MPL-2.0
using System;
using System.IO;
using System.Linq;
using NexVerse.Server.Api;

int done=0;
void Check(bool good,string description)
{
 if(!good)throw new Exception("R619_NPC_CHAT_FAIL "+description);
 ++done;Console.WriteLine("R619_NPC_CHAT_PASS "+description);
}
string dir=Path.Combine(Path.GetTempPath(),"nexnpc-chat-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
try
{
 string file=Path.Combine(dir,"npc-only.sqlite");
 Guid ownerA=Guid.NewGuid(),ownerB=Guid.NewGuid(),npc1=Guid.NewGuid(),npc2=Guid.NewGuid();
 Guid event1=Guid.NewGuid(),event2=Guid.NewGuid(),event3=Guid.NewGuid();
 var archive=new NexNpcChatArchive(file,30);
 Check(archive.RetentionDays==30,"Default 30 day retention");
 Check(!archive.Enabled(ownerA)&&!archive.Enabled(ownerB),"Archive disabled per citizen by default");
 Check(!archive.Append(event1,ownerA,npc1,"owner","before consent"),
    "IM before explicit consent not stored");
 Check(archive.Read(ownerA,npc1).Count==0,"Non-consented IM absent from disk");
 archive.SetEnabled(ownerA,true);
 Check(archive.Enabled(ownerA),"Owner enabled direct NPC IM archive");
 Check(archive.Append(event1,ownerA,npc1,"owner","Hello my NPC"),
    "Owner direct IM stored with active consent");
 Check(archive.Append(event2,ownerA,npc1,"npc","Hello owner"),
    "NPC reply stored with active consent");
 Check(!archive.Append(event2,ownerA,npc1,"npc","replayed"),
    "Repeated signed event id cannot duplicate");
 Check(archive.Read(ownerA,npc1).Count==2,
    "Two different direct IM events belong to same NPC");
 Check(archive.Read(ownerA,npc1)[0].sender=="owner"&&
    archive.Read(ownerA,npc1)[1].sender=="npc",
    "Ordered conversation identifies both genuine sender roles");
 Check(archive.Read(ownerB,npc1).Count==0,
    "Another World citizen can never read owner chat");
 Check(archive.Read(ownerA,npc2).Count==0,
    "Another owned NPC does not receive unrelated conversation");
 Check(!archive.Append(Guid.NewGuid(),ownerB,npc1,"npc","unauthorized"),
    "No storage for owner with no consent");
 Check(!archive.Append(Guid.NewGuid(),ownerA,npc1,"third_party","private"),
    "Other sender roles cannot be archived");
 Check(!archive.Append(Guid.NewGuid(),ownerA,npc1,"npc",new string('x',4001)),
    "Oversize message denied");
 Check(!archive.Append(Guid.Empty,ownerA,npc1,"npc","zero event"),
    "Missing event identifier denied");
 Check(!archive.Append(Guid.NewGuid(),ownerA,ownerA,"npc","fake NPC owner"),
    "Identical NPC/owner identifier denied");
 Check(!archive.Append(Guid.NewGuid(),ownerA,Guid.Empty,"npc","zero NPC"),
    "Missing NPC identifier denied");
 Check(!archive.Append(Guid.NewGuid(),Guid.Empty,npc1,"npc","zero owner"),
    "Missing owner identifier denied");
 var reopened=new NexNpcChatArchive(file,30);
 Check(reopened.Enabled(ownerA),"Opt-in persists after World restart");
 Check(reopened.Read(ownerA,npc1).Count==2,
    "Direct NPC messages survive World restart");
 archive.Erase(ownerA);
 Check(archive.Enabled(ownerA)&&archive.Read(ownerA,npc1).Count==0,
    "Delete history erases all records but retains explicit opt-in");
 Check(archive.Append(event3,ownerA,npc2,"npc","second NPC"),
    "Opt-in applies to all own NPCs");
 archive.SetEnabled(ownerA,false);
 Check(!archive.Enabled(ownerA)&&archive.Read(ownerA,npc2).Count==0,
    "Opt-out immediately purges retained transcript records");
 Check(!reopened.Append(Guid.NewGuid(),ownerA,npc2,"owner","after opt-out"),
    "Existing archive objects honor latest consent after opt-out");
 Check(new NexNpcChatArchive(Path.Combine(dir,"clamp-low.sqlite"),1).RetentionDays==7,
    "Retention cannot drop below seven days");
 Check(new NexNpcChatArchive(Path.Combine(dir,"clamp-high.sqlite"),999).RetentionDays==90,
    "Retention cannot exceed 90 days");
 archive.SetEnabled(ownerB,true);
 Check(archive.Append(Guid.NewGuid(),ownerB,npc1,"npc","other owner test"),
    "Independent owner opt-in works");
 archive.SetEnabled(ownerA,true);
 Check(archive.Read(ownerA,npc1).Count==0&&archive.Read(ownerB,npc1).Count==1,
    "Opt-out has no side effects on a different owner");
 Console.WriteLine("R619_NPC_CHAT_STORE="+done+"/"+done+"_PASS");
}
finally{try{Directory.Delete(dir,true);}catch(IOException){}}
