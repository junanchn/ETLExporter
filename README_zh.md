# ETLExporter

Windows ETL（事件跟踪日志）分析工具套件，用于从 ETL 跟踪文件中提取和可视化性能数据。

## 主要功能

- 利用微软 TraceProcessor API 实现高效的 ETL 处理
- 分析 Windows ETL 跟踪以提取性能指标（CPU、内存、磁盘 I/O）
- 导出为层次化 JSON 格式便于处理
- 比较不同运行或版本之间的性能差异
- 在交互式网页树形表格中可视化结果

常见用途：性能回归测试、内存泄漏检测、CPU 热点分析。

## 快速开始

### 前置要求
- .NET Framework 4.8
- Windows 10/11 或 Windows Server 2016+

### 安装
```bash
git clone https://github.com/junanchn/ETLExporter.git
cd ETLExporter
msbuild ETLExporter.sln /p:Configuration=Release
```

## 使用方法

### 1. 捕获 ETL 跟踪
```bash
# 使用 Windows Performance Recorder
wpr -start CPU -start Heap -start DiskIO
# 运行您的场景
wpr -stop output.etl
```

### 2. 使用 ETLExport 分析

创建 `config.json`：
```json
{
  "StartEvent": {
    "ProviderId": "ProviderGUID",
    "TaskName": "TaskName",
    "OpcodeName": "win:Start"
  },
  "EndEvent": {
    "ProviderId": "ProviderGUID",
    "TaskName": "TaskName",
    "OpcodeName": "win:Stop"
  },
  "ProcessRegex": "YourApp\\.exe",
  "SymbolPaths": [
    "srv*C:\\Symbols*https://msdl.microsoft.com/download/symbols"
  ],
  "SymbolProcesses": ["YourApp.exe"],
  "Tables": [
    "CpuUsageSampled",
    "HeapAllocations",
    "DiskUsage"
  ]
}
```

运行分析：
```bash
ETLExport.exe config.json trace.etl
```

输出文件：
- `trace.etl.CpuUsageSampled.json`
- `trace.etl.HeapAllocations.json`
- `trace.etl.DiskUsage.json`

### 2. ETLMerge - 合并结果

合并多个分析结果：
```bash
ETLMerge.exe merged.json result1.json result2.json result3.json
```

### 3. ETLDiff - 比较结果

比较两个分析结果：
```bash
ETLDiff.exe diff.json test.json baseline.json
```

输出将包含每个指标的三个值：
- 测试值
- 基线值
- 差异（测试值 - 基线值）

### 4. TreeTableViewer - 可视化结果

1. 在浏览器中打开 `TreeTableViewer.html`
2. 将生成的 JSON 文件拖放到页面上
3. 使用界面功能：
   - 使用方向键展开/折叠树节点
   - 点击表头排序列
   - 搜索和过滤数据

## 分析表

| 表名 | 描述 | 列 |
|------|------|-----|
| `CpuUsagePrecise` | 基于上下文切换的 CPU 使用率 | Count, CPU Usage, Ready, Waits |
| `CpuUsageSampled` | 采样的 CPU 使用率 | Count, Weight |
| `DiskUsage` | 磁盘 I/O 操作 | Count, Size, Disk Service Time |
| `HeapAllocations` | 堆内存分配 | Count, Size |
| `HeapAllocationsReverse` | 堆分配（反向调用栈） | Count, Size |
| `Images` | DLL/EXE 加载 | Count, Size |
| `TotalCommit` | 虚拟内存提交 | Count, Size |
| `TotalCommitReverse` | 虚拟内存（反向调用栈） | Count, Size |

## 配置说明

### 事件过滤器
使用任意组合定义分析时间范围：
- `ProviderId`：提供程序 GUID
- `ProviderName`：提供程序名称字符串
- `TaskName`：任务名称
- `OpcodeName`：操作代码名称（例如 "win:Start"、"win:Stop"）
- `Opcode`：操作代码值

### 进程过滤
- `ProcessRegex`：通过命令行过滤进程的正则表达式

### 符号解析
- `SymbolPaths`：符号服务器路径数组
- `SymbolProcesses`：需要符号解析的进程名称

### 分析表
- `Tables` 指定要生成的分析表

## 示例工作流

### 性能回归分析
```bash
# 1. 捕获基线和测试跟踪
wpr -start CPU -start Heap
# 运行基线
wpr -stop baseline.etl

wpr -start CPU -start Heap
# 运行测试
wpr -stop test.etl

# 2. 分析
ETLExport.exe config.json baseline.etl
ETLExport.exe config.json test.etl

# 3. 比较
ETLDiff.exe comparison.json test.etl.CpuUsageSampled.json baseline.etl.CpuUsageSampled.json

# 4. 可视化
# 打开 TreeTableViewer.html 并加载 comparison.json
```

### 多次运行分析
```bash
# 分析多次运行
ETLExport.exe config.json run1.etl
ETLExport.exe config.json run2.etl
ETLExport.exe config.json run3.etl

# 合并结果
ETLMerge.exe merge.json run1.etl.HeapAllocations.json run2.etl.HeapAllocations.json run3.etl.HeapAllocations.json

# 查看聚合数据
# 打开 TreeTableViewer.html 并加载 merge.json

## 数据格式

导出的 JSON 文件使用分层树结构：
```json
{
  "columnNames": ["Count", "Size"],
  "treeData": [
    {
      "n": "ProcessName",
      "c": [
        {
          "n": "StackFrame1",
          "c": [
            {
              "n": "StackFrame2",
              "0": 100,  // 计数
              "1": 4096  // 大小（字节）
            }
          ]
        }
      ]
    }
  ]
}
```

## 提示

- 始终配置符号路径以获得有意义的调用栈
- 使用自定义事件精确标记分析区域
- 使用特定的正则表达式专注于相关进程
- 对于大型 ETL 文件，分别分析特定表

## 相关资源

- [Windows Performance Toolkit 文档](https://docs.microsoft.com/zh-cn/windows-hardware/test/wpt/)
- [ETW 文档](https://docs.microsoft.com/zh-cn/windows/win32/etw/event-tracing-portal)
- [TraceProcessor 文档](https://docs.microsoft.com/zh-cn/windows/apps/trace-processing/)

## 许可证

MIT 许可证 - 详见 [LICENSE](LICENSE) 文件。
