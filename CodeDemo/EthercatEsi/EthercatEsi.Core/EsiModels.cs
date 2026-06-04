using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace EthercatEsi.Core;

public sealed record EsiDocument(
    string SourcePath,
    string Version,
    VendorInfo Vendor,
    IReadOnlyList<DeviceInfo> Devices,
    EsiNode NodeTree)
{
    public string FileName => Path.GetFileName(SourcePath);

    public string DisplayName => $"{FileName} ({Devices.Count} device{(Devices.Count == 1 ? string.Empty : "s")})";
}

public sealed record VendorInfo(string Id, string Name);

public sealed record DeviceInfo(
    string Name,
    string TypeName,
    string ProductCode,
    string RevisionNumber,
    string Physics,
    string GroupType,
    string ProfileNumber,
    IReadOnlyList<DataTypeInfo> DataTypes,
    IReadOnlyList<ObjectEntryInfo> Objects,
    IReadOnlyList<SyncManagerInfo> SyncManagers,
    IReadOnlyList<string> Fmmus,
    IReadOnlyList<PdoInfo> RxPdos,
    IReadOnlyList<PdoInfo> TxPdos)
{
    public string DisplayName
    {
        get
        {
            var product = string.IsNullOrWhiteSpace(ProductCode) ? "no product code" : ProductCode;
            var revision = string.IsNullOrWhiteSpace(RevisionNumber) ? "no revision" : RevisionNumber;
            return $"{Name} [{product}, {revision}]";
        }
    }

    public IReadOnlyList<PdoEntryInfo> PdoEntries =>
        RxPdos.Concat(TxPdos).SelectMany(pdo => pdo.Entries).ToArray();
}

public sealed record DataTypeInfo(
    string Name,
    string BaseType,
    int? BitSize,
    string Meaning,
    IReadOnlyList<SubItemInfo> SubItems);

public sealed record ObjectEntryInfo(
    string Index,
    string Name,
    string Type,
    int? BitSize,
    string Access,
    string DefaultValue,
    string Meaning,
    IReadOnlyList<SubItemInfo> SubItems);

public sealed record SubItemInfo(
    string SubIndex,
    string Name,
    string Type,
    int? BitSize,
    string Access,
    string DefaultValue,
    string Meaning);

public sealed record SyncManagerInfo(
    int Number,
    string Usage,
    string StartAddress,
    string DefaultSize,
    string MinSize,
    string MaxSize,
    string ControlByte,
    string Enable,
    string Meaning);

public sealed record PdoInfo(
    string Direction,
    string Index,
    string Name,
    string SyncManager,
    string Fixed,
    IReadOnlyList<PdoEntryInfo> Entries)
{
    public string DisplayName => $"{Direction} {Index} {Name}";
}

public sealed record PdoEntryInfo(
    string Direction,
    string PdoIndex,
    string PdoName,
    string SyncManager,
    int EntryNumber,
    string Index,
    string SubIndex,
    int? BitLength,
    string Name,
    string DataType,
    string Meaning);

public sealed record EsiNode(
    string DisplayName,
    string ElementName,
    string Value,
    string Meaning,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<EsiNode> Children)
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public string AttributesText
    {
        get => Attributes.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, Attributes.Select(attribute => $"{attribute.Key} = {attribute.Value}"));
        set
        {
            OnPropertyChanged();
        }
        
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
