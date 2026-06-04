namespace EthercatEsi.Core;

public static class EsiMeaningProvider
{
    private static readonly IReadOnlyDictionary<string, string> ElementMeanings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["EtherCATInfo"] = "EtherCAT Slave Information (ESI) 根节点，描述 EtherCAT 从站设备、对象字典、PDO、同步管理器和 EEPROM 信息。",
        ["Vendor"] = "供应商信息，包含厂商 ID、名称和图标。",
        ["Id"] = "供应商或对象的标识值，ESI 中常用 #x 表示十六进制。",
        ["Name"] = "可读名称，LcId 属性表示语言区域。",
        ["Descriptions"] = "设备描述集合，包含设备分组和具体设备定义。",
        ["Groups"] = "设备分类，用于 EtherCAT 主站工具按类型组织设备。",
        ["Group"] = "单个设备分组定义。",
        ["Type"] = "类型名称；在 Device 下通常带 ProductCode、RevisionNo 等设备身份属性。",
        ["Devices"] = "该 ESI 文件包含的设备型号列表，一个 XML 可包含多个设备变体。",
        ["Device"] = "单个 EtherCAT 从站设备定义，包含通信能力、CiA 402 对象字典、PDO 映射和同步管理器。",
        ["Info"] = "补充信息节点，常见于默认值、状态机、Mailbox、EEPROM 子结构。",
        ["StateMachine"] = "EtherCAT 状态机参数，描述 Init/Pre-Op/Safe-Op/Op 切换相关行为。",
        ["Timeout"] = "通信或状态切换超时时间配置。",
        ["PreopTimeout"] = "进入 Pre-Operational 状态的超时时间，单位通常为毫秒。",
        ["SafeopOpTimeout"] = "Safe-Operational 切换到 Operational 的超时时间。",
        ["BackToInitTimeout"] = "退回 Init 状态的超时时间。",
        ["BackToSafeopTimeout"] = "退回 Safe-Operational 状态的超时时间。",
        ["Behavior"] = "状态机行为开关，例如是否允许无同步信号进入 Safe-Op。",
        ["Mailbox"] = "邮箱通信配置，用于 CoE/SoE/FoE 等非周期通信。",
        ["RequestTimeout"] = "Mailbox 请求等待超时时间。",
        ["ResponseTimeout"] = "Mailbox 响应等待超时时间。",
        ["CoE"] = "CANopen over EtherCAT 能力声明，影响 SDO、PDO 配置和对象字典访问。",
        ["FoE"] = "File over EtherCAT 能力声明，常用于固件下载。",
        ["Profile"] = "设备协议 Profile，伺服驱动通常是 CiA 402 Profile 402。",
        ["ProfileNo"] = "Profile 编号；402 表示 CiA 402 驱动和运动控制设备。",
        ["ChannelInfo"] = "通道 Profile 信息，部分厂商把 ProfileNo 放在该节点下。",
        ["Dictionary"] = "对象字典定义，包含数据类型和对象索引。",
        ["DataTypes"] = "对象字典数据类型集合。",
        ["DataType"] = "单个数据类型定义，说明位宽、基础类型和子项布局。",
        ["BaseType"] = "复合或数组类型的基础数据类型。",
        ["BitSize"] = "数据对象或数据类型占用位数。",
        ["BitOffs"] = "复合类型子项在结构中的位偏移。",
        ["ArrayInfo"] = "数组类型范围和元素数量。",
        ["LBound"] = "数组起始下标。",
        ["Elements"] = "数组或映射集合中的元素数量。",
        ["SubItem"] = "对象或复合数据类型的子项，常对应 CANopen 子索引。",
        ["SubIdx"] = "子索引编号。",
        ["SubIndex"] = "PDO 条目或对象子项的子索引编号。",
        ["Objects"] = "对象字典对象集合，每个 Object 对应一个 CANopen/EtherCAT 对象索引。",
        ["Object"] = "对象字典条目，描述一个参数、状态量、命令量或 PDO 映射对象。",
        ["Index"] = "对象索引或 PDO 索引，0x6000 以后常见于 CiA 402 驱动 Profile。",
        ["Flags"] = "访问权限、PDO 映射能力、保存行为等标志集合。",
        ["Access"] = "访问权限：ro 只读，rw 读写，wo 只写。",
        ["PdoMapping"] = "该对象是否允许映射到周期过程数据 PDO。",
        ["DefaultValue"] = "默认值，常用十六进制编码。",
        ["DefaultData"] = "默认数据，部分厂商用该节点替代 DefaultValue。",
        ["Fmmu"] = "Fieldbus Memory Management Unit，用于把逻辑地址映射到从站物理过程数据区域。",
        ["Sm"] = "Sync Manager，同步管理器；MBoxOut/MBoxIn 用于邮箱，Outputs/Inputs 用于周期 PDO。",
        ["RxPdo"] = "接收 PDO，主站写给从站的周期输出数据，例如控制字、目标位置、目标速度。",
        ["TxPdo"] = "发送 PDO，从站发给主站的周期输入数据，例如状态字、实际位置、实际速度。",
        ["Entry"] = "PDO 映射条目，说明该 PDO 中包含哪个对象索引、子索引、位长和数据类型。",
        ["BitLen"] = "PDO 条目占用位数。",
        ["DataType"] = "PDO 条目或对象的数据类型。",
        ["Dc"] = "Distributed Clocks 分布式时钟配置，用于高精度同步。",
        ["OpMode"] = "分布式时钟运行模式。",
        ["AssignActivate"] = "DC 模式下的 AssignActivate 寄存器配置值。",
        ["CycleTimeSync0"] = "SYNC0 周期时间。",
        ["ShiftTimeSync0"] = "SYNC0 相位偏移。",
        ["Eeprom"] = "EEPROM 镜像或引导邮箱配置，主站可据此识别和初始化设备。",
        ["BootStrap"] = "引导模式 Mailbox 配置，常用于 FoE 固件更新。",
        ["ByteSize"] = "EEPROM 或数据块字节大小。",
    };

    private static readonly IReadOnlyDictionary<string, string> ObjectMeanings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["0x1000"] = "设备类型，标识设备 Profile 和设备类别。",
        ["0x1001"] = "错误寄存器，汇总设备当前错误状态。",
        ["0x1008"] = "设备名称字符串。",
        ["0x1009"] = "硬件版本字符串。",
        ["0x100A"] = "软件版本字符串。",
        ["0x1018"] = "Identity 对象，包含 Vendor ID、Product Code、Revision Number、Serial Number。",
        ["0x10F1"] = "错误响应设置，常用于同步错误、本地错误处理策略。",
        ["0x1C00"] = "Sync Manager 通信类型对象。",
        ["0x1C12"] = "Sync Manager 2 PDO 分配，通常关联 RxPDO/主站到从站输出。",
        ["0x1C13"] = "Sync Manager 3 PDO 分配，通常关联 TxPDO/从站到主站输入。",
        ["0x1C32"] = "SM2 同步参数。",
        ["0x1C33"] = "SM3 同步参数。",
        ["0x603F"] = "错误码，驱动当前故障代码。",
        ["0x6040"] = "控制字 Controlword，主站写入，用于 CiA 402 状态机使能、故障复位、启动运动。",
        ["0x6041"] = "状态字 Statusword，从站反馈 CiA 402 状态机状态、故障、到位等状态位。",
        ["0x605A"] = "Quick Stop 选项代码，定义快速停止行为。",
        ["0x6060"] = "运行模式 Modes of operation，主站写入选择 CSP/CSV/CST、Profile Position、Homing 等模式。",
        ["0x6061"] = "运行模式显示 Modes of operation display，从站反馈当前实际生效模式。",
        ["0x6064"] = "位置实际值 Position actual value。",
        ["0x606C"] = "速度实际值 Velocity actual value。",
        ["0x6071"] = "目标转矩 Target torque。",
        ["0x6077"] = "转矩实际值 Torque actual value。",
        ["0x607A"] = "目标位置 Target position，位置模式或 CSP 中主站下发的位置命令。",
        ["0x607D"] = "软件位置限位，限制最小/最大目标位置。",
        ["0x6081"] = "轮廓速度 Profile velocity。",
        ["0x6083"] = "轮廓加速度 Profile acceleration。",
        ["0x6084"] = "轮廓减速度 Profile deceleration。",
        ["0x6085"] = "Quick Stop 减速度。",
        ["0x608F"] = "编码器分辨率参数。",
        ["0x6091"] = "齿轮比参数。",
        ["0x6092"] = "进给常数参数。",
        ["0x6098"] = "回零方法 Homing method。",
        ["0x6099"] = "回零速度 Homing speeds。",
        ["0x609A"] = "回零加速度 Homing acceleration。",
        ["0x60B8"] = "探针功能 Touch probe function，配置高速锁存/探针触发。",
        ["0x60B9"] = "探针状态 Touch probe status。",
        ["0x60BA"] = "探针 1 正沿锁存位置。",
        ["0x60BB"] = "探针 1 负沿锁存位置。",
        ["0x60BC"] = "探针 2 正沿锁存位置。",
        ["0x60BD"] = "探针 2 负沿锁存位置。",
        ["0x60C2"] = "插补周期时间。",
        ["0x60FD"] = "数字输入 Digital inputs。",
        ["0x60FE"] = "数字输出 Digital outputs。",
        ["0x60FF"] = "目标速度 Target velocity，速度模式或 CSV 中主站下发的速度命令。",
        ["0x6502"] = "支持的驱动模式 Supported drive modes，位掩码表示设备支持哪些 CiA 402 模式。",
    };

    public static string GetElementMeaning(string elementName) =>
        ElementMeanings.TryGetValue(elementName, out var meaning)
            ? meaning
            : "厂商扩展或 ESI 标准节点，需结合父节点和属性判断用途。";

    public static string GetObjectMeaning(string index)
    {
        var normalizedIndex = EsiValueParser.NormalizeHex(index, minimumDigits: 4);
        if (ObjectMeanings.TryGetValue(normalizedIndex, out var meaning))
        {
            return meaning;
        }

        if (!EsiValueParser.TryParseHex(normalizedIndex, out var numericIndex))
        {
            return string.Empty;
        }

        return numericIndex switch
        {
            >= 0x1600 and <= 0x17FF => "RxPDO 映射对象，定义主站输出到从站的周期数据布局。",
            >= 0x1A00 and <= 0x1BFF => "TxPDO 映射对象，定义从站输入到主站的周期数据布局。",
            >= 0x2000 and <= 0x5FFF => "厂商自定义对象，含义需查对应伺服驱动手册。",
            >= 0x6000 and <= 0x67FF => "CiA 402 驱动 Profile 对象，通常用于伺服控制、状态反馈和运动参数。",
            >= 0x8000 and <= 0x9FFF => "设备配置对象，常用于 EtherCAT 主站启动参数或厂商配置。",
            >= 0xF000 and <= 0xFFFF => "EtherCAT 模块化设备或设备信息对象。",
            _ => string.Empty
        };
    }

    public static string GetSyncManagerMeaning(string usage) =>
        usage.Trim() switch
        {
            "MBoxOut" => "Mailbox 输出通道，主站发送 SDO/CoE 等邮箱请求到从站。",
            "MBoxIn" => "Mailbox 输入通道，从站返回 SDO/CoE 等邮箱响应给主站。",
            "Outputs" => "过程数据输出通道，承载 RxPDO，即主站写给伺服的周期命令。",
            "Inputs" => "过程数据输入通道，承载 TxPDO，即伺服反馈给主站的周期状态。",
            _ => "同步管理器通道，具体用途由文本和 ControlByte 决定。"
        };

    public static string Combine(params string[] meanings)
    {
        var parts = meanings
            .Where(meaning => !string.IsNullOrWhiteSpace(meaning))
            .Select(meaning => meaning.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return string.Join(Environment.NewLine, parts);
    }
}
