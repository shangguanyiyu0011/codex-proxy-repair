using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CodexAutoProxyRepair {
 static class ToolDiagnostics {
  internal static string Clean(string text) {
   if(String.IsNullOrEmpty(text))return text;
   string value=Regex.Replace(text,@"(?i)(bearer\s+|(?:token|api[_-]?key|password|secret)\s*[:=]\s*)[^\s,;""}]+","$1[redacted]");
   value=Regex.Replace(value,@"(?i)[a-z]:\\Users\\[^\\\s]+","[user-profile]");
   value=Regex.Replace(value,@"(?i)(?:https?|socks5?)://[^\s]+","[address]");
   foreach(string privateValue in new[]{Environment.UserName,Environment.MachineName})if(!String.IsNullOrEmpty(privateValue)&&privateValue.Length>2)value=Regex.Replace(value,Regex.Escape(privateValue),"[redacted]",RegexOptions.IgnoreCase);
   return value.Length>1000?value.Substring(0,1000):value;
  }
  static Dictionary<string,object> Route(string value){var uri=new Uri(value);IPAddress ip;bool loopback=String.Equals(uri.Host,"localhost",StringComparison.OrdinalIgnoreCase)||(IPAddress.TryParse(uri.Host.Trim('[',']'),out ip)&&IPAddress.IsLoopback(ip));return new Dictionary<string,object>{{"type",loopback?"loopback":"remote-host-redacted"},{"port",uri.Port},{"scheme",uri.Scheme}};}
  public static Dictionary<string,object> Collect(){
   var result=new Dictionary<string,object>{{"schema",1},{"createdUtc",DateTime.UtcNow.ToString("o")},{"toolVersion",typeof(ToolDiagnostics).Assembly.GetName().Version.ToString()},{"signatureStatus","not-evaluated-by-diagnostic"},{"privacy","Whitelist only: no chat content, tokens, raw logs, command lines, machine identifiers or ledger ciphertext."}};
   result["environment"]=new Dictionary<string,object>{{"windowsVersion",Environment.OSVersion.Version.ToString()},{"is64BitOS",Environment.Is64BitOperatingSystem},{"is64BitProcess",Environment.Is64BitProcess},{"dotNetRuntime",Environment.Version.ToString()}};
   try{var p=ProxyConfig.Read();result["proxy"]=new Dictionary<string,object>{{"status","supported-manual"},{"http",Route(p.http)},{"https",Route(p.https)},{"sameRoute",p.http==p.https}};}catch(Exception e){result["proxy"]=new Dictionary<string,object>{{"status","unavailable-or-unsupported"},{"errorType",e.GetType().Name}};}
   try{var f=UpdateRuntime.Inspect();Version version;result["officialPackage"]=new Dictionary<string,object>{{"recognized",UpdateRuntime.Flag(f,"installed")},{"version",Version.TryParse(UpdateRuntime.Text(f,"version",""),out version)?version.ToString():"unknown"},{"storeHealthy",UpdateRuntime.Flag(f,"storePresent")}};}catch(Exception e){result["officialPackage"]=new Dictionary<string,object>{{"status","unknown"},{"errorType",e.GetType().Name}};}
   try{var c=Compatibility.Check();result["compatibility"]=new Dictionary<string,object>{{"summary",Clean(c.Summary)}};}catch(Exception e){result["compatibility"]=new Dictionary<string,object>{{"status","unknown"},{"errorType",e.GetType().Name}};}
   try{var v=ServiceVerification.Run();result["verification"]=new Dictionary<string,object>{{"codexState",Clean(v.CodexState)},{"dotsState",Clean(v.DotsState)},{"recentRetryCount",v.RecentRetryCount},{"summary",Clean(v.Summary)}};}catch(Exception e){result["verification"]=new Dictionary<string,object>{{"status","unknown"},{"errorType",e.GetType().Name}};}
   try{result["repairRecord"]=Clean(RepairLedger.GetSafeSummary());}catch(Exception e){result["repairRecord"]=new Dictionary<string,object>{{"status","unavailable"},{"errorType",e.GetType().Name}};}
   return result;
  }
  public static void Write(string path){string json=new JavaScriptSerializer().Serialize(Collect());File.WriteAllText(path,json,new UTF8Encoding(true));}
  public static int SelfTest(){string sample="Bearer sample-token password=sample-password https://user:secret@private.example/path?token=123 C:\\Users\\ExamplePerson\\secret.txt "+Environment.UserName+" "+Environment.MachineName;string clean=Clean(sample);return clean.Contains("sample-token")||clean.Contains("sample-password")||clean.Contains("private.example")||clean.Contains("ExamplePerson")?1:0;}
  public static void ExportWithDialog(IWin32Window owner){
   string path;
   using(var save=new SaveFileDialog{Title=L10n.Render("导出脱敏诊断（仅保存到本地）"),Filter=L10n.Render("诊断 JSON|*.json"),FileName=L10n.Render("Codex诊断_")+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".json",AddExtension=true,OverwritePrompt=true}){if(save.ShowDialog(owner)!=DialogResult.OK)return;path=save.FileName;}
   using(var dialog=new DiagnosticProgress(path))dialog.ShowDialog(owner);
  }
  sealed class DiagnosticProgress:Form {
   readonly string path;bool running=true,cancelled;
   public DiagnosticProgress(string destination){path=destination;Text="导出脱敏诊断";ClientSize=new Size(580,190);MinimumSize=new Size(500,220);StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(244,236,227);Font=new Font("Microsoft YaHei UI",10);MinimizeBox=false;MaximizeBox=false;
    var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=3,AutoScroll=true};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    var description=new Label{Text="正在读取本机状态，只保存经过筛选的诊断字段。",AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,0,0,16)};layout.Controls.Add(description,0,0);
    layout.Controls.Add(new ProgressBar{Style=ProgressBarStyle.Marquee,Dock=DockStyle.Top,Height=18,Margin=new Padding(0,0,0,16)},0,1);
    var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.RightToLeft,Margin=Padding.Empty};var cancel=new Button{Text="取消导出",AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new Size(120,34),Padding=new Padding(12,4,12,4),Margin=Padding.Empty};cancel.Click+=(s,e)=>{cancelled=true;running=false;Close();};actions.Controls.Add(cancel);layout.Controls.Add(actions,0,2);Controls.Add(layout);
    Action fitText=()=>description.MaximumSize=new Size(Math.Max(200,layout.ClientSize.Width-layout.Padding.Horizontal-6),0);layout.SizeChanged+=(s,e)=>fitText();fitText();
    FormClosing+=(s,e)=>{if(running){cancelled=true;running=false;}};
    Shown+=(s,e)=>Start();L10n.Attach(this);
   }
   void Start(){var worker=new BackgroundWorker();worker.DoWork+=(s,e)=>{e.Result=new JavaScriptSerializer().Serialize(Collect());};worker.RunWorkerCompleted+=(s,e)=>{if(cancelled||IsDisposed||Disposing)return;Action done=()=>{if(cancelled||IsDisposed||Disposing)return;running=false;if(e.Error!=null){MessageBox.Show(this,L10n.Render("诊断读取未完成（"+e.Error.GetType().Name+"），未导出原始日志。"),L10n.Render("导出结果"));}else{try{File.WriteAllText(path,(string)e.Result,new UTF8Encoding(true));MessageBox.Show(this,L10n.Render("脱敏诊断已保存到你选择的位置。未上传任何资料。"),L10n.Render("导出完成"));}catch(Exception x){MessageBox.Show(this,L10n.Render("保存失败（"+x.GetType().Name+"）。"),L10n.Render("导出结果"));}}Close();};if(InvokeRequired)BeginInvoke(done);else done();};worker.RunWorkerAsync();}
  }
 }
}
