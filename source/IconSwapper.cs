using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WowIconSwapper {
    public sealed class Spell {
        public int Id; public string Name, Icon, Context;
        public override string ToString() { return Name + "  ·  " + Id + (String.IsNullOrEmpty(Context) ? "" : "  ·  " + Context); }
    }
    public static class Core {
        public static readonly string Home = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string State = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WoWIconSwapper");
        static readonly HttpClient Http = MakeClient();
        static HttpClient MakeClient() {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var client = new HttpClient(); client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WoWIconSwapper/1.0"); return client;
        }
        static Dictionary<string, object> Json(string value) { return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(value); }
        static string Field(Dictionary<string, object> obj, string key) { return obj.ContainsKey(key) ? Convert.ToString(obj[key]) : ""; }
        public static int ParseId(string query) {
            int id;
            if (Int32.TryParse(query.Trim(), out id) && id > 0) return id;
            var match = Regex.Match(query, @"(?:spell[=/]|/spells/)(\d+)", RegexOptions.IgnoreCase);
            return match.Success && Int32.TryParse(match.Groups[1].Value, out id) && id > 0 ? id : 0;
        }
        public static async Task<List<Spell>> Search(string query) {
            int id = ParseId(query);
            if (id > 0) return new List<Spell> { await Lookup(id) };
            var obj = Json(await Http.GetStringAsync("https://www.wowhead.com/search/suggestions-template?q=" + Uri.EscapeDataString(query)));
            var result = new List<Spell>();
            if (!obj.ContainsKey("results")) throw new Exception("No spell results returned. Try a spell ID or Wowhead spell link.");
            foreach (var item in (System.Collections.IEnumerable)obj["results"]) {
                var row = item as Dictionary<string, object>;
                if (row == null || Field(row, "type") != "6") continue;
                string context = "";
                if (row.ContainsKey("pinBreadcrumb")) context = String.Join(" / ", ((System.Collections.IEnumerable)row["pinBreadcrumb"]).Cast<object>().Select(Convert.ToString).ToArray());
                result.Add(new Spell { Id = Convert.ToInt32(row["id"]), Name = Field(row, "name"), Icon = Field(row, "icon"), Context = context });
            }
            if (result.Count == 0) throw new Exception("No matching spells found. Try the exact English ability name or its spell ID.");
            return result;
        }
        public static async Task<Spell> Lookup(int id) {
            var obj = Json(await Http.GetStringAsync("https://nether.wowhead.com/tooltip/spell/" + id));
            string name = Field(obj, "name"), icon = Field(obj, "icon");
            if (String.IsNullOrWhiteSpace(name) || String.IsNullOrWhiteSpace(icon)) throw new Exception("No icon found for spell " + id + ".");
            return new Spell { Id = id, Name = name, Icon = icon };
        }
        public static string FindNumericIcon(string data, string id) {
            using (var reader = new StringReader(data)) {
                string line;
                while ((line = reader.ReadLine()) != null) {
                    if (!line.StartsWith(id + ";", StringComparison.Ordinal)) continue;
                    string path = line.Substring(id.Length + 1).Replace('\\', '/');
                    if (path.StartsWith("interface/icons/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".blp", StringComparison.OrdinalIgnoreCase))
                        return ValidateIcon(Path.GetFileNameWithoutExtension(path));
                }
            }
            return null;
        }
        public static string ValidateIcon(string name) {
            if (!Regex.IsMatch(name ?? "", @"^[a-zA-Z0-9_\-]+$") || Regex.IsMatch(name, @"^\d+$"))
                throw new Exception("A texture filename could not be resolved. No file was installed.");
            return name.ToLowerInvariant();
        }
        public static async Task<string> ResolveIcon(string raw) {
            if (!Regex.IsMatch(raw ?? "", @"^\d+$")) return ValidateIcon(raw);
            string[] files = { Path.Combine(State, "interface.csv"), Path.Combine(Home, "icon-names.csv") };
            foreach (string file in files) {
                if (!File.Exists(file)) continue;
                string found = FindNumericIcon(File.ReadAllText(file), raw);
                if (found != null) return found;
            }
            string data = await Http.GetStringAsync("https://raw.githubusercontent.com/wowdev/wow-listfile/master/parts/interface.csv");
            Directory.CreateDirectory(State); File.WriteAllText(files[0], data);
            string resolved = FindNumericIcon(data, raw);
            if (resolved == null) throw new Exception("Texture file ID " + raw + " is not in the current public icon list. No file was installed.");
            return resolved;
        }
        public static string RetailFolder(string folder) {
            if (String.IsNullOrWhiteSpace(folder)) throw new Exception("Choose your World of Warcraft or _retail_ folder.");
            string full = Path.GetFullPath(folder.Trim());
            if (Directory.Exists(Path.Combine(full, "_retail_"))) full = Path.Combine(full, "_retail_");
            if (!File.Exists(Path.Combine(full, "Wow.exe")) || !String.Equals(new DirectoryInfo(full).Name, "_retail_", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Choose the World of Warcraft folder containing _retail_, or _retail_ itself. It must contain Wow.exe.");
            return full;
        }
        public static Bitmap Resize(string file, bool fit) {
            using (var image = Image.FromFile(file)) {
                var output = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(output)) {
                    g.Clear(Color.Transparent); g.CompositingMode = CompositingMode.SourceCopy;
                    if (image.Width == 64 && image.Height == 64) g.DrawImageUnscaled(image, 0, 0);
                    else {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        float scale = fit ? Math.Min(64f / image.Width, 64f / image.Height) : Math.Max(64f / image.Width, 64f / image.Height);
                        float width = image.Width * scale, height = image.Height * scale;
                        using (var attributes = new ImageAttributes()) {
                            attributes.SetWrapMode(WrapMode.TileFlipXY);
                            g.DrawImage(image, new Rectangle((int)Math.Round((64-width)/2), (int)Math.Round((64-height)/2), (int)Math.Round(width), (int)Math.Round(height)), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                        }
                    }
                }
                return output;
            }
        }
        public static void WriteTga(Bitmap bitmap, string file) {
            using (var writer = new BinaryWriter(File.Create(file))) {
                byte[] header = new byte[18]; header[2] = 2; header[12] = 64; header[14] = 64; header[16] = 32; header[17] = 8;
                writer.Write(header);
                for (int y = 63; y >= 0; y--) for (int x = 0; x < 64; x++) {
                    Color p = bitmap.GetPixel(x, y); writer.Write(p.B); writer.Write(p.G); writer.Write(p.R); writer.Write(p.A);
                }
            }
        }
        public static string Install(Bitmap bitmap, string folder, string icon) {
            string retail = RetailFolder(folder);
            string icons = Path.Combine(retail, "Interface", "Icons");
            Directory.CreateDirectory(icons);
            string target = Path.Combine(icons, ValidateIcon(icon) + ".tga");
            string temp = Path.Combine(icons, ".icon-swapper-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                WriteTga(bitmap, temp);
                if (File.Exists(target)) {
                    string backups = Path.Combine(retail, "Interface", "IconSwapperBackups"); Directory.CreateDirectory(backups);
                    string backup = Path.Combine(backups, icon + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".tga");
                    File.Replace(temp, target, backup);
                } else File.Move(temp, target);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
            return target;
        }
    }
    public sealed class MainForm : Form {
        TextBox query = new TextBox(), folder = new TextBox();
        ComboBox results = new ComboBox(), sizing = new ComboBox();
        PictureBox preview = new PictureBox();
        Label imageName = new Label(), resolved = new Label(), status = new Label();
        Button search = new Button(), install = new Button();
        string imagePath, iconName; Bitmap converted; bool loading;
        public MainForm() {
            Text = "WoW Icon Swapper"; ClientSize = new Size(680, 625); MinimumSize = new Size(696, 664);
            AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(245, 247, 250);
            StartPosition = FormStartPosition.CenterScreen; MaximizeBox = false; FormBorderStyle = FormBorderStyle.FixedSingle;
            AddLabel("WoW Icon Swapper", 24, 20, 630, 34, 20, true);
            AddLabel("Custom ability icons for Retail WoW", 26, 58, 620, 24, 10, false);
            AddLabel("1   Choose an image", 24, 100, 500, 26, 12, true);
            preview.SetBounds(26, 137, 96, 96); preview.SizeMode = PictureBoxSizeMode.StretchImage; preview.BackColor = Color.FromArgb(222, 227, 235); preview.BorderStyle = BorderStyle.FixedSingle; Controls.Add(preview);
            var browseImage = AddButton("Choose image…", 142, 138, 160, 34);
            browseImage.Click += (s,e) => { using (var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff", Title = "Choose your custom icon" }) { if (dialog.ShowDialog() == DialogResult.OK) LoadImage(dialog.FileName); } };
            imageName.SetBounds(142, 177, 500, 25); imageName.Text = "PNG, JPG, BMP, GIF or TIFF · drag and drop supported"; imageName.AutoEllipsis = true; Controls.Add(imageName);
            sizing.SetBounds(142, 204, 245, 28); sizing.DropDownStyle = ComboBoxStyle.DropDownList; sizing.Items.AddRange(new object[] { "Fill square (center crop)", "Fit entire image (transparent padding)" }); sizing.SelectedIndex = 0; Controls.Add(sizing);
            sizing.SelectedIndexChanged += (s,e) => { if (imagePath != null) LoadImage(imagePath); };
            AddLabel("2   Find the ability", 24, 250, 500, 26, 12, true);
            query.SetBounds(26, 287, 494, 29); Controls.Add(query);
            search.Text = "Find spell"; search.SetBounds(532, 285, 120, 33); Controls.Add(search); search.Click += async (s,e) => await Search();
            query.KeyDown += async (s,e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (!loading) await Search(); } };
            query.TextChanged += (s,e) => { if (!loading) { results.Items.Clear(); iconName = null; resolved.Text = "Enter an English spell name, ID, or Wowhead link."; UpdateInstall(); } };
            results.SetBounds(26, 328, 626, 30); results.DropDownStyle = ComboBoxStyle.DropDownList; results.DropDownWidth = 900; Controls.Add(results);
            results.SelectedIndexChanged += async (s,e) => { if (!loading && results.SelectedItem != null) await ResolveSelected(); };
            resolved.SetBounds(26, 365, 626, 26); resolved.AutoEllipsis = true; resolved.Text = "Enter an English spell name, ID, or Wowhead link."; Controls.Add(resolved);
            AddLabel("3   Choose your WoW folder", 24, 405, 500, 26, 12, true);
            folder.SetBounds(26, 442, 494, 29); Controls.Add(folder);
            var browseFolder = AddButton("Browse…", 532, 440, 120, 33);
            browseFolder.Click += (s,e) => { using (var dialog = new FolderBrowserDialog { Description = "Select World of Warcraft or its _retail_ folder", SelectedPath = folder.Text }) { if (dialog.ShowDialog() == DialogResult.OK) folder.Text = dialog.SelectedPath; } };
            AddLabel("Installs into _retail_\\Interface\\Icons. Existing files are backed up.", 26, 481, 626, 24, 9, false);
            install.SetBounds(26, 520, 626, 40); install.Text = "Install custom icon"; install.BackColor = Color.FromArgb(37, 99, 210); install.ForeColor = Color.White; install.FlatStyle = FlatStyle.Flat; install.Enabled = false; Controls.Add(install);
            install.Click += (s,e) => Install();
            status.SetBounds(26, 570, 626, 45); status.Text = "Close WoW before installing. Start it again after installation."; Controls.Add(status);
            AllowDrop = true; DragEnter += (s,e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s,e) => { var files = (string[])e.Data.GetData(DataFormats.FileDrop); if (files.Length > 0) LoadImage(files[0]); };
            string settings = Path.Combine(Core.State, "wow-folder.txt");
            if (File.Exists(settings)) { try { folder.Text = File.ReadAllText(settings).Trim(); } catch {} }
            if (String.IsNullOrEmpty(folder.Text)) foreach (string root in new [] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) }) {
                string candidate = Path.Combine(root, "World of Warcraft", "_retail_"); if (File.Exists(Path.Combine(candidate, "Wow.exe"))) { folder.Text = candidate; break; }
            }
        }
        void AddLabel(string text, int x, int y, int w, int h, int size, bool bold) { Controls.Add(new Label { Text = text, Bounds = new Rectangle(x,y,w,h), Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular) }); }
        Button AddButton(string text, int x, int y, int w, int h) { var b = new Button { Text = text, Bounds = new Rectangle(x,y,w,h) }; Controls.Add(b); return b; }
        void UpdateInstall() { install.Enabled = !loading && converted != null && iconName != null; }
        void Busy(bool busy, string message) { loading = busy; search.Enabled = !busy; results.Enabled = !busy; query.Enabled = !busy; status.Text = message; UpdateInstall(); }
        void Error(Exception ex) { status.Text = "Could not complete the action."; MessageBox.Show(this, ex.Message, "WoW Icon Swapper", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        void LoadImage(string path) {
            try {
                var next = Core.Resize(path, sizing.SelectedIndex == 1);
                preview.Image = next; if (converted != null) converted.Dispose(); converted = next; imagePath = path;
                imageName.Text = Path.GetFileName(path) + "  →  64 × 64"; UpdateInstall();
            } catch (Exception ex) { Error(new Exception("Could not open this image. Choose a PNG, JPG, BMP, GIF or TIFF.\n\n" + ex.Message)); }
        }
        async Task Search() {
            if (String.IsNullOrWhiteSpace(query.Text)) return;
            iconName = null; Busy(true, "Looking up spells…");
            try {
                var spells = await Core.Search(query.Text.Trim()); results.Items.Clear();
                foreach (var spell in spells) results.Items.Add(spell);
                results.SelectedIndex = 0;
            } catch (Exception ex) { results.Items.Clear(); resolved.Text = "Lookup failed. Check your connection and try again."; Error(ex); }
            finally { Busy(false, "Choose the matching spell, then install your icon."); }
            if (results.Items.Count > 0) await ResolveSelected();
        }
        async Task ResolveSelected() {
            var selected = results.SelectedItem as Spell; if (selected == null) return;
            iconName = null; Busy(true, "Resolving texture filename…");
            try {
                var spell = await Core.Lookup(selected.Id);
                iconName = await Core.ResolveIcon(spell.Icon);
                resolved.Text = spell.Name + "  →  " + iconName + ".tga";
            } catch (Exception ex) { resolved.Text = "Texture name could not be resolved."; Error(ex); }
            finally { Busy(false, iconName == null ? "No file installed. Try another spell or check your connection." : "Ready. Close WoW before installing this icon."); }
        }
        void Install() {
            try {
                string retail = Core.RetailFolder(folder.Text);
                if (System.Diagnostics.Process.GetProcessesByName("Wow").Length > 0) throw new Exception("Close World of Warcraft before installing, then click Install again.");
                string target = Core.Install(converted, retail, iconName);
                try { Directory.CreateDirectory(Core.State); File.WriteAllText(Path.Combine(Core.State, "wow-folder.txt"), retail); } catch {}
                folder.Text = retail; status.Text = "Installed! Launch WoW to load your new icon.";
                MessageBox.Show(this, "Installed successfully:\n" + target + "\n\nLaunch WoW again to load it. A /reload is not enough.", "Icon installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (UnauthorizedAccessException) { Error(new Exception("Windows denied access to the WoW folder. Close the app and use Run as administrator, then install again.")); }
            catch (Exception ex) { Error(ex); }
        }
        protected override void Dispose(bool disposing) { if (disposing && converted != null) { preview.Image = null; converted.Dispose(); converted = null; } base.Dispose(disposing); }
    }
    public static class Program {
        [STAThread] public static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
    }
}
