# 半导体/3C 行业：CIM 与 MES 的区别与关系

## 1. 术语澄清：这里的 CIM 是什么？

在半导体与 3C（电子制造）行业里，"CIM"通常指 **Computer Integrated Manufacturing（计算机集成制造）**，强调：
- 设备与上层系统的集成（接口、协议、事件、命令）
- 生产自动化编排（派工、搬运、上料、换线、Recipe 下发、状态联动）
- 工厂范围的信息联动（MES/ERP/WMS/QMS/APS/EAP/FDC/SPC 等）

> 注意：这和电力行业的 CIM（Common Information Model，IEC 61970/61968）**不是**一回事。

---

## 2. MES 是什么？它解决什么问题？

MES（Manufacturing Execution System，制造执行系统）核心定位是**生产执行与生产运营管理**，通常覆盖：
- 工单/批次/流程路线（Route / Process Flow）
- WIP（在制品）跟踪、过站、报工、良率
- 生产计划执行与反馈（与 APS/ERP 交互）
- 追溯（Traceability）：物料批次、序列号、工艺参数、设备、人员、时间
- 质量相关执行：抽检、判定、返工、Hold/Release
- 基础的设备交互（复杂工厂中常由 CIM/EAP 承接）

**一句话：MES 管"生产怎么跑、跑到哪里、结果如何、如何追溯"；它是生产管理与执行的业务系统。**

---

## 3. CIM（半导体/3C）是什么？它解决什么问题？

在半导体/3C 工厂，CIM 是"工厂自动化与系统集成平台"，常见覆盖：
- 上承：对接 MES/ERP/WMS/QMS/APS（订单、工单、WIP、规则、配方、库存等）
- 下接：对接设备与自动化系统（EAP、AMHS、PLC、AOI、测试机、贴片线、AGV、料塔等）
- 协调：跨系统的自动化流程编排与事件驱动

在半导体语境里，CIM 经常与以下子系统强绑定：
- **EAP（Equipment Automation Program）**：设备自动化层，典型协议 SECS/GEM
- **AMHS/MCS/Stocker**：物料搬运与存储自动化
- **FDC（Fault Detection & Classification）**：设备健康与异常检测
- **SPC（Statistical Process Control）**：统计过程控制
- **Recipe Management**：配方/程序版本、下发、校验、防错

**一句话：CIM 管"系统与设备如何自动协同、数据如何在工厂里流转与联动"；它更偏平台/集成/自动化。**

---

## 4. CIM 与 MES 的核心区别

| 维度 | MES（制造执行） | CIM（计算机集成制造） |
|---|---|---|
| 核心目标 | 生产执行管理、WIP 跟踪、追溯、报工、路线控制 | 系统集成与工厂自动化编排 |
| 关注对象 | 工单/批次/在制品、工艺路线、站点、人员、质量判定 | 设备、自动化系统、配方、搬运系统、设备状态与事件 |
| 与设备交互 | 有些 MES 直接对设备，复杂工厂往往不直接深连 | 通常深度对接设备（SECS/GEM、EAP 等） |
| 典型数据 | Route、WIP、TrackIn/TrackOut、SN/批次追溯、良率 | Equipment State、Alarm/Event、Recipe、Transport Job、Carrier |
| 实时性要求 | 中高（秒级到分钟级） | 高（秒级甚至亚秒级） |
| 集成模式 | 作为业务主系统，向上下游发指令/收反馈 | 作为"集成枢纽"，编排 MES 指令与设备/自动化动作闭环 |
| 成功指标 | 产线按计划执行、追溯完整、报表准确 | 自动化闭环稳定、设备联动可靠、异常处理及时 |

---

## 5. 两者关系

常见落地方式：
- **MES 是生产运营管理的大脑（业务决策/规则/路线）**：决定 Lot/SN 下一站去哪、做哪道工序、如何判定良品/不良
- **CIM 是工厂自动化的中枢神经（连接与联动）**：把 MES 的决策转成设备可执行的动作，并从设备实时拿回状态与数据形成闭环

---

## 6. 典型交互链路示例

### 6.1 半导体：Lot 进站（TrackIn）自动化闭环

1. MES 下发：Lot 可进站、工艺参数/规则、目标设备/站点
2. CIM 判定：设备可用性（State/Capability）、人员权限、载具/料盒信息
3. CIM/EAP：校验并下发 Recipe（版本/参数/防错），触发设备准备
4. 设备回报：Ready/Running/End、采集关键参数、报警事件
5. CIM 汇总：结果与关键参数回写 MES（过站、良率、异常、Hold/Alarm）

### 6.2 3C：SN 过站 + AOI/测试数据回写

1. MES：SN 过站请求（工单、站点、工艺版本）
2. CIM/集成层：联动工站设备（条码枪、PLC、测试机）
3. 设备：返回测试结果、测量值（AOI 缺陷信息）
4. CIM/集成层：统一格式化、校验、关联 SN，回写 MES/QMS，并触发拦截/放行

---

## 7. 本 Demo 中的对应关系

本 Demo 模拟 3C 行业 CIM 层的典型职责：

| Demo 组件 | 对应真实角色 |
|---|---|
| `Cim.DeviceSimulator` | 设备/PLC 模拟（SMT 贴片机、AOI、FCT 测试机） |
| `Cim.DbAdapter` (IEventBus) | CIM 内部消息总线（可换 RabbitMQ/Kafka） |
| `Cim.MqWorker` | CIM 事件处理层（设备数据标准化写库） |
| `Cim.RestApi` | CIM 对外集成接口（供 MES/QMS 调用） |
| SQLite (TrackEvents/TestResults) | CIM 集成数据库（归一化后的追溯数据） |

真实落地时，RestApi 的调用方是 MES；MES 通过 CIM 的 REST 接口来：
- 查询设备实时状态（设备预约/锁机）
- 下发/校验 Recipe
- 触发 TrackIn/TrackOut（或 CIM 自动完成后回写 MES）
- 上传测试结果（FCT/ICT/AOI）
