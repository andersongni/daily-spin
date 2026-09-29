using Microsoft.Web.WebView2.WinForms;
namespace RoletaDaDaily;
internal static class Program {
 [STAThread] static void Main(){ ApplicationConfiguration.Initialize(); Application.Run(new MainForm()); }
}
public sealed class MainForm : Form {
 private readonly WebView2 browser = new() { Dock = DockStyle.Fill };
 public MainForm(){
  Text="Roleta da Daily"; Width=1180; Height=850; MinimumSize=new Size(820,620); StartPosition=FormStartPosition.CenterScreen; Controls.Add(browser);
  Shown += async (_,_) => { try {
   await browser.EnsureCoreWebView2Async();
   browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled=true;
   browser.CoreWebView2.Settings.IsStatusBarEnabled=false;
   string page=Path.Combine(AppContext.BaseDirectory,"wwwroot","index.html");
   browser.CoreWebView2.Navigate(new Uri(page).AbsoluteUri);
  } catch(Exception ex) { MessageBox.Show("Não foi possível iniciar. Verifique se o Microsoft Edge WebView2 Runtime está instalado.\n\n"+ex.Message,"Roleta da Daily",MessageBoxButtons.OK,MessageBoxIcon.Error); } };
 }
}