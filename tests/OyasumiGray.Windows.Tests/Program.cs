using OyasumiGray.Core;
using OyasumiGray.Windows;

Application.EnableVisualStyles();

using var form = new SettingsForm(AppSettings.Default);
form.MinimumSize = Size.Empty;
form.ClientSize = new Size(440, 250);
form.Show();
Application.DoEvents();

const string status = "現在の状態: 移行中・白黒度50%\n次の切替: 09/27 23:00";
form.SetStatus(status);
Application.DoEvents();
if (!form.VerticalScroll.Visible) throw new Exception("設定画面がスクロール可能でない");

form.AutoScrollPosition = new Point(0, 100);
Application.DoEvents();
var scrollY = form.VerticalScroll.Value;
if (scrollY == 0) throw new Exception("スクロール位置を設定できない");

var changes = 0;
form.Status.TextChanged += (_, _) => changes++;
form.SetStatus(status);
Application.DoEvents();

if (changes != 0) throw new Exception($"同じ状態で {changes} 回ラベルが更新された");
if (form.VerticalScroll.Value != scrollY)
    throw new Exception($"スクロール位置が {scrollY} から {form.VerticalScroll.Value} に移動した");

Console.WriteLine("PASS 同じ状態の再通知でスクロール位置を保つ");
