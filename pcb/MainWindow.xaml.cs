﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using pcb.core.util;
using pcb.core.autocomplete;
using MahApps.Metro.Controls.Dialogs;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit;
using System.Text.RegularExpressions;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using MahApps.Metro;
using System.Diagnostics;
using Microsoft.Win32;
using System.Threading;
using System.Net;
using System.Globalization;

namespace pcb
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        Tree tree;
        List<string> displayData = new List<string>();
        List<string> completionData = new List<string>();
        bool closed = false;
        string path = "";
        string version = "0.6.7";
        string backupFileName = "";
        autocomplete_menu autocomplete;
        IHighlightingDefinition syntaxHightlighting;
        //folding
        System.Windows.Threading.DispatcherTimer foldingUpdateTimer = new System.Windows.Threading.DispatcherTimer();
        FoldingManager foldingManager;
        BraceFoldingStrategy foldingStrategy;
        FindReplaceDialog findReplaceDialog;
        public bool useAutocomplete = true;
        public bool useBlockStruc = false;
        //init
        void initBackUpFile()
        {
            bool notExist = false;
            int index = 0;
            if (!Directory.Exists("documents/backup"))
                Directory.CreateDirectory("documents/backup");
            backupFileName = "documents/backup/backup.txt";
            while (!notExist)
            {
                if (!File.Exists(backupFileName))
                    notExist = true;
                else
                {
                    backupFileName = "documents/backup/backup" + (index++).ToString() + ".txt";
                }
            }
            try
            {
                File.Create(backupFileName);
            }
            catch (Exception ex)
            {
                showMessage(Properties.Resources.cannotInitBackup, Properties.Resources.warn);
                log(ex);
            }
        }
        void setUp()
        {
            if (File.Exists(@"ref/config.txt"))
            {
                try
                {
                    configs();
                }
                catch (Exception ex)
                {
                    showMessage(Properties.Resources.ioError + " ref/config.txt", Properties.Resources.warn);
                    log(ex);
                }
            }
            else
            {
                IHighlightingDefinition customHighlighting;
                using (Stream s = typeof(MainWindow).Assembly.GetManifestResourceStream("pcb.CB.xshd"))
                {
                    if (s == null)
                        throw new InvalidOperationException("Could not find embedded resource");
                    using (XmlReader reader = new XmlTextReader(s))
                    {
                        customHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.
                            HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                }
                HighlightingManager.Instance.RegisterHighlighting("Custom Highlighting", new string[] { ".cool" }, customHighlighting);
                syntaxHightlighting = customHighlighting;
                Editor.SyntaxHighlighting = customHighlighting;
            }
        }
        void configs()
        {
            string theme = "Blue";
            string comment = "#57A64A";
            string str = "White";
            string command = "Orange";
            string before = "Yellow";
            string init = "Aqua";
            string folding = "#a3632e";
            string marks = "#FF00FF";
            string doc = "#4d7ea8";
            if (File.Exists(@"ref/config.txt"))
            {
                string[] contents = File.ReadAllLines(@"ref/config.txt", Encoding.UTF8);
                foreach (string line in contents)
                {
                    if (Regex.IsMatch(line, @"theme:(\w+)"))
                    {
                        theme = Regex.Match(line, @"theme:(\w+)").Groups[1].ToString();
                        ThemeManager.ChangeAppStyle(Application.Current, ThemeManager.GetAccent(theme),
            ThemeManager.GetAppTheme("BaseDark"));
                        notifyArea.Background = WindowTitleBrush;
                        BorderBrush = WindowTitleBrush;
                    }
                    else if (Regex.IsMatch(line, @"^comment:(#[0-9A-F]{6})$"))
                        comment = Regex.Match(line, @"^comment:(#[0-9A-F]{6})$").Groups[1].ToString();
                    else if (Regex.IsMatch(line, @"^string:(#[0-9A-F]{6})$"))
                        str = Regex.Match(line, @"^string:(#[0-9A-F]{6})$").Groups[1].ToString();
                    else if (Regex.IsMatch(line, @"^command:(#[0-9A-F]{6})$"))
                        command = Regex.Match(line, @"^command:(#[0-9A-F]{6})$").Groups[1].ToString();
                    else if (Regex.IsMatch(line, @"^prefix:(#[0-9A-F]{6})$"))
                        before = Regex.Match(line, @"^prefix:(#[0-9A-F]{6})$").Groups[1].ToString();
                    else if (Regex.IsMatch(line, @"^init:(#[0-9A-F]{6})$"))
                        init = Regex.Match(line, @"^init:(#[0-9A-F]{6})$").Groups[1].ToString();
                    else if (Regex.IsMatch(line, @"^folding:(#[0-9A-F]{6})$"))
                        folding = Regex.Match(line, @"^folding:(#[0-9A-F]{6})$").Groups[1].ToString();
                    else if (Regex.IsMatch(line, @"^marks:(#[0-9A-F]{6})$"))
                        marks = Regex.Match(line, @"^marks:(#[0-9A-F]{6})$").Groups[1].ToString();
                    else if (Regex.IsMatch(line, @"^doc:(#[0-9A-F]{6})$"))
                        doc = Regex.Match(line, @"^doc:(#[0-9A-F]{6})$").Groups[1].ToString();
                    else if (line.StartsWith("setup:"))
                    {
                        if (line.Length > 6)
                            Editor.AppendText(line.Substring(6) + Environment.NewLine);
                    }
                    else if (line.StartsWith("dir:"))
                    {
                        if (line[4].ToString() == 0.ToString())
                            core.chain.StraightCbChain.setDirection(Direction.positiveY);
                        else
                            core.chain.StraightCbChain.setDirection(Direction.positiveX);
                    }
                    else if (line.StartsWith("AEC:"))
                    {
                        if (line[4].ToString() == 0.ToString())
                            core.PcbParser.markerType = true;
                        else
                            core.PcbParser.markerType = false;
                    }
                    else if (line.StartsWith("NL:"))
                    {
                        int limit = 0;
                        int.TryParse(line.Substring(3), out limit);
                        core.chain.StraightCbChain.setRowCbLimit(limit);
                    }
                    else if (line.StartsWith("block:"))
                    {
                        if (line[6].ToString() == 0.ToString())
                            useBlockStruc = false;
                        else
                            useBlockStruc = true;
                    }
                    else if (line.StartsWith("X:"))
                    {
                        int XLength = 3;
                        int.TryParse(line.Substring(2), out XLength);
                        if (XLength < 3)
                            XLength = 3;
                        core.chain.BoxCbChain.xLimit = XLength;
                    }
                    else if (line.StartsWith("Z:"))
                    {
                        int ZLength = 3;
                        int.TryParse(line.Substring(2), out ZLength);
                        if (ZLength < 3)
                            ZLength = 3;
                        core.chain.BoxCbChain.zLimit = ZLength;
                    }
                    else if (line.StartsWith("top_id:"))
                    {
                        core.chain.BoxCbChain.baseBlock = line.Substring(7);
                    }
                    else if (line.StartsWith("side_id:"))
                    {
                        core.chain.BoxCbChain.outerBlock = line.Substring(8);
                    }
                    else if (line.StartsWith("top_dam:"))
                    {
                        byte damage = 0;
                        byte.TryParse(line.Substring(8), out damage);
                        core.chain.BoxCbChain.baseDamage = damage;
                    }
                    else if (line.StartsWith("side_dam:"))
                    {
                        byte damage = 0;
                        byte.TryParse(line.Substring(9), out damage);
                        core.chain.BoxCbChain.outerDamage = damage;
                    }
                    else if (line.StartsWith("font:"))
                    {
                        try
                        {
                            Editor.FontFamily = new FontFamily(line.Substring(5));
                        }
                        catch
                        {
                            showMessage("Font错误！", "错误！");
                        }
                    }
                }
                if (theme != "Blue")
                {
                    try
                    {
                        ThemeManager.ChangeAppStyle(Application.Current, ThemeManager.GetAccent(theme),
                    ThemeManager.GetAppTheme("BaseDark"));
                        notifyArea.Background = WindowTitleBrush;
                        BorderBrush = WindowTitleBrush;
                    }
                    catch
                    {
                        showMessage(Properties.Resources.wrongTheme, Properties.Resources.warn);
                    }
                }
            }
            IHighlightingDefinition customHighlighting;
            using (Stream s = typeof(MainWindow).Assembly.GetManifestResourceStream("pcb.CB.xshd"))
            {                
                if (s == null)
                    throw new InvalidOperationException("Could not find embedded resource");
                string xml = new StreamReader(s).ReadToEnd();
                //comment
                xml = xml.Replace("#0F5E46", comment);
                //string
                xml = xml.Replace("white", str);
                //command
                xml = xml.Replace("Orange", command);
                //before
                xml = xml.Replace("Yellow", before);
                //init
                xml = xml.Replace("Aqua", init);
                //folding
                xml = xml.Replace("#a3632e", folding);
                //marks
                xml = xml.Replace("#FF00FF", marks);
                //doc
                xml = xml.Replace("#4d7ea8", doc);
                byte[] byteArray = Encoding.UTF8.GetBytes(xml);
                Stream stream = new MemoryStream(byteArray);
                using (XmlReader reader = new XmlTextReader(stream))
                {
                    customHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.
                        HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
            HighlightingManager.Instance.RegisterHighlighting("Custom Highlighting", new string[] { ".cool" }, customHighlighting);
            syntaxHightlighting = customHighlighting;
            Editor.SyntaxHighlighting = customHighlighting;
        }
        void tryLoadText()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            string filePath = arguments[0];
            if (arguments.Length > 1)
                filePath = arguments[1];
            if (filePath.EndsWith(".pcb") || filePath.EndsWith(".txt"))
            {
                try
                {
                    loadFile(filePath);
                }
                catch (Exception ex)
                {
                    showMessage(Properties.Resources.ioError, Properties.Resources.warn);
                    log(ex);
                }
            }
        }
        void registerCommands()
        {
            Editor.InputBindings.Add(new InputBinding(new DeescapeCommand(this), new KeyGesture(Key.OemQuestion, ModifierKeys.Alt)));
            Editor.InputBindings.Add(new InputBinding(new DeescapeCommand(this), new KeyGesture(Key.Divide, ModifierKeys.Alt)));
            Editor.InputBindings.Add(new InputBinding(new add_type(this), new KeyGesture(Key.T, ModifierKeys.Alt)));
            Editor.InputBindings.Add(new InputBinding(new foldAll(this), new KeyGesture(Key.K, ModifierKeys.Control)));
            Editor.InputBindings.Add(new InputBinding(new generate(this), new KeyGesture(Key.G, ModifierKeys.Control)));
            Editor.InputBindings.Add(new InputBinding(new EscapeCommand(this), new KeyGesture(Key.OemPipe, ModifierKeys.Alt)));
            Editor.InputBindings.Add(new InputBinding(new selectLine(Editor), new KeyGesture(Key.L, ModifierKeys.Control)));
            Editor.InputBindings.Add(new InputBinding(new save(this), new KeyGesture(Key.S, ModifierKeys.Control)));
            Editor.InputBindings.Add(new InputBinding(new blackTech(this), new KeyGesture(Key.C, ModifierKeys.Alt)));
            Editor.InputBindings.Add(new InputBinding(new insertFormat(this), new KeyGesture(Key.F, ModifierKeys.Alt)));
            Editor.InputBindings.Add(new InputBinding(new insertRandomNBT(this), new KeyGesture(Key.R, ModifierKeys.Alt)));
            Editor.InputBindings.Add(new InputBinding(new findReplaceCommand(this), new KeyGesture(Key.F, ModifierKeys.Control)));
            Editor.InputBindings.Add(new InputBinding(new commentOut(this), new KeyGesture(Key.OemQuestion, ModifierKeys.Control)));
            Editor.InputBindings.Add(new InputBinding(new commentOut(this), new KeyGesture(Key.Divide, ModifierKeys.Control)));
            Editor.InputBindings.Add(new InputBinding(new unComment(this), new KeyGesture(Key.OemPipe, ModifierKeys.Control)));
        }
        public MainWindow()
        {
            InitializeComponent();

            foldingUpdateTimer.Interval = TimeSpan.FromSeconds(3);
            foldingUpdateTimer.Tick += foldingUpdateTimer_Tick;
            foldingUpdateTimer.Start();
            Show();

            foldingStrategy = new BraceFoldingStrategy();
            foldingManager = FoldingManager.Install(Editor.TextArea);
            foldingStrategy.UpdateFoldings(foldingManager, Editor.Document);

            //events
            Editor.TextArea.TextEntered += Editor_TextEntering;
            Editor.TextArea.Caret.PositionChanged += Editor_caretChanged;
            //options
            Editor.Options.AllowScrollBelowDocument = true;

            //change background of current line
            Editor.TextArea.TextView.BackgroundRenderers.Add(new XBackgroundRenderer(Editor));

            setUp();

            findReplaceDialog = new FindReplaceDialog(this);
            findReplaceDialog.Owner = this;
            try
            {
                tree = InitAutocomplete.init();
                core.LoadConfig.readConfig();
            }
            catch (AutocompleteParseException ex)
            {
                CustomMessageBox.ShowMessage(ex.Message, Properties.Resources.error, false);
                log(ex);
                App.Current.Shutdown();
                return;
            }
            catch (Exception ex)
            {
                CustomMessageBox.ShowMessage(ex.Message, Properties.Resources.error, false);
                CustomMessageBox.ShowMessage(ex.StackTrace, Properties.Resources.error, false);
                log(ex);
                App.Current.Shutdown();
                return;
            }
            try
            {
                File.WriteAllText("/documents/log/log.txt", "");
            } catch { }
            autocomplete = new autocomplete_menu(Editor, this);
            autocomplete.Owner = this;
            if (Thread.CurrentThread.CurrentCulture.Name == "zh")
            {
                checkUpdate();
            }
            FontFamily font = new FontFamily(Properties.UIresources.font);
            Editor.FontFamily = font;
            foreach (MenuItem item in menu.Items)
            {
                item.FontFamily = font;
                if (item.Items.Count > 0)
                {
                    foreach (object subItem in item.Items)
                    {
                        if (subItem is MenuItem) {
                            ((MenuItem)subItem).FontFamily = font;
                        }
                    }
                }
            }
            tryLoadText();
            initBackUpFile();
            registerCommands();
            updateNotification();
            Editor.Focus();
        }       
        bool Islatest(string LatestVersion)
        {
            try
            {
                int version1 = int.Parse(version.Split('.')[0]);
                int version2 = int.Parse(version.Split('.')[1]);
                int version3 = int.Parse(version.Split('.')[2]);

                int Latestversion1 = int.Parse(LatestVersion.Split('.')[0]);
                int Latestversion2 = int.Parse(LatestVersion.Split('.')[1]);
                int Latestversion3 = int.Parse(LatestVersion.Split('.')[2]);

                if (Latestversion1 > version1)
                    return true;
                else if (Latestversion1 == version1)
                {
                    if (Latestversion2 > version2)
                        return true;
                    else if (Latestversion2 == version2)
                    {
                        if (Latestversion3 > version3)
                            return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
        void checkUpdate()
        {
            new Thread(() =>
            {
                try
                {
                    string urlAddress = "http://www.mcbbs.net/thread-533943-1-1.html";
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlAddress);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        Stream receiveStream = response.GetResponseStream();
                        StreamReader readStream = null;

                        if (response.CharacterSet == null)
                        {
                            readStream = new StreamReader(receiveStream);
                        }
                        else
                        {
                            readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                        }

                        string data = readStream.ReadToEnd();

                        if (Regex.IsMatch(data, @"<th>软件版本:<\/th>\s*<td>([^<]*)<\/td>"))
                        {
                            Match match = Regex.Match(data, @"<th>软件版本:<\/th>\s*<td>([^<]*)<\/td>");
                            string latestVersion = match.Groups[1].ToString().Trim();
                            if (Islatest(latestVersion))
                            {
                                var mySettings = new MetroDialogSettings()
                                {
                                    AffirmativeButtonText = "下载(打开浏览器)",
                                    NegativeButtonText = "关闭"
                                };
                                Match url = Regex.Match(data, @"<th>下载地址:<\/th>\s*<td><a href=""([^""]*)");

                                Dispatcher.Invoke((Action)(() =>
                                {
                                    if (CustomMessageBox.ShowMessage("当前版本:" + version + Environment.NewLine + "最新版本:" +
                                        latestVersion + Environment.NewLine + "下载地址:" +
                                        url.Groups[1].ToString(), "发现新版本！") == CustomMessageBox.State.yes)
                                    {
                                        Process.Start(url.Groups[1].ToString());
                                    }
                                }));                                
                            }
                            else
                            {
                                throw new Exception();
                            }
                        }
                        else
                        {
                            throw new Exception();
                        }
                        response.Close();
                        readStream.Close();
                    }
                }
                catch
                {
                    new Thread(() =>
                    {
                        try
                        {
                            string urlAddress = "http://www.pcapcb.com/forum.php?mod=viewthread&tid=24";
                            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlAddress);
                            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                Stream receiveStream = response.GetResponseStream();
                                StreamReader readStream = null;

                                if (response.CharacterSet == null)
                                {
                                    readStream = new StreamReader(receiveStream);
                                }
                                else
                                {
                                    readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                                }

                                string data = readStream.ReadToEnd();

                                if (Regex.IsMatch(data, @"版本: ([0-9.]*)"))
                                {
                                    Match match = Regex.Match(data, @"版本: ([0-9.]*)");
                                    string latestVersion = match.Groups[1].ToString().Trim();
                                    if (Islatest(latestVersion))
                                    {
                                        var mySettings = new MetroDialogSettings()
                                        {
                                            AffirmativeButtonText = "下载(打开浏览器)",
                                            NegativeButtonText = "关闭"
                                        };
                                        Match url = Regex.Match(data, @"下载地址: <a href=""([^""]*)""");               
                                        Dispatcher.Invoke((Action)(() =>
                                        {
                                            if (CustomMessageBox.ShowMessage("当前版本:" + version + Environment.NewLine +
                                                "最新版本:" + latestVersion + Environment.NewLine + "下载地址:" +
                                                    url.Groups[1].ToString(), "发现新版本！") == CustomMessageBox.State.yes)
                                            {
                                                System.Diagnostics.Process.Start(url.Groups[1].ToString());
                                            }
                                        }));
                                    }
                                }
                                response.Close();
                                readStream.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            log(ex);
                        }
                    }).Start();
                }
            }).Start();

        }
        //autocomplete
        void parseDocument()
        {
            string[] lines = Editor.Text.Split('\n');
            Value.runtime_names.Clear();
            Value.runtime_scbObj.Clear();
            Value.runtime_tags.Clear();
            Value.runtime_teams.Clear();
            Value.runtime_triggerObj.Clear();
            foreach (string rawLine in lines)
            {
                string text = rawLine.Trim();
                if (!text.StartsWith("//"))
                    if (Regex.IsMatch(text, @"scoreboard objectives add ([a-zA-Z0-9_]+) \w"))
                    {
                        string str = Regex.Match(text, @"scoreboard objectives add ([a-zA-Z0-9_]+) \w").Groups[1].ToString();
                        if (!Value.runtime_scbObj.Contains(str))
                            Value.runtime_scbObj.Add(str);

                        string[] elements = text.Split(' ');
                        if (elements.Length > 4 && elements[4] == "trigger")
                            if (!Value.runtime_triggerObj.Contains(elements[3]))
                                Value.runtime_triggerObj.Add(elements[3]);
                    }
                    else if (Regex.IsMatch(text, @"scoreboard players tag \S* \w+ ([a-zA-Z0-9_]+)$"))
                    {
                        string tag = (Regex.Match(text, @"scoreboard players tag \S* \w+ ([a-zA-Z0-9_]+)$").Groups[1].ToString());
                        if (!Value.runtime_tags.Contains(tag))
                            Value.runtime_tags.Add(tag);
                    }
                    else if (text.StartsWith("mark:"))
                    {
                        string segment = text.Split(' ')[0].Substring(5);
                        if (!Value.runtime_names.Contains(segment))
                            Value.runtime_names.Add(segment);
                    }
                    else if (text.StartsWith("init:scoreboard teams add"))
                    {
                        string team = (text.Split(' ')[3]);
                        if (!Value.runtime_teams.Contains(team))
                            Value.runtime_teams.Add(team);
                    }
            }
        }
        void addElements()
        {
            if (autocomplete == null)
                return;
            string text = Editor.Document.GetText(Editor.Document.GetOffset(Editor.TextArea.Caret.Line, 0), Editor.TextArea.Caret.Offset - Editor.Document.GetOffset(Editor.TextArea.Caret.Line, 0));            
            text = text.TrimStart(' ', '\t');
            if (text.Length == 0)
            {
                autocomplete.Hide();
                autocomplete.shown = false;
                return;
            }
            if (text.Contains('\t'))
            {
                autocomplete.Hide();
                autocomplete.shown = false;
                return;
            }
            CompletionData list;
            try
            {
                list = tree.autocomplete(text);
            } catch (Exception ex)
            {
                autocomplete.Hide();
                autocomplete.shown = false;
                log(ex);
                return;
            }

            if (list.displayData.Count > 0)
            {
                autocomplete.updateitem(list.displayData,list.startLength);
            }
            else
            {
                autocomplete.Hide();
                autocomplete.shown = false;
            }
            Editor.Focus();
        }
        //folding
        void foldingUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (foldingStrategy != null)
            {
                foldingStrategy.UpdateFoldings(foldingManager, Editor.Document);
            }
            backup();
        }
        //io
        void backup()
        {
            try
            {
                File.WriteAllText(backupFileName, Editor.Text);
            }
            catch {}
        }
        void loadFile(string filePath)
        {
            path = filePath;
            LastEditTB.Text = Properties.UIresources.lastSaved + File.GetLastWriteTime(path).ToString();
            string text = File.ReadAllText(path, new UTF8Encoding());
            string[] lines = text.Split('\n');
            bool a = true;
            int i = 0;
            while (a)
            {
                string line = lines[i].Trim('\t').Trim('\n').Trim('\r');
                if (line.StartsWith("dir:"))
                {
                    if (line[4].ToString() == 0.ToString())
                        core.chain.StraightCbChain.setDirection(core.util.Direction.positiveY);
                    else
                        core.chain.StraightCbChain.setDirection(core.util.Direction.positiveX);
                }
                else if (line.StartsWith("AEC:"))
                {
                    if (line[4].ToString() == 0.ToString())
                        core.PcbParser.markerType = true;
                    else
                        core.PcbParser.markerType = false;
                }
                else if (line.StartsWith("NL:"))
                {
                    int limit = 0;
                    int.TryParse(line.Substring(3), out limit);
                    core.chain.StraightCbChain.setRowCbLimit(limit);
                }
                else if (line.StartsWith("block:"))
                {
                    if (line[6].ToString() == 0.ToString())
                        useBlockStruc = false;
                    else
                        useBlockStruc = true;
                }
                else if (line.StartsWith("X:"))
                {
                    int XLength = 3;
                    int.TryParse(line.Substring(2), out XLength);
                    if (XLength < 3)
                        XLength = 3;
                    core.chain.BoxCbChain.xLimit = XLength;
                }
                else if (line.StartsWith("Z:"))
                {
                    int ZLength = 3;
                    int.TryParse(line.Substring(2), out ZLength);
                    if (ZLength < 3)
                        ZLength = 3;
                    core.chain.BoxCbChain.zLimit = ZLength;
                }
                else if (line.StartsWith("top_id:"))
                {
                    core.chain.BoxCbChain.baseBlock = line.Substring(7);
                }
                else if (line.StartsWith("side_id:"))
                {
                    core.chain.BoxCbChain.outerBlock = line.Substring(8);
                }
                else if (line.StartsWith("top_dam:"))
                {
                    byte damage = 0;
                    byte.TryParse(line.Substring(8), out damage);
                    core.chain.BoxCbChain.baseDamage = damage;
                }
                else if (line.StartsWith("side_dam:"))
                {
                    byte damage = 0;
                    byte.TryParse(line.Substring(9), out damage);
                    core.chain.BoxCbChain.outerDamage = damage;
                }
                else
                {
                    a = false;
                    i--;
                }
                i++;
            }
            StringBuilder Append = new StringBuilder();
            for (int b = i; b < lines.Length; b++)
            {
                Append.Append(lines[b]);
            }
            Editor.Text = Append.ToString();
            Editor.Text = Editor.Text.TrimEnd('\n');
            Editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromArgb(20, 127, 127, 127));
            if (foldingStrategy != null)
            {
                foldingStrategy.UpdateFoldings(foldingManager, Editor.Document);
                foreach (FoldingSection section in foldingManager.AllFoldings)
                {
                    section.IsFolded = true;
                }
            }
            Title = System.IO.Path.GetFileName(path) + "----" + Properties.UIresources.pcbName;
            SaveFile.IsEnabled = true;
        }
        //windows special function
        public void log(Exception ex)
        {
            try
            {
                if (!File.Exists("documents/log/log.txt"))
                    File.Create("documents/log/log.txt");
                var writer = File.AppendText("documents/log/log.txt");
                writer.WriteLine(ex.Message);
                writer.WriteLine(ex.StackTrace);
            }
            catch { }
        }
        void showMessage(string text, string title)
        {
            var mySettings = new MetroDialogSettings()
            {
                AffirmativeButtonText = Properties.UIresources.close
            };

            this.ShowMessageAsync(title, text, MessageDialogStyle.Affirmative, mySettings);
        }
        void updateNotification()
        {
            if (Editor.SelectionLength > 0)
            {
                notificationTB.Text = String.Format(Properties.UIresources.statsDisplaySelect, Editor.SelectionLength);
            }
            else
            {
                notificationTB.Text = String.Format(Properties.UIresources.statsDisplay, Editor.TextArea.Caret.Line, (Editor.CaretOffset - Editor.Document.GetLineByNumber(Editor.TextArea.Caret.Line).Offset + 1));
            }
            parseDocument();
        }
        void commentSelectedLine()
        {
            int startingLineIndex = Editor.Document.GetLineByOffset(Editor.SelectionStart).LineNumber;
            int endingLineIndex = Editor.Document.GetLineByOffset(Editor.SelectionStart +
                Editor.SelectionLength).LineNumber;
            for (int i = startingLineIndex; i <= endingLineIndex; i++)
            {
                var line = Editor.Document.GetLineByNumber(i);
                if (!Editor.Document.GetText(line).TrimStart().StartsWith("//"))
                    Editor.Document.Replace(line, "//" + Editor.Document.GetText(line));
            }
        }
        void uncommentSelectedLine()
        {
            int startingLineIndex = Editor.Document.GetLineByOffset(Editor.SelectionStart).LineNumber;
            int endingLineIndex = Editor.Document.GetLineByOffset(Editor.SelectionStart +
                Editor.SelectionLength).LineNumber;
            for (int i = startingLineIndex; i <= endingLineIndex; i++)
            {
                var line = Editor.Document.GetLineByNumber(i);
                if (Regex.IsMatch(Editor.Document.GetText(line), @"(\s*)//(.*)"))
                {
                    var match = Regex.Match(Editor.Document.GetText(line), @"(\s*)//(.*)");
                    string text = match.Groups[1].ToString() + match.Groups[2].ToString();
                    Editor.Document.Replace(line, text);

                }
            }
        }
        string getText()
        {
            string text = "";
            text += "dir:" + (core.chain.StraightCbChain.initialDir == Direction.positiveY ? "0" : "1") + Environment.NewLine;
            if (core.PcbParser.markerType) text += "AEC:1" + Environment.NewLine;
            if (useBlockStruc) text += "block:1" + Environment.NewLine;
            if (core.chain.StraightCbChain.limit != 0) text += "NL:" + core.chain.StraightCbChain.limit.ToString() + Environment.NewLine;
            if (core.chain.BoxCbChain.xLimit != 0) text += "X:" + core.chain.BoxCbChain.xLimit.ToString() + Environment.NewLine;
            if (core.chain.BoxCbChain.zLimit != 0) text += "Z:" + core.chain.BoxCbChain.zLimit.ToString() + Environment.NewLine;
            if (core.chain.BoxCbChain.baseBlock != "") text += "top_id:" + core.chain.BoxCbChain.baseBlock + Environment.NewLine;
            if (core.chain.BoxCbChain.outerBlock != "") text += "side_id:" + core.chain.BoxCbChain.outerBlock + Environment.NewLine;
            text += "top_dam:" + core.chain.BoxCbChain.baseDamage.ToString() + Environment.NewLine;
            text += "side_dam:" + core.chain.BoxCbChain.outerDamage.ToString() + Environment.NewLine;
            text += Editor.Text;
            return text;
        }
        void deleteBackupFile()
        {
            try
            {
                File.Delete(backupFileName);
            }
            catch
            {
                try
                {
                    System.Windows.Threading.DispatcherTimer dispatcherTimer;
                    dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
                    dispatcherTimer.Tick += new EventHandler((object sender1, EventArgs e1) => {
                        File.Delete(backupFileName);
                    });
                    dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
                    dispatcherTimer.Start();
                }
                catch { }
            }
        }
        //events
        void Editor_TextEntering(object sender, TextCompositionEventArgs e)
        {
            if (useAutocomplete == false)
            {
                return;
            }
            try
            {
                if (e.Text == ")" && Editor.Text[Editor.SelectionStart] == ')')
                {
                    Editor.Document.Remove(Editor.SelectionStart, 1);
                }
                if (e.Text == "}" && Editor.Text[Editor.SelectionStart] == '}')
                {
                    Editor.Document.Remove(Editor.SelectionStart, 1);
                }
                if (e.Text == "]" && Editor.Text[Editor.SelectionStart] == ']')
                {
                    Editor.Document.Remove(Editor.SelectionStart, 1);
                }
            }
            catch
            { }
            if (e.Text == "{")
            {
                Editor.Document.Insert(Editor.SelectionStart, "}");
                Editor.SelectionStart--;
                Editor_TextChanged(null, null);
            }
            if (e.Text == "[")
            {
                Editor.Document.Insert(Editor.SelectionStart, "]");
                Editor.SelectionStart--;
                Editor_TextChanged(null, null);
            }
            if (e.Text == "(")
            {
                Editor.Document.Insert(Editor.SelectionStart, ")");
                Editor.SelectionStart--;
                Editor_TextChanged(null, null);
            }
            if (e.Text == '"'.ToString())
            {
                Editor.Document.Insert(Editor.SelectionStart, "\"");
                Editor.SelectionStart--;
                Editor_TextChanged(null, null);
            }
        }
        void Editor_TextChanged(object sender, EventArgs e)
        {
            if (!useAutocomplete)
                return;            
            addElements();
        }
        void Editor_caretChanged(object sender, EventArgs e)
        {
            updateNotification();
            if (autocomplete != null && autocomplete.Visibility == Visibility.Visible)
                addElements();
            Editor.TextArea.TextView.LineTransformers.RemoveAt(Editor.TextArea.TextView.LineTransformers.Count - 1);
            Editor.TextArea.TextView.LineTransformers.Add(new BracketBracing(Editor.CaretOffset, Editor.TextArea.Caret.Line));
        }
        void NewFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
                showMessage(Properties.UIresources.openPcbError, Properties.Resources.error);
            }
        }
        void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
                if (Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "documents"))
                {
                    openFileDialog.InitialDirectory += @"documents\";
                }
                openFileDialog.Filter = "*.pcb,*.txt|*.pcb;*.txt";
                if (openFileDialog.ShowDialog() == true)
                {
                    if (Editor.Text.Length == 0)
                    {
                        loadFile(openFileDialog.FileName);
                        return;
                    }
                    if (CustomMessageBox.ShowMessage(Properties.UIresources.overwritePrompt, Properties.Resources.warn) == CustomMessageBox.State.yes)
                    {
                        loadFile(openFileDialog.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                showMessage(Properties.Resources.ioError + "\n" + ex.ToString(), Properties.Resources.error);
                log(ex);
                Title = Properties.UIresources.pcbName;
            }
        }
        void SaveNewFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog savefiledialog = new SaveFileDialog();
                savefiledialog.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
                if (Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "documents"))
                {
                    savefiledialog.InitialDirectory += @"documents\";
                }
                savefiledialog.Filter = "*.pcb|*.pcb|*.txt|*.txt";
                savefiledialog.DefaultExt = "pcb";
                savefiledialog.AddExtension = true;
                if (savefiledialog.ShowDialog() == true)
                {
                    File.WriteAllText(savefiledialog.FileName, getText(), new UTF8Encoding());
                    path = savefiledialog.FileName;
                    LastEditTB.Text = Properties.UIresources.lastSaved + DateTime.Now.ToString();
                }
                Title = System.IO.Path.GetFileName(path) + "----" + Properties.UIresources.pcbName;
                SaveFile.IsEnabled = true;
            }
            catch (Exception ex)
            {
                showMessage(Properties.Resources.ioError + "\n" + ex.ToString(), Properties.Resources.error);
                log(ex);
            }
        }
        void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                File.WriteAllText(path, getText());
                LastEditTB.Text = Properties.UIresources.lastSaved + DateTime.Now.ToString();
            }
            catch (Exception ex)
            {
                showMessage(Properties.Resources.ioError + "\n" + ex.ToString(), Properties.Resources.error);
                log(ex);
            }
        }
        void Escape_Click(object sender, RoutedEventArgs e)
        {
            Editor.SelectedText = CommandUtil.escape(Editor.SelectedText);
        }
        void Deesacpe_Click(object sender, RoutedEventArgs e)
        {
            Editor.SelectedText = CommandUtil.unescape(Editor.SelectedText);
        }
        void insertUUID_Click(object sender, RoutedEventArgs e)
        {
            long[] uuid = CommandUtil.randomUUIDPair();
            Editor.Document.Insert(Editor.SelectionStart, CommandUtil.UUIDPairToString(uuid[0],uuid[1]));
        }
        void inserType_Click(object sender, RoutedEventArgs e)
        {
            if (core.PcbParser.markerType)
                Editor.Document.Insert(Editor.SelectionStart, "type=AreaEffectCloud");
            else
                Editor.Document.Insert(Editor.SelectionStart, "type=ArmorStand");
        }
        void insertFormat_Click(object sender, RoutedEventArgs e)
        {
            Editor.Document.Insert(Editor.SelectionStart, "§");
        }
        void colorBlackTech_Click(object sender, RoutedEventArgs e)
        {
            var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
            string text = Editor.Document.GetText(line);
            Editor.Document.Replace(line, CommandUtil.colorBlackTech(text));
        }
        void comment_Click(object sender, RoutedEventArgs e)
        {
            commentSelectedLine();
        }
        void uncomment_Click(object sender, RoutedEventArgs e)
        {
            uncommentSelectedLine();
        }
        void foldAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (FoldingSection section in foldingManager.AllFoldings)
            {
                section.IsFolded = true;
            }
        }
        void FindReplace_Click(object sender, RoutedEventArgs e)
        {
            findReplaceDialog.Show();
        }
        void showSetting(object sender, RoutedEventArgs e)
        {
            new Settings(this);
        }
        void showList(object sender, RoutedEventArgs e)
        {
            add_ac_list window = new add_ac_list();
            window.ShowDialog();
        }
        void openPyEditor(object sender, RoutedEventArgs e)
        {
            ScriptWindow window = new ScriptWindow(Editor);
        }
        void generateFromEditor(object sender, RoutedEventArgs e)
        {
            string text = Editor.Text;
            if (Editor.SelectionLength > 0)
            {
                text = Editor.SelectedText;
            }
            var parser = new core.PcbParser();
            core.chain.AbstractCBChain chain;
            if (useBlockStruc)
                chain = new core.chain.BoxCbChain(new int[] { 0, 2, 0 });
            else
                chain = new core.chain.StraightCbChain(new int[] { 0, 2, 0 });
            try
            {
                string[] oocs = parser.getOOC(text, chain);
                string warn = parser.checkForCondDir();
                if (warn.Length > 0)
                    showMessage(warn, "warning!");
                new Output(oocs);
            } catch (core.PcbException ex)
            {
                showMessage(ex.Message, "pcb error!");
            } catch (Exception ex)
            {
                showMessage(ex.Message, "error!");
                log(ex);
            }
        }
        void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (closed)
                return;
            e.Cancel = true;
            closed = true;
            string gettext = getText();
            if (Editor.Text == "")
            {
                deleteBackupFile();
                Application.Current.Shutdown();
                return;
            }
            if (autocomplete.Visibility == Visibility.Visible)
                autocomplete.Hide();
            if (path == "")
            {                
                var dialogResult = CustomMessageBox.ShowMessage(Properties.UIresources.promptSaveNew, Properties.Resources.warn);
                if (dialogResult == CustomMessageBox.State.yes)
                {
                    SaveFileDialog savefiledialog = new SaveFileDialog();
                    savefiledialog.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    if (Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "documents"))
                    {
                        savefiledialog.InitialDirectory += @"documents\";
                    }
                    savefiledialog.Filter = "*.pcb|*.pcb|*.txt|*.txt";
                    if (savefiledialog.ShowDialog() == true)
                    {
                        File.WriteAllText(savefiledialog.FileName, gettext, new UTF8Encoding());
                        deleteBackupFile();
                        Application.Current.Shutdown();
                    }
                    else
                    {
                        closed = false;
                        return;
                    }
                }
                else if (dialogResult == CustomMessageBox.State.close)
                {
                    closed = false;
                    return;
                }
                else if (dialogResult == CustomMessageBox.State.no)
                {
                    deleteBackupFile();
                    Application.Current.Shutdown();
                }
            }
            else
            {
                try
                {
                    string text = "";
                    text = File.ReadAllText(path, new UTF8Encoding());
                    if (text != gettext)
                    {
                        var dialogResult = CustomMessageBox.ShowMessage(Properties.UIresources.unsaveDetected, Properties.Resources.warn);
                        if (dialogResult == CustomMessageBox.State.yes)
                        {
                            File.WriteAllText(path, gettext);
                            Application.Current.Shutdown();
                        }
                        else if (dialogResult == CustomMessageBox.State.close)
                        {
                            closed = false;
                            return;
                        }
                        else if (dialogResult == CustomMessageBox.State.no)
                        {
                            deleteBackupFile();
                            Application.Current.Shutdown();
                        }
                    }
                    else
                    {
                        deleteBackupFile();
                        Application.Current.Shutdown();                    
                    }
                }
                catch
                {

                }
            }
            deleteBackupFile();
        }
        //command classes
        class generate : ICommand
        {
            MainWindow parent;
            public generate(MainWindow window)
            {
                parent = window;
            }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter)
            {
                return true;
            }
            public void Execute(object parameter)
            {
                var Editor = parent.Editor;
                string text = Editor.Text;
                if (Editor.SelectionLength > 0)
                {
                    text = Editor.SelectedText;
                }
                var parser = new core.PcbParser();
                core.chain.AbstractCBChain chain;
                if (parent.useBlockStruc)
                    chain = new core.chain.BoxCbChain(new int[] { 0, 2, 0 });
                else
                    chain = new core.chain.StraightCbChain(new int[] { 0, 2, 0 });
                try
                {
                    string[] oocs = parser.getOOC(text, chain);
                    string warn = parser.checkForCondDir();
                    if (warn.Length > 0)
                        parent.showMessage(warn, Properties.Resources.warn);
                    new Output(oocs);
                }
                catch (core.PcbException ex)
                {
                    parent.showMessage(ex.Message, Properties.Resources.error);
                }
                catch (Exception ex)
                {
                    parent.showMessage(ex.Message, Properties.Resources.error);
                    parent.log(ex);
                }
            }
        }
        class blackTech : ICommand
        {
            MainWindow parent;
            public blackTech(MainWindow window)
            {
                parent = window;
            }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter)
            {
                return true;
            }
            public void Execute(object parameter)
            {
                var Editor = parent.Editor;
                var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
                string text = Editor.Document.GetText(line);
                Editor.Document.Replace(line, CommandUtil.colorBlackTech(text));
            }
        }
        class commentOut : ICommand
        {
            MainWindow parent;
            public commentOut(MainWindow window)
            {
                parent = window;
            }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter)
            {
                return true;
            }
            public void Execute(object parameter)
            {
                parent.commentSelectedLine();
            }
        }
        class unComment : ICommand
        {
            MainWindow parent;
            public unComment(MainWindow window)
            {
                parent = window;
            }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter)
            {
                return true;
            }
            public void Execute(object parameter)
            {
                parent.uncommentSelectedLine();
            }
        }
        class EscapeCommand : ICommand
        {
            MainWindow parent;
            public EscapeCommand(MainWindow window)
            {
                parent = window;
            }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter)
            {
                return true;
            }
            public void Execute(object parameter)
            {
                try
                {
                    parent.Editor.Document.Replace(parent.Editor.SelectionStart, parent.Editor.SelectionLength, CommandUtil.escape(parent.Editor.SelectedText));
                }
                catch
                { }
            }
        }
        class DeescapeCommand : ICommand
        {
            MainWindow parent;
            public DeescapeCommand(MainWindow window)
            {
                parent = window;
            }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter)
            {
                return true;
            }
            public void Execute(object parameter)
            {
                try
                {
                    string text = parent.Editor.SelectedText.Replace("\\\"", "\"");
                    text = text.Replace("\\\\", "\\");
                    parent.Editor.Document.Replace(parent.Editor.SelectionStart, parent.Editor.SelectionLength, text);

                }
                catch
                {

                }
            }
        }
        class foldAll : ICommand
        {
            MainWindow parent;
            public foldAll(MainWindow window)
            {
                parent = window;
            }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter)
            {
                return true;
            }
            public void Execute(object parameter)
            {
                foreach (FoldingSection section in parent.foldingManager.AllFoldings)
                {
                    section.IsFolded = true;
                }
            }
        }
        class add_type : ICommand
        {
            MainWindow parent;
            public add_type(MainWindow window)
            {
                parent = window;
            }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter)
            {
                return true;
            }
            public void Execute(object parameter)
            {
                if (core.PcbParser.markerType)
                    parent.Editor.Document.Insert(parent.Editor.SelectionStart, "type=AreaEffectCloud");
                else
                    parent.Editor.Document.Insert(parent.Editor.SelectionStart, "type=ArmorStand");
            }
        }
        class save : ICommand
        {
            MainWindow parent;
            public save(MainWindow window)
            {
                parent = window;
            }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter)
            {
                return true;
            }
            public void Execute(object parameter)
            {
                try
                {
                    if (parent.path != "")
                    {
                        File.WriteAllText(parent.path, parent.getText());
                        parent.LastEditTB.Text = Properties.UIresources.lastSaved + DateTime.Now.ToString();
                    }
                    else
                    {
                        SaveFileDialog savefiledialog = new SaveFileDialog();
                        savefiledialog.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
                        if (Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "documents"))
                        {
                            savefiledialog.InitialDirectory += @"documents\";
                        }
                        savefiledialog.Filter = "*.pcb|*.pcb|*.txt|*.txt";
                        if (savefiledialog.ShowDialog() == true)
                        {
                            parent.path = savefiledialog.SafeFileName;
                            File.WriteAllText(savefiledialog.FileName, parent.getText(), new UTF8Encoding());
                            parent.LastEditTB.Text = Properties.UIresources.lastSaved + DateTime.Now.ToString();
                            parent.Title = System.IO.Path.GetFileName(parent.path) + "----" + Properties.UIresources.pcbName;
                            parent.SaveFile.IsEnabled = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    parent.showMessage(Properties.Resources.ioError + "\n" + ex.ToString(), Properties.Resources.error);
                    parent.log(ex);
                }
            }
        }
        class insertFormat : ICommand
        {
            MainWindow parent;
            public insertFormat(MainWindow window)
            {
                parent = window;
            }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter)
            {
                return true;
            }
            public void Execute(object parameter)
            {
                parent.Editor.Document.Insert(parent.Editor.SelectionStart, "§");
            }
        }
        class insertRandomNBT : ICommand
        {
            MainWindow parent;
            public insertRandomNBT(MainWindow window)
            {
                parent = window;
            }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter)
            {
                return true;
            }
            public void Execute(object parameter)
            {
                long[] uuid = CommandUtil.randomUUIDPair();
                string text = CommandUtil.UUIDPairToString(uuid[0],uuid[1]);
                parent.Editor.Document.Insert(parent.Editor.SelectionStart, text);
            }
        }
        class selectLine : ICommand
        {
            private TextEditor editor;
            public selectLine(TextEditor _editor)
            {
                editor = _editor;
            }

            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                DocumentLine line = editor.Document.GetLineByOffset(editor.CaretOffset);
                editor.Select(line.Offset, line.Length);
            }
        }
        class findReplaceCommand : ICommand
        {
            private MainWindow parent;
            public findReplaceCommand(MainWindow window)
            {
                parent = window;
            }

            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                parent.findReplaceDialog.Show();
            }
        }
    }
}
public class BraceFoldingStrategy : ICSharpCode.AvalonEdit.Folding.NewFolding
{
    /// <summary>
    /// Gets/Sets the opening brace. The default value is '{'.
    /// </summary>
    public char OpeningBrace { get; set; }

