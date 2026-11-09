using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PhotoLayoutTool.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new LayoutForm());
    }
}

internal sealed class LayoutForm : Form
{
    private const int CanvasW = 1800;
    private const int CanvasH = 1200;
    private const int MaxPhotosPerPage = 40;
    private readonly List<PhotoItem> _photos = new();
    private readonly PreviewPanel _preview = new();
    private readonly Label _countLabel = new();
    private readonly Label _gridLabel = new();
    private readonly Label _pageLabel = new();
    private readonly Button _previousPageButton = new();
    private readonly Button _nextPageButton = new();
    private readonly TextBox _numberBox = new();
    private readonly CheckBox _autoGridCheckBox = new();
    private readonly NumericUpDown _colsInput = new();
    private readonly NumericUpDown _rowsInput = new();
    private readonly TrackBar _gapSlider = new();
    private readonly NumericUpDown _gapInput = new();
    private readonly Label _selectedPhotoLabel = new();
    private readonly Button _rotateLeftButton = new();
    private readonly Button _rotateRightButton = new();
    private readonly System.Windows.Forms.Timer _numberRefreshTimer = new();
    private readonly System.Windows.Forms.Timer _layoutRefreshTimer = new();
    private readonly List<PhotoBox> _photoBoxes = new();
    private Bitmap? _previewBitmap;
    private Image? _studioLogo;
    private bool _syncingGridControls;
    private bool _syncingGapControls;
    private int _selectedPhotoIndex = -1;
    private int _currentPage;

    public LayoutForm()
    {
        Text = "fiona索引制作工具";
        MinimumSize = new Size(1120, 760);
        Size = new Size(1220, 820);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(246, 247, 249);
        Icon = LoadAppIcon();
        _studioLogo = LoadEmbeddedImage("fiona-logo.png");
        _numberRefreshTimer.Interval = 220;
        _numberRefreshTimer.Tick += (_, _) =>
        {
            _numberRefreshTimer.Stop();
            RequestLayoutRefresh();
        };
        _layoutRefreshTimer.Interval = 80;
        _layoutRefreshTimer.Tick += (_, _) =>
        {
            _layoutRefreshTimer.Stop();
            RefreshLayout();
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(16),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildWorkArea(), 1, 0);

        RefreshLayout();
    }

