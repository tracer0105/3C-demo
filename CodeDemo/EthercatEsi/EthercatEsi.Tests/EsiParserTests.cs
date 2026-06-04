using EthercatEsi.Core;

namespace EthercatEsi.Tests;

public sealed class EsiParserTests
{
    private static readonly string SamplesDirectory =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Samples"));

    [Fact]
    public void Parse_reads_vendor_and_all_devices_from_each_sample()
    {
        var parser = new EsiParser();

        var servotronix = parser.Parse(Path.Combine(SamplesDirectory, "Servotronix_CDHD2.xml"));
        var xinje = parser.Parse(Path.Combine(SamplesDirectory, "XINJE-DS5C2-ECT.xml"));
        var yako = parser.Parse(Path.Combine(SamplesDirectory, "YAKO_MS_ECAT_V2.4.xml"));

        Assert.Equal("Servotronix Motion Control Ltd.", servotronix.Vendor.Name);
        Assert.Equal("#x2E1", servotronix.Vendor.Id);
        Assert.Single(servotronix.Devices);
        Assert.Equal("CD02 EtherCAT Drive (CoE)", servotronix.Devices[0].Name);
        Assert.Equal(540, servotronix.Devices[0].Objects.Count);

        Assert.Equal("Xinje Electronics, Inc.", xinje.Vendor.Name);
        Assert.Single(xinje.Devices);
        Assert.Equal("XINJE-DS5C2 EtherCAT(CoE) Drive Rev5.0 v4.2.00", xinje.Devices[0].Name);
        Assert.Equal(653, xinje.Devices[0].Objects.Count);

        Assert.Equal("Shenzhen YAKO Automation Technology Co.,Ltd", yako.Vendor.Name);
        Assert.Equal(12, yako.Devices.Count);
        Assert.Equal(1570, yako.Devices.Sum(device => device.Objects.Count));
    }

    [Fact]
    public void Parse_extracts_sync_managers_pdos_and_pdo_entries()
    {
        var parser = new EsiParser();

        var document = parser.Parse(Path.Combine(SamplesDirectory, "XINJE-DS5C2-ECT.xml"));
        var device = document.Devices[0];

        Assert.Equal(4, device.SyncManagers.Count);
        Assert.Equal(4, device.RxPdos.Count);
        Assert.Equal(4, device.TxPdos.Count);

        var firstRxPdo = device.RxPdos[0];
        Assert.Equal("0x1600", firstRxPdo.Index);
        Assert.Equal("1st RxPDO Mapping", firstRxPdo.Name);
        Assert.Equal("2", firstRxPdo.SyncManager);
        Assert.Equal(5, firstRxPdo.Entries.Count);
        Assert.Contains(firstRxPdo.Entries, entry => entry.Index == "0x6040" && entry.BitLength == 16);
        Assert.Contains(firstRxPdo.Entries, entry => entry.Index == "0x607A" && entry.DataType == "DINT");
    }

    [Fact]
    public void Parse_adds_chinese_meanings_for_esi_nodes_and_standard_cia402_objects()
    {
        var parser = new EsiParser();

        var document = parser.Parse(Path.Combine(SamplesDirectory, "YAKO_MS_ECAT_V2.4.xml"));
        var device = document.Devices[0];

        Assert.Contains("ESI", document.NodeTree.Meaning);
        Assert.Contains("供应商", document.NodeTree.Children[0].Meaning);

        var controlWord = device.Objects.Single(item => item.Index == "0x6040");
        Assert.Contains("控制字", controlWord.Meaning);

        var modeOfOperation = device.Objects.Single(item => item.Index == "0x6060");
        Assert.Contains("运行模式", modeOfOperation.Meaning);

        var targetPosition = device.RxPdos.SelectMany(pdo => pdo.Entries).First(entry => entry.Index == "0x607A");
        Assert.Contains("目标位置", targetPosition.Meaning);
    }
}