    /// <summary>
    /// Gets/Sets the closing brace. The default value is '}'.
    /// </summary>
    public char ClosingBrace { get; set; }

    /// <summary>
    /// Creates a new BraceFoldingStrategy.
    /// </summary>
    public BraceFoldingStrategy()
    {
        this.OpeningBrace = '{';
        this.ClosingBrace = '}';
    }

    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        int firstErrorOffset;
        IEnumerable<NewFolding> foldings = CreateNewFoldings(document, out firstErrorOffset);
        manager.UpdateFoldings(foldings, firstErrorOffset);
    }

    /// <summary>
    /// Create <see cref="NewFolding"/>s for the specified document.
    /// </summary>
    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        return CreateNewFoldings(document);
    }

    /// <summary>
    /// Create <see cref="NewFolding"/>s for the specified document.
    /// </summary>
    public IEnumerable<NewFolding> CreateNewFoldings(ITextSource document)
    {
        List<NewFolding> newFoldings = new List<NewFolding>();

        Stack<int> startOffsets = new Stack<int>();
        int lastNewLineOffset = 0;
        char openingBrace = this.OpeningBrace;
        char closingBrace = this.ClosingBrace;
        bool Comment = false;
        bool tabSpaceOnly = true;
        for (int i = 0; i < document.TextLength; i++)
        {
            char c = document.GetCharAt(i);
            if (tabSpaceOnly)
            {
                if (i + 1 < document.TextLength)
                {

                    if (document.GetCharAt(i) == '/' && document.GetCharAt(i + 1) == '/')
                    {
                        Comment = true;
                    }
                }
            }
            if (!(c == '\t' || c == ' '))
            {
                tabSpaceOnly = false;
            }

            if (c == openingBrace && Comment)
            {
                startOffsets.Push(i);
            }
            else if (c == closingBrace && startOffsets.Count > 0 && Comment)
            {
                int startOffset = startOffsets.Pop();
                // don't fold if opening and closing brace are on the same line
                if (startOffset < lastNewLineOffset)
                {
                    newFoldings.Add(new NewFolding(startOffset, i + 1));
                }
            }
            else if (c == '\n' || c == '\r')
            {
                Comment = false;
                tabSpaceOnly = true;
                lastNewLineOffset = i + 1;
            }
        }
        newFoldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return newFoldings;
    }
}
public class XBackgroundRenderer : IBackgroundRenderer
{
    TextEditor editor;

