using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using EthercatEsi.Core;
using Microsoft.Win32;

namespace EthercatEsi.Wpf;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly EsiParser _parser = new();
    private EsiDocument? _selectedDocument;
    private DeviceInfo? _selectedDevice;
    private EsiNode? _selectedNode;
    private string _statusText = "请选择 EtherCAT ESI XML 文件。";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<EsiDocument> Documents { get; } = new();

    public EsiDocument? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (Equals(_selectedDocument, value))
            {
                return;
            }

            _selectedDocument = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedTreeRoots));
            OnPropertyChanged(nameof(DocumentSummary));

            SelectedDevice = value?.Devices.FirstOrDefault();
            SelectedNode = value?.NodeTree;
        }
    }

    public DeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (Equals(_selectedDevice, value))
            {
                return;
            }

            _selectedDevice = value;
            OnPropertyChanged();
        }
    }

    public EsiNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (Equals(_selectedNode, value))
            {
                return;
            }

            _selectedNode = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<EsiNode> SelectedTreeRoots =>
        SelectedDocument is null ? Array.Empty<EsiNode>() : new[] { SelectedDocument.NodeTree };

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
            OnPropertyChanged();
        }
    }

    public string DocumentSummary
    {
        get
        {
            if (SelectedDocument is null)
            {
                return "尚未加载 XML。";
            }

            var totalObjects = SelectedDocument.Devices.Sum(device => device.Objects.Count);
            var totalRxPdos = SelectedDocument.Devices.Sum(device => device.RxPdos.Count);
            var totalTxPdos = SelectedDocument.Devices.Sum(device => device.TxPdos.Count);
            var totalSms = SelectedDocument.Devices.Sum(device => device.SyncManagers.Count);

            return
                $"文件：{SelectedDocument.FileName}{Environment.NewLine}" +
                $"ESI Version：{SelectedDocument.Version}{Environment.NewLine}" +
                $"供应商：{SelectedDocument.Vendor.Name} ({SelectedDocument.Vendor.Id}){Environment.NewLine}" +
                $"设备数量：{SelectedDocument.Devices.Count}{Environment.NewLine}" +
                $"对象字典条目：{totalObjects}{Environment.NewLine}" +
                $"RxPDO / TxPDO：{totalRxPdos} / {totalTxPdos}{Environment.NewLine}" +
                $"Sync Manager：{totalSms}";
        }
        set
        {
            //if (Equals(_documentSummary, value))
            //{
            //    return;
            //}

            //_selectedDevice = value;
            OnPropertyChanged(nameof(DocumentSummary));
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSamples();
    }

    private void OpenFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 EtherCAT ESI XML 文件",
            Filter = "EtherCAT ESI XML (*.xml)|*.xml|All files (*.*)|*.*",
            Multiselect = true
        };

        if (Directory.Exists(GetSamplesDirectory()))
        {
            dialog.InitialDirectory = GetSamplesDirectory();
        }

        if (dialog.ShowDialog(this) == true)
        {
            LoadFiles(dialog.FileNames);
        }
    }

    private void LoadSamples_Click(object sender, RoutedEventArgs e)
    {
        LoadSamples();
    }

    private void NodeTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is EsiNode node)
        {
            SelectedNode = node;
        }
    }

    private void LoadSamples()
    {
        var samplesDirectory = GetSamplesDirectory();
        if (!Directory.Exists(samplesDirectory))
        {
            StatusText = $"未找到示例目录：{samplesDirectory}";
            return;
        }

        var sampleFiles = Directory.EnumerateFiles(samplesDirectory, "*.xml")
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        LoadFiles(sampleFiles);
    }

    private void LoadFiles(IEnumerable<string> filePaths)
    {
        var loaded = 0;
        var errors = new List<string>();

        foreach (var filePath in filePaths)
        {
            try
            {
                var document = _parser.Parse(filePath);
                var existing = Documents.FirstOrDefault(item =>
                    string.Equals(item.SourcePath, document.SourcePath, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    Documents.Remove(existing);
                }

                Documents.Add(document);
                SelectedDocument = document;
                loaded++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(filePath)}：{ex.Message}");
            }
        }

        StatusText = errors.Count == 0
            ? $"已加载 {loaded} 个 XML 文件。"
            : $"已加载 {loaded} 个 XML 文件，失败 {errors.Count} 个：{string.Join("; ", errors)}";

        OnPropertyChanged(nameof(DocumentSummary));
    }

    private static string GetSamplesDirectory()
    {
        var outputSamples = Path.Combine(AppContext.BaseDirectory, "Samples");
        if (Directory.Exists(outputSamples))
        {
            return outputSamples;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Samples"));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
