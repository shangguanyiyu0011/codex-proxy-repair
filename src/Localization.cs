using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CodexAutoProxyRepair {
 // Translates presentation text only. Business logic retains its original strings.
 static class L10n {
  static readonly string[] Codes={"zh-CN","zh-TW","en","ja","ko"};
  static readonly Dictionary<string,Dictionary<string,string>> Catalogs=new Dictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
  static readonly Dictionary<string,string[]> SortedKeys=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
  static readonly List<WeakReference> Forms=new List<WeakReference>();
  static string current="en";
  public static string CurrentLanguage {get{return current;}}
  static bool Valid(string code){foreach(string c in Codes)if(String.Equals(c,code,StringComparison.OrdinalIgnoreCase))return true;return false;}
  static string Canonical(string code){foreach(string c in Codes)if(String.Equals(c,code,StringComparison.OrdinalIgnoreCase))return c;return "en";}
  static string FromCulture(string name){name=(name??"").ToLowerInvariant();if(name.StartsWith("zh"))return name.StartsWith("zh-tw")||name.StartsWith("zh-hk")||name.StartsWith("zh-mo")||name.StartsWith("zh-hant")?"zh-TW":"zh-CN";if(name.StartsWith("ja"))return "ja";if(name.StartsWith("ko"))return "ko";return "en";}
  static string PreferenceDirectory(){string root=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);if(String.IsNullOrEmpty(root)||!Directory.Exists(root)||(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)return null;return Path.Combine(root,"CodexAutoProxyRepairSettings");}
  static string ReadPreference(){try{string dir=PreferenceDirectory();if(dir==null||!Directory.Exists(dir)||(File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)return null;string path=Path.Combine(dir,"language.txt");if(!File.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||new FileInfo(path).Length>32)return null;string value=File.ReadAllText(path,Encoding.UTF8).Trim();return Valid(value)?Canonical(value):null;}catch{return null;}}
  static void SavePreference(){try{string dir=PreferenceDirectory();if(dir==null)return;if(Directory.Exists(dir)){if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)return;}else Directory.CreateDirectory(dir);if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)return;string path=Path.Combine(dir,"language.txt");if(File.Exists(path)&&((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||new FileInfo(path).Length>32))return;File.WriteAllText(path,current,new UTF8Encoding(false));}catch{}}
  static Dictionary<string,string> Load(string code){var result=new Dictionary<string,string>(StringComparer.Ordinal);try{using(var stream=typeof(L10n).Assembly.GetManifestResourceStream("CodexAutoProxyRepair.Locales."+code+".json")){if(stream==null)return result;using(var reader=new StreamReader(stream,Encoding.UTF8)){var data=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(reader.ReadToEnd());if(data!=null)foreach(var pair in data)if(!String.IsNullOrEmpty(pair.Key)&&pair.Value!=null)result[pair.Key]=pair.Value;}}}catch{}return result;}
  public static void Initialize(){Catalogs.Clear();SortedKeys.Clear();foreach(string code in Codes)if(code!="zh-CN"){var catalog=Load(code);Catalogs[code]=catalog;SortedKeys[code]=OrderKeys(catalog);}current=ReadPreference()??FromCulture(CultureInfo.CurrentUICulture.Name);}
  public static void ChangeLanguage(string code){if(!Valid(code))return;current=Canonical(code);SavePreference();for(int i=Forms.Count-1;i>=0;i--){var attachment=Forms[i].Target as Attachment;if(attachment==null||attachment.Form.IsDisposed){Forms.RemoveAt(i);continue;}attachment.Refresh();}}
  static string[] OrderKeys(Dictionary<string,string> catalog){var keys=new List<string>(catalog.Keys);keys.Sort((a,b)=>{int n=b.Length.CompareTo(a.Length);return n!=0?n:StringComparer.Ordinal.Compare(a,b);});return keys.ToArray();}
  static string Translate(string input,Dictionary<string,string> catalog,string[] keys){if(String.IsNullOrEmpty(input)||catalog==null||catalog.Count==0)return input;string exact;if(catalog.TryGetValue(input,out exact))return exact;var output=new StringBuilder();int at=0;while(at<input.Length){string match=null;foreach(string key in keys)if(key.Length<=input.Length-at&&String.CompareOrdinal(input,at,key,0,key.Length)==0){match=key;break;}if(match==null){output.Append(input[at]);at++;}else{output.Append(catalog[match]);at+=match.Length;}}return output.ToString();}
  public static string Render(string value){Dictionary<string,string> catalog;string[] keys;if(current=="zh-CN"||!Catalogs.TryGetValue(current,out catalog)||!SortedKeys.TryGetValue(current,out keys))return value;return Translate(value,catalog,keys);}
  public static void Attach(Form form){if(form==null)return;foreach(var weak in Forms){var old=weak.Target as Attachment;if(old!=null&&old.Form==form)return;}var attachment=new Attachment(form);Forms.Add(new WeakReference(attachment));form.Disposed+=(s,e)=>{Forms.RemoveAll(w=>!w.IsAlive||Object.ReferenceEquals(w.Target,attachment));};attachment.Add(form);}
  sealed class Attachment {
   public readonly Form Form;readonly Dictionary<Control,string> original=new Dictionary<Control,string>();bool applying;
   public Attachment(Form form){Form=form;}
   static bool DisplayText(Control c){return c is Form||c is Label||c is ButtonBase||c is GroupBox||c is TabPage||(c is TextBoxBase && ((TextBoxBase)c).ReadOnly);}
   public void Add(Control c){if(original.ContainsKey(c))return;original[c]=c.Text;c.TextChanged+=TextChanged;c.ControlAdded+=Added;c.Disposed+=Disposed;foreach(Control child in c.Controls)Add(child);Apply(c);}
   void Added(object sender,ControlEventArgs e){Add(e.Control);}
   void Disposed(object sender,EventArgs e){var c=(Control)sender;original.Remove(c);c.TextChanged-=TextChanged;c.ControlAdded-=Added;c.Disposed-=Disposed;}
   void TextChanged(object sender,EventArgs e){if(applying)return;var c=(Control)sender;original[c]=c.Text;Apply(c);}
   void Apply(Control c){string value;if(c.IsDisposed||!DisplayText(c)||!original.TryGetValue(c,out value))return;string translated=Render(value);if(c.Text==translated)return;applying=true;try{c.Text=translated;}finally{applying=false;}}
   public void Refresh(){foreach(var c in new List<Control>(original.Keys))Apply(c);Form.PerformLayout();}
  }
  public static int SelfTest(){try{if(FromCulture("zh-HK")!="zh-TW"||FromCulture("zh-CN")!="zh-CN"||FromCulture("fr-FR")!="en"||FromCulture("ja-JP")!="ja"||FromCulture("ko-KR")!="ko")return 1;var test=new Dictionary<string,string>{{"中文","AB"},{"中文长","中文"},{"AB","changed"}};var keys=OrderKeys(test);if(Translate("中文长 42 中文",test,keys)!="中文 42 AB"||Translate("中文",test,keys)!="AB")return 2;foreach(string code in Codes)if(code!="zh-CN"&&Load(code).Count==0)return 3;return 0;}catch{return 4;}}
 }
}

