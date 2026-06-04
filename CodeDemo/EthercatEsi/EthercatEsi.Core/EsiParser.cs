using System.Xml.Linq;

namespace EthercatEsi.Core;

public sealed class EsiParser
{
    public EsiDocument Parse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("ESI XML path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("ESI XML file was not found.", filePath);
        }

        var document = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidDataException("The XML document does not have a root element.");

        if (!string.Equals(root.Name.LocalName, "EtherCATInfo", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The XML document is not an EtherCATInfo ESI file.");
        }

        var vendorElement = Child(root, "Vendor");
        var descriptionsElement = Child(root, "Descriptions");
        var devicesElement = descriptionsElement is null ? null : Child(descriptionsElement, "Devices");

        var vendor = ParseVendor(vendorElement);
        var devices = devicesElement is null
            ? Array.Empty<DeviceInfo>()
            : Children(devicesElement, "Device").Select(ParseDevice).ToArray();

        return new EsiDocument(
            filePath,
            AttributeValue(root, "Version"),
            vendor,
            devices,
            BuildNode(root));
    }

    private static VendorInfo ParseVendor(XElement? vendorElement)
    {
        if (vendorElement is null)
        {
            return new VendorInfo(string.Empty, string.Empty);
        }

        return new VendorInfo(
            TextOf(vendorElement, "Id"),
            EsiValueParser.NormalizeText(TextOf(vendorElement, "Name")));
    }

    private static DeviceInfo ParseDevice(XElement deviceElement)
    {
        var typeElement = Child(deviceElement, "Type");
        var profileElement = Child(deviceElement, "Profile");
        var dictionaryElement = profileElement is null ? null : Child(profileElement, "Dictionary");
        var dataTypesElement = dictionaryElement is null ? null : Child(dictionaryElement, "DataTypes");
        var objectsElement = dictionaryElement is null ? null : Child(dictionaryElement, "Objects");

        var dataTypes = dataTypesElement is null
            ? Array.Empty<DataTypeInfo>()
            : Children(dataTypesElement, "DataType").Select(ParseDataType).ToArray();

        var objects = objectsElement is null
            ? Array.Empty<ObjectEntryInfo>()
            : Children(objectsElement, "Object").Select(ParseObject).ToArray();

        var syncManagers = Children(deviceElement, "Sm")
            .Select((syncManager, index) => ParseSyncManager(syncManager, index))
            .ToArray();

        var fmmus = Children(deviceElement, "Fmmu")
            .Select(fmmu => EsiValueParser.NormalizeText(fmmu.Value))
            .Where(value => value.Length > 0)
            .ToArray();

        var rxPdos = Children(deviceElement, "RxPdo").Select(pdo => ParsePdo(pdo, "RxPDO")).ToArray();
        var txPdos = Children(deviceElement, "TxPdo").Select(pdo => ParsePdo(pdo, "TxPDO")).ToArray();

        return new DeviceInfo(
            EsiValueParser.NormalizeText(TextOf(deviceElement, "Name")),
            EsiValueParser.NormalizeText(typeElement?.Value),
            AttributeValue(typeElement, "ProductCode"),
            AttributeValue(typeElement, "RevisionNo"),
            AttributeValue(deviceElement, "Physics"),
            TextOf(deviceElement, "GroupType"),
            ResolveProfileNumber(profileElement),
            dataTypes,
            objects,
            syncManagers,
            fmmus,
            rxPdos,
            txPdos);
    }

    private static string ResolveProfileNumber(XElement? profileElement)
    {
        if (profileElement is null)
        {
            return string.Empty;
        }

        var directProfile = TextOf(profileElement, "ProfileNo");
        if (!string.IsNullOrWhiteSpace(directProfile))
        {
            return directProfile;
        }

        var channelInfo = Child(profileElement, "ChannelInfo");
        return channelInfo is null ? string.Empty : TextOf(channelInfo, "ProfileNo");
    }

    private static DataTypeInfo ParseDataType(XElement dataTypeElement)
    {
        var name = TextOf(dataTypeElement, "Name");
        var meaning = name.StartsWith("DT", StringComparison.OrdinalIgnoreCase)
            ? "复合数据类型，通常对应对象字典中同名对象的子索引结构。"
            : "基础数据类型，定义对象或 PDO 条目的位宽。";

        return new DataTypeInfo(
            name,
            TextOf(dataTypeElement, "BaseType"),
            EsiValueParser.ParseNullableInt(TextOf(dataTypeElement, "BitSize")),
            meaning,
            Children(dataTypeElement, "SubItem").Select(ParseSubItem).ToArray());
    }

