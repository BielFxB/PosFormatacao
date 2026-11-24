using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("PosFormatacao")]
[assembly: AssemblyDescription("Instalador de programas e drivers para Windows")]
[assembly: AssemblyVersion("1.1.0.0")]

internal sealed class Package
{
    public readonly string Category, Name, Id, Architecture, Source;

    public Package(string line)
    {
        string[] fields = line.Split('|');
        Category = fields[0];
        Name = fields[1];
        Id = fields[2];
        Architecture = fields.Length > 3 ? fields[3] : "";
        Source = fields.Length > 4 ? fields[4] : "winget";
    }

    public override string ToString() { return Name; }
}

internal sealed class PosFormatacao : Form
{
    private static readonly Color Background = Color.FromArgb(19, 25, 37);
    private static readonly Color PanelColor = Color.FromArgb(29, 38, 54);
    private static readonly Color TextColor = Color.FromArgb(236, 241, 249);
    private static readonly Color Muted = Color.FromArgb(173, 188, 209);
    private static readonly Color Accent = Color.FromArgb(113, 221, 194);
    private readonly Package[] catalog;
    private readonly HashSet<Package> selected = new HashSet<Package>();
    private readonly TextBox search = new TextBox();
    private readonly TextBox log = new TextBox();
    private readonly Label count = new Label();
    private readonly Label status = new Label();
    private readonly Button install = new Button();
    private readonly Button scan = new Button();
    private readonly Button drivers = new Button();
    private readonly CheckBox includeDrivers = new CheckBox();
    private readonly TabControl tabs = new TabControl();
    private readonly bool driverWorker;
    private bool busy;

    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new PosFormatacao(args.Length == 1 && args[0] == "--drivers"));
    }

    private PosFormatacao(bool driverWorker)
    {
        this.driverWorker = driverWorker;
        catalog = ReadResource("Catalogo").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => new Package(line)).ToArray();
        Text = driverWorker ? "PosFormatacao · Atualização de drivers" : "PosFormatacao";
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(920, 720);
        MinimumSize = new Size(700, 590);
        BackColor = Background;
        ForeColor = TextColor;
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 7 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, driverWorker ? 0 : 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, driverWorker ? 0 : 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, driverWorker ? 0 : 51));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, driverWorker ? 0 : 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, driverWorker ? 100 : 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(layout);

        var header = new Panel { Dock = DockStyle.Fill };
        header.Controls.Add(new Label {
            Text = "PosFormatacao", Font = new Font("Segoe UI Semibold", 25),
            AutoSize = true, Location = new Point(0, 0)
        });
        header.Controls.Add(new Label {
            Text = driverWorker ? "Drivers recomendados pelo Windows Update" : "Programas e drivers para um Windows recém-instalado",
            ForeColor = Muted, AutoSize = true, Location = new Point(3, 49)
        });
        layout.Controls.Add(header, 0, 0);

        var filter = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        filter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        filter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        filter.Controls.Add(new Label { Text = "Buscar", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        search.Dock = DockStyle.Fill;
        search.AccessibleName = "Buscar programas";
        search.BackColor = PanelColor;
        search.ForeColor = TextColor;
        search.TextChanged += delegate { PopulateTabs(true); };
        filter.Controls.Add(search, 1, 0);
        count.Dock = DockStyle.Fill;
        count.TextAlign = ContentAlignment.MiddleRight;
        count.ForeColor = Muted;
        filter.Controls.Add(count, 2, 0);
        layout.Controls.Add(filter, 0, 1);

        tabs.Dock = DockStyle.Fill;
        tabs.Multiline = true;
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.Padding = new Point(12, 6);
        tabs.DrawItem += DrawTab;
        layout.Controls.Add(tabs, 0, 2);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 7, 0, 0) };
        StyleButton(install, "Instalar selecionados", true);
        StyleButton(scan, "Consultar drivers", false);
        StyleButton(drivers, "Atualizar drivers", false);
        install.Click += async delegate { await InstallSelected(); };
        scan.Click += async delegate { await RunDrivers(true); };
        drivers.Click += async delegate { await RunDrivers(false); };
        actions.Controls.AddRange(new Control[] { install, scan, drivers });
        layout.Controls.Add(actions, 0, 3);
        includeDrivers.Text = "Atualizar drivers após instalar os programas";
        includeDrivers.AutoSize = true;
        includeDrivers.ForeColor = Muted;
        layout.Controls.Add(includeDrivers, 0, 4);

        log.Dock = DockStyle.Fill;
        log.AccessibleName = "Registro de atividade";
        log.Multiline = true;
        log.ReadOnly = true;
        log.ScrollBars = ScrollBars.Vertical;
        log.BackColor = PanelColor;
        log.ForeColor = TextColor;
        log.Font = new Font("Consolas", 10);
        layout.Controls.Add(log, 0, 5);
        status.Dock = DockStyle.Fill;
        status.ForeColor = Muted;
        status.TextAlign = ContentAlignment.MiddleLeft;
        status.Text = "Pronto";
        layout.Controls.Add(status, 0, 6);

        filter.Visible = tabs.Visible = actions.Visible = includeDrivers.Visible = !driverWorker;
        if (driverWorker)
            Shown += async delegate { await RunDrivers(false); };
        else
        {
            PopulateTabs(false);
            WriteLog("Marque os programas que deseja instalar ou consulte os drivers disponíveis.");
        }
        FormClosing += delegate(object sender, FormClosingEventArgs e) {
            if (!busy) return;
            e.Cancel = true;
            MessageBox.Show(this, "Aguarde a operação atual terminar.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
    }

    private void DrawTab(object sender, DrawItemEventArgs e)
    {
        bool active = e.Index == tabs.SelectedIndex;
        using (var brush = new SolidBrush(active ? Accent : PanelColor))
            e.Graphics.FillRectangle(brush, e.Bounds);
        TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, Font, e.Bounds,
            active ? Background : TextColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void PopulateTabs(bool filtering)
    {
        string category = tabs.SelectedTab == null ? "" : tabs.SelectedTab.Text;
        while (tabs.TabPages.Count > 0)
            tabs.TabPages[0].Dispose();
        string query = search.Text.Trim();
        var groups = catalog.Where(p => p.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
            p.Category.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0).GroupBy(p => p.Category);
        foreach (var group in groups)
        {
            var page = new TabPage(group.Key) { BackColor = PanelColor, Padding = new Padding(8) };
            var list = new CheckedListBox {
                Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.None,
                BackColor = PanelColor, ForeColor = TextColor, IntegralHeight = false,
                AccessibleName = group.Key
            };
            foreach (Package package in group)
                list.Items.Add(package, selected.Contains(package));
            list.ItemCheck += delegate(object sender, ItemCheckEventArgs e) {
                var package = (Package)list.Items[e.Index];
                if (e.NewValue == CheckState.Checked) selected.Add(package);
                else selected.Remove(package);
                RefreshCount();
            };
            page.Controls.Add(list);
            tabs.TabPages.Add(page);
            if (group.Key == category) tabs.SelectedTab = page;
        }
        if (tabs.TabPages.Count == 0)
        {
            var page = new TabPage("Resultados") { BackColor = PanelColor };
            page.Controls.Add(new Label { Text = "Nenhum programa encontrado.", AutoSize = true, Location = new Point(12, 12) });
            tabs.TabPages.Add(page);
        }
        if (filtering) search.Focus();
        RefreshCount();
    }

    private static void StyleButton(Button button, string caption, bool primary)
    {
        button.Text = caption;
        button.AutoSize = true;
        button.Height = 36;
        button.Padding = new Padding(10, 0, 10, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = primary ? Accent : PanelColor;
        button.ForeColor = primary ? Background : TextColor;
        button.FlatAppearance.BorderColor = primary ? Accent : Muted;
    }

    private void RefreshCount()
    {
        count.Text = selected.Count + (selected.Count == 1 ? " selecionado" : " selecionados");
        install.Enabled = selected.Count > 0 && !busy;
    }

    private void SetBusy(bool value, string message)
    {
        busy = value;
        search.Enabled = tabs.Enabled = scan.Enabled = drivers.Enabled = includeDrivers.Enabled = !value;
        status.Text = message;
        RefreshCount();
    }

    private async Task InstallSelected()
    {
        Package[] queue = catalog.Where(p => selected.Contains(p)).ToArray();
        if (queue.Count(p => p.Name.StartsWith("Node.js", StringComparison.Ordinal)) > 1 ||
            queue.Count(p => p.Id == "Python.Python.3.14") > 1)
        {
            MessageBox.Show(this, "Escolha uma linha do Node.js e uma arquitetura do instalador Python 3.14 por execução.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        bool hasManualDownload = queue.Any(p => p.Id.StartsWith("https://", StringComparison.Ordinal));
        bool updateDrivers = includeDrivers.Checked;
        string confirmation = "Executar os " + queue.Length + " item(ns) selecionado(s) e aceitar os termos dos pacotes e da fonte WinGet?";
        if (updateDrivers) confirmation += "\n\nO PosFormatacao também instalará os drivers recomendados pelo Windows Update e aceitará os termos correspondentes.";
        if (hasManualDownload) confirmation += "\n\nO FileZilla será aberto no navegador para instalação manual.";
        if (MessageBox.Show(this, confirmation,
            Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;

        string winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\WindowsApps\winget.exe");
        int failures = 0;
        int manual = 0;
        bool restart = false;
        SetBusy(true, "Instalando programas…");
        try
        {
            for (int i = 0; i < queue.Length; i++)
            {
                Package package = queue[i];
                status.Text = String.Format("{0}/{1} · {2}", i + 1, queue.Length, package.Name);
                WriteLog("\r\n" + status.Text);
                try
                {
                    if (package.Id.StartsWith("https://", StringComparison.Ordinal))
                    {
                        using (Process.Start(new ProcessStartInfo(package.Id) { UseShellExecute = true })) { }
                        manual++;
                        WriteLog("Download oficial aberto no navegador. Conclua a instalação por lá.");
                        continue;
                    }
                    if (!File.Exists(winget))
                    {
                        failures++;
                        WriteLog("WinGet indisponível. Instale ou atualize o Instalador de Aplicativo pela Microsoft Store.");
                        continue;
                    }
                    string arguments = "install --id \"" + package.Id + "\" --source " + package.Source + " --exact --silent --no-upgrade" +
                        " --accept-source-agreements --accept-package-agreements --disable-interactivity";
                    if (package.Architecture.Length > 0) arguments += " --architecture " + package.Architecture;
                    int result = await RunProcess(winget, arguments);
                    switch (unchecked((uint)result))
                    {
                        case 0: WriteLog("Instalação concluída."); break;
                        case 0x8A150061: WriteLog("Já instalado."); break;
                        case 0x8A15002B: WriteLog("Sem instalação adicional aplicável."); break;
                        case 3010:
                        case 0x8A150109: restart = true; WriteLog("Instalado. Reinicialização necessária."); break;
                        default: failures++; WriteLog(String.Format("Falha no WinGet (0x{0:X8}).", result)); break;
                    }
                }
                catch (Exception error)
                {
                    failures++;
                    WriteLog(error.Message);
                }
            }
        }
        finally
        {
            string summary = failures == 0 ? "Seleção processada" : failures + " instalação(ões) com falha";
            if (manual > 0) summary += " · FileZilla aberto no navegador";
            if (restart) summary += " · Reinicie o Windows";
            SetBusy(false, summary);
            WriteLog(summary);
        }
        if (updateDrivers && !restart) await RunDrivers(false, false);
    }

    private async Task RunDrivers(bool scanOnly, bool confirm = true)
    {
        if (!scanOnly && confirm && !driverWorker && MessageBox.Show(this,
            "Pesquisar e instalar drivers recomendados pelo Windows Update e aceitar os termos desses drivers?",
            Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
        SetBusy(true, scanOnly ? "Consultando drivers…" : "Atualizando drivers…");
        string summary = "Falha na operação de drivers";
        try
        {
            var identity = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            if (!scanOnly && !identity.IsInRole(WindowsBuiltInRole.Administrator))
            {
                if (driverWorker) throw new InvalidOperationException("A instalação de drivers precisa de permissões de administrador.");
                using (Process worker = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--drivers")
                { UseShellExecute = true, Verb = "runas" }))
                {
                    if (worker == null) throw new InvalidOperationException("Não foi possível abrir a janela de drivers.");
                    WriteLog("Acompanhe o resultado na janela de atualização de drivers.");
                    await Task.Run(() => worker.WaitForExit());
                    summary = "Janela de drivers fechada";
                }
            }
            else
            {
                string command = "$ProgressPreference = 'SilentlyContinue'; [Console]::OutputEncoding = [Text.Encoding]::UTF8; & {\n" +
                    ReadResource("Motor") + "\n} -Modo " + (scanOnly ? "ConsultarDrivers" : "Drivers") +
                    " 3>&1 6>&1 | ForEach-Object { [string]$_ }" +
                    "\nexit $LASTEXITCODE";
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
                string powershell = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
                int result = await RunProcess(powershell, "-NoProfile -NonInteractive -EncodedCommand " + encoded);
                summary = result == 0 ? (scanOnly ? "Consulta concluída" : "Etapa de drivers concluída")
                    : result == 3010 ? "Reinicie o Windows para continuar" : "Etapa de drivers encerrada com falhas";
            }
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            if (error.NativeErrorCode == 1223) summary = "Atualização de drivers cancelada";
            else WriteLog(error.Message);
        }
        catch (Exception error) { WriteLog(error.Message); }
        finally
        {
            SetBusy(false, summary);
            WriteLog(summary);
        }
    }

    private Task<int> RunProcess(string file, string arguments)
    {
        return Task.Run(() => {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo(file, arguments) {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { PostLog(e.Data); };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { PostLog(e.Data); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                return process.ExitCode;
            }
        });
    }

    private void PostLog(string line)
    {
        if (!String.IsNullOrWhiteSpace(line))
            BeginInvoke((Action)(() => WriteLog(line)));
    }

    private void WriteLog(string line) { log.AppendText(line + Environment.NewLine); }

    private static string ReadResource(string name)
    {
        using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(name), Encoding.UTF8))
            return reader.ReadToEnd();
    }
}
