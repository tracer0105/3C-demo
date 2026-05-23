-- 工业生产场景模拟查询
-- 基于当前项目真实表：
-- EquipmentStatus / TrackEvents / TestResults / TestItems / Alarms
--
-- 说明：
-- 1. 当前项目时间字段按 UTC 理解，下面统一转成中国工厂本地时间（+8 hours）
-- 2. 白班：08:00-20:00，夜班：20:00-次日08:00
-- 3. 夜班的班次日期按“班次开始日”归属


-- 1. 按班次统计工位产出
-- 口径：TRACKOUT 代表该工位完成产出
WITH base AS (
    SELECT
        SerialNumber,
        LotId,
        EquipmentId,
        StationId,
        datetime(EventTime, '+8 hours') AS local_time
    FROM TrackEvents
    WHERE EventType = 'TRACKOUT'
),
shifted AS (
    SELECT
        SerialNumber,
        LotId,
        EquipmentId,
        StationId,
        local_time,
        CASE
            WHEN time(local_time) >= '08:00:00' AND time(local_time) < '20:00:00' THEN '白班'
            ELSE '夜班'
        END AS shift_name,
        CASE
            WHEN time(local_time) < '08:00:00' THEN date(local_time, '-1 day')
            ELSE date(local_time)
        END AS shift_date
    FROM base
)
SELECT
    shift_date,
    shift_name,
    StationId,
    COUNT(*) AS output_qty,
    COUNT(DISTINCT SerialNumber) AS distinct_sn_qty
FROM shifted
GROUP BY shift_date, shift_name, StationId
ORDER BY shift_date DESC, shift_name, StationId;


-- 2. 按白班 / 夜班统计测试良率
-- 注意：当前 TestResults 只保留同一 SN+工位 的最新结果，不是完整复测历史
WITH base AS (
    SELECT
        SerialNumber,
        LotId,
        EquipmentId,
        StationId,
        Verdict,
        datetime(TestedAt, '+8 hours') AS local_time
    FROM TestResults
),
shifted AS (
    SELECT
        SerialNumber,
        LotId,
        EquipmentId,
        StationId,
        Verdict,
        local_time,
        CASE
            WHEN time(local_time) >= '08:00:00' AND time(local_time) < '20:00:00' THEN '白班'
            ELSE '夜班'
        END AS shift_name,
        CASE
            WHEN time(local_time) < '08:00:00' THEN date(local_time, '-1 day')
            ELSE date(local_time)
        END AS shift_date
    FROM base
)
SELECT
    shift_date,
    shift_name,
    StationId,
    COUNT(*) AS total_qty,
    SUM(CASE WHEN Verdict = 'PASS' THEN 1 ELSE 0 END) AS pass_qty,
    SUM(CASE WHEN Verdict = 'FAIL' THEN 1 ELSE 0 END) AS fail_qty,
    SUM(CASE WHEN Verdict = 'ABORT' THEN 1 ELSE 0 END) AS abort_qty,
    ROUND(
        100.0 * SUM(CASE WHEN Verdict = 'PASS' THEN 1 ELSE 0 END) / NULLIF(COUNT(*), 0),
        2
    ) AS pass_rate_pct
FROM shifted
GROUP BY shift_date, shift_name, StationId
ORDER BY shift_date DESC, shift_name, StationId;


-- 3. 查询某个工位在某段时间内的测试情况
-- 修改 params 里的工位和时间
WITH params AS (
    SELECT
        'TEST-01-ST1' AS station_id,
        '2026-05-01 00:00:00' AS start_local,
        '2026-06-01 00:00:00' AS end_local
),
base AS (
    SELECT
        tr.SerialNumber,
        tr.LotId,
        tr.EquipmentId,
        tr.StationId,
        tr.TestProgram,
        tr.Verdict,
        tr.Operator,
        datetime(tr.TestedAt, '+8 hours') AS local_time
    FROM TestResults tr
)
SELECT
    b.local_time,
    b.SerialNumber,
    b.LotId,
    b.EquipmentId,
    b.StationId,
    b.TestProgram,
    b.Verdict,
    b.Operator
FROM base b
CROSS JOIN params p
WHERE b.StationId = p.station_id
  AND b.local_time >= p.start_local
  AND b.local_time <  p.end_local
ORDER BY b.local_time DESC;