    private static ObjectEntryInfo ParseObject(XElement objectElement)
    {
        var index = EsiValueParser.NormalizeHex(TextOf(objectElement, "Index"), minimumDigits: 4);
        var objectMeaning = EsiMeaningProvider.GetObjectMeaning(index);
        var type = TextOf(objectElement, "Type");
        var subItems = ObjectSubItems(objectElement).Select(ParseSubItem).ToArray();

        return new ObjectEntryInfo(
            index,
            TextOf(objectElement, "Name"),
            type,
            EsiValueParser.ParseNullableInt(TextOf(objectElement, "BitSize")),
            DescendantText(objectElement, "Flags", "Access"),
            FirstDescendantText(objectElement, "DefaultValue", "DefaultData"),
            EsiMeaningProvider.Combine(objectMeaning, TypeMeaning(type)),
            subItems);
    }

    private static IEnumerable<XElement> ObjectSubItems(XElement objectElement)
    {
        foreach (var directSubItem in Children(objectElement, "SubItem"))
        {
            yield return directSubItem;
        }

        var infoElement = Child(objectElement, "Info");
        if (infoElement is null)
        {
            yield break;
        }

        foreach (var infoSubItem in Children(infoElement, "SubItem"))
        {
            yield return infoSubItem;
        }
    }

    private static SubItemInfo ParseSubItem(XElement subItemElement)
    {
        var subIndex = TextOf(subItemElement, "SubIdx");
        if (string.IsNullOrWhiteSpace(subIndex))
        {
            subIndex = TextOf(subItemElement, "SubIndex");
        }

        return new SubItemInfo(
            EsiValueParser.NormalizeHex(subIndex),
            TextOf(subItemElement, "Name"),
            TextOf(subItemElement, "Type"),
            EsiValueParser.ParseNullableInt(TextOf(subItemElement, "BitSize")),
            DescendantText(subItemElement, "Flags", "Access"),
            FirstDescendantText(subItemElement, "DefaultValue", "DefaultData"),
            EsiMeaningProvider.GetElementMeaning("SubItem"));
    }

    private static SyncManagerInfo ParseSyncManager(XElement syncManagerElement, int index)
    {
        var usage = EsiValueParser.NormalizeText(syncManagerElement.Value);
        return new SyncManagerInfo(
            index,
            usage,
            AttributeValue(syncManagerElement, "StartAddress"),
            AttributeValue(syncManagerElement, "DefaultSize"),
            AttributeValue(syncManagerElement, "MinSize"),
            AttributeValue(syncManagerElement, "MaxSize"),
            AttributeValue(syncManagerElement, "ControlByte"),
            AttributeValue(syncManagerElement, "Enable"),
            EsiMeaningProvider.GetSyncManagerMeaning(usage));
    }

    private static PdoInfo ParsePdo(XElement pdoElement, string direction)
    {
        var pdoIndex = EsiValueParser.NormalizeHex(TextOf(pdoElement, "Index"), minimumDigits: 4);
        var pdoName = TextOf(pdoElement, "Name");
        var syncManager = AttributeValue(pdoElement, "Sm");
        var entries = Children(pdoElement, "Entry")
            .Select((entry, index) => ParsePdoEntry(entry, direction, pdoIndex, pdoName, syncManager, index + 1))
            .ToArray();

        return new PdoInfo(
            direction,
            pdoIndex,
            pdoName,
            syncManager,
            AttributeValue(pdoElement, "Fixed"),
            entries);
    }

    private static PdoEntryInfo ParsePdoEntry(
        XElement entryElement,
        string direction,
        string pdoIndex,
        string pdoName,
        string syncManager,
        int entryNumber)
    {
        var objectIndex = EsiValueParser.NormalizeHex(TextOf(entryElement, "Index"), minimumDigits: 4);
        return new PdoEntryInfo(
            direction,
            pdoIndex,
            pdoName,
            syncManager,
            entryNumber,
            objectIndex,
            EsiValueParser.NormalizeHex(TextOf(entryElement, "SubIndex")),
            EsiValueParser.ParseNullableInt(TextOf(entryElement, "BitLen")),
            TextOf(entryElement, "Name"),
            TextOf(entryElement, "DataType"),
            EsiMeaningProvider.GetObjectMeaning(objectIndex));
    }

