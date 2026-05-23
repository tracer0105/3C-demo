# 工业生产场景 SQL 速查

这份文档基于当前项目 `CIM` 的真实 SQLite 表结构整理，适合做 3C / 制造执行 / 设备联网场景下的基础分析查询。

## 1. 当前项目实际表结构

### 1.1 `EquipmentStatus`

设备当前快照表，只保留每台设备的最新状态。

关键字段：

- `EquipmentId`：设备编号
- `EquipmentName`：设备名称
- `State`：`IDLE / RUNNING / ALARM / MAINTENANCE / DOWN`
- `RecipeId`：当前配方
- `LotId`：当前批次
- `UpdatedAt`：最后更新时间

适合查询：

- 当前设备状态总览
- 当前哪台设备在报警/停机
- 当前设备绑定的批次/配方

不适合直接查询：

- 历史停机时长
- OEE 历史趋势

原因：这张表是“当前状态快照”，不是历史状态流水。

### 1.2 `TrackEvents`

过站事件流水表。

关键字段：

- `SerialNumber`：产品 SN
- `LotId`：批次
- `EquipmentId`：设备编号
- `StationId`：工位编号
- `EventType`：`TRACKIN / TRACKOUT`
- `RecipeId`：配方
- `Operator`：操作员
- `EventTime`：事件时间
- `Remarks`：备注

适合查询：

- 按班次/工位/设备/批次统计产出
- 单个 SN 追溯
- 节拍、滞留、在制品 WIP

### 1.3 `TestResults`

测试结果主表。

关键字段：

- `SerialNumber`
- `LotId`
- `EquipmentId`
- `StationId`
- `TestProgram`
- `Verdict`：`PASS / FAIL / ABORT`
- `TestedAt`
- `Operator`

注意：

项目里对 `TestResults` 做了 `UNIQUE(SerialNumber, StationId)`，并且写入时使用了 `UPSERT`。

这意味着：

- 同一个 `SN + 工位` 只保留最新一条测试结果
- 如果同一台产品在同一工位反复复测，旧结果会被覆盖

所以：

- 这张表适合查“当前最终测试结论”
- 不适合直接做“所有测试尝试次数”统计

### 1.4 `TestItems`

测试项明细表。

关键字段：

- `TestResultId`
- `ItemName`
- `MeasuredValue`
- `LowerLimit`
- `UpperLimit`
- `Unit`
- `Verdict`

适合查询：

- 不良项 Top N
- 某测试项的实测值分布
- 超上限/低下限分析

### 1.5 `Alarms`

设备报警表。

关键字段：

- `EquipmentId`
- `AlarmCode`
- `AlarmLevel`：`WARNING / ERROR / CRITICAL`
- `Description`
- `Status`：`ACTIVE / CLEARED`
- `RaisedAt`
- `ClearedAt`

适合查询：

- 活跃告警
- 某设备报警频次
- 平均恢复时间

## 2. 时间字段的使用原则

这个项目里写库时使用的是 `DateTime.UtcNow`，所以表里的时间可以按“UTC 时间字符串”理解。

如果你的产线分析要按中国工厂班次算，建议先转本地时间，再做班次切分。

SQLite 常见写法：

```sql
datetime(EventTime, '+8 hours')
datetime(TestedAt, '+8 hours')
datetime(RaisedAt, '+8 hours')
```

不要一边用 UTC，一边直接按 `08:00` / `20:00` 切白班夜班，否则班次统计会错 8 小时。

## 3. 常用 SQL 函数

下面按工业场景里最常用的类别整理。

### 3.1 聚合函数

```sql
COUNT(*)                        -- 记录数
COUNT(DISTINCT SerialNumber)    -- 去重 SN 数
SUM(...)                        -- 求和
AVG(...)                        -- 平均值
MAX(...)                        -- 最大值
MIN(...)                        -- 最小值
```

典型用途：

- 产出数量
- 去重过站数量
- 平均节拍
- 最长停留时间

### 3.2 条件判断

```sql
CASE
    WHEN Verdict = 'PASS' THEN 1
    ELSE 0
END
```

典型用途：

- 白班 / 夜班分组
- PASS / FAIL 条件汇总
- 告警等级分类

### 3.3 空值处理

```sql
COALESCE(ClearedAt, CURRENT_TIMESTAMP)
IFNULL(Operator, 'UNKNOWN')
```

典型用途：

- 活跃告警没有清除时间时，先用当前时间替代
- 操作员字段为空时给默认值

### 3.4 数值处理

```sql
ROUND(value, 2)
ABS(value)
CAST(value AS REAL)
```

典型用途：

- 平均节拍保留 2 位小数
- 测试偏差分析
- 字段类型转换

### 3.5 日期时间函数

```sql
date(EventTime)
time(EventTime)
datetime(EventTime)
strftime('%Y-%m-%d %H', EventTime)
julianday(end_time) - julianday(start_time)
```

典型用途：

- 按天 / 小时统计
- 班次切分
- 节拍分钟数计算

分钟差常见写法：

```sql
ROUND((julianday(trackout_time) - julianday(trackin_time)) * 24 * 60, 2)
```

### 3.6 窗口函数

如果 SQLite 版本支持窗口函数，可以直接用：

```sql
ROW_NUMBER() OVER (PARTITION BY StationId ORDER BY EventTime DESC)
LAG(EventTime) OVER (PARTITION BY SerialNumber, StationId ORDER BY EventTime)
LEAD(EventTime) OVER (PARTITION BY SerialNumber, StationId ORDER BY EventTime)
```

典型用途：

