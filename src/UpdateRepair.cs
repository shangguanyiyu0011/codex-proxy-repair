using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CodexAutoProxyRepair {
 sealed class UpdateWorkArea : IDisposable {
  public readonly string DirectoryPath;
  bool owned;
  public UpdateWorkArea() {
   DirectoryPath=Path.Combine(Path.GetTempPath(),"CodexUpdate-"+Guid.NewGuid().ToString("N"));
   if(Directory.Exists(DirectoryPath)||File.Exists(DirectoryPath))throw new IOException("临时目录冲突。");
   Directory.CreateDirectory(DirectoryPath);owned=true;
   if((File.GetAttributes(DirectoryPath)&FileAttributes.ReparsePoint)!=0)throw new IOException("临时目录不可用。");
  }
  public void Dispose(){try{if(owned&&Directory.Exists(DirectoryPath)&&(File.GetAttributes(DirectoryPath)&FileAttributes.ReparsePoint)==0)Directory.Delete(DirectoryPath,true);}catch{}}
 }
 static class UpdateRuntime {
  public const string DownloadUrl="https://persistent.oaistatic.com/codex-app-prod/ChatGPT-x64.msix";
  public const string StoreFamily="Microsoft.WindowsStore_8wekyb3d8bbwe";
  [StructLayout(LayoutKind.Sequential)] struct NativeArray {public uint count;public IntPtr data;}
  [StructLayout(LayoutKind.Sequential)] struct AppContainer {public IntPtr sid,userSid,name,displayName,description;public NativeArray capabilities,binaries;public IntPtr workingDirectory,packageFullName;}
  [DllImport("FirewallAPI.dll")] static extern uint NetworkIsolationEnumAppContainers(uint flags,out uint count,out IntPtr containers);
  [DllImport("FirewallAPI.dll")] static extern uint NetworkIsolationFreeAppContainers(IntPtr containers);
  [StructLayout(LayoutKind.Sequential)] struct SystemInfo { public ushort architecture,reserved;public uint pageSize;public IntPtr minimumAddress,maximumAddress;public UIntPtr mask;public uint processors,type,granularity;public ushort level,revision; }
  [DllImport("kernel32.dll")] static extern void GetNativeSystemInfo(out SystemInfo info);
  public static bool IsX64(){SystemInfo value;GetNativeSystemInfo(out value);return value.architecture==9;}
  public static string Quote(string value){
   var text=new StringBuilder("\"");int backslashes=0;
   foreach(char c in value){if(c=='\\'){backslashes++;continue;}if(c=='\"'){text.Append('\\',backslashes*2+1);text.Append(c);}else{text.Append('\\',backslashes);text.Append(c);}backslashes=0;}
   text.Append('\\',backslashes*2);return text.Append('"').ToString();
  }
  public static Dictionary<string,object> Helper(string mode,string packageFile,UpdateWorkArea area) {
   string script=Path.Combine(area.DirectoryPath,"update-helper.ps1");
   if(!File.Exists(script)){using(var stream=typeof(UpdateRuntime).Assembly.GetManifestResourceStream("CodexAutoProxyRepair.UpdateHelper.ps1")){if(stream==null)throw new IOException("更新组件缺失。");using(var f=new FileStream(script,FileMode.CreateNew))stream.CopyTo(f);}}
   string result=Path.Combine(area.DirectoryPath,"result-"+Guid.NewGuid().ToString("N")+".json");
   string arguments="-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(script)+" -Mode "+mode+" -ResultPath "+Quote(result);
   if(!String.IsNullOrEmpty(packageFile))arguments+=" -PackagePath "+Quote(packageFile);
   var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   using(var process=Process.Start(start)){
    Task<string> output=process.StandardOutput.ReadToEndAsync(),error=process.StandardError.ReadToEndAsync();
    if(mode=="Inspect"&&!process.WaitForExit(30000)){try{process.Kill();process.WaitForExit();}catch{}throw new IOException("只读安装检测超时，未执行修复。");}
    if(mode!="Inspect")process.WaitForExit();Task.WaitAll(output,error);
    if(!File.Exists(result))throw new IOException("Windows 更新组件未返回结果。请检查 PowerShell 和应用安装状态。");
    var value=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(result,Encoding.UTF8));
    if(process.ExitCode!=0||!Flag(value,"ok"))throw new IOException(Text(value,"message","更新操作未完成。"));
    return value;
   }
  }
  public static Dictionary<string,object> Inspect(){using(var area=new UpdateWorkArea())return Helper("Inspect",null,area);}
  public static string Text(Dictionary<string,object> d,string key,string fallback){object value;return d.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):fallback;}
  public static bool Flag(Dictionary<string,object>d,string key){object value;return d.TryGetValue(key,out value)&&value is bool&&(bool)value;}
  public static string StoreSid(){
   uint count;IntPtr items;uint result=NetworkIsolationEnumAppContainers(0,out count,out items);
   try{if(result!=0)throw new IOException("无法读取系统真实商店网络标识，本项暂不适用。");var sids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int step=Marshal.SizeOf(typeof(AppContainer));
    for(uint i=0;i<count;i++){var item=(AppContainer)Marshal.PtrToStructure(IntPtr.Add(items,checked((int)i*step)),typeof(AppContainer));if(item.sid!=IntPtr.Zero&&String.Equals(Marshal.PtrToStringUni(item.name),StoreFamily,StringComparison.OrdinalIgnoreCase))sids.Add(new SecurityIdentifier(item.sid).Value);}
    if(sids.Count!=1)throw new IOException("无法唯一识别 Microsoft Store 网络标识，不会修改权限。");foreach(string sid in sids)return sid;throw new IOException("商店标识不可用。");
   }finally{if(items!=IntPtr.Zero)NetworkIsolationFreeAppContainers(items);}
  }
  static string IsolationExe(){return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"CheckNetIsolation.exe");}
  public static HashSet<string> Exemptions(){
   var start=new ProcessStartInfo(IsolationExe(),"LoopbackExempt -s"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   using(var p=Process.Start(start)){Task<string> output=p.StandardOutput.ReadToEndAsync(),error=p.StandardError.ReadToEndAsync();p.WaitForExit();Task.WaitAll(output,error);if(p.ExitCode!=0)throw new IOException("无法读取 Windows 商店本机网络权限。");var s=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(Match m in Regex.Matches(output.Result,@"S-1-15-2(?:-\d+)+"))s.Add(m.Value);return s;}
  }
  public static bool IsLoopbackProxy(ProxyConfig p){return Loopback(p.http)&&Loopback(p.https);}
  static bool Loopback(string address){var uri=new Uri(address);if(String.Equals(uri.Host,"localhost",StringComparison.OrdinalIgnoreCase))return true;IPAddress ip;return IPAddress.TryParse(uri.Host.Trim('[',']'),out ip)&&IPAddress.IsLoopback(ip);}
  public static bool ChangeStore(bool add){
   using(var gate=new Mutex(false,@"Local\CodexAutoProxyStoreRepair")){
    bool held=false;try{
     try{held=gate.WaitOne(0);}catch(AbandonedMutexException){held=true;}
     if(!held)throw new IOException("另一个工具窗口正在处理商店权限，请稍后再试。");
     using(var ledger=RepairLedger.Open()){
      string sid=StoreSid();var baseline=Exemptions();bool existed=baseline.Contains(sid);
      if(add&&existed)return false;
      // Persist the intent before displaying UAC. Only this invocation's successful
      // native command AND post-check can promote a new permission to undoable.
      RepairLedger.Entry entry=add?ledger.BeginAdd(sid):ledger.BeginUndo(sid);
      if(!add&&!existed){ledger.Change(entry,"externallyAbsent");return false;}
      bool launched=false;
      try{
       var start=new ProcessStartInfo(IsolationExe(),"LoopbackExempt "+(add?"-a":"-d")+" -n="+StoreFamily){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
       using(var p=Process.Start(start)){if(p==null)throw new IOException("Windows 未返回权限操作进程。");launched=true;p.WaitForExit();if(p.ExitCode!=0)throw new IOException("Windows 未完成商店权限操作。");}
       bool present=Exemptions().Contains(sid);if(present!=add)throw new IOException("商店权限结果未通过复查。");
       ledger.Change(entry,add?"applied":"revoked");return true;
      }catch(Exception error){
       var native=error as Win32Exception;bool cancelled=!launched&&native!=null&&native.NativeErrorCode==1223;
       // A cancelled UAC did not launch the command. Other failures and crashes
       // retain pending/undoPending: current presence cannot establish ownership.
       if(cancelled){try{ledger.Change(entry,add?"notApplied":"applied");}catch{throw new IOException("已取消管理员确认，但记录未能保存；自动撤销已停用，请检查诊断。");}throw new OperationCanceledException("已取消 Windows 管理员确认，未执行本次权限修改。");}
       throw new IOException("商店权限操作或结果记录未完成，结果可能未知。已保留待确认记录，禁止自动撤销；请查看诊断。",error);
      }
     }
    }finally{if(held)gate.ReleaseMutex();}
   }
  }
  public static Version CheckLatest(ProxyConfig proxy,out long size){
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var request=(HttpWebRequest)WebRequest.Create(DownloadUrl);request.Method="HEAD";request.Proxy=new WebProxy(proxy.https);request.Timeout=15000;request.ReadWriteTimeout=30000;request.AllowAutoRedirect=false;
   using(var response=(HttpWebResponse)request.GetResponse()){if(response.StatusCode!=HttpStatusCode.OK)throw new IOException("官方更新信息暂不可用。");size=response.ContentLength;Version version;if(!Version.TryParse(response.Headers["x-ms-meta-package_version"],out version))throw new IOException("官方服务未提供可识别的版本信息。");return version;}
  }
  public static void Download(ProxyConfig proxy,string destination,Func<bool> cancelled,Action<int,string> progress){
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var request=(HttpWebRequest)WebRequest.Create(DownloadUrl);request.Proxy=new WebProxy(proxy.https);request.Timeout=15000;request.ReadWriteTimeout=30000;request.AllowAutoRedirect=false;
   using(var response=(HttpWebResponse)request.GetResponse()){
    if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength<1024)throw new IOException("官方安装包下载响应无效。");
    long length=response.ContentLength,total=0;int last=-1;byte[] buffer=new byte[131072];
    using(var input=response.GetResponseStream())using(var file=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
     while(true){if(cancelled())throw new OperationCanceledException("已取消下载，尚未提交更新。");int n=input.Read(buffer,0,buffer.Length);if(n==0)break;file.Write(buffer,0,n);total+=n;int percent=(int)Math.Min(100,total*100/length);if(percent!=last){last=percent;progress(percent,"下载官方包："+(total/1048576)+" / "+(length/1048576)+" MB");}}
     if(total!=length)throw new IOException("官方安装包未下载完整，已停止更新。");
    }
   }
  }
 }
 class UpdateRepairForm:Form {
  readonly Label summary=new Label();readonly TextBox detail=new TextBox();readonly ProgressBar progress=new ProgressBar();
  readonly Button inspect=new Button(),store=new Button(),undo=new Button(),official=new Button(),cancel=new Button(),openStore=new Button();
  readonly StringBuilder logSource=new StringBuilder();
  bool busy;volatile bool cancelDownload,committing;
  public UpdateRepairForm(){
   Text="修复 Codex 更新";Font=new Font("Microsoft YaHei UI",9.5f);Size=new Size(800,650);MinimumSize=new Size(660,560);StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(244,236,227);ForeColor=Color.FromArgb(41,39,35);
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=6,AutoScroll=true};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,25));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   layout.Controls.Add(new Label{Text="修复 Codex 更新",Font=new Font(Font.FontFamily,17,FontStyle.Bold),Dock=DockStyle.Fill,AutoSize=true,Margin=new Padding(0,0,0,12)},0,0);
   summary.Text="正在检测…";summary.Dock=DockStyle.Fill;summary.AutoSize=true;summary.Margin=new Padding(0,0,0,12);summary.ForeColor=Color.FromArgb(98,92,85);layout.Controls.Add(summary,0,1);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Margin=Padding.Empty};
   Configure(inspect,"重新检测",110);Configure(store,"修复商店连接",180);Configure(undo,"撤销本工具添加的商店权限",235);Configure(official,"获取官方新版",180);Configure(openStore,"打开商店验证",150);Configure(cancel,"取消下载",110);
   store.BackColor=official.BackColor=Color.FromArgb(51,84,200);store.ForeColor=official.ForeColor=Color.White;undo.Visible=false;cancel.Visible=false;
   foreach(var b in new[]{inspect,store,undo,official,openStore,cancel})buttons.Controls.Add(b);layout.Controls.Add(buttons,0,2);
   detail.Multiline=true;detail.ReadOnly=true;detail.ScrollBars=ScrollBars.Vertical;detail.Dock=DockStyle.Fill;detail.MinimumSize=new Size(0,140);detail.BackColor=Color.FromArgb(247,240,232);detail.ForeColor=Color.FromArgb(41,39,35);detail.BorderStyle=BorderStyle.None;layout.Controls.Add(detail,0,3);progress.Dock=DockStyle.Fill;layout.Controls.Add(progress,0,4);
   var footer=new Label{Text="仅适用于类似的代理场景；商店权限操作需要 Windows 管理员确认。",Dock=DockStyle.Fill,AutoSize=true};layout.Controls.Add(footer,0,5);Controls.Add(layout);
   Action fitText=()=>{int width=Math.Max(200,layout.ClientSize.Width-layout.Padding.Horizontal-6);summary.MaximumSize=new Size(width,0);footer.MaximumSize=new Size(width,0);buttons.MaximumSize=new Size(width,0);};layout.SizeChanged+=(s,e)=>fitText();fitText();
   inspect.Click+=(s,e)=>RefreshInfo();store.Click+=(s,e)=>RepairStore();undo.Click+=(s,e)=>UndoStore();official.Click+=(s,e)=>InstallOfficial();openStore.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo("ms-windows-store://home"){UseShellExecute=true});}catch(Exception x){Log(x.Message);}};cancel.Click+=(s,e)=>{cancelDownload=true;cancel.Enabled=false;Log("正在取消下载，请等待当前读取结束；尚未提交更新。");};
   FormClosing+=(s,e)=>{if(busy){e.Cancel=true;Log(committing?"Windows 正在验证或提交更新，请等待结果，不要结束更新进程。":"操作进行中，请先等待完成；下载阶段可使用取消下载。");}};
   Shown+=(s,e)=>{Log(RepairLedger.GetSafeSummary());RefreshInfo();};SetBusy(false);L10n.Attach(this);
  }
  static void Configure(Button b,string text,int width){b.Text=text;b.AutoSize=true;b.AutoSizeMode=AutoSizeMode.GrowAndShrink;b.MinimumSize=new Size(width,36);b.Padding=new Padding(12,4,12,4);b.Margin=new Padding(0,0,8,8);b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=Color.FromArgb(247,240,232);b.ForeColor=Color.FromArgb(41,39,35);b.UseVisualStyleBackColor=false;}
  void Log(string value){if(IsDisposed)return;if(InvokeRequired){BeginInvoke(new Action<string>(Log),value);return;}logSource.AppendLine(value);detail.Text=logSource.ToString();detail.SelectionStart=detail.TextLength;detail.ScrollToCaret();}
  void SetBusy(bool value){if(IsDisposed)return;if(InvokeRequired){BeginInvoke(new Action<bool>(SetBusy),value);return;}busy=value;inspect.Enabled=!value;store.Enabled=!value;official.Enabled=!value;string ledgerStatus;bool canUndo=!value&&RepairLedger.HasUndo(out ledgerStatus);undo.Enabled=canUndo;undo.Visible=true;openStore.Enabled=!value;cancel.Enabled=false;cancel.Visible=false;progress.Style=value?ProgressBarStyle.Marquee:ProgressBarStyle.Continuous;}
  void Work(Func<string> action){SetBusy(true);progress.Value=0;var worker=new BackgroundWorker();worker.DoWork+=(s,e)=>{try{e.Result=action();}catch(Exception x){e.Result=x is OperationCanceledException?x.Message:"未完成："+x.Message;}};worker.RunWorkerCompleted+=(s,e)=>{committing=false;Log(e.Error==null?Convert.ToString(e.Result):"未完成："+e.Error.Message);SetBusy(false);};worker.RunWorkerAsync();}
  public static string ReadLocalSummary(){try{var facts=UpdateRuntime.Inspect();if(!UpdateRuntime.Flag(facts,"installed"))return "不适用：未找到状态正常的官方 Store 版";if(!UpdateRuntime.Flag(facts,"storePresent"))return "商店缺失或异常：本机权限路线不适用，可检查官方包更新";ProxyConfig p;try{p=ProxyConfig.Read();}catch{return "暂无法判断：手动代理未就绪";}if(!UpdateRuntime.IsLoopbackProxy(p))return "本机环回权限修复不适用；可检查官方更新";bool allowed=UpdateRuntime.Exemptions().Contains(UpdateRuntime.StoreSid());return allowed?"商店本机权限已存在；未验证下载队列":"疑似适用：本机代理已启用，商店缺少环回例外";}catch{return "暂无法判断：无法读取商店权限或安装状态";}}
  void RefreshInfo(){Work(()=>{var facts=UpdateRuntime.Inspect();string local=ReadLocalSummary();string version=UpdateRuntime.Text(facts,"version","未知");BeginInvoke(new Action(()=>summary.Text="安装版本："+version+Environment.NewLine+local));if(!UpdateRuntime.Flag(facts,"installed"))return "请先安装官方 Store 版 Codex。";if(!UpdateRuntime.IsX64())return "当前更新包路线仅支持 x64 Windows。";var p=ProxyConfig.Read();long size;Version newest=UpdateRuntime.CheckLatest(p,out size);string message="官方候选版本："+newest+"，约 "+(size/1048576)+" MB。";Version old;if(Version.TryParse(version,out old)&&newest<=old)message+=" 当前已是相同或更高版本。";return message+Environment.NewLine+"版本检测仅供参考；实际更新前仍会核验完整数字签名与应用身份。";});}
  void RepairStore(){Work(()=>{var p=ProxyConfig.Read();var facts=UpdateRuntime.Inspect();if(!UpdateRuntime.Flag(facts,"storePresent"))throw new IOException("未找到 Microsoft Store，本项不适用。");if(!UpdateRuntime.IsLoopbackProxy(p))throw new IOException("当前代理未同时使用本机 HTTP/HTTPS 地址，本项不适用。");bool changed=UpdateRuntime.ChangeStore(true);return (changed?"已为 Microsoft Store 添加本机网络访问例外并保存撤销记录。请点击打开商店验证；无改善可撤销，关闭并重开工具后仍保留记录。":"该例外原本已经存在，没有重复修改，也没有生成新的撤销记录。请检查其他原因，或尝试官方包更新路线。")+Environment.NewLine+RepairLedger.GetSafeSummary();});}
  void UndoStore(){string status;if(!RepairLedger.HasUndo(out status)){Log(status);return;}Work(()=>{bool changed=UpdateRuntime.ChangeStore(false);return (changed?"已撤销本工具添加的商店环回例外，并复查该项已不存在。":"记录中的新增项当前已不存在；已更新历史，没有再次修改权限。")+Environment.NewLine+RepairLedger.GetSafeSummary();});}
  void InstallOfficial(){
   cancelDownload=false;
   Work(()=>{if(!UpdateRuntime.IsX64())throw new IOException("当前官方包路线仅支持 x64 Windows。");var proxy=ProxyConfig.Read();var facts=UpdateRuntime.Inspect();if(!UpdateRuntime.Flag(facts,"installed")||!String.Equals(UpdateRuntime.Text(facts,"architecture",""),"X64",StringComparison.OrdinalIgnoreCase))throw new IOException("未找到适用的官方 x64 应用，本项不适用。");long bytes;Version newest=UpdateRuntime.CheckLatest(proxy,out bytes);Version current;if(Version.TryParse(UpdateRuntime.Text(facts,"version",""),out current)&&newest<=current)return "当前已为相同或更高版本，无需下载更新。";
    using(var area=new UpdateWorkArea()){
     string destination=Path.Combine(area.DirectoryPath,"ChatGPT-x64.msix");BeginInvoke(new Action(()=>{cancel.Enabled=true;cancel.Visible=true;progress.Style=ProgressBarStyle.Continuous;}));Log("开始从固定 OpenAI 官方地址下载。下载阶段可以取消。");
     UpdateRuntime.Download(proxy,destination,()=>cancelDownload,(percent,message)=>{if(!IsDisposed)BeginInvoke(new Action(()=>{progress.Value=percent;summary.Text=message;}));});
     if(cancelDownload)throw new OperationCanceledException("已取消下载，未提交更新。");
     committing=true;BeginInvoke(new Action(()=>{cancel.Enabled=false;cancel.Visible=false;progress.Style=ProgressBarStyle.Marquee;summary.Text="正在验签并交由 Windows 暂存更新，请等待完成。";}));Log("完整下载已完成。正在核验签名、官方身份、架构及更新版本；提交后不再提供取消。");
     var result=UpdateRuntime.Helper("ApplyUpdate",destination,area);return UpdateRuntime.Text(result,"message","操作完成。")+" 版本："+UpdateRuntime.Text(result,"version","");
    }
   });
  }
 }
}