    private static EsiNode BuildNode(XElement element)
    {
        var children = element.Elements().Select(BuildNode).ToArray();
        var elementName = element.Name.LocalName;
        return new EsiNode(
            BuildDisplayName(element),
            elementName,
            children.Length == 0 ? TrimLongValue(EsiValueParser.NormalizeText(element.Value)) : string.Empty,
            BuildMeaning(element),
            element.Attributes().ToDictionary(attribute => attribute.Name.LocalName, attribute => attribute.Value, StringComparer.OrdinalIgnoreCase),
            children);
    }

    private static string BuildDisplayName(XElement element)
    {
        var elementName = element.Name.LocalName;
        var index = EsiValueParser.NormalizeHex(TextOf(element, "Index"), minimumDigits: 4);
        var name = TextOf(element, "Name");

        return elementName switch
        {
            "EtherCATInfo" => $"EtherCATInfo {AttributeValue(element, "Version")}".TrimEnd(),
            "Vendor" => $"Vendor {TextOf(element, "Name")}".TrimEnd(),
            "Device" => $"Device {TextOf(element, "Name")}".TrimEnd(),
            "DataType" => $"DataType {TextOf(element, "Name")}".TrimEnd(),
            "Object" => $"Object {index} {name}".TrimEnd(),
            "Entry" => $"Entry {index}:{EsiValueParser.NormalizeHex(TextOf(element, "SubIndex"))} {name}".TrimEnd(),
            "RxPdo" => $"RxPDO {index} {name}".TrimEnd(),
            "TxPdo" => $"TxPDO {index} {name}".TrimEnd(),
            "Sm" => $"Sm {EsiValueParser.NormalizeText(element.Value)}".TrimEnd(),
            "Fmmu" => $"Fmmu {EsiValueParser.NormalizeText(element.Value)}".TrimEnd(),
            _ => element.Elements().Any()
                ? elementName
                : $"{elementName}: {TrimLongValue(EsiValueParser.NormalizeText(element.Value))}".TrimEnd()
        };
    }

    private static string BuildMeaning(XElement element)
    {
        var elementName = element.Name.LocalName;
        var tagMeaning = EsiMeaningProvider.GetElementMeaning(elementName);

        if (string.Equals(elementName, "Sm", StringComparison.OrdinalIgnoreCase))
        {
            return EsiMeaningProvider.Combine(tagMeaning, EsiMeaningProvider.GetSyncManagerMeaning(element.Value));
        }

        if (string.Equals(elementName, "Object", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(elementName, "Entry", StringComparison.OrdinalIgnoreCase))
        {
            return EsiMeaningProvider.Combine(tagMeaning, EsiMeaningProvider.GetObjectMeaning(TextOf(element, "Index")));
        }

        if (string.Equals(elementName, "Index", StringComparison.OrdinalIgnoreCase))
        {
            return EsiMeaningProvider.Combine(tagMeaning, EsiMeaningProvider.GetObjectMeaning(element.Value));
        }

        return tagMeaning;
    }

    private static string TypeMeaning(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return string.Empty;
        }

        return typeName.StartsWith("DT", StringComparison.OrdinalIgnoreCase)
            ? $"使用复合数据类型 {typeName}，子项定义可在 DataTypes 中查看。"
            : $"使用基础数据类型 {typeName}。";
    }

    private static string TrimLongValue(string value)
    {
        const int maxLength = 180;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    private static XElement? Child(XElement element, string localName) =>
        element.Elements().FirstOrDefault(child => string.Equals(child.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<XElement> Children(XElement element, string localName) =>
        element.Elements().Where(child => string.Equals(child.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

    private static string TextOf(XElement? element, string localName)
    {
        if (element is null)
        {
            return string.Empty;
        }

        var child = Child(element, localName);
        return child is null ? string.Empty : EsiValueParser.NormalizeText(child.Value);
    }

    private static string AttributeValue(XElement? element, string localName)
    {
        if (element is null)
        {
            return string.Empty;
        }

        var attribute = element.Attributes()
            .FirstOrDefault(item => string.Equals(item.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase));

        return attribute is null ? string.Empty : EsiValueParser.NormalizeText(attribute.Value);
    }

    private static string DescendantText(XElement element, string containerName, string childName)
    {
        var container = Child(element, containerName);
        return container is null ? string.Empty : TextOf(container, childName);
    }

    private static string FirstDescendantText(XElement element, params string[] localNames)
    {
        foreach (var descendant in element.Descendants())
        {
            if (localNames.Any(localName => string.Equals(descendant.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase)))
            {
                return EsiValueParser.NormalizeText(descendant.Value);
            }
        }

        return string.Empty;
    }
}