-- 4. 查询某个工位的汇总良率
WITH params AS (
    SELECT
        'TEST-01-ST1' AS station_id,
        '2026-05-01 00:00:00' AS start_local,
        '2026-06-01 00:00:00' AS end_local
),
base AS (
    SELECT
        StationId,
        Verdict,
        datetime(TestedAt, '+8 hours') AS local_time
    FROM TestResults
)
SELECT
    b.StationId,
    COUNT(*) AS total_qty,
    SUM(CASE WHEN b.Verdict = 'PASS' THEN 1 ELSE 0 END) AS pass_qty,
    SUM(CASE WHEN b.Verdict = 'FAIL' THEN 1 ELSE 0 END) AS fail_qty,
    SUM(CASE WHEN b.Verdict = 'ABORT' THEN 1 ELSE 0 END) AS abort_qty,
    ROUND(
        100.0 * SUM(CASE WHEN b.Verdict = 'PASS' THEN 1 ELSE 0 END) / NULLIF(COUNT(*), 0),
        2
    ) AS pass_rate_pct
FROM base b
CROSS JOIN params p
WHERE b.StationId = p.station_id
  AND b.local_time >= p.start_local
  AND b.local_time <  p.end_local
GROUP BY b.StationId;


-- 5. 查询某个工位的不良项 Top N
WITH params AS (
    SELECT
        'TEST-01-ST1' AS station_id,
        '2026-05-01 00:00:00' AS start_local,
        '2026-06-01 00:00:00' AS end_local
),
base AS (
    SELECT
        tr.StationId,
        ti.ItemName,
        ti.Verdict,
        datetime(tr.TestedAt, '+8 hours') AS local_time
    FROM TestResults tr
    INNER JOIN TestItems ti
        ON ti.TestResultId = tr.Id
)
SELECT
    b.StationId,
    b.ItemName,
    COUNT(*) AS fail_count
FROM base b
CROSS JOIN params p
WHERE b.StationId = p.station_id
  AND b.Verdict = 'FAIL'
  AND b.local_time >= p.start_local
  AND b.local_time <  p.end_local
GROUP BY b.StationId, b.ItemName
ORDER BY fail_count DESC, b.ItemName
LIMIT 10;


-- 6. 按批次查看过站轨迹
WITH params AS (
    SELECT 'LOT-20240101' AS lot_id
)
SELECT
    datetime(te.EventTime, '+8 hours') AS local_time,
    te.SerialNumber,
    te.LotId,
    te.EquipmentId,
    te.StationId,
    te.EventType,
    te.RecipeId,
    te.Operator,
    te.Remarks
FROM TrackEvents te
CROSS JOIN params p
WHERE te.LotId = p.lot_id
ORDER BY te.SerialNumber, te.EventTime;


-- 7. 查询单个 SN 的全流程追溯
WITH params AS (
    SELECT 'SN-TEST-001' AS serial_number
)
SELECT
    'TRACK' AS record_type,
    datetime(te.EventTime, '+8 hours') AS local_time,
    te.SerialNumber,
    te.LotId,
    te.EquipmentId,
    te.StationId,
    te.EventType AS result_or_event,
    te.Operator,
    te.RecipeId AS extra_info
FROM TrackEvents te
CROSS JOIN params p
WHERE te.SerialNumber = p.serial_number

UNION ALL

SELECT
    'TEST' AS record_type,
    datetime(tr.TestedAt, '+8 hours') AS local_time,
    tr.SerialNumber,
    tr.LotId,
    tr.EquipmentId,
    tr.StationId,
    tr.Verdict AS result_or_event,
    tr.Operator,
    tr.TestProgram AS extra_info
FROM TestResults tr
CROSS JOIN params p
WHERE tr.SerialNumber = p.serial_number

ORDER BY local_time;


-- 8. 查询当前每个工位的 WIP
-- 定义：同一个 SN + 工位，最后一条过站记录是 TRACKIN，则视为还在该工位内
WITH latest_event AS (
    SELECT
        SerialNumber,
        StationId,
        EquipmentId,
        LotId,
        EventType,
        EventTime,
        ROW_NUMBER() OVER (
            PARTITION BY SerialNumber, StationId
            ORDER BY EventTime DESC, Id DESC
        ) AS rn
    FROM TrackEvents
)
SELECT
    StationId,
    COUNT(*) AS wip_qty
FROM latest_event
WHERE rn = 1
  AND EventType = 'TRACKIN'
GROUP BY StationId
ORDER BY wip_qty DESC, StationId;


-- 9. 查询当前 WIP 明细
WITH latest_event AS (
    SELECT
        SerialNumber,
        StationId,
        EquipmentId,
        LotId,
        EventType,
        EventTime,
        ROW_NUMBER() OVER (
            PARTITION BY SerialNumber, StationId
            ORDER BY EventTime DESC, Id DESC
        ) AS rn
    FROM TrackEvents
)
SELECT
    StationId,
    EquipmentId,
    LotId,
    SerialNumber,
    datetime(EventTime, '+8 hours') AS last_trackin_local_time