    private Control BuildSidebar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(16),
            AutoScroll = true,
        };

        var title = new Label
        {
            Text = "输出",
            Font = new Font("Microsoft YaHei UI", 16, FontStyle.Bold),
            Location = new Point(16, 18),
            Size = new Size(220, 34),
        };

        _countLabel.Font = new Font("Microsoft YaHei UI", 10);
        _countLabel.ForeColor = Color.FromArgb(96, 108, 124);
        _countLabel.Location = new Point(16, 62);
        _countLabel.Size = new Size(220, 24);

        _gridLabel.Font = new Font("Microsoft YaHei UI", 10);
        _gridLabel.ForeColor = Color.FromArgb(96, 108, 124);
        _gridLabel.Location = new Point(16, 88);
        _gridLabel.Size = new Size(220, 24);

        var numberLabel = new Label
        {
            Text = "手工输入编号",
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(68, 78, 92),
            Location = new Point(16, 146),
            Size = new Size(220, 24),
        };

        _numberBox.Font = new Font("Microsoft YaHei UI", 13);
        _numberBox.Location = new Point(16, 174);
        _numberBox.Size = new Size(220, 34);
        _numberBox.TextChanged += (_, _) =>
        {
            _numberRefreshTimer.Stop();
            _numberRefreshTimer.Start();
        };

        var layoutLabel = new Label
        {
            Text = "排版",
            Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(68, 78, 92),
            Location = new Point(16, 220),
            Size = new Size(220, 24),
        };

        _autoGridCheckBox.Text = "自动设置行列";
        _autoGridCheckBox.Checked = false;
        _autoGridCheckBox.Font = new Font("Microsoft YaHei UI", 10);
        _autoGridCheckBox.Location = new Point(16, 250);
        _autoGridCheckBox.Size = new Size(220, 26);
        _autoGridCheckBox.CheckedChanged += (_, _) =>
        {
            UpdateGridControlState();
            RequestLayoutRefresh();
        };

        ConfigurePageControls();

        var gapLabel = MakeSmallLabel("照片间距", 286);
        ConfigureGapControls(286);

        var colsLabel = MakeSmallLabel("每行列数", 360);
        ConfigureNumberInput(_colsInput, 360, 1, 7, 7);
        _colsInput.ValueChanged += (_, _) => RefreshManualGrid();

        var rowsLabel = MakeSmallLabel("行数", 400);
        ConfigureNumberInput(_rowsInput, 400, 1, 6, 6);
        _rowsInput.ValueChanged += (_, _) => RefreshManualGrid();

        var rotateLabel = new Label
        {
            Text = "单张旋转",
            Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(68, 78, 92),
            Location = new Point(16, 444),
            Size = new Size(220, 24),
        };

        _selectedPhotoLabel.Font = new Font("Microsoft YaHei UI", 9);
        _selectedPhotoLabel.ForeColor = Color.FromArgb(96, 108, 124);
        _selectedPhotoLabel.Location = new Point(16, 472);
        _selectedPhotoLabel.Size = new Size(220, 22);

        ConfigureRotateButton(_rotateLeftButton, "左转90°", 16, 500, () => RotateSelectedPhoto(-90));
        ConfigureRotateButton(_rotateRightButton, "右转90°", 128, 500, () => RotateSelectedPhoto(90));

        var addButton = MakeButton("添加照片", 552, false);
        addButton.Click += (_, _) => PickPhotos();

        var clearButton = MakeButton("清空照片", 602, false);
        clearButton.Click += (_, _) => ClearPhotos();

        var jpgButton = MakeButton("导出 JPG", 672, true);
        jpgButton.Height = 42;
        jpgButton.Click += (_, _) => ExportLayout("jpg");

        var pngButton = MakeButton("导出 PNG", 724, false);
        pngButton.Click += (_, _) => ExportLayout("png");

        var hint = new Label
        {
            Text = "点击预览中的单张照片后，可以单独旋转。自动模式下，少量照片会减少列数，例如 12 张排成 4 列 x 3 行。",
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.FromArgb(96, 108, 124),
            Location = new Point(16, 796),
            Size = new Size(220, 82),
        };

        panel.Controls.AddRange(new Control[]
        {
            title, _countLabel, _gridLabel, _previousPageButton, _pageLabel, _nextPageButton,
            numberLabel, _numberBox,
            layoutLabel, _autoGridCheckBox, gapLabel, _gapSlider, _gapInput,
            colsLabel, _colsInput, rowsLabel, _rowsInput,
            rotateLabel, _selectedPhotoLabel, _rotateLeftButton, _rotateRightButton,
            addButton, clearButton, jpgButton, pngButton, hint,
        });
        UpdateGridControlState();
        UpdateSelectedPhotoState();
        return panel;
    }

    private void ConfigurePageControls()
    {
        _previousPageButton.Text = "上一版";
        _previousPageButton.Font = new Font("Microsoft YaHei UI", 9);
        _previousPageButton.Location = new Point(16, 112);
        _previousPageButton.Size = new Size(68, 30);
        _previousPageButton.Click += (_, _) => ChangePage(-1);

        _pageLabel.Font = new Font("Microsoft YaHei UI", 9);
        _pageLabel.ForeColor = Color.FromArgb(96, 108, 124);
        _pageLabel.TextAlign = ContentAlignment.MiddleCenter;
        _pageLabel.Location = new Point(86, 112);
        _pageLabel.Size = new Size(64, 30);

        _nextPageButton.Text = "下一版";
        _nextPageButton.Font = new Font("Microsoft YaHei UI", 9);
        _nextPageButton.Location = new Point(152, 112);
        _nextPageButton.Size = new Size(68, 30);
        _nextPageButton.Click += (_, _) => ChangePage(1);
    }

    private static Label MakeSmallLabel(string text, int y)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Microsoft YaHei UI", 10),
            ForeColor = Color.FromArgb(96, 108, 124),
            Location = new Point(16, y),
            Size = new Size(92, 28),
            TextAlign = ContentAlignment.MiddleLeft,
        };
    }

    private static void ConfigureNumberInput(NumericUpDown input, int y, int min, int max, int value)
    {
        input.Font = new Font("Microsoft YaHei UI", 11);
        input.Minimum = min;
        input.Maximum = max;
        input.Value = value;
        input.Location = new Point(120, y);
        input.Size = new Size(116, 30);
    }

    private void ConfigureGapControls(int y)
    {
        _gapSlider.Minimum = 0;
        _gapSlider.Maximum = 40;
        _gapSlider.TickFrequency = 10;
        _gapSlider.Value = 2;
        _gapSlider.Location = new Point(16, y + 30);
        _gapSlider.Size = new Size(154, 36);
        _gapSlider.ValueChanged += (_, _) =>
        {
            if (_syncingGapControls) return;
            if ((int)_gapInput.Value != _gapSlider.Value)
            {
                try
                {
                    _syncingGapControls = true;
                    _gapInput.Value = _gapSlider.Value;
                }
                finally
                {
                    _syncingGapControls = false;
                }
            }
            RequestLayoutRefresh();
        };

        _gapInput.Font = new Font("Microsoft YaHei UI", 10);
        _gapInput.Minimum = 0;
        _gapInput.Maximum = 40;
        _gapInput.Value = 2;
        _gapInput.Location = new Point(176, y + 32);
        _gapInput.Size = new Size(60, 28);
        _gapInput.ValueChanged += (_, _) =>
        {
            if (_syncingGapControls) return;
            if (_gapSlider.Value != (int)_gapInput.Value)
            {
                try
                {
                    _syncingGapControls = true;
                    _gapSlider.Value = (int)_gapInput.Value;
                }
                finally
                {
                    _syncingGapControls = false;
                }
            }
            RequestLayoutRefresh();
        };
    }

    private Button MakeButton(string text, int y, bool primary)
    {
        var button = new Button
        {
            Text = text,
            Font = new Font("Microsoft YaHei UI", 11, primary ? FontStyle.Bold : FontStyle.Regular),
            Location = new Point(16, y),
            Size = new Size(220, 38),
            BackColor = primary ? Color.FromArgb(15, 118, 110) : SystemColors.Control,
            ForeColor = primary ? Color.White : Color.FromArgb(29, 35, 43),
            FlatStyle = primary ? FlatStyle.Flat : FlatStyle.Standard,
        };
        if (primary)
        {
            button.FlatAppearance.BorderSize = 0;
        }
        return button;
    }

    private static void ConfigureRotateButton(Button button, string text, int x, int y, Action onClick)
    {
        button.Text = text;
        button.Font = new Font("Microsoft YaHei UI", 10);
        button.Location = new Point(x, y);
        button.Size = new Size(108, 34);
        button.Click += (_, _) => onClick();
    }

    private Control BuildWorkArea()
    {
        var work = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 0, 0, 0),
            BackColor = Color.FromArgb(246, 247, 249),
        };

        _preview.BackColor = Color.White;
        _preview.Location = new Point(18, 18);
        _preview.Size = new Size(900, 600);
        _preview.BorderStyle = BorderStyle.FixedSingle;
        _preview.AllowDrop = true;
        _preview.Paint += (_, e) =>
        {
            if (_previewBitmap is null) return;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_previewBitmap, 0, 0, _preview.Width, _preview.Height);
        };
        _preview.DragEnter += (_, e) =>
        {
            e.Effect = GetImportBatch(e.Data).Paths.Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        };
        _preview.DragDrop += (_, e) => AddPhotos(GetImportBatch(e.Data));
        _preview.MouseClick += (_, e) => SelectPhotoAt(e.Location);

        var dropLabel = new Label
        {
            Text = "把照片直接拖到上方预览区域",
            Font = new Font("Microsoft YaHei UI", 11),
            ForeColor = Color.FromArgb(96, 108, 124),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        work.Controls.Add(_preview);
        work.Controls.Add(dropLabel);
        work.Resize += (_, _) =>
        {
            var availableW = Math.Max(480, work.ClientSize.Width - 36);
            var availableH = Math.Max(320, work.ClientSize.Height - 100);
            var targetW = Math.Min(availableW, (int)(availableH * 1.5));
            var targetH = (int)(targetW / 1.5);
            if (targetH > availableH)
            {
                targetH = availableH;
                targetW = (int)(targetH * 1.5);
            }
            _preview.Size = new Size(targetW, targetH);
            _preview.Location = new Point(18 + (availableW - targetW) / 2, 18);
            dropLabel.Location = new Point(_preview.Left, _preview.Bottom + 14);
            dropLabel.Size = new Size(_preview.Width, 28);
        };
        return work;
    }

    private static ImportBatch GetImportBatch(IDataObject? data)
    {
        var paths = new List<string>();
        string? folderNumber = null;
        if (data is null || !data.GetDataPresent(DataFormats.FileDrop)) return new ImportBatch(paths, folderNumber);
        if (data.GetData(DataFormats.FileDrop) is not string[] files) return new ImportBatch(paths, folderNumber);

        foreach (var item in files)
        {
            if (Directory.Exists(item))
            {
                folderNumber ??= ExtractFiveDigitNumber(Path.GetFileName(item));
                paths.AddRange(EnumeratePhotoFiles(item));
            }
            else if (IsSupportedPhotoFile(item))
            {
                paths.Add(item);
            }
        }

        return new ImportBatch(
            paths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            folderNumber
        );
    }

    private static IEnumerable<string> EnumeratePhotoFiles(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                .Where(IsSupportedPhotoFile)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsSupportedPhotoFile(string path)
    {
        if (!File.Exists(path)) return false;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return new[]
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif",
            ".tif", ".tiff", ".heic", ".heif", ".hif"
        }.Contains(ext);
    }

    private static string? ExtractFiveDigitNumber(string value)
    {
        var match = System.Text.RegularExpressions.Regex.Match(value, @"\d{5}");
        return match.Success ? match.Value : null;
    }

    private void PickPhotos()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.heic;*.heif;*.hif|所有文件|*.*",
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddPhotos(new ImportBatch(dialog.FileNames.ToList(), null));
        }
    }

    private void AddPhotos(ImportBatch batch)
    {
        if (!string.IsNullOrWhiteSpace(batch.FolderNumber))
        {
            _numberBox.Text = batch.FolderNumber;
        }

        var loadedPhotos = new PhotoItem?[batch.Paths.Count];
        Parallel.For(
            0,
            batch.Paths.Count,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount) },
            index =>
            {
                var path = batch.Paths[index];
                try
                {
                    var preview = TryGetWindowsThumbnail(path, 1024, 1024);
                    if (preview is null)
                    {
                        using var source = Image.FromFile(path);
                        preview = CreatePreviewImage(source);
                    }
                    loadedPhotos[index] = new PhotoItem(path, preview);
                }
                catch
                {
                    loadedPhotos[index] = null;
                }
            });

        var failed = 0;
        foreach (var photo in loadedPhotos)
        {
            if (photo is null)
            {
                failed++;
                continue;
            }

            _photos.Add(photo);
            if (_selectedPhotoIndex < 0)
            {
                _selectedPhotoIndex = _photos.Count - 1;
            }
        }
        if (failed > 0)
        {
            MessageBox.Show(
                this,
                $"有 {failed} 个文件无法读取。HEIC/HEIF 需要 Windows 安装 HEIF 图像扩展；Live Photo 会使用其中的静态照片文件。",
                "部分文件未导入",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }
        RefreshLayout();
    }

    private void ClearPhotos()
    {
        foreach (var photo in _photos) photo.PreviewImage.Dispose();
        _photos.Clear();
        _selectedPhotoIndex = -1;
        _currentPage = 0;
        RefreshLayout();
    }

    private void RequestLayoutRefresh()
    {
        _layoutRefreshTimer.Stop();
        _layoutRefreshTimer.Start();
    }

    private void RefreshLayout()
    {
        var pageCount = GetPageCount();
        _currentPage = Math.Clamp(_currentPage, 0, pageCount - 1);
        _previewBitmap?.Dispose();
        _previewBitmap = RenderLayout(900, 600, true, _currentPage);
        _countLabel.Text = $"已添加 {_photos.Count} 张，每版最多 {MaxPhotosPerPage} 张";
        var pagePhotoCount = GetPagePhotoCount(_currentPage);
        var grid = GetCurrentGridSize(pagePhotoCount);
        if (_autoGridCheckBox.Checked)
        {
            SyncGridInputs(grid);
        }
        UpdateSelectedPhotoState();
        _gridLabel.Text = $"当前排版：{grid.Cols} 列 x {grid.Rows} 行，本版 {pagePhotoCount} 张";
        _pageLabel.Text = $"{_currentPage + 1} / {pageCount}";
        _previousPageButton.Enabled = _currentPage > 0;
        _nextPageButton.Enabled = _currentPage + 1 < pageCount;
        _preview.Invalidate();
    }

    private void ChangePage(int offset)
    {
        var nextPage = Math.Clamp(_currentPage + offset, 0, GetPageCount() - 1);
        if (nextPage == _currentPage) return;
        _currentPage = nextPage;
        _selectedPhotoIndex = -1;
        RefreshLayout();
    }

    private void UpdateSelectedPhotoState()
    {
        var hasSelection = _selectedPhotoIndex >= 0 && _selectedPhotoIndex < _photos.Count;
        _selectedPhotoLabel.Text = hasSelection
            ? $"已选择第 {_selectedPhotoIndex + 1} 张，旋转 {_photos[_selectedPhotoIndex].Rotation}°"
            : "点击预览中的照片进行选择";
        _rotateLeftButton.Enabled = hasSelection;
        _rotateRightButton.Enabled = hasSelection;
    }

    private void SelectPhotoAt(Point point)
    {
        var canvasPoint = new PointF(
            point.X * (CanvasW / (float)Math.Max(1, _preview.Width)),
            point.Y * (CanvasH / (float)Math.Max(1, _preview.Height))
        );

        for (var i = _photoBoxes.Count - 1; i >= 0; i--)
        {
            if (_photoBoxes[i].Bounds.Contains(canvasPoint))
            {
                _selectedPhotoIndex = _photoBoxes[i].Index;
                RefreshLayout();
                return;
            }
        }

        _selectedPhotoIndex = -1;
        RefreshLayout();
    }

    private void RotateSelectedPhoto(int degrees)
    {
        if (_selectedPhotoIndex < 0 || _selectedPhotoIndex >= _photos.Count) return;
        var photo = _photos[_selectedPhotoIndex];
        photo.Rotation = NormalizeRotation(photo.Rotation + degrees);
        RefreshLayout();
    }

    private void RefreshManualGrid()
    {
        if (_syncingGridControls || _autoGridCheckBox.Checked) return;
        RequestLayoutRefresh();
    }

    private void UpdateGridControlState()
    {
        _colsInput.Enabled = !_autoGridCheckBox.Checked;
        _rowsInput.Enabled = !_autoGridCheckBox.Checked;
    }

    private void SyncGridInputs(GridSize grid)
    {
        _syncingGridControls = true;
        try
        {
            _colsInput.Value = grid.Cols;
            _rowsInput.Value = grid.Rows;
        }
        finally
        {
            _syncingGridControls = false;
        }
    }

    private GridSize GetCurrentGridSize(int pagePhotoCount)
    {
        return _autoGridCheckBox.Checked
            ? GetAutoGridSize(pagePhotoCount, GetGap())
            : new GridSize((int)_colsInput.Value, (int)_rowsInput.Value);
    }

    private int GetPageCapacity()
    {
        if (_autoGridCheckBox.Checked) return MaxPhotosPerPage;
        return Math.Min(MaxPhotosPerPage, (int)(_colsInput.Value * _rowsInput.Value));
    }

    private int GetPageCount()
    {
        return Math.Max(1, (int)Math.Ceiling(_photos.Count / (double)GetPageCapacity()));
    }

    private int GetPagePhotoCount(int pageIndex)
    {
        var start = pageIndex * GetPageCapacity();
        return Math.Clamp(_photos.Count - start, 0, GetPageCapacity());
    }

    private int GetGap()
    {
        return (int)_gapInput.Value;
    }

    private Bitmap RenderLayout(int width, int height, bool usePreviewImages, int pageIndex)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;

        var scaleX = width / (float)CanvasW;
        var scaleY = height / (float)CanvasH;
        if (usePreviewImages)
        {
            _photoBoxes.Clear();
        }

        if (_photos.Count == 0)
        {
            DrawEmptyMessage(graphics, width, height);
            DrawStudioLogo(graphics, scaleX, scaleY);
            return bitmap;
        }

        var pageCapacity = GetPageCapacity();
        var pageStart = pageIndex * pageCapacity;
        var pagePhotoCount = Math.Clamp(_photos.Count - pageStart, 0, pageCapacity);
        var grid = GetCurrentGridSize(pagePhotoCount);
        const float left = 50;
        const float top = 40f;
        const float areaW = 1700;
        const float areaH = 1120;
        var gap = (float)GetGap();
        var maxCellW = (areaW - gap * (grid.Cols - 1)) / grid.Cols;
        var maxCellH = (areaH - gap * (grid.Rows - 1)) / grid.Rows;
        var cellW = maxCellW;
        var cellH = maxCellH;
        var contentW = cellW * grid.Cols + gap * (grid.Cols - 1);
        var gridLeft = left + (areaW - contentW) / 2f;

        var visibleCount = Math.Min(pagePhotoCount, grid.Cols * grid.Rows);
        for (var i = 0; i < visibleCount; i++)
        {
            var row = i / grid.Cols;
            var col = i % grid.Cols;
            var box = new RectangleF(gridLeft + col * (cellW + gap), top + row * (cellH + gap), cellW, cellH);
            var photoIndex = pageStart + i;
            if (usePreviewImages)
            {
                _photoBoxes.Add(new PhotoBox(photoIndex, box));
            }
            DrawPhoto(graphics, _photos[photoIndex], box, scaleX, scaleY, usePreviewImages);
            if (usePreviewImages && photoIndex == _selectedPhotoIndex)
            {
                DrawSelection(graphics, box, scaleX, scaleY);
            }
        }

        DrawStudioLogoAndNumber(graphics, scaleX, scaleY);
        return bitmap;
    }

    private static GridSize GetAutoGridSize(int count, int gap)
    {
        if (count <= 0) return new GridSize(7, 6);
        const int maxCols = 7;
        const int maxRows = 6;
        const double areaW = 1700;
        const double areaH = 1120;
        GridSize best = new(7, 6);
        var bestScore = double.NegativeInfinity;

        for (var cols = 1; cols <= maxCols; cols++)
        {
            var rows = (int)Math.Ceiling(count / (double)cols);
            if (rows > maxRows) continue;
            var thumbW = (areaW - gap * (cols - 1)) / cols;
            var thumbH = (areaH - gap * (rows - 1)) / rows;
            var idealW = Math.Min(thumbW, thumbH * 1.5);
            var emptySlots = cols * rows - count;
            var balancePenalty = Math.Abs(cols / (double)rows - 1.35);
            var score = idealW - emptySlots * 120 - balancePenalty * 35;
            if (score > bestScore)
            {
                bestScore = score;
                best = new GridSize(cols, rows);
            }
        }
        return best;
    }

    private static void DrawImageContained(Graphics graphics, Image image, RectangleF box, float scaleX, float scaleY)
    {
        var imageRatio = image.Width / (float)image.Height;
        var boxRatio = box.Width / box.Height;
        float drawW;
        float drawH;
        if (imageRatio > boxRatio)
        {
            drawW = box.Width;
            drawH = box.Width / imageRatio;
        }
        else
        {
            drawH = box.Height;
            drawW = box.Height * imageRatio;
        }

        var drawX = box.X + (box.Width - drawW) / 2f;
        var drawY = box.Y + (box.Height - drawH) / 2f;
        var dest = new RectangleF(drawX * scaleX, drawY * scaleY, drawW * scaleX, drawH * scaleY);
        graphics.DrawImage(image, dest);
    }

    private static void DrawPhoto(Graphics graphics, PhotoItem photo, RectangleF box, float scaleX, float scaleY, bool usePreviewImage)
    {
        DrawRotatedImageContained(graphics, photo.PreviewImage, photo.Rotation, box, scaleX, scaleY);
    }

    private static void DrawRotatedImageContained(Graphics graphics, Image image, int rotation, RectangleF box, float scaleX, float scaleY)
    {
        var normalized = NormalizeRotation(rotation);
        if (normalized == 0)
        {
            DrawImageContained(graphics, image, box, scaleX, scaleY);
            return;
        }

        using var rotated = CreateRotatedImage(image, normalized);
        DrawImageContained(graphics, rotated, box, scaleX, scaleY);
    }

    private static Bitmap CreateRotatedImage(Image source, int rotation)
    {
        var bitmap = new Bitmap(source);
        switch (NormalizeRotation(rotation))
        {
            case 90:
                bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
                break;
            case 180:
                bitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
                break;
            case 270:
                bitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
                break;
        }
        return bitmap;
    }

    private static int NormalizeRotation(int rotation)
    {
        var normalized = rotation % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static void DrawSelection(Graphics graphics, RectangleF box, float scaleX, float scaleY)
    {
        using var pen = new Pen(Color.FromArgb(240, 126, 34), Math.Max(2f, 5f * scaleX));
        using var innerPen = new Pen(Color.FromArgb(255, 255, 255), Math.Max(1f, 2f * scaleX));
        var rect = new RectangleF(
            box.X * scaleX + 2,
            box.Y * scaleY + 2,
            box.Width * scaleX - 4,
            box.Height * scaleY - 4
        );
        graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        graphics.DrawRectangle(innerPen, rect.X + 5, rect.Y + 5, Math.Max(1, rect.Width - 10), Math.Max(1, rect.Height - 10));
    }

    private static Bitmap CreatePreviewImage(Image source)
    {
        const int maxSide = 720;
        var scale = Math.Min(1f, maxSide / (float)Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, 0, 0, width, height);
        return bitmap;
    }

    private static Bitmap? TryGetWindowsThumbnail(string path, int width, int height)
    {
        IShellItemImageFactory? factory = null;
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
            factory.GetImage(
                new NativeSize(width, height),
                ShellItemImageFactoryFlags.ThumbnailOnly | ShellItemImageFactoryFlags.BiggerSizeOk,
                out hBitmap);
            if (hBitmap == IntPtr.Zero) return null;
            using var shellBitmap = Image.FromHbitmap(hBitmap);
            return new Bitmap(shellBitmap);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
            if (factory is not null && Marshal.IsComObject(factory)) Marshal.FinalReleaseComObject(factory);
        }
    }

    private void DrawStudioLogo(Graphics graphics, float scaleX, float scaleY)
    {
        DrawStudioLogoAndNumber(graphics, scaleX, scaleY);
    }

    private void DrawStudioLogoAndNumber(Graphics graphics, float scaleX, float scaleY)
    {
        if (_studioLogo is null) return;
        const float right = 50f;
        const float bottom = 40f;
        const float logoW = 195f;
        const float numberH = 34f;
        var logoH = logoW * _studioLogo.Height / _studioLogo.Width;
        var logoX = CanvasW - right - logoW;
        var logoY = CanvasH - bottom - numberH - 6f - logoH;
        graphics.DrawImage(_studioLogo, logoX * scaleX, logoY * scaleY, logoW * scaleX, logoH * scaleY);

        var number = _numberBox.Text.Trim();
        if (number.Length == 0) return;
        using var brush = new SolidBrush(Color.FromArgb(40, 48, 58));
        using var font = new Font("Arial", 24 * scaleY, FontStyle.Bold);
        using var format = new StringFormat { Alignment = StringAlignment.Far };
        var numberBox = new RectangleF(
            logoX * scaleX,
            (logoY + logoH + 6f) * scaleY,
            logoW * scaleX,
            numberH * scaleY);
        graphics.DrawString($"No.{number}", font, brush, numberBox, format);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory imageFactory);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr objectHandle);

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage(NativeSize size, ShellItemImageFactoryFlags flags, out IntPtr hBitmap);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize
    {
        public NativeSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }
    }

    [Flags]
    private enum ShellItemImageFactoryFlags
    {
        BiggerSizeOk = 0x1,
        ThumbnailOnly = 0x8,
    }

    private static void DrawEmptyMessage(Graphics graphics, int width, int height)
    {
        using var brush = new SolidBrush(Color.FromArgb(100, 112, 128));
        using var font = new Font("Microsoft YaHei UI", 22);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString("拖入照片生成 6 寸缩略图排版", font, brush, new RectangleF(0, 0, width, height), format);
    }

    private void ExportLayout(string formatName)
    {
        if (_photos.Count == 0)
        {
            MessageBox.Show(this, "请先拖入照片。", "无法导出", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var date = DateTime.Now.ToString("yyyyMMdd");
        var prefix = _numberBox.Text.Trim();
        if (prefix.Length == 0) prefix = "照片排版";
        else prefix = $"No.{prefix}";

        using var dialog = new SaveFileDialog
        {
            Filter = formatName == "jpg" ? "JPG 图片|*.jpg" : "PNG 图片|*.png",
            FileName = $"{prefix}_{date}.{formatName}",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var pageCount = GetPageCount();
            var directory = Path.GetDirectoryName(dialog.FileName) ?? Environment.CurrentDirectory;
            var baseName = Path.GetFileNameWithoutExtension(dialog.FileName);
            var extension = Path.GetExtension(dialog.FileName);
            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                var outputPath = pageCount == 1
                    ? dialog.FileName
                    : Path.Combine(directory, $"{baseName}_第{pageIndex + 1}版{extension}");
                using var bitmap = RenderLayout(CanvasW, CanvasH, false, pageIndex);
                if (formatName == "jpg")
                {
                    SaveJpeg(bitmap, outputPath, 94L);
                }
                else
                {
                    bitmap.Save(outputPath, ImageFormat.Png);
                }
            }
            var message = pageCount == 1
                ? $"已保存：\n{dialog.FileName}"
                : $"已保存 {pageCount} 个版面：\n{directory}";
            MessageBox.Show(this, message, "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"导出失败：\n{ex.Message}", "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void SaveJpeg(Bitmap bitmap, string path, long quality)
    {
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        bitmap.Save(path, codec, parameters);
    }

    private static Icon? LoadAppIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "app-icon.ico");
            return File.Exists(iconPath) ? new Icon(iconPath) : null;
        }
        catch
        {
            return null;
        }
    }

    private static Image? LoadEmbeddedImage(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null) return null;
        var stream = assembly.GetManifestResourceStream(resourceName);
        return stream is null ? null : Image.FromStream(stream);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _numberRefreshTimer.Dispose();
            _layoutRefreshTimer.Dispose();
            foreach (var photo in _photos) photo.PreviewImage.Dispose();
            _previewBitmap?.Dispose();
            _studioLogo?.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class PreviewPanel : Panel
    {
        public PreviewPanel()
        {
            DoubleBuffered = true;
        }
    }

    private sealed class PhotoItem
    {
        public PhotoItem(string path, Bitmap previewImage)
        {
            Path = path;
            PreviewImage = previewImage;
        }

        public string Path { get; }
        public Bitmap PreviewImage { get; }
        public int Rotation { get; set; }
    }

    private sealed record PhotoBox(int Index, RectangleF Bounds);
    private sealed record ImportBatch(List<string> Paths, string? FolderNumber);
    private sealed record GridSize(int Cols, int Rows);
}
