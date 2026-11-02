// SPDX-License-Identifier: MPL-2.0
using System;
using System.IO;
using System.Security.Cryptography;
using NexVerse.Core.Jobs;

string root=Path.Combine(Path.GetTempPath(),"ogl-iar-r633-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int cases=0;
void Pass(bool ok,string message)
{
    if(!ok)throw new Exception("R633 FAIL: "+message);
    cases++;
    Console.WriteLine("R633_"+message+"=PASS");
}
void Denied<T>(Action action,string message) where T:Exception
{
    try{action();}
    catch(T){Pass(true,message);return;}
    throw new Exception("R633 FAIL: "+message+" unexpectedly succeeded");
}
try
{
    Guid id=Guid.NewGuid(), owner=Guid.NewGuid(), stranger=Guid.NewGuid();
    byte[] data=new byte[]{0x1f,0x8b,0x08,0x00,0x01,0x02,0x03,0x04};
    string digest=Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    var store=new OglPersonalIarTransferStore(root);
    Denied<ArgumentException>(()=>store.Begin(Guid.Empty,owner,8),"REJECT_EMPTY_OPERATION");
    Denied<ArgumentException>(()=>store.Begin(id,Guid.Empty,8),"REJECT_EMPTY_OWNER");
    Denied<ArgumentOutOfRangeException>(()=>store.Begin(id,owner,0),"REJECT_ZERO_SIZE");
    Denied<ArgumentOutOfRangeException>(()=>store.Begin(id,owner,OglPersonalIarTransferStore.MaximumArchiveBytes+1),"REJECT_OVERSIZE");
    store.Begin(id,owner,8);
    Pass(File.Exists(Path.Combine(root,id.ToString("N")+".json")),"PRIVATE_MANIFEST_CREATED");
    Denied<InvalidOperationException>(()=>store.Begin(id,owner,8),"NO_DUPLICATE_OPERATION");
    Denied<FileNotFoundException>(()=>store.Append(id,stranger,0,data.AsMemory(0,4)),"OWNER_ISOLATION_ON_APPEND");
    Denied<InvalidOperationException>(()=>store.Append(id,owner,1,data.AsMemory(0,4)),"OFFSET_MUST_MATCH");
    Denied<ArgumentOutOfRangeException>(()=>store.Append(id,owner,0,ReadOnlyMemory<byte>.Empty),"NO_EMPTY_CHUNK");
    Denied<InvalidOperationException>(()=>store.VerifyChecksum(id,owner,digest),"NO_PREMATURE_VERIFY");
    Pass(store.Append(id,owner,0,data.AsMemory(0,4))==4,"FIRST_CHUNK_ACCEPTED");
    Denied<InvalidOperationException>(()=>store.Append(id,owner,0,data.AsMemory(0,4)),"NO_REPLAY_OVERWRITE");
    Denied<InvalidOperationException>(()=>store.Append(id,owner,4,data.AsMemory(0,8)),"TOTAL_SIZE_LIMIT");
    store=new OglPersonalIarTransferStore(root);
    Pass(store.Append(id,owner,4,data.AsMemory(4,4))==8,"RESUME_AFTER_RESTART");
    Denied<FileNotFoundException>(()=>store.VerifyChecksum(id,stranger,digest),"OWNER_ISOLATION_ON_VERIFY");
    Denied<InvalidDataException>(()=>store.VerifyChecksum(id,owner,new string('0',64)),"WRONG_SHA_FAILS_CLOSED");
    Pass(!File.Exists(Path.Combine(root,id.ToString("N")+".iar")),"NO_FALSE_ARCHIVE_ON_BAD_SHA");
    Pass(store.VerifyChecksum(id,owner,digest)==digest,"SHA256_VALIDATED");
    Denied<InvalidOperationException>(()=>store.Append(id,owner,8,data.AsMemory(0,1)),"NO_APPEND_AFTER_VERIFY");
    Denied<FileNotFoundException>(()=>store.OpenChecksumVerified(id,stranger).Dispose(),"OWNER_ISOLATION_ON_READ");
    using(Stream input=store.OpenChecksumVerified(id,owner))
    {
        byte[] actual=new byte[data.Length];
        int read=input.Read(actual,0,actual.Length);
        Pass(read==data.Length && actual.AsSpan().SequenceEqual(data),"PRIVATE_CHECKSUM_VERIFIED_STREAM");
    }
    Denied<FileNotFoundException>(()=>store.Remove(id,stranger),"OWNER_ISOLATION_ON_DELETE");
    store.Remove(id,owner);
    Pass(!File.Exists(Path.Combine(root,id.ToString("N")+".json")) &&
         !File.Exists(Path.Combine(root,id.ToString("N")+".iar")),"OWNER_ERASURE");
    Denied<FileNotFoundException>(()=>store.OpenChecksumVerified(id,owner).Dispose(),"DELETED_TRANSFER_NOT_READABLE");
    Guid interrupted=Guid.NewGuid();
    store.Begin(interrupted,owner,8);
    store.Append(interrupted,owner,0,data.AsMemory(0,4));
    using(FileStream damaged=new(Path.Combine(root,interrupted.ToString("N")+".part"),FileMode.Append))
        damaged.WriteByte(0x7f);
    Denied<InvalidDataException>(()=>store.Append(interrupted,owner,4,data.AsMemory(4,4)),
        "INCONSISTENT_RESTART_STATE_FAILS_CLOSED");
    store.Remove(interrupted,owner);
    Guid symlinkId=Guid.NewGuid();
    store.Begin(symlinkId,owner,8);
    string symlink=Path.Combine(root,symlinkId.ToString("N")+".part");
    File.CreateSymbolicLink(symlink,Path.Combine(root,"external-target"));
    Denied<InvalidDataException>(()=>store.Append(symlinkId,owner,0,data),"REJECT_SYMLINKED_UPLOAD_TARGET");
    File.Delete(symlink);
    store.Remove(symlinkId,owner);
    Console.WriteLine("R633_PERSONAL_IAR_TRANSFER_REGRESSION="+cases+"/"+cases+"_PASS");
}
finally { Directory.Delete(root,true); }