FROM latest_event
WHERE rn = 1
  AND EventType = 'TRACKIN'
ORDER BY StationId, last_trackin_local_time;


-- 10. 设备当前状态总览
SELECT
    EquipmentId,
    EquipmentName,
    State,
    RecipeId,
    LotId,
    datetime(UpdatedAt, '+8 hours') AS updated_local_time
FROM EquipmentStatus
ORDER BY EquipmentId;


-- 11. 活跃告警清单
SELECT
    EquipmentId,
    AlarmCode,
    AlarmLevel,
    Description,
    Status,
    datetime(RaisedAt, '+8 hours') AS raised_local_time
FROM Alarms
WHERE Status = 'ACTIVE'
ORDER BY RaisedAt DESC;


-- 12. 设备告警统计与平均恢复时间
SELECT
    EquipmentId,
    COUNT(*) AS alarm_count,
    SUM(CASE WHEN Status = 'ACTIVE' THEN 1 ELSE 0 END) AS active_alarm_count,
    ROUND(
        AVG(
            (julianday(COALESCE(ClearedAt, CURRENT_TIMESTAMP)) - julianday(RaisedAt)) * 24 * 60
        ),
        2
    ) AS avg_recovery_min
FROM Alarms
GROUP BY EquipmentId
ORDER BY alarm_count DESC, EquipmentId;


-- 13. 统计每个工位平均节拍
-- 说明：按同一 SN + 工位内，第 N 次 TRACKIN 配第 N 次 TRACKOUT
WITH ins AS (
    SELECT
        SerialNumber,
        StationId,
        EquipmentId,
        EventTime AS trackin_time,
        ROW_NUMBER() OVER (
            PARTITION BY SerialNumber, StationId
            ORDER BY EventTime
        ) AS rn
    FROM TrackEvents
    WHERE EventType = 'TRACKIN'
),
outs AS (
    SELECT
        SerialNumber,
        StationId,
        EquipmentId,
        EventTime AS trackout_time,
        ROW_NUMBER() OVER (
            PARTITION BY SerialNumber, StationId
            ORDER BY EventTime
        ) AS rn
    FROM TrackEvents
    WHERE EventType = 'TRACKOUT'
),
paired AS (
    SELECT
        i.SerialNumber,
        i.StationId,
        i.EquipmentId,
        i.trackin_time,
        o.trackout_time,
        ROUND(
            (julianday(o.trackout_time) - julianday(i.trackin_time)) * 24 * 60,
            2
        ) AS cycle_min
    FROM ins i
    INNER JOIN outs o
        ON o.SerialNumber = i.SerialNumber
       AND o.StationId = i.StationId
       AND o.rn = i.rn
    WHERE julianday(o.trackout_time) >= julianday(i.trackin_time)
)
SELECT
    StationId,
    COUNT(*) AS cycle_count,
    ROUND(AVG(cycle_min), 2) AS avg_cycle_min,
    ROUND(MIN(cycle_min), 2) AS min_cycle_min,
    ROUND(MAX(cycle_min), 2) AS max_cycle_min
FROM paired
GROUP BY StationId
ORDER BY avg_cycle_min DESC, StationId;


-- 14. 按小时看某工位产出趋势
WITH params AS (
    SELECT
        'SMT-01-ST1' AS station_id,
        '2026-05-20 00:00:00' AS start_local,
        '2026-05-21 00:00:00' AS end_local
),
base AS (
    SELECT
        StationId,
        datetime(EventTime, '+8 hours') AS local_time
    FROM TrackEvents
    WHERE EventType = 'TRACKOUT'
)
SELECT
    strftime('%Y-%m-%d %H:00:00', b.local_time) AS hour_bucket,
    COUNT(*) AS output_qty
FROM base b
CROSS JOIN params p
WHERE b.StationId = p.station_id
  AND b.local_time >= p.start_local
  AND b.local_time <  p.end_local
GROUP BY hour_bucket
ORDER BY hour_bucket;


-- 15. 查某个批次在哪些工位测试失败过
WITH params AS (
    SELECT 'LOT-20240101' AS lot_id
)
SELECT
    tr.LotId,
    tr.StationId,
    tr.SerialNumber,
    tr.Verdict,
    datetime(tr.TestedAt, '+8 hours') AS tested_local_time
FROM TestResults tr
CROSS JOIN params p
WHERE tr.LotId = p.lot_id
  AND tr.Verdict = 'FAIL'
ORDER BY tr.TestedAt DESC;
