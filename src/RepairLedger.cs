using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace CodexAutoProxyRepair {
 // This is an ownership journal, not a snapshot of Windows permissions. An
 // interrupted pending operation never becomes undoable merely because a SID exists.
 internal sealed class RepairLedger : IDisposable {
  const int Schema=1, MaxHistory=12, MaxBytes=65536;
  const string Family="Microsoft.WindowsStore_8wekyb3d8bbwe";
  internal sealed class Entry {
   public string Id{get;set;} public string Family{get;set;} public string Sid{get;set;}
   public string Baseline{get;set;} public string State{get;set;}
   public string AddedUtc{get;set;} public string UpdatedUtc{get;set;}
  }
  public sealed class Document {
   public int SchemaVersion{get;set;} public string Scope{get;set;}
   public List<Entry> Records{get;set;}
  }
  [StructLayout(LayoutKind.Sequential)] struct Blob {public int Size;public IntPtr Data;}
  [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CryptProtectData(ref Blob input,string description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
  [DllImport("crypt32.dll",SetLastError=true)] static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
  [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
  readonly string directory,path,scope;readonly SecurityIdentifier owner;
  Mutex gate;bool held;Document data;
  static string Now(){return DateTime.UtcNow.ToString("o");}
  static string Scope(){
   string machine;using(var key=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")){machine=key==null?null:key.GetValue("MachineGuid") as string;}
   string user=WindowsIdentity.GetCurrent().User.Value;
   if(String.IsNullOrWhiteSpace(machine)||String.IsNullOrWhiteSpace(user))throw new IOException("无法确认当前电脑和用户，已停用持久撤销。");
   using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes("CodexAutoProxyRepair/scope/v1\n"+machine+"\n"+user))).Replace("-","").ToLowerInvariant();
  }
  internal static RepairLedger Open(){return new RepairLedger(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexAutoProxyRepair"),Scope());}
  RepairLedger(string directory,string scope){
   this.directory=Path.GetFullPath(directory);this.path=Path.Combine(this.directory,"store-repair-v1.dat");this.scope=scope;owner=WindowsIdentity.GetCurrent().User;
   try{
    gate=new Mutex(false,@"Global\CodexAutoProxyRepairLedger-"+scope);try{held=gate.WaitOne(0);}catch(AbandonedMutexException){held=true;}
    if(!held)throw new IOException("另一个工具正在处理撤销记录，请稍后重试。");
    CheckParents();
    if(File.Exists(this.directory))throw new IOException("撤销记录目录发生冲突，未修改商店权限。");
    if(Directory.Exists(this.directory)){
     CheckOwned(this.directory,true);
     if(!File.Exists(path))throw new IOException("撤销记录目录没有有效归属记录，已安全拒绝写入。");
     data=Read();
    }else{
     var security=new DirectorySecurity();security.SetOwner(owner);security.SetAccessRuleProtection(true,false);
     security.AddAccessRule(new FileSystemAccessRule(owner,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
     security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
     Directory.CreateDirectory(this.directory,security);CheckOwned(this.directory,true);
     data=new Document{SchemaVersion=Schema,Scope=scope,Records=new List<Entry>()};Save(false);
    }
   }catch{Dispose();throw;}
  }
  void CheckParents(){
   string current=directory;while(!String.IsNullOrEmpty(current)){if(File.Exists(current))throw new IOException("撤销记录路径冲突。");if(Directory.Exists(current)&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("撤销记录路径包含重定向，已安全拒绝。");current=Path.GetDirectoryName(current);}
  }
  void CheckOwned(string target,bool isDirectory){
   CheckParents();if((File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)throw new IOException("撤销记录不可为链接。");
   FileSystemSecurity security=isDirectory?(FileSystemSecurity)Directory.GetAccessControl(target):File.GetAccessControl(target);
   if(!owner.Equals(security.GetOwner(typeof(SecurityIdentifier))))throw new IOException("撤销记录不属于当前用户，已拒绝使用。");
   var system=new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null);
   foreach(FileSystemAccessRule rule in security.GetAccessRules(true,true,typeof(SecurityIdentifier))){
    FileSystemRights writes=FileSystemRights.Write|FileSystemRights.Modify|FileSystemRights.Delete|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership;
    if(rule.AccessControlType==AccessControlType.Allow&&(rule.FileSystemRights&writes)!=0&&!owner.Equals(rule.IdentityReference)&&!system.Equals(rule.IdentityReference))throw new IOException("撤销记录允许其他账户写入，已拒绝使用。");
   }
  }
  static byte[] Protect(byte[] bytes,bool decrypt){
   Blob input=new Blob(),output=new Blob();input.Size=bytes.Length;input.Data=Marshal.AllocHGlobal(bytes.Length);
   try{Marshal.Copy(bytes,0,input.Data,bytes.Length);bool ok=decrypt?CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptProtectData(ref input,"CodexAutoProxyRepair ledger v1",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);if(!ok)throw new IOException("撤销记录损坏、被修改或不属于当前用户，已安全拒绝。");byte[] result=new byte[output.Size];Marshal.Copy(output.Data,result,0,result.Length);return result;}
   finally{Marshal.FreeHGlobal(input.Data);if(output.Data!=IntPtr.Zero)LocalFree(output.Data);}
  }
  Document Read(){
   try{
    CheckOwned(path,false);byte[] bytes;
    using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(file.Length<1||file.Length>MaxBytes)throw new IOException();bytes=new byte[(int)file.Length];int done=0;while(done<bytes.Length){int n=file.Read(bytes,done,bytes.Length-done);if(n==0)throw new IOException();done+=n;}}
    var value=new JavaScriptSerializer().Deserialize<Document>(Encoding.UTF8.GetString(Protect(bytes,true)));Validate(value);return value;
   }catch(IOException){throw;}catch{throw new IOException("撤销记录无法验证，已安全拒绝；不会删除任何现有权限。");}
  }
  void Validate(Document value){
   if(value==null||value.SchemaVersion!=Schema||!String.Equals(value.Scope,scope,StringComparison.Ordinal)||value.Records==null||value.Records.Count>MaxHistory)throw new IOException("撤销记录版本或电脑/用户归属不匹配，已安全拒绝。");
   var ids=new HashSet<string>(StringComparer.Ordinal);int active=0;
   foreach(var e in value.Records){Guid id;DateTime added,updated;
    if(e==null||!Guid.TryParseExact(e.Id,"N",out id)||!ids.Add(e.Id)||e.Family!=Family||e.Baseline!="absent"||!ValidSid(e.Sid)||!DateTime.TryParse(e.AddedUtc,out added)||!DateTime.TryParse(e.UpdatedUtc,out updated)||!State(e.State))throw new IOException("撤销记录内容无效，已安全拒绝。");
    if(e.State=="pending"||e.State=="applied"||e.State=="undoPending")active++;
   }
   if(active>1)throw new IOException("撤销记录存在冲突，已安全拒绝。");
  }
  static bool State(string state){return state=="pending"||state=="applied"||state=="undoPending"||state=="notApplied"||state=="revoked"||state=="externallyAbsent";}
  static bool ValidSid(string value){try{return value!=null&&value.StartsWith("S-1-15-2-",StringComparison.Ordinal)&&new SecurityIdentifier(value).Value==value;}catch{return false;}}
  void Save(bool replace){
   Validate(data);CheckOwned(directory,true);if(replace)CheckOwned(path,false);
   byte[] bytes=Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(data)),false);if(bytes.Length>MaxBytes)throw new IOException("撤销记录超出大小限制。");
   string temporary=Path.Combine(directory,".ledger-"+Guid.NewGuid().ToString("N")+".tmp");
   // An elevated token can otherwise give new files the Administrators owner,
   // even inside our user-owned directory. Apply owner and ACL at creation.
   var security=new FileSecurity();security.SetOwner(owner);security.SetAccessRuleProtection(true,false);
   security.AddAccessRule(new FileSystemAccessRule(owner,FileSystemRights.FullControl,AccessControlType.Allow));
   security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),FileSystemRights.FullControl,AccessControlType.Allow));
   try{using(var file=new FileStream(temporary,FileMode.CreateNew,FileSystemRights.Write,FileShare.None,4096,FileOptions.WriteThrough,security)){file.Write(bytes,0,bytes.Length);file.Flush(true);}CheckOwned(temporary,false);if(replace){CheckOwned(path,false);File.Replace(temporary,path,null);}else File.Move(temporary,path);}
   finally{try{if(File.Exists(temporary)&&(File.GetAttributes(temporary)&FileAttributes.ReparsePoint)==0)File.Delete(temporary);}catch{}}
  }
  internal Entry BeginAdd(string sid){
   if(!ValidSid(sid))throw new IOException("真实商店 SID 无效。");
   foreach(var e in data.Records){if(e.State=="pending"||e.State=="undoPending")throw new IOException("上次商店权限操作中断或结果未知，不能自动确认归属或撤销。请先检查诊断记录。");if(e.State=="applied"){if(!String.Equals(e.Sid,sid,StringComparison.OrdinalIgnoreCase))throw new IOException("现有新增记录与真实商店 SID 不匹配，不会转移或覆盖撤销归属。");e.State="externallyAbsent";e.UpdatedUtc=Now();}}
   while(data.Records.Count>=MaxHistory)data.Records.RemoveAt(0);
   var value=new Entry{Id=Guid.NewGuid().ToString("N"),Family=Family,Sid=sid,Baseline="absent",State="pending",AddedUtc=Now(),UpdatedUtc=Now()};data.Records.Add(value);Save(true);return value;
  }
  internal Entry BeginUndo(string sid){
   Entry entry=null;foreach(var e in data.Records){if(e.State=="pending"||e.State=="undoPending")throw new IOException("上次权限操作结果未知，已禁止自动撤销；不会据现有权限推断归属。");if(e.State=="applied")entry=e;}
   if(entry==null||entry.Baseline!="absent"||entry.Family!=Family||!String.Equals(entry.Sid,sid,StringComparison.OrdinalIgnoreCase))throw new IOException("没有与当前真实商店 SID 匹配的本工具新增记录，不会删除原有权限。");
   Change(entry,"undoPending");return entry;
  }
  internal void Change(Entry entry,string state){if(!data.Records.Contains(entry)||!State(state))throw new IOException("撤销记录状态无效。");entry.State=state;entry.UpdatedUtc=Now();Save(true);}
  internal static bool HasUndo(out string message){
   try{string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexAutoProxyRepair");if(!Directory.Exists(root)&&!File.Exists(root)){message="尚无本工具新增的商店权限记录。";return false;}using(var ledger=Open()){bool result=false;foreach(var e in ledger.data.Records){if(e.State=="pending"||e.State=="undoPending"){message="存在中断或结果未知的权限操作；自动撤销已停用，请查看诊断。";return false;}if(e.State=="applied")result=true;}message=result?"已保存本工具新增记录，可在重新打开工具后撤销；撤销前会复查真实商店 SID。":"没有待撤销的本工具新增项。";return result;}}
   catch(Exception){message="撤销记录无法验证，自动撤销已停用；不会删除现有权限。";return false;}
  }
  internal static string GetSafeSummary(){
   string status;HasUndo(out status);var text=new StringBuilder(status);
   try{string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexAutoProxyRepair");if(!Directory.Exists(root))return text.ToString();using(var ledger=Open()){foreach(var e in ledger.data.Records)text.AppendLine().Append("商店权限记录：").Append(StateLabel(e.State)).Append("；添加时间 UTC ").Append(e.AddedUtc).Append("；状态时间 UTC ").Append(e.UpdatedUtc);}}catch{text.AppendLine().Append("历史记录读取失败或归属校验不通过；未执行权限修改。");}return text.ToString();
  }
  static string StateLabel(string state){switch(state){case "pending":return "添加待确认（不可自动撤销）";case "applied":return "新增已核验，待撤销";case "undoPending":return "撤销待确认（不可继续自动撤销）";case "notApplied":return "未添加";case "revoked":return "已撤销并核验";default:return "新增项已在外部移除";}}
  public void Dispose(){if(held){held=false;gate.ReleaseMutex();}if(gate!=null){gate.Dispose();gate=null;}}
  internal static int SelfTest(){
   // Isolated directory, synthetic scope/SID. No real journal or Windows permission calls.
   string root=Path.Combine(Path.GetTempPath(),"CodexLedgerTest-"+Guid.NewGuid().ToString("N"));string sid="S-1-15-2-1-2-3-4-5-6-7";int checks=0;
   try{
    using(var ledger=new RepairLedger(root,"selftest-"+Guid.NewGuid().ToString("N"))){if(ledger.data.Records.Count!=0)throw new Exception();checks++;var entry=ledger.BeginAdd(sid);ledger.Change(entry,"applied");}
    // Read the protected fixture only to recover its synthetic scope for reopening.
    string savedScope;using(var file=new FileStream(Path.Combine(root,"store-repair-v1.dat"),FileMode.Open,FileAccess.Read)){byte[] bytes=new byte[(int)file.Length];file.Read(bytes,0,bytes.Length);savedScope=new JavaScriptSerializer().Deserialize<Document>(Encoding.UTF8.GetString(Protect(bytes,true))).Scope;}
    using(var ledger=new RepairLedger(root,savedScope)){if(ledger.data.Records[0].State!="applied")throw new Exception();checks++;bool denied=false;try{ledger.BeginUndo("S-1-15-2-9");}catch(IOException){denied=true;}if(!denied)throw new Exception();checks++;var entry=ledger.BeginUndo(sid);ledger.Change(entry,"revoked");}
    using(var ledger=new RepairLedger(root,savedScope)){if(ledger.data.Records[0].State!="revoked")throw new Exception();checks++;ledger.BeginAdd(sid);}
    using(var ledger=new RepairLedger(root,savedScope)){var entry=ledger.data.Records[ledger.data.Records.Count-1];if(entry.State!="pending")throw new Exception();bool denied=false;try{ledger.BeginUndo(sid);}catch(IOException){denied=true;}if(!denied)throw new Exception();checks++;ledger.Change(entry,"notApplied");}
    bool rejected=false;try{using(var ledger=new RepairLedger(root,"wrong-scope")){} }catch(IOException){rejected=true;}if(!rejected)throw new Exception();checks++;
    File.WriteAllText(Path.Combine(root,"store-repair-v1.dat"),"broken");rejected=false;try{using(var ledger=new RepairLedger(root,savedScope)){} }catch(IOException){rejected=true;}if(!rejected)throw new Exception();checks++;
    return checks==7?0:1;
   }catch(Exception error){Console.Error.WriteLine("RepairLedger self-test failed after "+checks+" checks: "+error.GetType().Name+": "+error.Message);return 1;}finally{try{if(Directory.Exists(root)&&(File.GetAttributes(root)&FileAttributes.ReparsePoint)==0)Directory.Delete(root,true);}catch{}}
  }
 }
}