    public XBackgroundRenderer(TextEditor e)
    {
        editor = e;
    }

    public KnownLayer Layer
    {
        get { return KnownLayer.Caret; }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        textView.EnsureVisualLines();
        var line = editor.Document.GetLineByOffset(editor.CaretOffset);
        Brush background = new SolidColorBrush(Color.FromArgb(20, 200, 200, 200));
        Pen border = new Pen();
        if (line.TotalLength > 5000)
            return;
        var segment = new TextSegment { StartOffset = line.Offset, EndOffset = line.EndOffset };

        foreach (Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
        {
            drawingContext.DrawRoundedRectangle(background, border, new Rect(r.Location, new Size(textView.ActualWidth, r.Height)), 3, 3);
        }
    }
}
public class BracketBracing : DocumentColorizingTransformer
{
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    private int linenum;
    private int selectedIndex;
    private SolidColorBrush brush = new SolidColorBrush();
    public BracketBracing(int index, int linenumber)
    {
        selectedIndex = index;
        linenum = linenumber;
        brush.Color = (Color)ColorConverter.ConvertFromString("#0E9ED7");
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (!(line.LineNumber == linenum))
            return;
        string text = CurrentContext.Document.GetText(line);
        if (text.Length == 0)
            return;
        if (selectedIndex - line.Offset < 0 || selectedIndex - line.Offset > text.Length - 1)
            return;
        if (text[selectedIndex - line.Offset] == '{' || text[selectedIndex - line.Offset] == '(' || text[selectedIndex - line.Offset] == '[')
        {
            Stack<char> quotes = new Stack<char>();
            int start = selectedIndex - line.Offset;
            for (int i = start; i < line.Length; i++)
            {
                if (text[i] == '{' || text[i] == '(' || text[i] == '[')
                    quotes.Push(text[i]);
                if (text[i] == '}' || text[i] == ')' || text[i] == ']')
                    if ((quotes.Peek() == '{' && text[i] == '}') || (quotes.Peek() == '[' && text[i] == ']') || (quotes.Peek() == '(' && text[i] == ')'))
                        quotes.Pop();
                    else
                        return;
                if (quotes.Count == 0)
                {
                    ChangeLinePart(start + line.Offset, line.Offset + start + 1, element => element.TextRunProperties.SetBackgroundBrush(Brushes.Blue));
                    ChangeLinePart(i + line.Offset, i + line.Offset + 1, element => element.TextRunProperties.SetBackgroundBrush(Brushes.Blue));
                    break;
                }
            }
        }
        if (text[selectedIndex - line.Offset] == '}' || text[selectedIndex - line.Offset] == ')' || text[selectedIndex - line.Offset] == ']')
        {
            Stack<char> quotes = new Stack<char>();
            int start = selectedIndex - line.Offset;
            for (int i = start; i > -1; i--)
            {
                if (text[i] == '}' || text[i] == ')' || text[i] == ']')
                    quotes.Push(text[i]);
                if (text[i] == '{' || text[i] == '(' || text[i] == '[')
                    if ((quotes.Peek() == '}' && text[i] == '{') || (quotes.Peek() == ']' && text[i] == '[') || (quotes.Peek() == ')' && text[i] == '('))
                        quotes.Pop();
                    else
                        return;
                if (quotes.Count == 0)
                {
                    ChangeLinePart(start + line.Offset, start + line.Offset + 1, element => element.TextRunProperties.SetBackgroundBrush(Brushes.Blue));
                    ChangeLinePart(i + line.Offset, i + line.Offset + 1, element => element.TextRunProperties.SetBackgroundBrush(Brushes.Blue));
                    break;
                }
            }

        }
    }
}
