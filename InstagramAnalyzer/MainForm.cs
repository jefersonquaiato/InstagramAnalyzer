using System.ComponentModel;
using System.Text;

namespace InstagramAnalyzer.InstagramAnalyzer;

public sealed class MainForm : Form
{
    private readonly InstagramExportReader _reader = new();
    private readonly Label _fileLabel = new();
    private readonly Label _followersValue = MakeValueLabel();
    private readonly Label _followingValue = MakeValueLabel();
    private readonly Label _notFollowingValue = MakeValueLabel();
    private readonly Label _onlyFollowsYouValue = MakeValueLabel();
    private readonly Label _mutualValue = MakeValueLabel();
    private readonly Label _statusLabel = new();
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _filter = new();
    private readonly DataGridView _grid = new();
    private readonly Button _exportButton = new();
    private BindingList<RelationshipRow> _allRows = new();
    private string? _loadedZip;

    public MainForm()
    {
        Text = "Instagram Analyzer";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(850, 600);
        Size = new Size(1050, 720);
        Font = new Font("Segoe UI", 9F);
        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            Padding = new Padding(18), BackColor = Color.FromArgb(247, 248, 250)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label
        {
            Text = "Instagram Analyzer", Font = new Font("Segoe UI", 20, FontStyle.Bold),
            AutoSize = true, Location = new Point(0, 0), ForeColor = Color.FromArgb(35, 39, 47)
        };
        var subtitle = new Label
        {
            Text = "Descubra quem você segue e não segue você de volta.",
            AutoSize = true, Location = new Point(2, 42), ForeColor = Color.DimGray
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        var summary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Padding = new Padding(0, 5, 0, 5)
        };
        for (var i = 0; i < 5; i++) summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        AddMetric(summary, 0, "Seguidores", _followersValue);
        AddMetric(summary, 1, "Seguindo", _followingValue);
        AddMetric(summary, 2, "Não seguem de volta", _notFollowingValue);
        AddMetric(summary, 3, "Seguem só você", _onlyFollowsYouValue);
        AddMetric(summary, 4, "Seguimento mútuo", _mutualValue);
        root.Controls.Add(summary, 0, 1);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 10, 0, 0) };
        var openButton = new Button { Text = "Abrir ZIP do Instagram", AutoSize = true, Height = 34 };
        openButton.Click += (_, _) => OpenZip();
        _exportButton.Text = "Exportar CSV";
        _exportButton.AutoSize = true;
        _exportButton.Height = 34;
        _exportButton.Enabled = false;
        _exportButton.Click += (_, _) => ExportCsv();
        _searchBox.Width = 190;
        _searchBox.PlaceholderText = "Pesquisar usuário...";
        _searchBox.Margin = new Padding(14, 4, 8, 0);
        _searchBox.TextChanged += (_, _) => ApplyFilter();
        _filter.DropDownStyle = ComboBoxStyle.DropDownList;
        _filter.Width = 220;
        _filter.Items.AddRange(new object[] { "Todos", "Não seguem de volta", "Seguem você, mas você não segue", "Seguimento mútuo" });
        _filter.SelectedIndex = 0;
        _filter.SelectedIndexChanged += (_, _) => ApplyFilter();
        _filter.Margin = new Padding(4, 4, 8, 0);
        toolbar.Controls.Add(openButton);
        toolbar.Controls.Add(_searchBox);
        toolbar.Controls.Add(_filter);
        toolbar.Controls.Add(_exportButton);
        root.Controls.Add(toolbar, 0, 2);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Usuário", DataPropertyName = nameof(RelationshipRow.Username), FillWeight = 35 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Relação", DataPropertyName = nameof(RelationshipRow.Kind), FillWeight = 45 });
        var linkColumn = new DataGridViewLinkColumn { HeaderText = "Perfil", DataPropertyName = nameof(RelationshipRow.ProfileUrl), FillWeight = 20, TrackVisitedState = false };
        _grid.Columns.Add(linkColumn);
        _grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 2 &&
                _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value is string url)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        };
        root.Controls.Add(_grid, 0, 3);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = Color.DimGray;
        root.Controls.Add(_statusLabel, 0, 4);
        _fileLabel.Visible = false;
    }

    private static Label MakeValueLabel() => new()
    {
        Text = "—", AutoSize = false, Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 19, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(45, 55, 72)
    };

    private static void AddMetric(TableLayoutPanel panel, int column, string caption, Label value)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10), Margin = new Padding(0, 0, 8, 0) };
        var label = new Label { Text = caption, Dock = DockStyle.Top, Height = 24, ForeColor = Color.DimGray, AutoEllipsis = true };
        card.Controls.Add(value);
        card.Controls.Add(label);
        panel.Controls.Add(card, column, 0);
    }

    private void OpenZip()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Selecione a exportação do Instagram",
            Filter = "Arquivo ZIP (*.zip)|*.zip",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var (followers, following) = _reader.ReadZip(dialog.FileName);
            _loadedZip = dialog.FileName;

            var notFollowingBack = following.Except(followers, StringComparer.OrdinalIgnoreCase)
                .Select(u => new RelationshipRow(u, RelationshipKind.DoesNotFollowBack, Profile(u)));
            var followsYouOnly = followers.Except(following, StringComparer.OrdinalIgnoreCase)
                .Select(u => new RelationshipRow(u, RelationshipKind.FollowsYouOnly, Profile(u)));
            var mutual = followers.Intersect(following, StringComparer.OrdinalIgnoreCase)
                .Select(u => new RelationshipRow(u, RelationshipKind.Mutual, Profile(u)));

            var rows = notFollowingBack.Concat(followsYouOnly).Concat(mutual)
                .OrderBy(r => r.Username, StringComparer.OrdinalIgnoreCase).ToList();
            _allRows = new BindingList<RelationshipRow>(rows);

            _followersValue.Text = followers.Count.ToString("N0");
            _followingValue.Text = following.Count.ToString("N0");
            _notFollowingValue.Text = following.Except(followers, StringComparer.OrdinalIgnoreCase).Count().ToString("N0");
            _onlyFollowsYouValue.Text = followers.Except(following, StringComparer.OrdinalIgnoreCase).Count().ToString("N0");
            _mutualValue.Text = followers.Intersect(following, StringComparer.OrdinalIgnoreCase).Count().ToString("N0");

            _fileLabel.Text = Path.GetFileName(dialog.FileName);
            _statusLabel.Text = $"Arquivo: {_fileLabel.Text}  |  Dados processados localmente no seu computador.";
            _exportButton.Enabled = true;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Não foi possível analisar o ZIP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string Profile(string username) => $"https://www.instagram.com/{Uri.EscapeDataString(username)}/";

    private void ApplyFilter()
    {
        var search = _searchBox.Text.Trim();
        var selected = _filter.SelectedIndex;
        var filtered = _allRows.Where(row =>
            (string.IsNullOrEmpty(search) || row.Username.Contains(search, StringComparison.OrdinalIgnoreCase)) &&
            (selected == 0 ||
             selected == 1 && row.Kind == RelationshipKind.DoesNotFollowBack ||
             selected == 2 && row.Kind == RelationshipKind.FollowsYouOnly ||
             selected == 3 && row.Kind == RelationshipKind.Mutual))
            .Select(row => new
            {
                row.Username,
                Kind = row.Kind switch
                {
                    RelationshipKind.DoesNotFollowBack => "Não segue de volta",
                    RelationshipKind.FollowsYouOnly => "Segue você; você não segue",
                    _ => "Seguimento mútuo"
                },
                row.ProfileUrl
            }).ToList();

        _grid.DataSource = filtered;
    }

    private void ExportCsv()
    {
        if (_allRows.Count == 0) return;
        using var dialog = new SaveFileDialog
        {
            Title = "Salvar análise",
            Filter = "CSV (*.csv)|*.csv",
            FileName = "instagram-analise.csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var lines = new List<string> { "usuario;tipo;perfil" };
        lines.AddRange(_allRows.Select(row =>
        {
            var kind = row.Kind switch
            {
                RelationshipKind.DoesNotFollowBack => "nao_segue_de_volta",
                RelationshipKind.FollowsYouOnly => "segue_voce_mas_voce_nao_segue",
                _ => "seguimento_mutuo"
            };
            return $"{Csv(row.Username)};{Csv(kind)};{Csv(row.ProfileUrl)}";
        }));
        File.WriteAllText(dialog.FileName, string.Join(Environment.NewLine, lines), new UTF8Encoding(true));
        MessageBox.Show(this, "CSV exportado com sucesso.", "Instagram Analyzer", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