- 找每组最新一条
- 做前后事件配对
- 算相邻两次事件间隔

## 4. 常用查询技巧

### 4.1 时间范围用左闭右开

推荐：

```sql
WHERE local_time >= '2026-05-01 08:00:00'
  AND local_time <  '2026-05-02 08:00:00'
```

不推荐：

```sql
WHERE local_time BETWEEN '2026-05-01 08:00:00' AND '2026-05-02 07:59:59'
```

左闭右开更不容易漏秒、也更方便拼接连续时间段。

### 4.2 先做标准化 CTE，再做统计

推荐先把“本地时间、班次、白夜班、日期”等衍生字段算出来：

```sql
WITH base AS (
    SELECT
        StationId,
        SerialNumber,
        datetime(EventTime, '+8 hours') AS local_time
    FROM TrackEvents
    WHERE EventType = 'TRACKOUT'
)
SELECT ...
FROM base;
```

好处：

- 主查询更短
- 口径更统一
- 后续更容易复用

### 4.3 条件聚合比多次子查询更实用

```sql
SUM(CASE WHEN Verdict = 'PASS' THEN 1 ELSE 0 END) AS pass_qty,
SUM(CASE WHEN Verdict = 'FAIL' THEN 1 ELSE 0 END) AS fail_qty
```

这类写法比先查 PASS 再查 FAIL 再关联更简单。

### 4.4 产出、良率、WIP 最好分开定义

工业现场很容易把几个概念混在一起。

- 产出：通常看 `TrackEvents` 里的 `TRACKOUT`
- 良率：通常看 `TestResults.Verdict`
- WIP：通常看“已进站未出站”的 SN

不要拿测试结果数量直接代替工位产出，也不要拿当前设备状态代替历史停机分析。

### 4.5 做追溯时优先按 SN 查流水

最常见方式：

- 先在 `TrackEvents` 查过站
- 再在 `TestResults` 查测试结论
- 必要时联到 `TestItems` 看具体失败项

这样最符合现场问题定位逻辑。

### 4.6 对大表统计，要补面向时间和工位的索引

当前项目已有部分索引，但如果数据量起来，建议补：

```sql
CREATE INDEX IF NOT EXISTS IX_TrackEvents_Station_Time
ON TrackEvents(StationId, EventTime);

CREATE INDEX IF NOT EXISTS IX_TestResults_Station_Time
ON TestResults(StationId, TestedAt);

CREATE INDEX IF NOT EXISTS IX_Alarms_Equipment_Time
ON Alarms(EquipmentId, RaisedAt);
```

## 5. 班次口径建议

工业场景里常用定义可以先统一：

- 白班：`08:00:00 ~ 19:59:59`
- 夜班：`20:00:00 ~ 次日 07:59:59`

建议班次日期定义为“班次开始的那一天”：

- `2026-05-20 09:00` 属于 `2026-05-20` 白班
- `2026-05-20 23:00` 属于 `2026-05-20` 夜班
- `2026-05-21 03:00` 也属于 `2026-05-20` 夜班

这点非常关键，否则跨午夜的夜班会被拆到两天，现场报表通常不接受。

推荐写法：

```sql
CASE
    WHEN time(local_time) >= '08:00:00' AND time(local_time) < '20:00:00' THEN '白班'
    ELSE '夜班'
END AS shift_name,
CASE
    WHEN time(local_time) < '08:00:00' THEN date(local_time, '-1 day')
    ELSE date(local_time)
END AS shift_date
```

## 6. 典型工业查询场景

### 6.1 按班次统计产出

数据源建议用 `TrackEvents` 的 `TRACKOUT`。

### 6.2 按白班 / 夜班统计良率

数据源建议用 `TestResults`。

### 6.3 按工位统计产出、良率、不良项

常用维度：

- `StationId`
- `EquipmentId`
- `LotId`
- `Operator`
- `TestProgram`

### 6.4 单件 SN 追溯

常用于：

- 客诉追溯
- 不良品流向分析
- 漏测 / 漏站检查

### 6.5 告警统计

常见指标：

- 活跃告警数
- 每设备报警次数
- 平均恢复分钟数

## 7. 这份目录里的配套文件

本目录还提供一个可直接改参数使用的 SQL 文件：

- `工业生产场景_模拟查询.sql`

内容包含：

- 按班次统计工位产出
- 按白班/夜班统计测试良率
- 按工位查测试结果
- 按工位查不良项 Top N
- 按批次查过站轨迹
- 按 SN 做全流程追溯
- 查当前 WIP
- 查设备告警统计
- 查工位平均节拍

## 8. 实际使用时的几个提醒

### 8.1 如果你想统计“所有复测次数”

当前 `TestResults` 表不够，因为它只保留同一 `SN + StationId` 的最新结果。

如果后面要做：

- 首测良率
- 复测次数
- 最终良率 vs 首次良率

就应该新增历史表，不能只靠当前这张 `TestResults`。

### 8.2 如果你想统计“历史设备利用率”

当前 `EquipmentStatus` 也不够，因为它只有当前快照，没有状态变更流水。

如果后面要做：

- 开机率
- 报警时长
- 停机时长
- OEE

应该增加设备状态历史表，例如 `EquipmentStateHistory`。

### 8.3 先统一口径，再做报表

至少先统一这几项：

- 时间是 UTC 还是本地时间
- 白班 / 夜班切分点
- 产出是否按 `TRACKOUT`
- 良率是否按“最终结果”还是“首测结果”
- WIP 是否按“当前最后事件是 TRACKIN”定义

否则 SQL 没问题，报表口径也会对不上。
