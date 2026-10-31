// SPDX-License-Identifier: MPL-2.0
// Standalone .NET 8 source-level tests for the World NPC owner registry.
// No simulator, database, personal credentials or production API needed.
using System;
using System.Collections.Generic;
using System.Linq;
using NexVerse.Server.Api;

int checks=0;
void Check(bool valid,string name)
{
    if(!valid)throw new Exception("NPC_DIRECTORY_FAIL "+name);
    ++checks;
    Console.WriteLine("NPC_DIRECTORY_PASS "+name);
}
var owner1=Guid.Parse("a1111111-1111-4111-8111-111111111111");
var owner2=Guid.Parse("a2222222-2222-4222-8222-222222222222");
var region1=Guid.Parse("b1111111-1111-4111-8111-111111111111");
var region2=Guid.Parse("b2222222-2222-4222-8222-222222222222");
var id1=Guid.Parse("c1111111-1111-4111-8111-111111111111");
var id2=Guid.Parse("c2222222-2222-4222-8222-222222222222");
NexNpcOwnerEntry N(Guid id,Guid owner,string name="NPC")=>new()
{
    npc_id=id,owner_principal_id=owner,name=name,region_name="Testregion"
};
var index=new NexNpcSnapshotIndex(2);
Check(!index.TryOwned(owner1,out _,out _),"No verified snapshots means unavailable");
Check(!index.Replace(Guid.Empty,1,new(){N(id1,owner1)}),"Zero region denied");
Check(!index.Replace(region1,1,new(){N(Guid.Empty,owner1)}),"Zero NPC denied");
Check(!index.Replace(region1,1,new(){N(id1,Guid.Empty)}),"Unowned NPC denied");
Check(!index.Replace(region1,1,new(){N(id1,owner1),N(id1,owner1)}),"Duplicate IDs in region denied");
Check(!index.Replace(region1,1,new(){N(id1,owner1,"  ")}),"Blank name denied");
Check(!index.Replace(region1,1,Enumerable.Range(0,1001).Select(i=>
    N(Guid.NewGuid(),owner1)).ToList()),"Oversized region denied");
Check(index.Replace(region1,1,new(){N(id1,owner1)}),"Valid signed-region snapshot accepted");
Check(!index.TryOwned(owner1,out _,out _),"Partial expected region coverage unavailable");
Check(index.Replace(region2,1,new(){N(id2,owner1)}),"Second region snapshot accepted");
Check(index.TryOwned(owner1,out var mine,out long version)&&
    mine.Count==2&&mine.Any(x=>x.npc_id==id1)&&mine.Any(x=>x.npc_id==id2),
    "All owner NPCs appear across both regions");
Check(index.TryOwned(owner2,out var other,out _)&&other.Count==0,
    "One resident cannot read another resident NPCs");
Check(!index.Replace(region1,1,new(){N(id1,owner2)}),"Replay or older sequence denied");
Check(index.TryOwned(owner1,out var preserved,out long sameVersion) &&
    preserved.Count==2 && version==sameVersion,
    "Replay never changes owner mappings or generation");
Check(index.Replace(region2,2,new(){N(id2,owner2)}),"Updated owner mapping accepted");
Check(index.TryOwned(owner1,out var first,out long changedVersion)&&first.Count==1
    &&changedVersion>version,"Reassignment removes NPC from original owner");
Check(index.TryOwned(owner2,out var second,out _)&&second.Count==1 &&
    second[0].npc_id==id2,"Reassigned NPC appears only for new owner");
Check(index.Replace(region2,3,new(){N(id1,owner2)}),"Conflicting region claim syntactically accepted");
Check(!index.TryOwned(owner1,out _,out _),"Conflicting cross-region ownership fails closed");
Check(index.Replace(region2,4,new(){N(id1,owner1)}),"Same-owner cross-region reconciliation accepted");
Check(index.TryOwned(owner1,out var deduplicated,out _)&&
    deduplicated.Count==1,"Duplicate NPC in two regions shown once");
Check(index.Replace(region1,2,new()),"Empty verified region snapshot accepted");
Check(index.TryOwned(owner1,out var none,out _)&&none.Count==1,
    "Empty region does not delete NPCs in another valid region");
bool badCoverage=false;
try{new NexNpcSnapshotIndex(0);}catch(ArgumentOutOfRangeException){badCoverage=true;}
Check(badCoverage,"Unconfigured expected region coverage rejected");
Console.WriteLine("NPC_DIRECTORY_ALL="+checks+"/"+checks+"_PASS");
